using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace HotkeySliding
{
    /// <summary>
    /// Adds "Hotbar Up" / "Hotbar Down" key rows and a "Hotbar Input" (Press / Double Press) row to
    /// Settings -> Controls (keyboard &amp; mouse tab).
    /// The Settings window is instantiated fresh every time it opens, and Settings.Awake calls
    /// KeyboardMouseSettings.Initialize -> SetupKeys, which wires every entry of m_keys into the vanilla rebind flow
    /// (click -> bind dialog -> ZInput.StartBindKey -> label refresh). We clone vanilla rows and insert KeySettings into
    /// m_keys *before* that runs, so vanilla does all the wiring, labelling, saving and resetting for us.
    ///
    /// Vanilla layout: a GridLayoutGroup filled column-major (m_keyRows per column, m_keyCols columns). Left-column rows
    /// have the caption on the left, right-column rows on the right. Some m_keys entries are inactive (not laid out).
    /// We add one row to the bottom of each column (left: Hotbar Up + Hotbar Down, right: Hotbar Input + spacer) and pin
    /// the grid to the new row count, so no vanilla row moves to another column.
    /// </summary>
    [HarmonyPatch(typeof(KeyboardMouseSettings), nameof(KeyboardMouseSettings.Initialize))]
    internal static class KeyboardMouseSettingsPatch
    {
        private const int AddedRowsPerColumn = 2;
        private const string InputRowName = "HotkeySliding_HotbarInput";

        private static bool _loggedLayout;

        private static void Prefix(KeyboardMouseSettings __instance)
        {
            try
            {
                Inject(__instance);
            }
            catch (Exception e)
            {
                Plugin.Warn($"Could not add hotbar row keys to the Controls settings tab (vanilla keys unaffected): {e}");
            }
        }

        private static void Inject(KeyboardMouseSettings settings)
        {
            List<KeySetting> keys = settings.m_keys;
            if (keys == null || keys.Count == 0)
                return;
            foreach (KeySetting k in keys)
            {
                if (k.m_keyName == Bindings.NextRow || k.m_keyName == Bindings.PrevRow)
                    return; // already injected on this instance
            }

            Bindings.EnsureRegistered();

            // Active vanilla rows under the key grid, in layout (sibling) order.
            GridLayoutGroup grid = null;
            var laidOut = new List<KeySetting>();
            foreach (KeySetting k in keys)
            {
                RectTransform rt = k.m_keyTransform;
                if (rt == null || rt.parent == null || !rt.gameObject.activeSelf)
                    continue;
                GridLayoutGroup g = rt.parent.GetComponent<GridLayoutGroup>();
                if (g == null || (grid != null && g != grid))
                    continue;
                grid = g;
                laidOut.Add(k);
            }
            if (grid == null || laidOut.Count == 0)
            {
                Plugin.Warn("Controls tab: key grid not found; bind the hotbar keys via the cfg overrides instead.");
                return;
            }
            laidOut.Sort((a, b) => a.m_keyTransform.GetSiblingIndex().CompareTo(b.m_keyTransform.GetSiblingIndex()));

            int cols = Mathf.Max(1, settings.m_keyCols);
            int rows = Mathf.CeilToInt(laidOut.Count / (float)cols);
            KeySetting leftLast = laidOut[Mathf.Min(rows, laidOut.Count) - 1];
            KeySetting rightLast = laidOut[laidOut.Count - 1];

            // Left column: Hotbar Up, Hotbar Down (after the left column's last vanilla row).
            RectTransform up = AddKeyRow(settings, leftLast, leftLast, Bindings.PrevRow, "Hotbar Up");
            RectTransform down = AddKeyRow(settings, leftLast, keys[keys.IndexOf(leftLast) + 1], Bindings.NextRow, "Hotbar Down");
            up.SetSiblingIndex(leftLast.m_keyTransform.GetSiblingIndex() + 1);
            down.SetSiblingIndex(up.GetSiblingIndex() + 1);

            // Right column: Hotbar Input, then an empty cell so both columns have the same height.
            var added = new List<RectTransform> { up, down };
            if (cols > 1 && rightLast != leftLast)
            {
                RectTransform input = AddInputRow(rightLast);
                input.SetSiblingIndex(rightLast.m_keyTransform.GetSiblingIndex() + 1);
                RectTransform spacer = AddSpacer(rightLast.m_keyTransform);
                spacer.SetSiblingIndex(input.GetSiblingIndex() + 1);
                added.Add(input);
                added.Add(spacer);
            }

            // Pin the grid to rows+2 per column (Flexible derives it from the rect height, which spills rows over),
            // and keep vanilla's index -> (column, row) gamepad navigation in step with it.
            int newRows = rows + AddedRowsPerColumn;
            settings.m_keyRows = newRows;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = newRows;

            RectTransform prt = (RectTransform)grid.transform;
            float needed = grid.padding.vertical + newRows * grid.cellSize.y + (newRows - 1) * grid.spacing.y;
            float extra = needed - prt.rect.height;
            if (extra > 0f && prt.GetComponent<ContentSizeFitter>() == null)
            {
                // Grow downwards only (top edge stays put).
                prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prt.rect.height + extra);
                prt.anchoredPosition -= new Vector2(0f, extra * (1f - prt.pivot.y));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
            GrowScrollContent(settings, added);

            if (!_loggedLayout)
            {
                _loggedLayout = true;
                Plugin.Log($"Controls tab: added hotbar rows under '{prt.name}' (vanilla laid-out keys={laidOut.Count}, " +
                           $"rows per column {rows} -> {newRows}, cols={cols}).");
            }
        }

        /// <summary>
        /// Clones <paramref name="template"/>'s row as a rebindable key and inserts its KeySetting into m_keys right after
        /// <paramref name="insertAfter"/> (m_keys order must match the column-major layout for gamepad navigation).
        /// </summary>
        private static RectTransform AddKeyRow(KeyboardMouseSettings settings, KeySetting template, KeySetting insertAfter,
            string keyName, string label)
        {
            RectTransform rt = CloneRow(template.m_keyTransform, keyName, label, out _);
            int at = settings.m_keys.IndexOf(insertAfter) + 1;
            settings.m_keys.Insert(at, new KeySetting
            {
                m_keyName = keyName,
                m_keyTransform = rt,
                m_blockedButtons = template.m_blockedButtons ?? Array.Empty<KeyCode>(),
            });
            return rt;
        }

        /// <summary>A row that looks like a key row but whose button cycles the hotbar input mode (not a keybind).</summary>
        private static RectTransform AddInputRow(KeySetting template)
        {
            RectTransform rt = CloneRow(template.m_keyTransform, InputRowName, "Hotbar Input", out Button button);
            if (button == null)
                return rt;
            TMP_Text valueText = button.GetComponentInChildren<TMP_Text>(true);
            void Refresh()
            {
                if (valueText != null)
                    valueText.text = Plugin.HotbarInput.Value == HotbarInputMode.Press ? "Press" : "Double Press";
            }
            Refresh();
            button.onClick.AddListener(() =>
            {
                Plugin.HotbarInput.Value = Plugin.HotbarInput.Value == HotbarInputMode.Press
                    ? HotbarInputMode.DoublePress
                    : HotbarInputMode.Press;
                Refresh();
            });
            return rt;
        }

        private static RectTransform AddSpacer(RectTransform template)
        {
            var go = new GameObject("HotkeySliding_Spacer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(template.parent, false);
            go.layer = template.gameObject.layer;
            return rt;
        }

        private static RectTransform CloneRow(RectTransform src, string name, string label, out Button button)
        {
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, src.parent, false);
            go.name = name;
            go.SetActive(true);

            // Clone keeps no runtime listeners; clear anyway in case the prefab has any.
            button = go.GetComponentInChildren<Button>(true);
            if (button != null)
                button.onClick.RemoveAllListeners();

            // Don't let the caption be re-localized back to the template's text.
            foreach (Localize loc in go.GetComponentsInChildren<Localize>(true))
                UnityEngine.Object.DestroyImmediate(loc);

            SetLabel(go, button, label);
            return (RectTransform)go.transform;
        }

        /// <summary>Sets the row's caption: the first text that isn't part of the key button.</summary>
        private static void SetLabel(GameObject row, Button button, string label)
        {
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (button != null && text.transform.IsChildOf(button.transform))
                    continue;
                text.text = label;
                return;
            }
        }

        /// <summary>Make the scroll view's content tall enough to reach the new rows.</summary>
        private static void GrowScrollContent(KeyboardMouseSettings settings, List<RectTransform> added)
        {
            if (added.Count == 0)
                return;
            foreach (ScrollRect scroll in settings.GetComponentsInChildren<ScrollRect>(true))
            {
                RectTransform content = scroll.content;
                if (content == null || !added[0].IsChildOf(content))
                    continue;

                float lowest = float.MaxValue;
                var corners = new Vector3[4];
                foreach (RectTransform rt in added)
                {
                    rt.GetWorldCorners(corners);
                    foreach (Vector3 c in corners)
                        lowest = Mathf.Min(lowest, content.InverseTransformPoint(c).y);
                }

                float missing = content.rect.yMin - lowest;
                if (missing > 0f)
                {
                    float delta = missing + 8f;
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, content.rect.height + delta);
                    // Keep the top edge where it was so only the bottom grows (no-op for the usual top pivot).
                    content.anchoredPosition -= new Vector2(0f, delta * (1f - content.pivot.y));
                }
                return;
            }
        }
    }
}

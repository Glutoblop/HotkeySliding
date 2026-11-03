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
    /// Adds "Next hotbar row" / "Previous hotbar row" rows to Settings -> Controls (keyboard &amp; mouse tab).
    /// The Settings window is instantiated fresh every time it opens, and Settings.Awake calls
    /// KeyboardMouseSettings.Initialize -> SetupKeys, which wires every entry of m_keys into the vanilla rebind flow
    /// (click -> bind dialog -> ZInput.StartBindKey -> label refresh). We clone a vanilla row and append KeySettings to
    /// m_keys *before* that runs, so vanilla does all the wiring, labelling, saving and resetting for us.
    /// </summary>
    [HarmonyPatch(typeof(KeyboardMouseSettings), nameof(KeyboardMouseSettings.Initialize))]
    internal static class KeyboardMouseSettingsPatch
    {
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

            KeySetting template = keys[keys.Count - 1];
            RectTransform templateRt = template.m_keyTransform;
            if (templateRt == null || templateRt.parent == null)
                return;

            Transform parent = templateRt.parent;
            bool autoLayout = parent.GetComponent<LayoutGroup>() != null;
            Vector2 step = RowStep(keys, templateRt);

            var added = new List<RectTransform>();
            AddRow(settings, template, Bindings.NextRow, "Next hotbar row", 1, step, autoLayout, added);
            AddRow(settings, template, Bindings.PrevRow, "Previous hotbar row", 2, step, autoLayout, added);

            if (!autoLayout)
                GrowScrollContent(settings, added);

            if (!_loggedLayout)
            {
                _loggedLayout = true;
                Plugin.Log($"Controls tab: added {added.Count} key rows under '{parent.name}' " +
                           $"(layoutGroup={autoLayout}, step={step}, vanilla rows={keys.Count - added.Count}).");
            }
        }

        private static void AddRow(KeyboardMouseSettings settings, KeySetting template, string keyName, string label,
            int offset, Vector2 step, bool autoLayout, List<RectTransform> added)
        {
            RectTransform src = template.m_keyTransform;
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, src.parent, false);
            go.name = keyName;
            RectTransform rt = (RectTransform)go.transform;
            if (autoLayout)
                rt.SetAsLastSibling();
            else
                rt.anchoredPosition = src.anchoredPosition + step * offset;

            // Clone keeps no runtime listeners; clear anyway in case the prefab has any.
            Button button = go.GetComponentInChildren<Button>(true);
            if (button != null)
                button.onClick.RemoveAllListeners();

            SetLabel(go, button, label);

            settings.m_keys.Add(new KeySetting
            {
                m_keyName = keyName,
                m_keyTransform = rt,
                m_blockedButtons = template.m_blockedButtons ?? Array.Empty<KeyCode>(),
            });
            added.Add(rt);
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

        /// <summary>Offset between two consecutive rows of the same column (falls back to the row height).</summary>
        private static Vector2 RowStep(List<KeySetting> keys, RectTransform last)
        {
            for (int i = keys.Count - 1; i > 0; i--)
            {
                RectTransform a = keys[i - 1].m_keyTransform, b = keys[i].m_keyTransform;
                if (a == null || b == null || a.parent != b.parent)
                    continue;
                Vector2 d = b.anchoredPosition - a.anchoredPosition;
                // Same column: moved down, (almost) no horizontal move.
                if (d.y < -1f && Mathf.Abs(d.x) < 1f)
                    return d;
            }
            return new Vector2(0f, -(last.rect.height + 4f));
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

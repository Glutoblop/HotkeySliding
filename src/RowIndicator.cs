using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HotkeySliding
{
    /// <summary>Small "Row X/Y" label parented to the HUD hotkey bar, placed just right of slot 8.</summary>
    internal static class RowIndicator
    {
        private const string ObjectName = "HotkeySliding_RowIndicator";
        private static readonly Dictionary<HotkeyBar, TextMeshProUGUI> Labels = new Dictionary<HotkeyBar, TextMeshProUGUI>();

        internal static void Refresh(HotkeyBar bar)
        {
            Player player = Player.m_localPlayer;
            bool show = Plugin.ShowIndicator.Value && player != null && !player.IsDead();
            int row = 0, rows = 1;
            if (show)
            {
                rows = HotbarState.RowCount(player);
                row = HotbarState.ActiveRow(player);
                if (Plugin.HideIndicatorOnFirstRow.Value && row == 0)
                    show = false;
            }

            Labels.TryGetValue(bar, out TextMeshProUGUI label);
            if (!show)
            {
                if (label != null && label.gameObject.activeSelf)
                    label.gameObject.SetActive(false);
                return;
            }

            if (label == null)
            {
                label = Create(bar);
                if (label == null)
                    return;
                Labels[bar] = label;
            }

            if (!label.gameObject.activeSelf)
                label.gameObject.SetActive(true);

            string text;
            try { text = string.Format(Plugin.IndicatorFormat.Value, row + 1, rows); }
            catch (System.FormatException) { text = $"Row {row + 1}/{rows}"; }
            if (label.text != text)
                label.text = text;

            label.rectTransform.localPosition = Anchor(bar) + new Vector3(Plugin.IndicatorOffsetX.Value, Plugin.IndicatorOffsetY.Value, 0f);
        }

        /// <summary>Right edge / vertical centre of hotbar slot 8, in the bar's local space.</summary>
        private static Vector3 Anchor(HotkeyBar bar)
        {
            float x = 7f * bar.m_elementSpace;
            float y = 0f;
            RectTransform prefab = bar.m_elementPrefab != null ? bar.m_elementPrefab.transform as RectTransform : null;
            if (prefab != null)
            {
                Rect r = prefab.rect;
                x += (1f - prefab.pivot.x) * r.width;
                y += (0.5f - prefab.pivot.y) * r.height;
            }
            else
            {
                x += bar.m_elementSpace * 0.5f;
            }
            return new Vector3(x, y, 0f);
        }

        private static TextMeshProUGUI Create(HotkeyBar bar)
        {
            Transform existing = bar.transform.Find(ObjectName);
            if (existing != null)
                Object.Destroy(existing.gameObject);

            GameObject go = new GameObject(ObjectName, typeof(RectTransform));
            go.transform.SetParent(bar.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 30f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.fontSize = 18f;
            label.color = new Color(1f, 0.85f, 0.45f, 1f);

            // Borrow the game's font from the slot prefab's key-binding label so it matches the HUD.
            TMP_Text template = null;
            if (bar.m_elementPrefab != null)
            {
                Transform binding = bar.m_elementPrefab.transform.Find("binding");
                template = binding != null ? binding.GetComponent<TMP_Text>() : null;
            }
            if (template != null)
            {
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
                label.fontSize = Mathf.Max(14f, template.fontSize);
            }
            return label;
        }

        internal static void DestroyAll()
        {
            foreach (TextMeshProUGUI label in Labels.Values)
            {
                if (label != null)
                    Object.Destroy(label.gameObject);
            }
            Labels.Clear();
        }
    }
}

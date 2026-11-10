using System.Collections.Generic;
using UnityEngine;

namespace HotkeySliding
{
    internal sealed class HotbarHud : MonoBehaviour
    {
        private static readonly HashSet<HotbarHud> Views = new HashSet<HotbarHud>();

        private HotkeyBar _bar;
        private CanvasGroup _visibility;
        private RowIndicator _indicator;

        internal static void Attach(HotkeyBar bar)
        {
            if (!bar.TryGetComponent<HotbarHud>(out _))
                bar.gameObject.AddComponent<HotbarHud>();
        }

        private void Awake()
        {
            _bar = GetComponent<HotkeyBar>();
            _visibility = gameObject.AddComponent<CanvasGroup>();
            Views.Add(this);
        }

        private void LateUpdate()
        {
            bool visible = !InputGuard.IsHotbarScreenOpen();
            if (_visibility.interactable != visible)
            {
                _visibility.alpha = visible ? 1f : 0f;
                _visibility.interactable = visible;
                _visibility.blocksRaycasts = visible;
            }
            if (!visible)
                return;

            Player player = Player.m_localPlayer;
            bool show = Plugin.ShowIndicator.Value && player != null && !player.IsDead();
            int row = show ? HotbarState.ActiveRow(player) : 0;
            show = show && (!Plugin.HideIndicatorOnFirstRow.Value || row != 0);
            if (!show)
            {
                if (_indicator != null && _indicator.gameObject.activeSelf)
                    _indicator.gameObject.SetActive(false);
                return;
            }

            if (_indicator == null)
                _indicator = CreateIndicator();
            if (!_indicator.gameObject.activeSelf)
                _indicator.gameObject.SetActive(true);

            _indicator.SetRow(row, HotbarState.RowCount(player));
            _indicator.rectTransform.localPosition = IndicatorPosition();
        }

        private RowIndicator CreateIndicator()
        {
            GameObject go = new GameObject("HotkeySliding_RowIndicator", typeof(RectTransform));
            go.layer = gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = Vector2.one * RowIndicator.Diameter;

            RowIndicator indicator = go.AddComponent<RowIndicator>();
            indicator.raycastTarget = false;
            return indicator;
        }

        // Right edge and vertical center of slot 8 in the bar's local space.
        private Vector3 IndicatorPosition()
        {
            float x = 7f * _bar.m_elementSpace;
            float y = 0f;
            RectTransform prefab = _bar.m_elementPrefab != null ? _bar.m_elementPrefab.transform as RectTransform : null;
            if (prefab != null)
            {
                Rect rect = prefab.rect;
                x += (1f - prefab.pivot.x) * rect.width;
                y += (0.5f - prefab.pivot.y) * rect.height;
            }
            else
            {
                x += _bar.m_elementSpace * 0.5f;
            }
            return new Vector3(x + Plugin.IndicatorOffsetX.Value, y + Plugin.IndicatorOffsetY.Value, 0f);
        }

        private void OnDestroy()
        {
            Views.Remove(this);
            if (_visibility != null)
                Destroy(_visibility);
            if (_indicator != null)
                Destroy(_indicator.gameObject);
        }

        internal static void DestroyAll()
        {
            foreach (HotbarHud view in Views)
                Destroy(view);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace HotkeySliding
{
    internal sealed class RowIndicator : MaskableGraphic
    {
        internal const float Diameter = 28f;
        private static readonly Color ActiveColor = new Color(1f, 0.85f, 0.45f, 1f);
        private static readonly Color FadedColor = new Color(1f, 0.85f, 0.45f, 0.25f);
        private int _row;
        private int _rows = 1;

        internal void SetRow(int row, int rows)
        {
            if (_row == row && _rows == rows)
                return;
            _row = row;
            _rows = rows;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float anglePerRow = 2f * Mathf.PI / _rows;
            float halfGap = _rows == 1 ? 0f : Mathf.Min(0.05f, anglePerRow * 0.12f);
            float sweep = anglePerRow - 2f * halfGap;
            int arcSteps = Mathf.CeilToInt(sweep / (Mathf.PI / 32f));

            // Row 1 is centered at the top and the other rows proceed clockwise.
            for (int row = 0; row < _rows; row++)
            {
                Color32 tint = row == _row ? ActiveColor : FadedColor;
                int tip = mesh.currentVertCount;
                mesh.AddVert(center, tint, Vector2.zero);

                float start = row * anglePerRow - sweep * 0.5f;
                for (int step = 0; step <= arcSteps; step++)
                {
                    float angle = start + sweep * step / arcSteps;
                    Vector2 point = center + radius * new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    mesh.AddVert(point, tint, Vector2.zero);
                }

                for (int step = 0; step < arcSteps; step++)
                    mesh.AddTriangle(tip, tip + step + 1, tip + step + 2);
            }
        }
    }
}

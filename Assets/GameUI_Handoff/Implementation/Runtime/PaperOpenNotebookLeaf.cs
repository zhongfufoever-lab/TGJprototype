using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage
{
    /// <summary>The left page of an open notebook, curving from its outer edge into the right binding.</summary>
    [AddComponentMenu("UI/Paper Collage/Paper Open Notebook Leaf")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperOpenNotebookLeaf : MaskableGraphic
    {
        public Texture2D paperTexture;
        public Rect textureUV = new Rect(0, 0, 1, 1);
        [Min(0)] public float outerTopDrop = 12;
        [Min(0)] public float outerBottomLift = 10;
        [Min(0)] public float outerEdgeBow = 4;
        [Range(0, .4f)] public float bindingShade = .14f;
        [Min(0)] public float feather = .65f;

        const int Columns = 16;
        const int Rows = 48;

        public override Texture mainTexture => paperTexture ? paperTexture : Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;

            // Subdivide in both directions so the photographic grain follows the bent
            // leaf instead of stretching across a single triangular paper face.
            for (int y = 0; y <= Rows; y++)
            {
                float v = (float)y / Rows;
                for (int x = 0; x <= Columns; x++)
                {
                    float u = (float)x / Columns;
                    vh.AddVert(Position(rect, u, v), Tint(u), UV(u, v));
                }
            }

            int stride = Columns + 1;
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    int a = y * stride + x;
                    vh.AddTriangle(a, a + stride, a + stride + 1);
                    vh.AddTriangle(a, a + stride + 1, a + 1);
                }
            }

            if (feather > 0) DrawFeather(vh, rect);
        }

        Vector2 Position(Rect rect, float u, float v)
        {
            float topDrop = Mathf.Min(outerTopDrop, rect.height * .12f);
            float bottomLift = Mathf.Min(outerBottomLift, rect.height * .12f);
            float bow = Mathf.Min(outerEdgeBow, rect.width * .15f);
            float acrossArc = Mathf.Sin(u * Mathf.PI);
            float left = rect.xMin + bow * Mathf.Sin(v * Mathf.PI);
            float top = rect.yMax - topDrop * (1 - u) + topDrop * .20f * acrossArc;
            float bottom = rect.yMin + bottomLift * (1 - u) - bottomLift * .15f * acrossArc;
            return new Vector2(Mathf.Lerp(left, rect.xMax, u), Mathf.Lerp(bottom, top, v));
        }

        Color Tint(float u)
        {
            // The outer part catches the light; the right edge falls into the binding.
            // Keep the photo's own creases visible beneath this broad curvature shade.
            float light = 1.015f + .025f * Mathf.Sin(u * Mathf.PI)
                - bindingShade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, u));
            return new Color(Mathf.Clamp01(color.r * light), Mathf.Clamp01(color.g * light),
                Mathf.Clamp01(color.b * light), color.a);
        }

        Vector2 UV(float u, float v)
        {
            return new Vector2(textureUV.x + textureUV.width * u, textureUV.y + textureUV.height * v);
        }

        static Vector2 BoundaryUV(int index)
        {
            // Clockwise perimeter, with each corner appearing exactly once.
            if (index < Rows) return new Vector2(0, (float)index / Rows);
            index -= Rows;
            if (index < Columns) return new Vector2((float)index / Columns, 1);
            index -= Columns;
            if (index < Rows) return new Vector2(1, 1 - (float)index / Rows);
            index -= Rows;
            return new Vector2(1 - (float)index / Columns, 0);
        }

        void DrawFeather(VertexHelper vh, Rect rect)
        {
            int count = 2 * (Columns + Rows);
            int start = vh.currentVertCount;
            float width = Mathf.Min(feather, Mathf.Min(rect.width, rect.height) * .05f);
            for (int i = 0; i < count; i++)
            {
                Vector2 uv = BoundaryUV(i);
                Vector2 previousUV = BoundaryUV((i + count - 1) % count);
                Vector2 nextUV = BoundaryUV((i + 1) % count);
                Vector2 point = Position(rect, uv.x, uv.y);
                Vector2 incoming = (point - Position(rect, previousUV.x, previousUV.y)).normalized;
                Vector2 outgoing = (Position(rect, nextUV.x, nextUV.y) - point).normalized;
                Vector2 n1 = new Vector2(-incoming.y, incoming.x);
                Vector2 n2 = new Vector2(-outgoing.y, outgoing.x);
                Vector2 normal = (n1 + n2).normalized;
                float miter = width / Mathf.Max(.5f, Vector2.Dot(normal, n1));
                Color tint = Tint(uv.x);
                Color clear = tint; clear.a = 0;
                vh.AddVert(point, tint, UV(uv.x, uv.y));
                vh.AddVert(point + normal * miter, clear, UV(uv.x, uv.y));
            }
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % count) * 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}

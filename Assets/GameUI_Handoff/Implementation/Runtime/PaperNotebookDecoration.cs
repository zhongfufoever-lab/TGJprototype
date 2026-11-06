using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage
{
    /// <summary>Texture-free page edges, binding shade and a ribbon for the existing paper surface.</summary>
    [AddComponentMenu("UI/Paper Collage/Paper Notebook Decoration")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperNotebookDecoration : MaskableGraphic
    {
        public enum Style { PageBlock, Gutter, Bookmark }

        public Style style = Style.PageBlock;
        [Min(0)] public float pageThickness = 24;
        [Min(0)] public float foreEdgeWidth = 8;
        [Range(3, 20)] public int cutLineCount = 12;
        [Range(0, 1)] public float cutLineOpacity = .26f;
        [Range(.25f, 2)] public float cutLineWidth = .55f;
        [Range(0, .45f)] public float bookmarkNotch = .18f;

        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            raycastTarget = false;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            switch (style)
            {
                case Style.PageBlock: DrawPageBlock(vh, r); break;
                case Style.Gutter: DrawGutter(vh, r); break;
                case Style.Bookmark: DrawBookmark(vh, r); break;
            }
        }

        void DrawPageBlock(VertexHelper vh, Rect r)
        {
            float thickness = Mathf.Clamp(pageThickness, 0, r.height * .35f);
            float edge = Mathf.Clamp(foreEdgeWidth, 0, r.width * .25f);
            float right = r.xMax - edge;
            float bottom = r.yMin + thickness;
            // The top face is intentionally empty. Its separate PaperSurface ends at
            // (right, bottom), leaving the front cut and narrow fore-edge exposed.
            Vector2 frontTL = new Vector2(r.xMin, bottom);
            Vector2 frontTR = new Vector2(right, bottom + 1);
            Vector2 frontBL = new Vector2(r.xMin + Mathf.Min(5, r.width * .02f), r.yMin);
            Vector2 frontBR = new Vector2(r.xMax - 1, r.yMin + 3);
            Vector2 sideTL = new Vector2(right, r.yMax);
            Vector2 sideTR = new Vector2(r.xMax - 1, r.yMax - Mathf.Min(8, thickness * .4f));
            if (edge > .1f)
            {
                Quad(vh, sideTL, sideTR, frontBR, frontTR,
                    Shade(.84f), Shade(.78f), Shade(.74f), Shade(.89f));
                Line(vh, sideTL + Vector2.down, frontTR, .7f, Shade(.68f, .32f));
            }
            if (thickness <= .1f) return;
            Quad(vh, frontTL, frontTR, frontBR, frontBL,
                Shade(.96f), Shade(.95f), Shade(.83f), Shade(.87f));

            int lines = Mathf.Clamp(cutLineCount, 3, 20);
            for (int i = 1; i <= lines; i++)
            {
                float t = (float)i / (lines + 1);
                Vector2 left = Vector2.Lerp(frontTL, frontBL, t);
                Vector2 rightEnd = Vector2.Lerp(frontTR, frontBR, t);
                // Small alternating offsets keep the exposed edge from reading as a ruled UI tile.
                left.x += 1.3f + (i % 3) * .6f;
                rightEnd.x -= 1.2f;
                left.y += (i % 3 - 1) * .18f;
                Color ink = Shade(.61f, cutLineOpacity * (i % 4 == 0 ? 1 : .72f));
                Line(vh, left, rightEnd, cutLineWidth, ink);
                if (edge > .1f)
                {
                    Vector2 upper = Vector2.Lerp(sideTL, sideTR, t);
                    upper.y -= 1;
                    Line(vh, upper, rightEnd, cutLineWidth * .7f,
                        Shade(.61f, cutLineOpacity * .45f));
                }
            }
            Line(vh, frontBL + Vector2.up * .7f, frontBR + Vector2.up * .7f,
                .8f, Shade(.59f, .32f));
        }

        void DrawGutter(VertexHelper vh, Rect r)
        {
            // Horizontal falloff: a narrow binding recess, then a broad soft fade onto the page.
            float[] stops = { 0, .08f, .18f, .34f, .57f, .8f, 1 };
            float[] opacity = { .30f, .76f, 1, .62f, .26f, .07f, 0 };
            for (int i = 0; i < stops.Length - 1; i++)
            {
                float left = Mathf.Lerp(r.xMin, r.xMax, stops[i]);
                float right = Mathf.Lerp(r.xMin, r.xMax, stops[i + 1]);
                Color a = color; a.a *= opacity[i];
                Color b = color; b.a *= opacity[i + 1];
                Quad(vh, new Vector2(left, r.yMax), new Vector2(right, r.yMax),
                    new Vector2(right, r.yMin), new Vector2(left, r.yMin), a, b, b, a);
            }
        }

        void DrawBookmark(VertexHelper vh, Rect r)
        {
            float inset = Mathf.Min(.65f, r.width * .04f);
            Vector2 tl = new Vector2(r.xMin + inset, r.yMax);
            Vector2 tr = new Vector2(r.xMax - inset, r.yMax);
            Vector2 br = new Vector2(tr.x, r.yMin + inset);
            Vector2 bl = new Vector2(tl.x, r.yMin + inset);
            Vector2 notch = new Vector2(r.center.x,
                r.yMin + Mathf.Min(r.height * Mathf.Clamp(bookmarkNotch, 0, .45f), r.width * .65f));
            int start = vh.currentVertCount;
            vh.AddVert(tl, Shade(1.04f), Vector2.zero);
            vh.AddVert(tr, Shade(.94f), Vector2.zero);
            vh.AddVert(br, Shade(.86f), Vector2.zero);
            vh.AddVert(notch, color, Vector2.zero);
            vh.AddVert(bl, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 3);
            vh.AddTriangle(start + 1, start + 2, start + 3);
            vh.AddTriangle(start, start + 3, start + 4);
            Color light = Color.Lerp(color, Color.white, .5f); light.a = color.a * .45f;
            Line(vh, tl + Vector2.right * .5f, bl + new Vector2(.5f, .7f), .65f, light);
            Line(vh, bl + Vector2.up * .6f, notch + Vector2.up * .6f, .6f, light);
            Line(vh, notch + Vector2.up * .6f, br + Vector2.up * .6f, .6f, light);
            Line(vh, tr + Vector2.left * .5f, br + new Vector2(-.5f, .7f),
                .65f, Shade(.55f, .3f));
        }

        Color Shade(float brightness, float opacity = 1)
        {
            return new Color(Mathf.Clamp01(color.r * brightness), Mathf.Clamp01(color.g * brightness),
                Mathf.Clamp01(color.b * brightness), color.a * opacity);
        }

        static void Quad(VertexHelper vh, Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl,
            Color ctl, Color ctr, Color cbr, Color cbl)
        {
            int n = vh.currentVertCount;
            vh.AddVert(tl, ctl, Vector2.zero); vh.AddVert(tr, ctr, Vector2.zero);
            vh.AddVert(br, cbr, Vector2.zero); vh.AddVert(bl, cbl, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }

        static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .001f || width <= 0) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            Vector2 half = normal * width * .5f;
            Quad(vh, from + half, to + half, to - half, from - half, tint, tint, tint, tint);
            Color clear = tint; clear.a = 0;
            Vector2 aa = normal * .35f;
            Quad(vh, from + half + aa, to + half + aa, to + half, from + half,
                clear, clear, tint, tint);
            Quad(vh, from - half, to - half, to - half - aa, from - half - aa,
                tint, tint, clear, clear);
        }
    }
}

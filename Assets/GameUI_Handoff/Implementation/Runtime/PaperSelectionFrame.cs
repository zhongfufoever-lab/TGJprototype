using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage
{
    /// <summary>A warm paper edge and shadow around selected art, with an entirely open center.</summary>
    [AddComponentMenu("UI/Paper Collage/Paper Selection Frame")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperSelectionFrame : MaskableGraphic
    {
        [Min(0)] public float cornerRadius = 10;
        [Range(.5f, 6)] public float lineWidth = 2.8f;
        [Range(0, 1)] public float edgeVariation = .4f;
        [Range(0, 12)] public float paperLipWidth = 5;
        public Color paperLipColor = new Color(.96f, .91f, .77f, .30f);
        public int edgeSeed = 17;
        [Min(0)] public float feather = .6f;
        public Color shadowColor = new Color(.08f, .09f, .10f, .24f);
        public Vector2 shadowOffset = new Vector2(1.5f, -2);

        public override Texture mainTexture => Texture2D.whiteTexture;

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
            float scale = canvas ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            float aa = Mathf.Max(.1f, feather) / scale;
            float width = Mathf.Min(lineWidth, Mathf.Min(rect.width, rect.height) * .2f);
            float lip = Mathf.Min(width + paperLipWidth, Mathf.Min(rect.width, rect.height) * .25f);
            // Every pass is a hollow ring. The pale lip reads as a separate paper edge
            // without tinting or covering the original artwork inside it.
            if (shadowColor.a > 0)
            {
                Rect shadowRect = rect;
                shadowRect.position += shadowOffset;
                Color tint = shadowColor; tint.a *= color.a;
                Ring(vh, shadowRect, lip, aa * 1.5f, tint);
            }
            Color paperTint = paperLipColor; paperTint.a *= color.a;
            if (paperTint.a > 0 && paperLipWidth > 0) Ring(vh, rect, lip, aa, paperTint);
            Ring(vh, rect, width, aa, color);
        }

        void Ring(VertexHelper vh, Rect rect, float width, float aa, Color tint)
        {
            const int cornerSteps = 10;
            float radius = Mathf.Min(Mathf.Max(cornerRadius, aa + width),
                Mathf.Min(rect.width, rect.height) * .5f);
            int start = vh.currentVertCount;
            Color clear = tint; clear.a = 0;
            Vector2[] centers = {
                new Vector2(rect.xMax-radius,rect.yMax-radius),
                new Vector2(rect.xMax-radius,rect.yMin+radius),
                new Vector2(rect.xMin+radius,rect.yMin+radius),
                new Vector2(rect.xMin+radius,rect.yMax-radius)
            };
            var points = new List<Vector2>();
            var normals = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                for (int i = 0; i <= cornerSteps; i++)
                {
                    float angle = (90 - corner * 90 - i * 90f / cornerSteps) * Mathf.Deg2Rad;
                    Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    points.Add(centers[corner] + normal * radius);
                    normals.Add(normal);
                }
                float endAngle = -corner * 90 * Mathf.Deg2Rad;
                Vector2 edgeNormal = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                Vector2 edgeStart = centers[corner] + edgeNormal * radius;
                Vector2 edgeEnd = centers[(corner + 1) % 4] + edgeNormal * radius;
                int edgeSteps = Mathf.CeilToInt(Vector2.Distance(edgeStart, edgeEnd) / 16);
                for (int step = 1; step < edgeSteps; step++)
                {
                    points.Add(Vector2.Lerp(edgeStart, edgeEnd, (float)step / edgeSteps));
                    normals.Add(edgeNormal);
                }
            }
            for (int point = 0; point < points.Count; point++)
            {
                Vector2 outer = points[point];
                Vector2 normal = normals[point];
                float variation = (Mathf.PerlinNoise((outer.x + edgeSeed * 31) * .07f,
                    (outer.y + edgeSeed * 19) * .07f) - .5f) * edgeVariation * 2;
                outer += normal * variation;
                vh.AddVert(outer, clear, Vector2.zero);
                vh.AddVert(outer - normal * aa, tint, Vector2.zero);
                vh.AddVert(outer - normal * (aa + width), tint, Vector2.zero);
                vh.AddVert(outer - normal * (aa * 2 + width), clear, Vector2.zero);
            }
            // One outer fringe, the solid fine line, and one inner fringe; no center fill.
            for (int point = 0; point < points.Count; point++)
            {
                int a = start + point * 4;
                int b = start + ((point + 1) % points.Count) * 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(a + band, b + band, b + band + 1);
                    vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }
    }
}

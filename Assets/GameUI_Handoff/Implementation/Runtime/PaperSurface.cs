using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage
{
    /// <summary>Photo UVs and a lightly irregular paper outline, using the shared UGUI material.</summary>
    [AddComponentMenu("UI/Paper Collage/Paper Surface")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperSurface : MaskableGraphic
    {
        public Texture2D paperTexture;
        public Rect textureUV = new Rect(0, 0, 1, 1);
        [Min(0)] public float cornerRadius;
        [Range(0, 3)] public float edgeVariation = .65f;
        public int edgeSeed = 1;
        [Min(0)] public float feather = .8f;

        public override Texture mainTexture => paperTexture ? paperTexture : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            float radius = Mathf.Min(cornerRadius, Mathf.Min(r.width, r.height) * .5f);
            var points = new List<Vector2>();
            var normals = new List<Vector2>();
            // Clockwise around the four corners. Straights get vertices too, so their edges
            // can have subpixel irregularity without stretching the texture's wrinkles.
            Vector2[] centers = {
                new Vector2(r.xMax-radius,r.yMax-radius), new Vector2(r.xMax-radius,r.yMin+radius),
                new Vector2(r.xMin+radius,r.yMin+radius), new Vector2(r.xMin+radius,r.yMax-radius)
            };
            for (int c = 0; c < 4; c++)
            {
                int steps = radius > 0 ? 12 : 1;
                for (int i = 0; i <= steps; i++)
                {
                    float a = (90 - c * 90 - i * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    AddPoint(points, normals, centers[c] + n * radius, n);
                }
                int next = (c + 1) % 4;
                float endAngle = (-c * 90) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                Vector2 start = centers[c] + normal * radius;
                Vector2 end = centers[next] + normal * radius;
                int count = Mathf.CeilToInt(Vector2.Distance(start, end) / 18);
                for (int i = 1; i < count; i++)
                    AddPoint(points, normals, Vector2.Lerp(start, end, (float)i / count), normal);
            }
            AddVertex(vh, r.center, color, r);
            for (int i = 0; i < points.Count; i++)
            {
                float noise = (Mathf.PerlinNoise((points[i].x + edgeSeed * 31) * .065f,
                    (points[i].y + edgeSeed * 19) * .065f) - .5f) * 2 * edgeVariation;
                Vector2 point = points[i] + normals[i] * noise;
                AddVertex(vh, point, color, r);
                Color clear = color; clear.a = 0;
                AddVertex(vh, point + normals[i] * feather, clear, r);
            }
            for (int i = 0; i < points.Count; i++)
            {
                int a = 1 + i * 2, b = 1 + ((i + 1) % points.Count) * 2;
                vh.AddTriangle(0, a, b);
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        static void AddPoint(List<Vector2> points, List<Vector2> normals, Vector2 p, Vector2 n)
        {
            if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude < .01f) return;
            points.Add(p); normals.Add(n);
        }

        void AddVertex(VertexHelper vh, Vector2 point, Color tint, Rect r)
        {
            Vector2 uv = new Vector2(textureUV.x + (point.x-r.xMin)/r.width*textureUV.width,
                textureUV.y + (point.y-r.yMin)/r.height*textureUV.height);
            vh.AddVert(point, tint, uv);
        }
    }
}

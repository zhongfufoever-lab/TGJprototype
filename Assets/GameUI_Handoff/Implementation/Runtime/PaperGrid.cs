using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage
{
    /// <summary>Independent low-contrast graph ruling in design pixels, without another texture.</summary>
    [AddComponentMenu("UI/Paper Collage/Paper Grid")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperGrid : MaskableGraphic
    {
        [Min(4)] public float spacing = 30;
        [Range(.25f, 2)] public float thickness = .8f;
        public Vector2 phase = new Vector2(9, 8);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float step = Mathf.Max(4, spacing);
            float aa = .65f / (canvas ? canvas.scaleFactor : 1);
            for (float x = r.xMin + Mathf.Repeat(phase.x, step); x < r.xMax; x += step)
            {
                Quad(vh,x-aa,r.yMin,x,r.yMax,0,1,true);
                Quad(vh,x,r.yMin,Mathf.Min(x+thickness,r.xMax),r.yMax,1,1,true);
                Quad(vh,x+thickness,r.yMin,Mathf.Min(x+thickness+aa,r.xMax),r.yMax,1,0,true);
            }
            for (float y = r.yMin + Mathf.Repeat(phase.y, step); y < r.yMax; y += step)
            {
                Quad(vh,r.xMin,y-aa,r.xMax,y,0,1,false);
                Quad(vh,r.xMin,y,r.xMax,Mathf.Min(y+thickness,r.yMax),1,1,false);
                Quad(vh,r.xMin,y+thickness,r.xMax,Mathf.Min(y+thickness+aa,r.yMax),1,0,false);
            }
        }

        void Quad(VertexHelper vh,float x1,float y1,float x2,float y2,float startAlpha,float endAlpha,bool vertical)
        {
            if(x2<=x1 || y2<=y1) return;
            int n = vh.currentVertCount;
            Color start=color,end=color; start.a*=startAlpha; end.a*=endAlpha;
            vh.AddVert(new Vector3(x1,y1),start,Vector2.zero);
            vh.AddVert(new Vector3(x1,y2),vertical?start:end,Vector2.zero);
            vh.AddVert(new Vector3(x2,y2),end,Vector2.zero);
            vh.AddVert(new Vector3(x2,y1),vertical?end:start,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n+2,n+3,n);
        }
    }
}

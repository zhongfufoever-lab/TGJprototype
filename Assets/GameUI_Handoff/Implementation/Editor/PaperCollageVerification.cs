using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Editor-only checks and fixed Game view previews; no runtime/input dependency.</summary>
    public static class PaperCollageVerification
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        [MenuItem("Tools/Game UI/Preview Paper Collage/1920 x 1080")]
        static void FullHD() => SetGameViewSize(1920,1080);
        [MenuItem("Tools/Game UI/Preview Paper Collage/1280 x 720")]
        static void HD() => SetGameViewSize(1280,720);
        [MenuItem("Tools/Game UI/Preview Paper Collage/1280 x 960 (4 by 3)")]
        static void FourByThree() => SetGameViewSize(1280,960);

        // Unity exposes fixed Game view sizes only through its editor's internal API.
        // This is editor-only and verified against the installed 6000.6.1f1 API.
        public static void SetGameViewSize(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            object sizes=sizesType.GetProperty("instance",Flags).GetValue(null);
            object group=sizesType.GetProperty("currentGroup",Flags).GetValue(sizes);
            Type type=group.GetType();
            int count=(int)type.GetMethod("GetTotalCount",Flags).Invoke(group,null);
            int selected=-1;
            for(int i=0;i<count;i++)
            {
                object size=type.GetMethod("GetGameViewSize",Flags).Invoke(group,new object[]{i});
                Type sizeType=size.GetType();
                if((int)sizeType.GetProperty("width",Flags).GetValue(size)==width &&
                    (int)sizeType.GetProperty("height",Flags).GetValue(size)==height &&
                    sizeType.GetProperty("sizeType",Flags).GetValue(size).ToString()=="FixedResolution")
                {selected=i;break;}
            }
            if(selected<0)
            {
                object kind=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
                object size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Flags,null,
                    new object[]{kind,width,height,"Paper Collage "+width+" x "+height},null);
                type.GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});
                selected=count;
            }
            var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,selected);
            view.Focus(); view.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [Serializable] public class Report
        {
            public string scene;
            public int width,height,layers,graphics,renderers,items;
            public float canvasScale,expectedScale;
            public string[] checks;
            public ArtSize[] visibleArt;
        }
        [Serializable] public class ArtSize
        {
            public string id;
            public float width,height;
        }

        [MenuItem("Tools/Game UI/Verify Paper Collage Study")]
        public static void VerifyMenu() => Debug.Log(ValidateCurrent());

        public static string ValidateCurrent()
        {
            Scene scene=SceneManager.GetActiveScene();
            Require(scene.path==PaperCollageBuilder.ScenePath,"Open the independent study scene first.");
            var roots=scene.GetRootGameObjects();
            var canvasRoot=roots.Single(go=>go.name=="PaperCollageUIRoot");
            var canvas=canvasRoot.GetComponent<Canvas>();
            var design=canvasRoot.transform.Find("Design_1920x1080").GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            var graphics=canvasRoot.GetComponentsInChildren<Graphic>(true);
            Require(canvasRoot.GetComponentsInChildren<Canvas>(true).Length==1,"Expected one Canvas.");
            Require(canvas.renderMode==RenderMode.ScreenSpaceOverlay,"Canvas must render as an overlay.");
            Require(graphics.All(g=>!g.raycastTarget),"Static graphics must not intercept input.");
            Require(graphics.All(g=>g.GetComponent<CanvasRenderer>()),"Every Graphic requires a CanvasRenderer.");
            var components=canvasRoot.GetComponentsInChildren<Component>(true);
            Require(components.All(c=>c),"A missing component was found.");
            Require(!components.Any(c=>c is Text || c.GetType().Name.Contains("TMP") || c is Selectable ||
                c is GraphicRaycaster || c.GetType().Name.Contains("InputModule") ||
                c is LayoutGroup || c is ContentSizeFitter),"Unexpected text, input or automatic layout component.");
            Require(!AssetDatabase.GetDependencies(PaperCollageBuilder.PrefabPath,true)
                .Any(p=>p.Contains("/Reference/")),"The generated UI must not depend on the flat reference.");
            var layout=JsonUtility.FromJson<PaperCollageBuilder.Layout>(
                File.ReadAllText(PaperCollageBuilder.Source+"/Specs/layout_1920x1080.json"));
            var transforms=canvasRoot.GetComponentsInChildren<RectTransform>(true);
            var visibleArt=new List<ArtSize>();
            foreach(var layer in layout.layers)
            {
                var rect=transforms.Single(t=>t.name==layer.id);
                float[] box=layer.rect_xywh;
                Require(Vector2.Distance(rect.anchoredPosition,new Vector2(box[0],-box[1]))<.01f,
                    "Wrong layer position: "+layer.id);
                Require(Vector2.Distance(rect.sizeDelta,new Vector2(box[2],box[3]))<.01f,
                    "Wrong layer dimensions: "+layer.id);
                Require(Mathf.Abs(Mathf.DeltaAngle(rect.localEulerAngles.z,-layer.rotation_degrees_clockwise))<.01f,
                    "Wrong clockwise rotation: "+layer.id);
                var image=rect.GetComponent<Image>();
                if(layer.material_role=="original_art")
                {
                    Require(image && image.sprite && image.preserveAspect && image.color==Color.white,
                        "Original art must be unfiltered and contain-fit: "+layer.id);
                    float fit=Mathf.Min(box[2]/image.sprite.rect.width,box[3]/image.sprite.rect.height);
                    visibleArt.Add(new ArtSize {id=layer.id,width=fit*image.sprite.rect.width,height=fit*image.sprite.rect.height});
                }
                else Require(rect.Find("Surface") && rect.Find("Surface").GetComponent<PaperSurface>(),
                    "Missing separate paper/shape surface: "+layer.id);
                if(layer.id=="graph_mount") Require(rect.GetComponentInChildren<PaperGrid>(),"Missing graph grid.");
            }
            Require(visibleArt.Count==7,"Expected six items and the complete concert hall.");
            var itemGroup=design.Find("InventoryPanel/Items");
            Require(itemGroup.childCount==6,"Expected the six original inventory items.");
            Require(itemGroup.GetSiblingIndex()==itemGroup.parent.childCount-1,"Inventory backing hides the items.");
            Vector2 gameSize=Handles.GetMainGameViewSize();
            float expected=Mathf.Min(gameSize.x/1920,gameSize.y/1080);
            Require(Mathf.Abs(canvas.scaleFactor-expected)<.001f,"The design must fit uniformly.");
            var corners=new Vector3[4]; design.GetWorldCorners(corners);
            Require(corners.All(p=>p.x>=-.5f && p.x<=gameSize.x+.5f && p.y>=-.5f && p.y<=gameSize.y+.5f),
                "The design is clipped at the current resolution.");
            Require(Vector2.Distance(design.sizeDelta,new Vector2(1920,1080))<.01f,"The design root must stay fixed.");
            var report=new Report {
                scene=scene.path,width=(int)gameSize.x,height=(int)gameSize.y,layers=layout.layers.Length,
                graphics=graphics.Length,renderers=canvasRoot.GetComponentsInChildren<CanvasRenderer>(true).Length,
                items=6,canvasScale=canvas.scaleFactor,expectedScale=expected,visibleArt=visibleArt.ToArray(),
                checks=new[]{"All specification coordinates, dimensions and clockwise rotations match",
                    "One overlay Canvas; every Graphic has a CanvasRenderer; six items draw above the backing",
                    "No Text, TMP, Selectable, input module, raycaster or automatic layout components",
                    "All decorative raycasts disabled; original art white and contain-fit",
                    "No dependency on the flat reference image; uniform centered fit without clipping"}
            };
            string json=JsonUtility.ToJson(report,true);
            string path=PaperCollageBuilder.Output+"/Verification/validation_"+report.width+"x"+report.height+".json";
            File.WriteAllText(path,json);
            return json;
        }

        static void Require(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException("Paper Collage verification: "+message);
        }
    }
}

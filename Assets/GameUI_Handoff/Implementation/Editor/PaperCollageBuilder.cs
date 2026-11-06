using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TGJ.PaperCollage.Editor
{
    public static class PaperCollageBuilder
    {
        public const string Source = "Assets/GameUI_Handoff";
        public const string Output = Source + "/Implementation";
        public const string ScenePath = Output + "/Scenes/PaperCollageStudy.unity";
        public const string PrefabPath = Output + "/Prefabs/PaperCollageUIRoot.prefab";
        const string Generated = Output + "/Generated";

        [Serializable] public class Layout { public Layer[] layers; public Palette palette; }
        [Serializable] public class Palette
        {
            public string outer_indigo, graph_ink_blue, backing_sage_bluegray, paper_oatmeal,
                accent_fog_lilac, secondary_sage, avatar_slate_teal, tiny_straw;
            public Color Get(string key)
            {
                string hex = (string)GetType().GetField(key).GetValue(this);
                return Hex(hex);
            }
        }
        [Serializable] public class Layer
        {
            public string id, semantic_path, asset, material_role, palette_key;
            public float[] rect_xywh;
            public float corner_radius_px, rotation_degrees_clockwise;
        }
        [Serializable] class Manifest { public Art[] art_files; public Paper[] paper_files; }
        [Serializable] class Art { public string file; public int width, height; public int[] alpha_bbox_top_left_xyxy; }
        [Serializable] class Paper { public string file; }

        [MenuItem("Tools/Game UI/Build Paper Collage Study")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the static study.");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Source+"/Specs/layout_1920x1080.json"));
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"/Specs/asset_manifest.json"));
            foreach (string asset in layout.layers.Select(l=>l.asset).Where(a=>!string.IsNullOrEmpty(a)).Distinct())
                if (!File.Exists(Source+"/"+asset)) throw new FileNotFoundException(asset);
            foreach (string folder in new[]{Generated+"/Art",Generated+"/Paper",Output+"/Prefabs",Output+"/Scenes",Output+"/Verification"})
                Directory.CreateDirectory(folder);

            var sprites = BuildArt(manifest, layout);
            var textures = BuildPaper();
            Scene previous = SceneManager.GetActiveScene();
            bool otherDirty = Enumerable.Range(0,SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).Any(s=>s.isDirty && s.path!=ScenePath);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // Only these generated objects belong to the builder; other scene additions survive.
            foreach (var go in scene.GetRootGameObjects())
                if (go.name=="PaperCollageUIRoot" || go.name=="PaperCollageCamera" || go.name=="PaperCollageLight")
                    Object.DestroyImmediate(go);

            var cameraGO = new GameObject("PaperCollageCamera",typeof(Camera));
            var camera = cameraGO.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = layout.palette.Get("outer_indigo");
            camera.orthographic = true; camera.orthographicSize=5; camera.cullingMask=0;
            cameraGO.transform.position=new Vector3(0,0,-10);
            new GameObject("PaperCollageLight",typeof(Light)).GetComponent<Light>().type=LightType.Directional;

            var root = new GameObject("PaperCollageUIRoot",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.layer=5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=0;
            var scaler=root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;

            var matte = Rect(root.transform,"Letterbox",0,0,1920,1080);
            matte.anchorMin=Vector2.zero; matte.anchorMax=Vector2.one;
            matte.offsetMin=matte.offsetMax=Vector2.zero;
            var matteImage=matte.gameObject.AddComponent<Image>();
            matteImage.color=layout.palette.Get("outer_indigo"); matteImage.raycastTarget=false;
            var design=Rect(root.transform,"Design_1920x1080",0,0,1920,1080);
            design.anchorMin=design.anchorMax=new Vector2(.5f,.5f);
            design.pivot=new Vector2(.5f,.5f); design.anchoredPosition=Vector2.zero;
            var groups=new Dictionary<string,RectTransform>();
            foreach(string name in new[]{"Background","TopBar","InventoryPanel","SceneFrame","DialoguePanel","OverlayRoot"})
                groups[name]=Rect(design,name,0,0,1920,1080);
            var items=Rect(groups["InventoryPanel"],"Items",0,0,1920,1080);

            for(int index=0;index<layout.layers.Length;index++)
            {
                Layer layer=layout.layers[index];
                string group=layer.semantic_path.StartsWith("Header/")?"TopBar":
                    layer.semantic_path.StartsWith("Inventory/")?"InventoryPanel":
                    layer.semantic_path.StartsWith("Scene/")?"SceneFrame":
                    layer.semantic_path.StartsWith("Dialogue/")?"DialoguePanel":"Background";
                Transform parent=layer.semantic_path.Contains("/Items/")?items:groups[group];
                float[] box=layer.rect_xywh;
                var holder=Rect(parent,layer.id,box[0],box[1],box[2],box[3]);
                holder.localRotation=Quaternion.Euler(0,0,-layer.rotation_degrees_clockwise);
                if(layer.material_role=="original_art")
                {
                    var image=holder.gameObject.AddComponent<Image>();
                    image.sprite=sprites[layer.asset]; image.preserveAspect=true;
                    image.color=Color.white; image.raycastTarget=false;
                    continue;
                }
                Color tint=layout.palette.Get(layer.palette_key);
                bool photo=layer.material_role.StartsWith("paper_");
                float radius=layer.material_role=="ui_disc" ? Mathf.Min(box[2],box[3])/2 : layer.corner_radius_px;
                if(layer.id=="avatar_shoulders") radius=16;
                if(photo && layer.id!="background")
                {
                    var shadow=Surface(holder,"Shadow",null,new Color(.04f,.055f,.075f,.25f),radius,index);
                    shadow.rectTransform.anchoredPosition=new Vector2(2.5f,-4);
                    shadow.feather=2.5f;
                }
                Texture2D texture=null;
                if(photo)
                {
                    string key=layer.id=="background"?"CardstockIndigo":
                        layer.material_role=="paper_cardstock"?"CardstockSage":
                        layer.id=="graph_mount"?"GraphIndigo":
                        layer.material_role=="paper_graph"?"GraphCream":
                        layer.material_role=="paper_ruled"?"RuledOatmeal":
                        layer.palette_key=="accent_fog_lilac"?"IvoryLilac":
                        layer.palette_key=="secondary_sage"?"IvorySage":"IvoryOatmeal";
                    texture=textures[key]; tint=Color.white;
                }
                if(layer.id=="top_divider") tint.a=.48f;
                var surface=Surface(holder,"Surface",texture,tint,radius,index);
                surface.edgeVariation=photo && layer.id!="background"?.7f:0;
                if(texture)
                {
                    // UV crop has the photograph's aspect ratio. No 9-slicing of wrinkles/ruling.
                    float photoWidth=layer.id=="background"?2100:
                        layer.material_role=="paper_cardstock"?1300:
                        layer.id=="dialogue_notebook"?550:
                        layer.material_role=="paper_graph"?1500:800;
                    float photoHeight=photoWidth*texture.height/texture.width;
                    float u=.06f+(index%4)*.12f, v=.04f+(index%3)*.14f;
                    if(layer.id=="dialogue_notebook") {u=.08f;v=.01f;}
                    surface.textureUV=new Rect(u,v,box[2]/photoWidth,box[3]/photoHeight);
                }
                if(layer.id=="graph_mount")
                {
                    // A clean independent grid avoids a second set of printed photo grid lines.
                    var grid=Rect(holder,"GraphRuling",0,0,box[2],box[3]).gameObject.AddComponent<PaperGrid>();
                    grid.spacing=30; grid.color=new Color(.69f,.74f,.8f,.16f); grid.raycastTarget=false;
                }
            }
            // Items must draw above the backing even though the semantic group was created first.
            items.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,ScenePath);
            if(!otherDirty)
            {
                // Display only the study. Existing clean scenes may be closed without saving.
                for(int i=SceneManager.sceneCount-1;i>=0;i--)
                {
                    Scene loaded=SceneManager.GetSceneAt(i);
                    if(loaded!=scene) EditorSceneManager.CloseScene(loaded,true);
                }
            }
            else if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            Selection.activeGameObject=root;
            Debug.Log("PAPER_COLLAGE_BUILT: "+ScenePath+"; "+layout.layers.Length+" independent layers, no text or input components.");
        }

        static Dictionary<string,Sprite> BuildArt(Manifest manifest,Layout layout)
        {
            var result=new Dictionary<string,Sprite>();
            foreach(var art in manifest.art_files.Where(a=>layout.layers.Any(l=>l.asset==a.file)))
            {
                string output=Generated+"/Art/"+Path.GetFileName(art.file);
                // Source bytes and source importer stay untouched. Crop ONLY transparent margins.
                var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if(!source.LoadImage(File.ReadAllBytes(Source+"/"+art.file))) throw new IOException(art.file);
                    int[] b=art.alpha_bbox_top_left_xyxy;
                    var crop=new Texture2D(b[2]-b[0],b[3]-b[1],TextureFormat.RGBA32,false);
                    try
                    {
                        crop.SetPixels(source.GetPixels(b[0],art.height-b[3],crop.width,crop.height));
                        crop.Apply(); WriteIfDifferent(output,crop.EncodeToPNG());
                    }
                    finally {Object.DestroyImmediate(crop);}
                }
                finally {Object.DestroyImmediate(source);}
                ImportTexture(output,true,TextureWrapMode.Clamp);
                result[art.file]=AssetDatabase.LoadAssetAtPath<Sprite>(output);
                if(!result[art.file]) throw new InvalidOperationException("Sprite import failed: "+output);
            }
            return result;
        }

        static Dictionary<string,Texture2D> BuildPaper()
        {
            var result=new Dictionary<string,Texture2D>();
            RemapPaper("cardstock_teal.jpg",new[]{"CardstockIndigo","CardstockSage"},
                new[]{"#242C3B","#77898D"},new[]{.27f,.65f},false,result);
            // The central mount is deliberately deeper than the outer background. A faint
            // luminance remap of real crumpled paper gives it folds without tinting the art.
            RemapPaper("ivory_crumpled.jpg",new[]{"IvoryOatmeal","IvoryLilac","IvorySage","GraphIndigo"},
                new[]{"#E2DFD2","#B0A3BB","#A5B4A6","#182231"},new[]{.85f,.75f,.75f,.24f},false,result);
            RemapPaper("ruled_crumpled.jpg",new[]{"RuledOatmeal"},new[]{"#E2DFD2"},new[]{.72f},true,result);
            RemapPaper("graph.jpg",new[]{"GraphCream"},new[]{"#E2DFD2"},new[]{.85f},true,result);
            return result;
        }

        static void RemapPaper(string file,string[] names,string[] colors,float[] contrast,bool chroma,
            Dictionary<string,Texture2D> result)
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                source.LoadImage(File.ReadAllBytes(Source+"/Textures/Paper/"+file));
                var pixels=source.GetPixels32();
                int width=Mathf.Min(2048,source.width),height=Mathf.RoundToInt((float)width*source.height/source.width);
                var sampled=new Color[width*height];
                Color mean=Color.black;
                for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                {
                    Color p=pixels[(y*source.height/height)*source.width+x*source.width/width];
                    sampled[y*width+x]=p; mean+=p;
                }
                mean/=sampled.Length;
                for(int variant=0;variant<names.Length;variant++)
                {
                    var outputPixels=new Color32[sampled.Length]; Color target=Hex(colors[variant]);
                    for(int i=0;i<sampled.Length;i++)
                    {
                        Color p=sampled[i]; float delta=(p.grayscale-mean.grayscale)*contrast[variant];
                        Color q=target+new Color(delta,delta,delta,0);
                        if(chroma)
                        {
                            Color residual=p-mean;
                            float lum=residual.grayscale;
                            q+=new Color(residual.r-lum,residual.g-lum,residual.b-lum,0)*.65f;
                        }
                        q.a=1; outputPixels[i]=q;
                    }
                    var derived=new Texture2D(width,height,TextureFormat.RGB24,false);
                    string path=Generated+"/Paper/"+names[variant]+".png";
                    try {derived.SetPixels32(outputPixels); derived.Apply(); WriteIfDifferent(path,derived.EncodeToPNG());}
                    finally {Object.DestroyImmediate(derived);}
                    ImportTexture(path,false,TextureWrapMode.Mirror);
                    result[names[variant]]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            finally {Object.DestroyImmediate(source);}
        }

        static void ImportTexture(string path,bool sprite,TextureWrapMode wrap)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            settings.spriteMode=(int)SpriteImportMode.Single;
            settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit=100; settings.mipmapEnabled=false;
            settings.alphaIsTransparency=sprite; settings.sRGBTexture=true;
            settings.npotScale=TextureImporterNPOTScale.None;
            settings.filterMode=FilterMode.Bilinear; settings.wrapMode=wrap;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void WriteIfDifferent(string path,byte[] bytes)
        {
            if(!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path,bytes);
        }

        static PaperSurface Surface(RectTransform parent,string name,Texture2D texture,Color tint,float radius,int seed)
        {
            var surface=Rect(parent,name,0,0,parent.sizeDelta.x,parent.sizeDelta.y).gameObject.AddComponent<PaperSurface>();
            surface.paperTexture=texture; surface.color=tint; surface.cornerRadius=radius;
            surface.edgeSeed=seed+1; surface.raycastTarget=false;
            return surface;
        }

        internal static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer=5;
            if(parent) rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
            return rect;
        }

        internal static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex,out Color color); return color; }
    }
}

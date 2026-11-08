using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage.Editor
{
    /// <summary>One-time, additive refinements of the existing study; never calls the rebuild tool.</summary>
    public static class PaperCollageRefinement
    {
        const string Generated = PaperCollageBuilder.Output + "/Generated/Paper/";
        const string Revision = "Refinement_20261104_";

        static Transform Design()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refining the study.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PaperCollageBuilder.ScenePath)
                throw new InvalidOperationException("Open PaperCollageStudy first.");
            return GameObject.Find("PaperCollageUIRoot").transform.Find("Design_1920x1080");
        }

        static bool Applied(Transform design, string stage) => design.Find("OverlayRoot/" + Revision + stage);

        static void Finish(Transform design, string stage)
        {
            NewRect(design.Find("OverlayRoot"), Revision + stage, 0, 0, 0, 0);
            EditorSceneManager.MarkSceneDirty(design.gameObject.scene);
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [MenuItem("Tools/Game UI/Refine Existing Study/1 - Central scene and paper layers")]
        public static void Central()
        {
            var design = Design();
            if (Applied(design, "Central")) return;
            Undo.RegisterFullObjectHierarchyUndo(design.gameObject, "Refine central paper collage");
            var group = design.Find("SceneFrame");
            var mount = (RectTransform)group.Find("graph_mount");
            var art = (RectTransform)group.Find("concert_hall");
            // Preserve the art, its importer and contain-fit; expand its visible box by 7.25%.
            SetBox(art, 389, 157, 972.43575f, 785.2845f);
            SetBox(mount, 363, 132, 1008, 830);
            var slate = DerivedPaper("GraphSlate", "#354858", false, false);
            var face = mount.Find("Surface").GetComponent<PaperSurface>();
            face.rectTransform.sizeDelta = mount.sizeDelta;
            face.paperTexture = slate; face.color = Color.white;
            face.textureUV = new Rect(0, 0, 1, 1);
            face.cornerRadius = 3; face.SetAllDirty();
            var shadow = mount.Find("Shadow").GetComponent<PaperSurface>();
            shadow.rectTransform.sizeDelta = mount.sizeDelta;
            shadow.rectTransform.anchoredPosition = new Vector2(3, -6);
            shadow.color = new Color(.035f, .045f, .065f, .38f);
            shadow.feather = 4; shadow.SetAllDirty();
            var grid = mount.Find("GraphRuling").GetComponent<PaperGrid>();
            grid.rectTransform.sizeDelta = mount.sizeDelta;
            grid.color = new Color(.73f, .79f, .81f, .105f); grid.SetAllDirty();
            // Retain both existing corner slips; add two full sheets beneath the main mount.
            var sage = Sheet(group, "underpaper_sage", 380, 135, 970, 814,
                "IvorySage", Color.white, 3, 1.5f);
            sage.SetSiblingIndex(mount.GetSiblingIndex());
            var cream = Sheet(group, "underpaper_oatmeal", 360, 130, 978, 829,
                "IvoryOatmeal", Color.white, 2, -.9f);
            cream.SetSiblingIndex(mount.GetSiblingIndex());
            // The existing bottom slip gets enough exposed area to read as a separate sheet.
            var bottom = (RectTransform)group.Find("graph_corner_bottom");
            SetBox(bottom, 1007, 856, 334, 140);
            foreach (var surface in bottom.GetComponentsInChildren<PaperSurface>())
            {
                surface.rectTransform.sizeDelta = bottom.sizeDelta;
                surface.SetAllDirty();
            }
            art.SetAsLastSibling();
            Finish(design, "Central");
        }

        [MenuItem("Tools/Game UI/Refine Existing Study/2 - Open notebook and readable paper")]
        public static void Notebook()
        {
            var design = Design();
            if (Applied(design, "Notebook")) return;
            Undo.RegisterFullObjectHierarchyUndo(design.gameObject, "Refine open notebook");
            var group = design.Find("DialoguePanel");
            var cover = (RectTransform)group.Find("dialogue_backing");
            var page = (RectTransform)group.Find("dialogue_notebook");
            var plain = DerivedPaper("NotebookIvory", "#E7E3D7", false, true);
            var ruled = DerivedPaper("NotebookReading", "#E7E3D7", true, true);
            var lilac = DerivedPaper("NotebookLilac", "#B0A3BB", false, true);
            SetBox(cover, 1383.5f, 118, 523.4f, 913.7f);
            foreach (var surface in cover.GetComponentsInChildren<PaperSurface>())
            {
                surface.cornerRadius = 7;
                if (surface.name == "Surface") surface.color = new Color(.65f, .75f, .77f, 1);
                surface.rectTransform.sizeDelta = cover.sizeDelta; surface.SetAllDirty();
            }
            // A narrow visible left leaf and binding make this read as half of an open book.
            var leaf = Sheet(group, "notebook_left_leaf", 1390, 135, 38, 872,
                null, Color.white, 5, 0);
            leaf.Find("Surface").GetComponent<PaperSurface>().paperTexture = plain;
            leaf.SetSiblingIndex(page.GetSiblingIndex());
            var blockRect = NewRect(group, "notebook_page_block", 1406.5f, 125.1f, 493.5f, 890.5f);
            var block = blockRect.gameObject.AddComponent<PaperNotebookDecoration>();
            block.style = PaperNotebookDecoration.Style.PageBlock;
            block.color = PaperCollageBuilder.Hex("#D7D1BE"); block.raycastTarget = false;
            blockRect.SetSiblingIndex(page.GetSiblingIndex());
            var ribbonShadowRect = NewRect(group, "notebook_bookmark_shadow", 1750, 977, 29, 73);
            var ribbonShadow = ribbonShadowRect.gameObject.AddComponent<PaperNotebookDecoration>();
            ribbonShadow.style = PaperNotebookDecoration.Style.Bookmark;
            ribbonShadow.color = new Color(.04f, .05f, .07f, .2f);
            ribbonShadowRect.SetSiblingIndex(page.GetSiblingIndex());
            var ribbonRect = NewRect(group, "notebook_bookmark", 1748, 974, 27, 72);
            var ribbon = ribbonRect.gameObject.AddComponent<PaperNotebookDecoration>();
            ribbon.style = PaperNotebookDecoration.Style.Bookmark;
            ribbon.color = PaperCollageBuilder.Hex("#A499B3");
            ribbonRect.SetSiblingIndex(page.GetSiblingIndex());
            SetBox(page, 1406.5f, 125.1f, 485.5f, 866.5f);
            foreach (var surface in page.GetComponentsInChildren<PaperSurface>())
            {
                surface.rectTransform.sizeDelta = page.sizeDelta;
                surface.cornerRadius = 8; surface.edgeVariation = .22f;
                if (surface.name == "Surface")
                {
                    surface.paperTexture = ruled; surface.textureUV = new Rect(0, 0, 1, 1);
                    surface.color = Color.white;
                }
                else
                {
                    surface.color = new Color(.12f, .11f, .09f, .14f);
                    surface.rectTransform.anchoredPosition = new Vector2(1, -2);
                }
                surface.SetAllDirty();
            }
            var gutterRect = NewRect(page, "BindingGutter", 0, 5, 22, 855);
            var gutter = gutterRect.gameObject.AddComponent<PaperNotebookDecoration>();
            gutter.style = PaperNotebookDecoration.Style.Gutter;
            gutter.color = new Color(.20f, .19f, .15f, .24f);
            // The foreground labels use the same quiet grain so the reading area stays calm.
            foreach (string id in new[] { "dialogue_title", "portrait_card", "choice_strip" })
            {
                var surface = group.Find(id + "/Surface").GetComponent<PaperSurface>();
                surface.paperTexture = id == "choice_strip" ? lilac : plain;
                surface.textureUV = new Rect(.05f, .12f, .8f, .4f);
                surface.color = Color.white; surface.SetAllDirty();
            }
            Finish(design, "Notebook");
        }

        [MenuItem("Tools/Game UI/Refine Existing Study/3 - Inventory selection outline")]
        public static void Selection()
        {
            var design = Design();
            if (Applied(design, "Selection")) return;
            Undo.RegisterFullObjectHierarchyUndo(design.gameObject, "Add inventory selection outline");
            var items = design.Find("InventoryPanel/Items");
            foreach (RectTransform item in items)
            {
                if (item.Find("SelectionFrame")) continue;
                var rect = NewRect(item, "SelectionFrame", -10, -10,
                    item.sizeDelta.x + 20, item.sizeDelta.y + 20);
                var frame = rect.gameObject.AddComponent<PaperSelectionFrame>();
                frame.color = PaperCollageBuilder.Hex("#F2E9CC");
                frame.lineWidth = 2; frame.cornerRadius = 10; frame.raycastTarget = false;
                rect.gameObject.SetActive(item.name == "ticket");
            }
            var preview = items.GetComponent<PaperInventorySelection>();
            if (!preview) preview = items.gameObject.AddComponent<PaperInventorySelection>();
            var serializedPreview = new SerializedObject(preview);
            serializedPreview.FindProperty("itemsRoot").objectReferenceValue = items;
            serializedPreview.ApplyModifiedPropertiesWithoutUndo();
            preview.SelectItem("ticket");
            Finish(design, "Selection");
        }

        [MenuItem("Tools/Game UI/Refine Existing Study/4 - Save refined scene and prefab")]
        public static void Save()
        {
            var design = Design();
            var root = design.parent.gameObject;
            // Save this exact hierarchy, including pre-existing scene overrides. No rebuild.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PaperCollageBuilder.PrefabPath,
                InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(root.scene);
        }

        static RectTransform NewRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = PaperCollageBuilder.Rect(parent, name, x, y, w, h);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Add paper detail");
            return rect;
        }

        static void SetBox(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }

        static RectTransform Sheet(Transform parent, string name, float x, float y, float w, float h,
            string texture, Color tint, float radius, float rotation)
        {
            var rect = NewRect(parent, name, x, y, w, h);
            rect.localRotation = Quaternion.Euler(0, 0, rotation);
            var shadowRect = NewRect(rect, "Shadow", 2, 4, w, h);
            var shadow = shadowRect.gameObject.AddComponent<PaperSurface>();
            shadow.color = new Color(.04f, .05f, .07f, .24f);
            shadow.cornerRadius = radius; shadow.feather = 3; shadow.raycastTarget = false;
            var faceRect = NewRect(rect, "Surface", 0, 0, w, h);
            var face = faceRect.gameObject.AddComponent<PaperSurface>();
            face.paperTexture = string.IsNullOrEmpty(texture) ? null :
                AssetDatabase.LoadAssetAtPath<Texture2D>(Generated + texture + ".png");
            face.color = tint; face.cornerRadius = radius;
            face.textureUV = new Rect(.1f, .1f, .8f, .8f); face.raycastTarget = false;
            return rect;
        }

        static Texture2D DerivedPaper(string name, string hex, bool ruled, bool calm)
        {
            string path = Generated + name + ".png";
            if (!File.Exists(path))
            {
                // Reuse real card fibre at low contrast; broad spine/corner folds replace crushed creases.
                var source = new Texture2D(2, 2);
                source.LoadImage(File.ReadAllBytes(PaperCollageBuilder.Source + "/Textures/Paper/cardstock_teal.jpg"));
                int width = 768, height = 1370;
                var pixels = new Color32[width * height];
                Color target = PaperCollageBuilder.Hex(hex);
                Color line = PaperCollageBuilder.Hex("#819598");
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    float u = (x + .5f) / width, v = (y + .5f) / height;
                    float fibre = source.GetPixelBilinear(u, v).grayscale - .5f;
                    float d = u - (.045f + .035f * Mathf.Sin(v * 3));
                    float fold = -.018f * Mathf.Exp(-d * d / .0008f)
                        + .012f * Mathf.Exp(-(d - .025f) * (d - .025f) / .0016f);
                    float corner = u + v - 1.02f;
                    fold += .009f * Mathf.Exp(-corner * corner / .003f) * Mathf.Clamp01((u - .65f) * 4);
                    float grain = fibre * (calm ? .055f : .14f) + (calm ? fold : 0);
                    Color value = target + new Color(grain, grain, grain, 0);
                    if (ruled)
                    {
                        float designY = (1 - v) * 866.5f;
                        float distance = Mathf.Abs(Mathf.Repeat(designY - 31 + 18, 36) - 18);
                        float alpha = .20f * (1 - Mathf.SmoothStep(.35f, 1, distance));
                        value = Color.Lerp(value, line, alpha);
                        float margin = Mathf.Abs(u - .945f);
                        value = Color.Lerp(value, new Color(.64f, .48f, .51f),
                            .12f * (1 - Mathf.SmoothStep(.0005f, .002f, margin)));
                    }
                    value.a = 1; pixels[y * width + x] = value;
                }
                var result = new Texture2D(width, height, TextureFormat.RGB24, false);
                result.SetPixels32(pixels); result.Apply();
                File.WriteAllBytes(path, result.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(result);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}

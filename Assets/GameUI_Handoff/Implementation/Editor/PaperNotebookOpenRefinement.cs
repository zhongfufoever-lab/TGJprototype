using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Refines the existing notebook in place; preserves the scene artwork and layout.</summary>
    public static class PaperNotebookOpenRefinement
    {
        const string PaperFolder = PaperCollageBuilder.Output + "/Generated/Paper/";

        [MenuItem("Tools/Game UI/Refine Existing Study/5 - Open notebook with creased pages")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before adjusting the notebook.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PaperCollageBuilder.ScenePath)
                throw new InvalidOperationException("Open PaperCollageStudy first.");
            var root = GameObject.Find("PaperCollageUIRoot");
            var group = root.transform.Find("Design_1920x1080/DialoguePanel");
            if (!group || (!group.Find("notebook_left_leaf") && !group.Find("notebook_right_leaf")))
                throw new InvalidOperationException("The existing notebook refinement is required.");

            var ruled = CreasedPaper("NotebookOpenReading", "#EAE7DE", .53f, true);
            var label = CreasedPaper("NotebookOpenLabel", "#EAE7DE", .30f, false);
            var lilac = CreasedPaper("NotebookOpenLilac", "#B0A3BB", .29f, false);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Open the complete creased notebook");
            Undo.RegisterFullObjectHierarchyUndo(group.gameObject, "Open the complete creased notebook");

            // This is the LEFT half of a fully open notebook. The spine sits at the
            // screen's right edge and the other half continues beyond the design crop.
            // Clip to the design rectangle so the book cannot spill into letterboxing.
            Ensure<RectMask2D>((RectTransform)group);
            var cover = (RectTransform)group.Find("dialogue_backing");
            Box(cover, 1378, 118, 1050, 902);
            ResizeSurfaces(cover, 8);
            var coverFace = cover.Find("Surface").GetComponent<PaperSurface>();
            coverFace.paperTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperFolder + "IvorySage.png");
            coverFace.color = Color.white;
            coverFace.SetAllDirty();

            var leaf = (RectTransform)(group.Find("notebook_right_leaf") ?? group.Find("notebook_left_leaf"));
            leaf.name = "notebook_right_leaf";
            Box(leaf, 1906, 125.1f, 506, 866.5f);
            // Retain the complete second page behind the crop; no green right border.
            foreach (var old in leaf.GetComponentsInChildren<PaperOpenNotebookLeaf>(true))
                old.gameObject.SetActive(false);
            foreach (var face in leaf.GetComponentsInChildren<PaperSurface>(true))
            {
                face.gameObject.SetActive(true);
                face.rectTransform.sizeDelta = leaf.sizeDelta;
                if (face.name == "Surface")
                {
                    face.paperTexture = ruled;
                    face.textureUV = new Rect(0, 0, 1, 1);
                    face.color = Color.white;
                }
                face.SetAllDirty();
            }
            var obsoleteEdges = group.Find("notebook_left_page_edges");
            if (obsoleteEdges) obsoleteEdges.gameObject.SetActive(false);

            var page = (RectTransform)group.Find("dialogue_notebook");
            Box(page, 1395, 125.1f, 511, 866.5f);
            ResizeSurfaces(page, 6);
            var pageFace = page.Find("Surface").GetComponent<PaperSurface>();
            pageFace.paperTexture = ruled;
            pageFace.textureUV = new Rect(0, 0, 1, 1);
            pageFace.color = Color.white;
            pageFace.edgeVariation = .45f;
            pageFace.SetAllDirty();
            var pageShadow = page.Find("Shadow").GetComponent<PaperSurface>();
            pageShadow.color = new Color(.10f, .09f, .07f, .11f);
            pageShadow.rectTransform.anchoredPosition = new Vector2(1, -2);
            pageShadow.SetAllDirty();
            // The main visible page bends gently down at its left outer corners;
            // its right side darkens into the binding, matching a visible left page.
            pageFace.enabled = false;
            pageShadow.enabled = false;
            var curvedShadowRect = Child(page, "OpenLeftPageShadow");
            Box(curvedShadowRect, 1, 2, 511, 866.5f);
            var curvedShadow = Ensure<PaperOpenNotebookLeaf>(curvedShadowRect);
            curvedShadow.paperTexture = null;
            curvedShadow.color = new Color(.10f, .09f, .07f, .15f);
            curvedShadow.SetAllDirty();
            curvedShadowRect.SetAsFirstSibling();
            var curvedRect = Child(page, "OpenLeftPage");
            Box(curvedRect, 0, 0, 511, 866.5f);
            var curved = Ensure<PaperOpenNotebookLeaf>(curvedRect);
            curved.paperTexture = ruled;
            curved.textureUV = new Rect(0, 0, 1, 1);
            curved.bindingShade = .055f;
            curved.color = Color.white;
            curved.SetAllDirty();

            var blockRect = (RectTransform)group.Find("notebook_page_block");
            Box(blockRect, 1397, 125.1f, 1017, 882.5f);
            var block = blockRect.GetComponent<PaperNotebookDecoration>();
            block.pageThickness = 16;
            block.foreEdgeWidth = 2;
            block.cutLineCount = 9;
            block.cutLineOpacity = .23f;
            block.color = PaperCollageBuilder.Hex("#DAD5C7");
            block.SetAllDirty();

            var gutterRect = (RectTransform)page.Find("BindingGutter");
            Box(gutterRect, 511, 3, 30, 860);
            gutterRect.localScale = new Vector3(-1, 1, 1);
            var gutter = gutterRect.GetComponent<PaperNotebookDecoration>();
            gutter.color = new Color(.18f, .16f, .11f, .31f);
            gutter.SetAllDirty();
            curvedRect.SetSiblingIndex(gutterRect.GetSiblingIndex());
            gutterRect.SetAsLastSibling();

            SetForeground(group, "dialogue_title", 1437.4f, 120.5f, 141.2f, 40.2f, label);
            SetForeground(group, "portrait_card", 1435.2f, 173.3f, 428.1f, 125.1f, label);
            SetForeground(group, "choice_strip", 1423.7f, 666.8f, 455.6f, 60.8f, lilac);
            Box((RectTransform)group.Find("avatar_tile"), 1465, 195.1f, 83.8f, 83.8f);
            Box((RectTransform)group.Find("avatar_head"), 1489.1f, 207.7f, 29.8f, 29.8f);
            Box((RectTransform)group.Find("avatar_shoulders"), 1478.8f, 241, 52.8f, 33.3f);

            EditorSceneManager.MarkSceneDirty(scene);
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("PAPER_NOTEBOOK_OPEN: complete open spread with the LEFT page visible; spine at right; white page continues past the crop; green cover at left; photographic creases restored.");
        }

        static void SetForeground(Transform group, string name, float x, float y, float w, float h, Texture2D texture)
        {
            var rect = (RectTransform)group.Find(name);
            Box(rect, x, y, w, h);
            ResizeSurfaces(rect, name == "portrait_card" ? 23 : 1.5f);
            var face = rect.Find("Surface").GetComponent<PaperSurface>();
            face.paperTexture = texture;
            face.textureUV = new Rect(.10f, .16f, .78f, .36f);
            face.color = Color.white;
            face.SetAllDirty();
        }

        static void ResizeSurfaces(RectTransform parent, float radius)
        {
            foreach (var surface in parent.GetComponentsInChildren<PaperSurface>(true))
            {
                surface.rectTransform.sizeDelta = parent.sizeDelta;
                surface.cornerRadius = radius;
                surface.raycastTarget = false;
                surface.SetAllDirty();
            }
        }

        static Texture2D CreasedPaper(string name, string hex, float contrast, bool ruled)
        {
            // Existing photographic paper keeps real creases, rather than replacing it
            // with nearly flat cardstock grain. No source image or importer is changed.
            string path = PaperFolder + name + ".png";
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var result = new Texture2D(768, 1464, TextureFormat.RGB24, false);
            try
            {
                source.LoadImage(File.ReadAllBytes(PaperCollageBuilder.Source + "/Textures/Paper/ivory_crumpled.jpg"));
                // Accumulate in double precision: summing millions of bright source
                // pixels into a float biases the mean and clips the derived paper white.
                double sum = 0;
                for (int sy = 0; sy < 64; sy++)
                for (int sx = 0; sx < 64; sx++)
                    sum += source.GetPixelBilinear((sx + .5f) / 64, (sy + .5f) / 64).grayscale;
                float mean = (float)(sum / (64 * 64));
                Color baseColor = PaperCollageBuilder.Hex(hex);
                Color ruling = PaperCollageBuilder.Hex("#819598");
                var pixels = new Color32[result.width * result.height];
                for (int y = 0; y < result.height; y++)
                for (int x = 0; x < result.width; x++)
                {
                    float u = (x + .5f) / result.width;
                    float v = (y + .5f) / result.height;
                    float crease = (source.GetPixelBilinear(u, v).grayscale - mean) * contrast;
                    // A broad, slight lifting highlight joins the shaded binding recess.
                    float lift = .018f * Mathf.Exp(-Mathf.Pow((u - .085f) / .06f, 2));
                    Color value = baseColor + new Color(crease + lift, crease + lift, crease + lift, 0);
                    if (ruled)
                    {
                        float designY = (1 - v) * 866.5f;
                        float distance = Mathf.Abs(Mathf.Repeat(designY - 31 + 18, 36) - 18);
                        float alpha = .24f * (1 - Mathf.SmoothStep(.32f, .95f, distance));
                        value = Color.Lerp(value, ruling, alpha);
                    }
                    value.a = 1;
                    pixels[y * result.width + x] = value;
                }
                result.SetPixels32(pixels);
                result.Apply();
                File.WriteAllBytes(path, result.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(result);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static T Ensure<T>(RectTransform rect) where T : Component
        {
            var component = rect.GetComponent<T>();
            return component ? component : Undo.AddComponent<T>(rect.gameObject);
        }

        static RectTransform Child(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing) return existing;
            var rect = PaperCollageBuilder.Rect(parent, name, 0, 0, 0, 0);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Add open notebook page detail");
            return rect;
        }

        static void Box(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }
}

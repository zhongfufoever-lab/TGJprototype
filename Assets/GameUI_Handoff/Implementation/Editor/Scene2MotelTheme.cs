using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Independent motel paper colors, applied to an already cloned UI without changing its layout.</summary>
    public static class Scene2MotelTheme
    {
        public const string PaperFolder = PaperCollageBuilder.Output + "/Generated/Scene2Motel/Paper";
        const string PalettePath = "Assets/Scene2_Motel_Unity_Handoff/Design/Scene2Motel/palette.json";
        const string MetadataPrefix = "Scene2MotelTheme:";

        [Serializable] sealed class PaletteFile
        {
            public string background, inventory_paper, central_graph_paper, backing_papers,
                accent_apricot, ivory_paper, dialogue_block, notebook_top_tape, selection_and_dot;
        }

        [Serializable] sealed class TextureMetadata
        {
            public int version = 1;
            public string sourceGuid, sourceHash, targetRGB;
        }

        /// <summary>Recolors only the clone's graphic instances and assigns newly generated paper textures.</summary>
        public static void Apply(GameObject clonedRoot)
        {
            if (!clonedRoot) throw new ArgumentNullException(nameof(clonedRoot));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the motel theme outside Play mode.");
            if (EditorUtility.IsPersistent(clonedRoot) || clonedRoot.scene.path == PaperCollageBuilder.ScenePath)
                throw new InvalidOperationException("Apply the motel theme to a clone in its own scene, preserving scene 1.");
            if (!clonedRoot.transform.Find("Design_1920x1080"))
                throw new InvalidOperationException("The cloned paper collage design root was not found.");

            var palette = JsonUtility.FromJson<PaletteFile>(File.ReadAllText(PalettePath));
            var background = Parse(palette.background);
            var ivory = Parse(palette.ivory_paper);
            var backing = Parse(palette.backing_papers);
            var apricot = Parse(palette.accent_apricot);
            var selection = Parse(palette.selection_and_dot);
            var targets = new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                { "background", background },
                { "top_divider", ivory }, { "header_dot", selection },
                { "location_slip", ivory }, { "time_slip", ivory },
                { "header_accent", apricot },
                { "nav_settings", ivory }, { "nav_map", ivory }, { "nav_archive", ivory },
                { "nav_save_load", ivory }, { "nav_main_menu", ivory },
                { "inventory_backing", Parse(palette.inventory_paper) },
                { "inventory_title", ivory }, { "inventory_caption", ivory },
                { "memories_tab", apricot }, { "collectibles_tab", ivory },
                { "selected_tab_line", backing },
                { "graph_corner_top", ivory }, { "graph_corner_bottom", apricot },
                { "underpaper_sage", backing }, { "underpaper_oatmeal", backing },
                { "graph_mount", Parse(palette.central_graph_paper) },
                { "dialogue_backing", backing },
                { "notebook_left_leaf", ivory }, { "notebook_right_leaf", ivory },
                { "dialogue_notebook", ivory },
                { "dialogue_title", Parse(palette.notebook_top_tape) },
                { "portrait_card", Parse(palette.dialogue_block) },
                { "avatar_tile", Parse(palette.central_graph_paper) },
                { "avatar_head", backing }, { "avatar_shoulders", backing },
                { "choice_strip", apricot }
            };
            EnsureOutputFolder();
            var cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            int papers = 0;

            foreach (var face in clonedRoot.GetComponentsInChildren<PaperSurface>(true))
            {
                if (IsShadow(face.transform, clonedRoot.transform)) continue;
                Color target = Target(face.transform, clonedRoot.transform, targets, ivory);
                Undo.RecordObject(face, "Apply motel paper colors");
                if (face.paperTexture)
                {
                    face.paperTexture = Remap(face.paperTexture, target, cache);
                    face.color = WithAlpha(Color.white, face.color.a);
                }
                else face.color = WithAlpha(target, face.color.a);
                Dirty(face);
                papers++;
            }

            foreach (var leaf in clonedRoot.GetComponentsInChildren<PaperOpenNotebookLeaf>(true))
            {
                if (IsShadow(leaf.transform, clonedRoot.transform)) continue;
                Color target = Target(leaf.transform, clonedRoot.transform, targets, ivory);
                Undo.RecordObject(leaf, "Apply motel notebook paper colors");
                if (leaf.paperTexture)
                {
                    leaf.paperTexture = Remap(leaf.paperTexture, target, cache);
                    leaf.color = WithAlpha(Color.white, leaf.color.a);
                }
                else leaf.color = WithAlpha(target, leaf.color.a);
                Dirty(leaf);
                papers++;
            }

            foreach (var graphic in clonedRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (IsShadow(graphic.transform, clonedRoot.transform)) continue;
                if (graphic is Image image)
                {
                    if (image.name == "Letterbox") image.color = WithAlpha(background, image.color.a);
                    // Art keeps its original RGB; transparent hit rectangles stay transparent.
                    else if (image.sprite) image.color = WithAlpha(Color.white, image.color.a);
                    else continue;
                }
                else if (graphic is PaperGrid grid)
                    grid.color = WithAlpha(backing, grid.color.a);
                else if (graphic is PaperSelectionFrame frame)
                {
                    frame.color = WithAlpha(selection, frame.color.a);
                    frame.paperLipColor = WithAlpha(selection, frame.paperLipColor.a);
                    // The existing shadow tint, opacity, offset and ring shape stay intact.
                }
                else if (graphic is PaperNotebookDecoration decoration)
                {
                    if (decoration.style == PaperNotebookDecoration.Style.Gutter) continue;
                    decoration.color = WithAlpha(decoration.style == PaperNotebookDecoration.Style.Bookmark
                        ? apricot : ivory, decoration.color.a);
                }
                else continue;
                Dirty(graphic);
            }
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("SCENE2_MOTEL_THEME: " + papers + " paper graphics; independent texture folder=" +
                PaperFolder + "; background=" + palette.background + "; inventory=" + palette.inventory_paper +
                "; graph=" + palette.central_graph_paper + "; apricot=" + palette.accent_apricot + ".");
        }

        static bool IsShadow(Transform transform, Transform root)
        {
            for (var current = transform; current && current != root; current = current.parent)
                if (current.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static Color Target(Transform transform, Transform root, Dictionary<string, Color> targets, Color fallback)
        {
            for (var current = transform; current && current != root; current = current.parent)
                if (targets.TryGetValue(current.name, out Color color)) return color;
            return fallback;
        }

        static Color Parse(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
                throw new InvalidOperationException("Invalid scene 2 palette value: " + value);
            return color;
        }

        static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

        static void Dirty(Graphic graphic)
        {
            graphic.SetAllDirty();
            EditorUtility.SetDirty(graphic);
        }

        static TextureMetadata Metadata(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            string data = importer ? importer.userData : null;
            if (string.IsNullOrEmpty(data) || !data.StartsWith(MetadataPrefix, StringComparison.Ordinal)) return null;
            return JsonUtility.FromJson<TextureMetadata>(data.Substring(MetadataPrefix.Length));
        }

        static Texture2D Remap(Texture2D source, Color target, Dictionary<string, Texture2D> cache)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            // Reapplying the theme always starts at the original scene-1 paper, avoiding
            // repeated recoloring, nested generated names and loss of crease contrast.
            if (sourcePath.StartsWith(PaperFolder + "/", StringComparison.Ordinal))
            {
                var previous = Metadata(sourcePath);
                string original = previous == null ? null : AssetDatabase.GUIDToAssetPath(previous.sourceGuid);
                if (!string.IsNullOrEmpty(original))
                {
                    sourcePath = original;
                    source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                }
            }
            string guid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (!source || string.IsNullOrEmpty(guid) || !File.Exists(sourcePath))
                throw new InvalidOperationException("Expected an existing photographic paper texture: " + sourcePath);
            string rgb = ColorUtility.ToHtmlStringRGB(target);
            string hash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            string key = guid + ":" + rgb + ":" + hash;
            if (cache.TryGetValue(key, out Texture2D cached)) return cached;
            string safeName = source.name;
            foreach (char character in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(character, '_');
            string output = PaperFolder + "/" + safeName + "_" + guid.Substring(0, 8) + "_" + rgb + ".png";
            var metadata = Metadata(output);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(output);
            if (existing && metadata != null && metadata.version == 1 && metadata.sourceGuid == guid &&
                metadata.targetRGB == rgb && metadata.sourceHash == hash)
            {
                cache[key] = existing;
                return existing;
            }

            // Decode a temporary copy of the bytes; the source importer's Read/Write,
            // colors and shared texture asset are never changed.
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D colored = null;
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(sourcePath)))
                    throw new IOException("Could not decode source paper: " + sourcePath);
                Color32[] pixels = decoded.GetPixels32();
                double sum = 0, weight = 0;
                foreach (Color32 pixel in pixels)
                {
                    double alpha = pixel.a / 255.0;
                    sum += (.299 * pixel.r + .587 * pixel.g + .114 * pixel.b) / 255.0 * alpha;
                    weight += alpha;
                }
                float mean = weight > 0 ? (float)(sum / weight) : 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    float difference = (.299f * pixel.r + .587f * pixel.g + .114f * pixel.b) / 255f - mean;
                    // Add the existing light/dark detail to the new RGB base instead of
                    // multiplying a blue/teal paper by a warm color. Ruling is retained too.
                    pixels[i] = new Color(Mathf.Clamp01(target.r + difference),
                        Mathf.Clamp01(target.g + difference), Mathf.Clamp01(target.b + difference), pixel.a / 255f);
                }
                colored = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
                colored.SetPixels32(pixels);
                colored.Apply();
                File.WriteAllBytes(output, colored.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (colored) UnityEngine.Object.DestroyImmediate(colored);
            }
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            var originalImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = originalImporter && originalImporter.alphaIsTransparency;
            importer.mipmapEnabled = originalImporter && originalImporter.mipmapEnabled;
            importer.wrapMode = source.wrapMode;
            importer.filterMode = source.filterMode;
            importer.anisoLevel = source.anisoLevel;
            importer.maxTextureSize = originalImporter ? originalImporter.maxTextureSize : 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.userData = MetadataPrefix + JsonUtility.ToJson(new TextureMetadata
                { sourceGuid = guid, sourceHash = hash, targetRGB = rgb });
            importer.SaveAndReimport();
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(output);
            if (!result) throw new IOException("The recolored motel paper did not import: " + output);
            cache[key] = result;
            return result;
        }

        static void EnsureOutputFolder()
        {
            string[] parts = PaperFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

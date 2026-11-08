// Editor-only handoff helper for ThatGameJam2026.
// Creates a new scene and prefab; it never rewrites an existing scene or prefab.
// This source has been reviewed statically, but has not been compiled/run in Unity here.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ThatGameJam2026.MainMenuHandoff
{
    public static class MainMenuLayerBuilder
    {
        private const string MenuPath = "Tools/ThatGameJam/Build Main Menu From Layers";
        private const string ManifestName = "layout.json";
        private const float DefaultWidth = 1920f;
        private const float DefaultHeight = 1080f;

        [Serializable]
        private sealed class LayerManifest
        {
            public float designWidth;
            public float designHeight;
            public LayerEntry[] layers;
        }

        [Serializable]
        private sealed class LayerEntry
        {
            public string id;
            public string file;
            public string parent;
            public int order;
            public float x;
            public float y;
            public float width;
            public float height;
            public bool interactive;
            public string role;
        }

        private sealed class ResolvedLayer
        {
            public LayerEntry entry;
            public string assetPath;
            public string buttonName;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Main Menu] 请先退出 Play Mode，再生成主界面。");
                return;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            Scene generatedScene = default(Scene);
            try
            {
                string packageRoot = FindPackageRoot();
                string manifestPath = packageRoot + "/" + ManifestName;
                LayerManifest manifest = JsonUtility.FromJson<LayerManifest>(
                    File.ReadAllText(ToAbsolutePath(manifestPath)));
                float designWidth = manifest != null && manifest.designWidth > 0f
                    ? manifest.designWidth : DefaultWidth;
                float designHeight = manifest != null && manifest.designHeight > 0f
                    ? manifest.designHeight : DefaultHeight;
                List<ResolvedLayer> layers = ValidateAndResolve(manifest, packageRoot);

                // Import only package-owned PNGs. Imported art stays readable=false.
                foreach (ResolvedLayer layer in layers)
                    ConfigureSprite(layer.assetPath);

                string generatedFolder = packageRoot + "/Generated";
                EnsureAssetFolder(generatedFolder);
                string scenePath = AssetDatabase.GenerateUniqueAssetPath(
                    generatedFolder + "/MainMenu_UI.unity");
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    generatedFolder + "/MainMenu_UI.prefab");

                // Additive generation preserves every scene already open in the editor.
                generatedScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(generatedScene);

                GameObject canvasObject = MakeCanvas(designWidth, designHeight);
                RectTransform designRoot = MakeRect("DesignRoot", canvasObject.transform);
                designRoot.anchorMin = designRoot.anchorMax = new Vector2(0.5f, 0.5f);
                designRoot.pivot = new Vector2(0.5f, 0.5f);
                designRoot.anchoredPosition = Vector2.zero;
                designRoot.sizeDelta = new Vector2(designWidth, designHeight);

                // All visual layers remain flat so manifest order is the exact draw order.
                // The manifest parent field is descriptive; x/y always use design coordinates.
                foreach (ResolvedLayer layer in layers)
                    MakeLayer(layer, designRoot);

                // Empty anchors only: no font asset, ChineseUI font or Text/TMP dependency.
                RectTransform titleAnchor = MakeRect("TitleAnchor_Empty", designRoot);
                SetTopLeftRect(titleAnchor, designWidth * 0.18f, designHeight * 0.28f,
                    designWidth * 0.28f, designHeight * 0.31f);

                MakeEventSystem();
                PrefabUtility.SaveAsPrefabAsset(canvasObject, prefabPath);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                    throw new IOException("Prefab 保存失败：" + prefabPath);
                if (!EditorSceneManager.SaveScene(generatedScene, scenePath))
                    throw new IOException("场景保存失败：" + scenePath);

                AssetDatabase.SaveAssets();
                Selection.activeGameObject = canvasObject;
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath));
                Debug.Log("[Main Menu] 已生成独立场景：" + scenePath +
                    "\nCanvas Prefab：" + prefabPath +
                    "\n原有场景保持打开。预览时请单独打开新场景，避免多个 Canvas / EventSystem 重叠。" +
                    "\n按钮 OnClick 为空；请接入项目自己的新游戏、继续、设置、制作人员、退出逻辑。");
            }
            catch (Exception exception)
            {
                // Any half-built scene belongs to this operation, not to the user's work.
                if (generatedScene.IsValid() && generatedScene.isLoaded)
                    EditorSceneManager.CloseScene(generatedScene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                Debug.LogError("[Main Menu] 未完成生成：" + exception.Message);
            }
        }

        private static string FindPackageRoot()
        {
            string selectedPath = Normalize(AssetDatabase.GetAssetPath(Selection.activeObject));
            List<string> candidates = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("MainMenuLayerBuilder t:MonoScript"))
            {
                string path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
                const string suffix = "/Unity/Editor/MainMenuLayerBuilder.cs";
                if (!path.EndsWith(suffix, StringComparison.Ordinal))
                    continue;
                string root = path.Substring(0, path.Length - suffix.Length);
                if (File.Exists(ToAbsolutePath(root + "/" + ManifestName)))
                    candidates.Add(root);
            }
            foreach (string candidate in candidates)
            {
                if (selectedPath == candidate || selectedPath.StartsWith(candidate + "/",
                    StringComparison.Ordinal))
                    return candidate;
            }
            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count > 1)
                throw new InvalidOperationException(
                    "找到多份素材包。请先在 Project 中选中本次素材包的 layout.json，再执行菜单。");
            throw new FileNotFoundException(
                "找不到 layout.json。请将整个素材包放入 Assets，保留 Art、Unity 和 layout.json 的相对位置。");
        }

        private static List<ResolvedLayer> ValidateAndResolve(LayerManifest manifest, string root)
        {
            if (manifest == null || manifest.layers == null || manifest.layers.Length == 0)
                throw new InvalidDataException("layout.json 没有有效的 layers 数组。");
            List<ResolvedLayer> result = new List<ResolvedLayer>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> orders = new HashSet<int>();
            HashSet<string> buttons = new HashSet<string>(StringComparer.Ordinal);
            foreach (LayerEntry entry in manifest.layers)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    throw new InvalidDataException("每个图层必须有唯一、非空的 id。");
                if (!orders.Add(entry.order))
                    throw new InvalidDataException("图层 order 必须唯一：" + entry.id);
                if (!IsFinite(entry.x) || !IsFinite(entry.y) || !IsFinite(entry.width) ||
                    !IsFinite(entry.height) || entry.width <= 0f || entry.height <= 0f)
                    throw new InvalidDataException("图层坐标或尺寸无效：" + entry.id);

                string relative = Normalize(entry.file);
                if (string.IsNullOrEmpty(relative) || !relative.StartsWith("Art/",
                    StringComparison.Ordinal) || relative.Contains("..") ||
                    !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("图层 file 必须是 Art/ 下的相对 PNG 路径：" + entry.id);
                string assetPath = root + "/" + relative;
                if (!File.Exists(ToAbsolutePath(assetPath)))
                    throw new FileNotFoundException("缺少图层文件：" + assetPath);

                string buttonName = entry.interactive
                    ? GetButtonName(entry.role) ?? GetButtonName(entry.id) : null;
                if (entry.interactive && (buttonName == null || !buttons.Add(buttonName)))
                    throw new InvalidDataException(
                        "交互图层 role 需唯一，且为 new_game / continue / settings / credits / quit：" + entry.id);
                result.Add(new ResolvedLayer
                {
                    entry = entry,
                    assetPath = assetPath,
                    buttonName = buttonName
                });
            }
            if (buttons.Count != 5)
                throw new InvalidDataException("主界面需要五个交互图层：new_game、continue、settings、credits、quit。");
            result.Sort((a, b) => a.entry.order.CompareTo(b.entry.order));
            return result;
        }

        private static string GetButtonName(string role)
        {
            string key = (role ?? "").Replace("_", "").Replace("-", "")
                .Replace(" ", "").ToLowerInvariant();
            if (key.StartsWith("button", StringComparison.Ordinal))
                key = key.Substring(6);
            else if (key.StartsWith("btn", StringComparison.Ordinal))
                key = key.Substring(3);
            switch (key)
            {
                case "newgame": return "Btn_NewGame";
                case "continue": return "Btn_Continue";
                case "settings": return "Btn_Settings";
                case "credits": return "Btn_Credits";
                case "quit": return "Btn_Quit";
                default: return null;
            }
        }

        private static void ConfigureSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidDataException("Unity 未识别此 PNG，请等待导入完成后重试：" + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.spriteBorder = Vector4.zero;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
        }

        private static GameObject MakeCanvas(float width, float height)
        {
            GameObject root = new GameObject("MainMenuCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(width, height);
            // Expand + centered fixed-size DesignRoot uniformly fits the artwork.
            // Non-16:9 screens reveal the LetterboxBackground, never crop the notebook.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform background = MakeRect("LetterboxBackground", root.transform);
            Stretch(background);
            Image black = background.gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            return root;
        }

        private static void MakeLayer(ResolvedLayer layer, RectTransform designRoot)
        {
            LayerEntry entry = layer.entry;
            string objectName = layer.buttonName ?? entry.id;
            RectTransform rect = MakeRect(objectName, designRoot);
            SetTopLeftRect(rect, entry.x, entry.y, entry.width, entry.height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(layer.assetPath);
            if (image.sprite == null)
                throw new InvalidDataException("无法加载 Sprite：" + layer.assetPath);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            image.raycastTarget = entry.interactive;
            if (!entry.interactive)
                return;

            // PNGs are tightly cropped: only this paper strip's own rect receives clicks.
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.RemoveAllListeners();

            RectTransform label = MakeRect("LabelAnchor_Empty", rect);
            Stretch(label);
            label.offsetMin = new Vector2(24f, 8f);
            label.offsetMax = new Vector2(-24f, -8f);
        }

        private static void MakeEventSystem()
        {
            GameObject root = new GameObject("MainMenuEventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            root.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            root.AddComponent<StandaloneInputModule>();
#endif
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetTopLeftRect(RectTransform rect, float x, float y,
            float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] segments = Normalize(assetPath).Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('\\', '/');
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
#endif

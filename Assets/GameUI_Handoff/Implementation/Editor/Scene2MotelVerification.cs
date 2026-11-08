using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Explicit menu/API checks only; never runs on import or saves scenes/assets.</summary>
    public static class Scene2MotelVerification
    {
        public const string ScenePath = "Assets/GameUI_Handoff/Implementation/Scenes/Scene2Motel.unity";
        const string Handoff = "Assets/Scene2_Motel_Unity_Handoff/";
        const string Art = "Assets/ThatGameJam2026/Scene2Motel/Art/";
        const string ItemsPath = "Design_1920x1080/InventoryPanel/Items";
        const string DefaultItem = "open_suitcase";
        static readonly string[] Ids = { "violin_case", "beer", "coat", "open_suitcase", "music_score", "biscuit_tin" };
        static readonly string[] PreviousIds = { "phone", "bag", "key", "ticket", "book", "wallet" };

        [Serializable] sealed class BoundsFile { public ArtBounds[] assets; }
        [Serializable] sealed class ArtBounds { public string path; public int[] canvas_size; public BoundsRect sprite_rect_unity; }
        [Serializable] sealed class BoundsRect
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }
        [Serializable] sealed class ArtReport
        {
            public string id, sourcePath, assetGuid;
            public long nativeFileId;
            public Rect spriteRect;
            public Vector2 drawingSizeInDesign, drawingSizeInPixels;
            public float sourceAspect, displayedAspect;
        }
        [Serializable] sealed class Report
        {
            public string scene, selectedItem, inputModule;
            public Vector2 gameViewSize, referenceResolution;
            public float gameViewAspect, designAspect, canvasScale, expectedCanvasScale;
            public int unchangedRectTransforms, visibleSelectionFrames;
            public ArtReport[] art;
            public string[] checks;
        }

        [MenuItem("Tools/Game UI/Open Scene 2 - Motel")]
        public static void Open()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play before opening the motel scene.");
            Require(File.Exists(ScenePath), "The generated motel scene does not exist: " + ScenePath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Game UI/Verify Scene 2 - Motel")]
        public static void VerifyMenu()
        {
            Debug.Log(ValidateCurrent());
            Debug.Log(VerifySelection());
        }

        /// <summary>Checks the currently open scene; returns a JSON report without writing a file.</summary>
        public static string ValidateCurrent()
        {
            var root = Root();
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            Require(canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Expected the inherited overlay Canvas.");
            Require(root.GetComponentsInChildren<Canvas>(true).Length == 1, "Expected one Canvas.");
            Require(scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
                scaler.referenceResolution == new Vector2(1920, 1080) && Mathf.Approximately(scaler.referencePixelsPerUnit, 100),
                "The original CanvasScaler reference/Expand configuration changed.");
            var raycaster = root.GetComponent<GraphicRaycaster>();
            Require(raycaster && raycaster.isActiveAndEnabled, "The inherited GraphicRaycaster must be active.");
            var items = Items(root);
            Require(items.childCount == Ids.Length, "Expected the six inherited item slots.");
            var selection = items.GetComponent<PaperInventorySelection>();
            Require(selection && selection.isActiveAndEnabled && Ids.Contains(selection.SelectedItem), "Missing/invalid motel inventory selection.");
            AssertSelection(items, selection, selection.SelectedItem);
            var system = SystemFor(root.scene);
            string input = ValidateInput(system);

            Canvas.ForceUpdateCanvases();
            var design = root.transform.Find("Design_1920x1080") as RectTransform;
            Require(design && design.sizeDelta == new Vector2(1920, 1080), "The original fixed design size changed.");
            Vector2 gameSize = Handles.GetMainGameViewSize();
            Require(gameSize.x > 0 && gameSize.y > 0, "Open the Game view before checking its display scale.");
            float expectedScale = Mathf.Min(gameSize.x / scaler.referenceResolution.x, gameSize.y / scaler.referenceResolution.y);
            Require(Mathf.Abs(canvas.scaleFactor - expectedScale) < .002f,
                "Canvas scale does not match the current Game view. Activate/repaint Game view and verify again.");
            var corners = new Vector3[4];
            design.GetWorldCorners(corners);
            Require(corners.Select(p => RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, p))
                .All(p => p.x >= -.6f && p.y >= -.6f && p.x <= gameSize.x + .6f && p.y <= gameSize.y + .6f),
                "The design is clipped at the current Game view resolution.");

            var bounds = JsonUtility.FromJson<BoundsFile>(File.ReadAllText(Handoff + "Design/Scene2Motel/sprite_bounds.json"));
            Require(bounds != null && bounds.assets != null, "Missing valid supplied Sprite bounds.");
            var art = new List<ArtReport>();
            for (int i = 0; i < Ids.Length; i++)
            {
                var item = items.Find(Ids[i]);
                Require(item, "Missing motel item: " + Ids[i]);
                var hit = item.Find("InventoryHitArea");
                Require(hit && hit.gameObject.activeInHierarchy && hit.GetComponent<Image>() && hit.GetComponent<Image>().raycastTarget,
                    "Missing inherited click area for " + Ids[i]);
                var frame = item.Find("SelectionFrame").GetComponent<PaperSelectionFrame>();
                Require(frame && !frame.raycastTarget, "Selection decorations must not intercept clicks.");
                art.Add(CheckArt(item.GetComponent<Image>(), bounds, "Props/" + Ids[i] + ".png", canvas.scaleFactor));
            }
            var room = design.Find("SceneFrame/motel_room");
            Require(room && !design.Find("SceneFrame/concert_hall"), "Expected the motel room instead of the concert hall.");
            Require(room.GetComponentsInParent<Mask>(true).All(mask => !mask.isActiveAndEnabled) &&
                room.GetComponentsInParent<RectMask2D>(true).All(mask => !mask.isActiveAndEnabled),
                "The complete room illustration must not be cropped by a UI mask.");
            art.Add(CheckArt(room.GetComponent<Image>(), bounds, "Room/room_lights_on.png", canvas.scaleFactor));

            var report = new Report
            {
                scene = root.scene.path, selectedItem = selection.SelectedItem, inputModule = input,
                gameViewSize = gameSize, gameViewAspect = gameSize.x / gameSize.y,
                referenceResolution = scaler.referenceResolution, designAspect = 1920f / 1080,
                canvasScale = canvas.scaleFactor, expectedCanvasScale = expectedScale,
                unchangedRectTransforms = CheckGeometry(root), visibleSelectionFrames = 1, art = art.ToArray(),
                checks = new[] { "Original CanvasScaler reference and inherited RectTransform geometry preserved",
                    "Seven original native Sprites use exact supplied Rects, white Image tint and contain-fit",
                    "Room lights-on composition complete; no concert hall Image or room clipping mask",
                    "Six original hit areas, one selected paper frame and persistent UI input references",
                    "Current Game view scale and design bounds verified; no scene/prefab/asset save performed" }
            };
            return JsonUtility.ToJson(report, true);
        }

        /// <summary>Hardware-independent raycast/event-route checks; always restores the suitcase selection.</summary>
        public static string VerifySelection()
        {
            var root = Root(); // A wrong scene is rejected before any selection changes.
            var items = Items(root);
            var selection = items.GetComponent<PaperInventorySelection>();
            Require(selection, "The motel clone has no selection handler.");
            var report = new List<string>();
            try
            {
                ValidateCurrent();
                var raycaster = root.GetComponent<GraphicRaycaster>();
                var system = SystemFor(root.scene);
                foreach (string id in Ids)
                {
                    var hit = Hit(items, raycaster, system, id);
                    Click(items, system, hit, hit, PointerEventData.InputButton.Left);
                    AssertSelection(items, selection, id);
                    report.Add("PASS raycast -> hit area -> Items -> single selection: " + id);
                }
                string before = selection.SelectedItem;
                var first = Hit(items, raycaster, system, Ids[0]);
                Click(items, system, first, first, PointerEventData.InputButton.Right);
                AssertSelection(items, selection, before);
                report.Add("PASS right click preserves " + before);
                var second = Hit(items, raycaster, system, Ids[1]);
                Click(items, system, first, second, PointerEventData.InputButton.Left);
                AssertSelection(items, selection, before);
                report.Add("PASS press violin_case/release beer preserves " + before);
            }
            finally
            {
                selection.SelectItem(DefaultItem);
                Canvas.ForceUpdateCanvases();
                AssertSelection(items, selection, DefaultItem);
            }
            report.Add("PASS restored open_suitcase; exactly one selected frame");
            return string.Join("\n", report);
        }

        static ArtReport CheckArt(Image image, BoundsFile bounds, string suffix, float canvasScale)
        {
            Require(image && image.sprite && image.type == Image.Type.Simple && image.preserveAspect && image.color == Color.white,
                "Expected an unfiltered contain-fit original Image: " + suffix);
            var entry = bounds.assets.Single(asset => asset.path == Art + suffix);
            var sprite = image.sprite;
            string path = Handoff + entry.path;
            Require(AssetDatabase.GetAssetPath(sprite) == path && sprite.rect == entry.sprite_rect_unity.Rect,
                "The Image must reference the supplied original PNG with its exact cropped Sprite Rect: " + suffix);
            Require(sprite.texture.width == entry.canvas_size[0] && sprite.texture.height == entry.canvas_size[1],
                "The original texture was rescaled during import: " + suffix);
            Require(EditorUtility.IsPersistent(sprite) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId) && fileId != 0,
                "The Sprite reference must be a persistent native subasset: " + suffix);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string assetGuid, out long nativeId);
            Rect box = image.rectTransform.rect;
            float fit = Mathf.Min(box.width / sprite.rect.width, box.height / sprite.rect.height);
            var drawing = sprite.rect.size * fit;
            Require(fit > 0 && drawing.x <= box.width + .01f && drawing.y <= box.height + .01f,
                "The complete original art must fit inside the inherited Image rectangle.");
            return new ArtReport { id = image.name, sourcePath = path, assetGuid = assetGuid, nativeFileId = nativeId,
                spriteRect = sprite.rect, drawingSizeInDesign = drawing, drawingSizeInPixels = drawing * canvasScale,
                sourceAspect = sprite.rect.width / sprite.rect.height, displayedAspect = drawing.x / drawing.y };
        }

        static int CheckGeometry(GameObject root)
        {
            // The scene's serialized prefab overrides are the accepted source layout.
            // A preview scene resolves them without replacing the user's active scene.
            var activeScene = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.OpenPreviewScene(PaperCollageBuilder.ScenePath);
            try
            {
                Require(SceneManager.GetActiveScene() == activeScene, "Opening the source preview unexpectedly changed the active scene.");
                var original = preview.GetRootGameObjects().SingleOrDefault(go => go.name == "PaperCollageUIRoot");
                Require(original, "The source scene-one preview has no PaperCollageUIRoot.");
                int checkedCount = 0;
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect == root.transform) continue; // Root Canvas dimensions are driven by the display.
                    string path = AnimationUtility.CalculateTransformPath(rect, root.transform);
                    string oldPath = path.Replace("SceneFrame/motel_room", "SceneFrame/concert_hall");
                    for (int i = 0; i < Ids.Length; i++)
                    {
                        string prefix = ItemsPath + "/" + Ids[i];
                        if (oldPath == prefix || oldPath.StartsWith(prefix + "/", StringComparison.Ordinal))
                            oldPath = ItemsPath + "/" + PreviousIds[i] + oldPath.Substring(prefix.Length);
                    }
                    var source = original.transform.Find(oldPath) as RectTransform;
                    Require(source && rect.anchorMin == source.anchorMin && rect.anchorMax == source.anchorMax &&
                        rect.pivot == source.pivot && Vector2.Distance(rect.anchoredPosition, source.anchoredPosition) < .002f &&
                        Vector2.Distance(rect.sizeDelta, source.sizeDelta) < .002f &&
                        Vector3.Distance(rect.localScale, source.localScale) < .0001f &&
                        Quaternion.Angle(rect.localRotation, source.localRotation) < .001f && rect.GetSiblingIndex() == source.GetSiblingIndex(),
                        "Inherited layout/stacking differs from scene one's final serialized layout: " + path);
                    checkedCount++;
                }
                return checkedCount;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                Require(SceneManager.GetActiveScene() == activeScene, "Closing the source preview unexpectedly changed the active scene.");
            }
        }

        static string ValidateInput(EventSystem system)
        {
#if ENABLE_INPUT_SYSTEM
            var module = system.GetComponent<InputSystemUIInputModule>();
            Require(module && module.isActiveAndEnabled && module.point && module.leftClick &&
                module.point.action != null && module.leftClick.action != null && EditorUtility.IsPersistent(module.actionsAsset) &&
                EditorUtility.IsPersistent(module.point) && EditorUtility.IsPersistent(module.leftClick),
                "The Input System UI module needs persistent action-asset, pointer and click references.");
            Require(system.GetComponents<StandaloneInputModule>().All(legacy => !legacy.isActiveAndEnabled),
                "A legacy input module must not compete with the configured Input System module.");
            return "InputSystemUIInputModule; persistent asset/point/click";
#else
            Require(system.GetComponent<StandaloneInputModule>() && system.GetComponent<StandaloneInputModule>().isActiveAndEnabled,
                "The configured legacy UI input module must be active.");
            return "StandaloneInputModule";
#endif
        }

        static RaycastResult Hit(Transform items, GraphicRaycaster raycaster, EventSystem system, string id)
        {
            var item = items.Find(id) as RectTransform;
            Require(item, "Missing motel item: " + id);
            var pointer = new PointerEventData(system) { pointerId = -1,
                position = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, item.TransformPoint(item.rect.center)),
                button = PointerEventData.InputButton.Left };
            var direct = new List<RaycastResult>();
            raycaster.Raycast(pointer, direct);
            Require(direct.Count > 0 && direct[0].gameObject == item.Find("InventoryHitArea").gameObject,
                "The real GraphicRaycaster did not hit the inherited item rectangle: " + id);
            var all = new List<RaycastResult>();
            system.RaycastAll(pointer, all);
            Require(all.Count > 0 && all[0].module == raycaster && all[0].gameObject == direct[0].gameObject,
                "A different raycaster or overlay blocks item " + id);
            Require(ExecuteEvents.GetEventHandler<IPointerClickHandler>(direct[0].gameObject) == items.gameObject,
                "The actual hit hierarchy does not route clicks to Items: " + id);
            return direct[0];
        }

        static void Click(Transform items, EventSystem system, RaycastResult press, RaycastResult release, PointerEventData.InputButton button)
        {
            var pointer = new PointerEventData(system) { pointerId = button == PointerEventData.InputButton.Right ? -2 : -1,
                button = button, position = release.screenPosition, pressPosition = press.screenPosition,
                pointerPressRaycast = press, pointerCurrentRaycast = release,
                pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(press.gameObject),
                rawPointerPress = press.gameObject, eligibleForClick = true, clickCount = 1 };
            Require(ExecuteEvents.ExecuteHierarchy(release.gameObject, pointer, ExecuteEvents.pointerClickHandler) == items.gameObject,
                "The click event did not route from its real hit object to Items.");
        }

        static void AssertSelection(Transform items, PaperInventorySelection selection, string expected)
        {
            Require(selection.SelectedItem == expected, "Expected selected ID " + expected + "; found " + selection.SelectedItem);
            int active = 0;
            foreach (string id in Ids)
            {
                var item = items.Find(id);
                var frame = item ? item.Find("SelectionFrame") : null;
                Require(frame && frame.gameObject.activeSelf == (id == expected), "Incorrect selected-frame state for " + id);
                if (frame.gameObject.activeInHierarchy) active++;
            }
            Require(active == 1, "Expected exactly one active selection frame.");
        }

        static GameObject Root()
        {
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == ScenePath, "Open Scene2Motel before verifying; scene one is never modified by these checks.");
            var root = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "PaperCollageUIRoot");
            Require(root && root.activeInHierarchy, "Expected the inherited active PaperCollageUIRoot.");
            return root;
        }

        static Transform Items(GameObject root)
        {
            var items = root.transform.Find(ItemsPath);
            Require(items, "Missing inherited inventory group.");
            return items;
        }

        static EventSystem SystemFor(Scene scene)
        {
            var systems = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<EventSystem>(true))
                .Where(system => system.isActiveAndEnabled).ToArray();
            Require(systems.Length == 1, "Expected exactly one active EventSystem in the motel scene.");
            return systems[0];
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Scene2 motel verification: " + message);
        }
    }
}

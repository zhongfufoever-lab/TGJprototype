using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace TGJ.PaperCollage.Editor
{
    /// <summary>Adds input and stronger paper frames to the existing six-item inventory.</summary>
    public static class PaperInventorySelectionRefinement
    {
        static readonly string[] ItemIds = { "phone", "bag", "key", "ticket", "book", "wallet" };
        const string HitAreaName = "InventoryHitArea";

        [MenuItem("Tools/Game UI/Refine Existing Study/6 - Clickable inventory selection")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before installing inventory interaction.");
            var root = StudyRoot();
            var items = Items(root);
            foreach (string id in ItemIds)
                Require(items.Find(id) && items.Find(id).GetComponent<Image>(), "Missing inventory art: " + id);
            var systems = SceneSystems(root.scene);
            Require(systems.Count(s => s.isActiveAndEnabled) <= 1,
                "Resolve the existing duplicate active EventSystems before installing inventory input.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Refine inventory selection and input");
            Undo.RegisterFullObjectHierarchyUndo(root, "Refine inventory selection and input");

            var canvas = root.GetComponent<Canvas>();
            Require(canvas, "PaperCollageUIRoot needs its existing Canvas.");
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (!raycaster) raycaster = Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            raycaster.enabled = true;

            foreach (string id in ItemIds)
            {
                var item = (RectTransform)items.Find(id);
                // The separate transparent rectangle provides a comfortable click area,
                // including holes and transparent margins of the contain-fit item sprite.
                var hitRect = Child(item, HitAreaName);
                Stretch(hitRect, 9, 8);
                var hitImage = hitRect.GetComponent<Image>();
                if (!hitImage) hitImage = Undo.AddComponent<Image>(hitRect.gameObject);
                hitImage.sprite = null;
                hitImage.color = Color.clear;
                hitImage.raycastTarget = true;
                hitImage.alphaHitTestMinimumThreshold = 0;
                hitImage.canvasRenderer.cullTransparentMesh = false;
                hitRect.gameObject.SetActive(true);

                var frameRect = Child(item, "SelectionFrame");
                Stretch(frameRect, 12, 11);
                var frame = frameRect.GetComponent<PaperSelectionFrame>();
                if (!frame) frame = Undo.AddComponent<PaperSelectionFrame>(frameRect.gameObject);
                frame.color = PaperCollageBuilder.Hex("#FFF0C8");
                frame.lineWidth = 2.8f;
                frame.paperLipWidth = 5;
                frame.paperLipColor = new Color(.97f, .92f, .78f, .34f);
                frame.cornerRadius = 9;
                frame.edgeVariation = .45f;
                frame.edgeSeed = Array.IndexOf(ItemIds, id) * 7 + 17;
                frame.shadowColor = new Color(.06f, .08f, .09f, .26f);
                frame.shadowOffset = new Vector2(1.5f, -2);
                frame.feather = .65f;
                frame.raycastTarget = false;
                frame.SetAllDirty();
                hitRect.SetAsLastSibling();
            }

            var selection = items.GetComponent<PaperInventorySelection>();
            if (!selection) selection = Undo.AddComponent<PaperInventorySelection>(items.gameObject);
            selection.Configure(items);
            selection.SelectItem(ItemIds.Contains(selection.SelectedItem) ? selection.SelectedItem : "ticket");
            EnsureEventSystem(root, systems);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("PAPER_INVENTORY_INTERACTION_READY: " + Validate());
        }

        /// <summary>Read-only structural report; does not change selection or save assets.</summary>
        public static string Validate()
        {
            var root = StudyRoot();
            var items = Items(root);
            Require(items.childCount == ItemIds.Length, "Expected exactly six inventory items.");
            var selection = items.GetComponent<PaperInventorySelection>();
            Require(selection, "Missing inventory selection handler.");
            Require(root.GetComponent<GraphicRaycaster>() && root.GetComponent<GraphicRaycaster>().enabled,
                "The Canvas needs an enabled GraphicRaycaster.");
            int selectedFrames = 0;
            foreach (string id in ItemIds)
            {
                var item = items.Find(id);
                Require(item, "Missing inventory item: " + id);
                var hit = item.Find(HitAreaName);
                Require(hit && hit.gameObject.activeSelf && hit.GetComponent<Image>() && hit.GetComponent<Image>().raycastTarget,
                    "Missing click rectangle: " + id);
                var frame = item.Find("SelectionFrame");
                Require(frame && frame.GetComponent<PaperSelectionFrame>() && !frame.GetComponent<PaperSelectionFrame>().raycastTarget,
                    "Missing non-blocking selection frame: " + id);
                if (frame.gameObject.activeSelf)
                {
                    selectedFrames++;
                    Require(selection.SelectedItem == id, "Visible selection and selected ID disagree.");
                }
            }
            Require(selectedFrames == 1, "Expected exactly one visible selected frame.");
            var systems = SceneSystems(root.scene).Where(s => s.isActiveAndEnabled).ToArray();
            Require(systems.Length == 1, "Expected exactly one active EventSystem.");
#if ENABLE_INPUT_SYSTEM
            var module = systems[0].GetComponent<InputSystemUIInputModule>();
            Require(module && module.enabled && module.point && module.leftClick,
                "The active Input System UI module needs pointer and click actions.");
            string input = "InputSystemUIInputModule";
#else
            Require(systems[0].GetComponent<StandaloneInputModule>() && systems[0].GetComponent<StandaloneInputModule>().enabled,
                "Missing active legacy UI input module.");
            string input = "StandaloneInputModule";
#endif
            return "6 clickable original items; 1 visible paper frame; selected=" + selection.SelectedItem +
                "; GraphicRaycaster active; " + input + " ready.";
        }

        static void EnsureEventSystem(GameObject root, EventSystem[] systems)
        {
            var system = systems.FirstOrDefault(s => s.isActiveAndEnabled) ?? systems.FirstOrDefault();
            if (!system)
            {
                var go = new GameObject("PaperCollageEventSystem", typeof(EventSystem));
                go.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Add paper collage UI EventSystem");
                system = go.GetComponent<EventSystem>();
            }
            Undo.RecordObject(system, "Enable inventory UI input");
            Undo.RecordObject(system.gameObject, "Enable inventory UI input");
            system.gameObject.SetActive(true);
            system.enabled = true;
#if ENABLE_INPUT_SYSTEM
            var module = system.GetComponent<InputSystemUIInputModule>();
            if (!module) module = Undo.AddComponent<InputSystemUIInputModule>(system.gameObject);
            Undo.RecordObject(module, "Configure inventory UI input actions");
            module.enabled = true;
            // Persist package action references, rather than transient actions created by
            // AssignDefaultActions(), so the scene and prefab reload with working input.
            if (!module.point || !module.leftClick || !EditorUtility.IsPersistent(module.actionsAsset))
            {
                const string path = "Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions";
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                Require(asset, "The installed Input System default UI actions could not be loaded.");
                var references = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().ToArray();
                module.actionsAsset = asset;
                module.point = Action(references, "Point");
                module.leftClick = Action(references, "Click");
                module.rightClick = Action(references, "RightClick");
                module.middleClick = Action(references, "MiddleClick");
                module.scrollWheel = Action(references, "ScrollWheel");
                module.move = Action(references, "Navigate");
                module.submit = Action(references, "Submit");
                module.cancel = Action(references, "Cancel");
                module.trackedDevicePosition = Action(references, "TrackedDevicePosition");
                module.trackedDeviceOrientation = Action(references, "TrackedDeviceOrientation");
            }
            foreach (var legacy in system.GetComponents<StandaloneInputModule>())
            {
                Undo.RecordObject(legacy, "Use the configured Input System UI module");
                legacy.enabled = false;
            }
#else
            var module = system.GetComponent<StandaloneInputModule>();
            if (!module) module = Undo.AddComponent<StandaloneInputModule>(system.gameObject);
            Undo.RecordObject(module, "Enable inventory legacy UI input");
            module.enabled = true;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static InputActionReference Action(InputActionReference[] references, string name)
        {
            var action = references.FirstOrDefault(r => r.action != null &&
                r.action.actionMap.name == "UI" && r.action.name == name);
            Require(action, "Missing built-in UI action reference: " + name);
            return action;
        }
#endif

        static GameObject StudyRoot()
        {
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == PaperCollageBuilder.ScenePath, "Open PaperCollageStudy first.");
            var root = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "PaperCollageUIRoot");
            Require(root, "The existing paper collage UI root was not found.");
            return root;
        }

        static Transform Items(GameObject root)
        {
            var items = root.transform.Find("Design_1920x1080/InventoryPanel/Items");
            Require(items, "The existing inventory group was not found.");
            return items;
        }

        static EventSystem[] SceneSystems(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<EventSystem>(true)).ToArray();

        static RectTransform Child(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Add inventory paper selection detail");
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.offsetMin = new Vector2(-horizontal, -vertical);
            rect.offsetMax = new Vector2(horizontal, vertical);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Paper inventory: " + message);
        }
    }
}

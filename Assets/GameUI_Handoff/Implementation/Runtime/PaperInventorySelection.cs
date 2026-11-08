using UnityEngine;
using UnityEngine.EventSystems;

namespace TGJ.PaperCollage
{
    /// <summary>One paper frame at a time, selected through UGUI mouse or touch events.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Paper Collage/Paper Inventory Selection")]
    public sealed class PaperInventorySelection : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Transform itemsRoot;
        [SerializeField] string selectedItem = "ticket";

        bool refreshPending = true;

        public string SelectedItem => selectedItem;

        Transform ItemsRoot => itemsRoot ? itemsRoot : transform;

        public void Configure(Transform root)
        {
            itemsRoot = root;
            refreshPending = true;
        }

        public void SelectItem(string itemId)
        {
            // Ignore unknown IDs rather than clearing a valid selection accidentally.
            if (!string.IsNullOrEmpty(itemId))
            {
                Transform item = ItemsRoot.Find(itemId);
                if (!item || item.parent != ItemsRoot || !item.Find("SelectionFrame")) return;
            }
            selectedItem = itemId ?? string.Empty;
            refreshPending = false;
            ApplySelection();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Transform pressed = ItemFor(eventData.pointerPressRaycast.gameObject);
            Transform released = ItemFor(eventData.pointerCurrentRaycast.gameObject);
            // The handler lives on Items. A press on one item and release on another
            // must not become a click merely because they share the same handler.
            if (!pressed || pressed != released) return;
            SelectItem(pressed.name);
        }

        Transform ItemFor(GameObject hit)
        {
            if (!hit) return null;
            Transform item = hit.transform;
            while (item && item != ItemsRoot)
            {
                if (item.parent == ItemsRoot)
                    return item.Find("SelectionFrame") ? item : null;
                item = item.parent;
            }
            return null;
        }

        void OnEnable() => refreshPending = true;

        // Unity may validate during loading. Defer hierarchy changes to the editor/player update.
        void OnValidate() => refreshPending = true;

        void OnTransformChildrenChanged() => refreshPending = true;

        void Update()
        {
            if (!refreshPending) return;
            refreshPending = false;
            ApplySelection();
        }

        void ApplySelection()
        {
            bool selected = false;
            foreach (Transform item in ItemsRoot)
            {
                Transform frame = item.Find("SelectionFrame");
                if (!frame) continue;
                bool show = !selected && !string.IsNullOrEmpty(selectedItem) && item.name == selectedItem;
                if (show) selected = true;
                if (frame.gameObject.activeSelf != show) frame.gameObject.SetActive(show);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
namespace TGJ {
public class ItemDrag:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler {
    [System.NonSerialized] public GrayboxGame game;
    [System.NonSerialized] public string item;
    public void OnBeginDrag(PointerEventData e){if(game)game.BeginDrag(item,e.position);}
    public void OnDrag(PointerEventData e){if(game)game.MoveDrag(e.position);}
    public void OnEndDrag(PointerEventData e){if(game)game.EndDrag(e.position);}
}
}

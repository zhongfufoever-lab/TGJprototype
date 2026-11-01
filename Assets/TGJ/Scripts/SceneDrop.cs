using UnityEngine;
using UnityEngine.EventSystems;
namespace TGJ {
public class SceneDrop:MonoBehaviour,IPointerClickHandler {
    [System.NonSerialized] public GrayboxGame game;
    public void OnPointerClick(PointerEventData e){if(game)game.ClickScene(e.position);}
}
}

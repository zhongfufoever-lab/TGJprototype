using System;
using UnityEngine;

namespace TGJ {
public class EditableUIRoot : MonoBehaviour {
    public RectTransform layout;
    public UIView[] views;
    [Header("可换美术的动态物品预制体")]
    public UIElement placedItemPrefab,dragItemPrefab;
    [Header("场景演出")]
    [Range(.2f,1f)] public float conversationWorldScale=.625f;
    public Vector2 parallaxDistance=new Vector2(20,20);
    public bool showPlaceholderLabels=true;
    public UIView Get(string id){foreach(var view in views)if(view&&view.id==id)return view;throw new InvalidOperationException("UI view missing: "+id);}
    public void HideViews(){foreach(var view in views)if(view)view.gameObject.SetActive(false);}
    public UIView Show(string id){var view=Get(id);view.gameObject.SetActive(true);return view;}
    public void ApplyAnnotations(){foreach(var e in GetComponentsInChildren<UIElement>(true)){if(e.functionLabel)e.functionLabel.gameObject.SetActive(showPlaceholderLabels);if(e.noteLabel)e.noteLabel.gameObject.SetActive(showPlaceholderLabels);}}
}
}

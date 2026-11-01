using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ {
public class UIView : MonoBehaviour {
    [Tooltip("界面绑定 ID，请保留。")]
    public string id;
    public UIElement[] elements;
    [Header("滚动列表（仅带列表的界面使用）")]
    public RectTransform content;
    public ScrollRect scroll;
    public UIElement rowPrefab,currentRowPrefab;
    [Min(0)] public float rowVerticalPadding=24;
    public InputField input;
    public CanvasGroup fade;
    [Tooltip("仅用于停止运行时的预览，运行时隐藏，打包时移除。")]
    public GameObject previewContent;
    readonly List<UIElement> rows=new List<UIElement>();
    Dictionary<string,UIElement> cache;
    public UIElement Get(string key){
        if(cache==null){cache=new Dictionary<string,UIElement>();foreach(var e in elements)if(e)cache.Add(e.key,e);}
        if(cache.TryGetValue(key,out var result))return result;
        throw new InvalidOperationException("UI binding missing: "+id+" / "+key);
    }
    public void ClearRows(){if(previewContent)previewContent.SetActive(false);foreach(var row in rows)if(row){row.gameObject.SetActive(false);Destroy(row.gameObject);}rows.Clear();}
    public UIElement AddRow(string text,bool current=false){
        var row=Instantiate(current&&currentRowPrefab?currentRowPrefab:rowPrefab,content,false);
        row.name="条目 "+(rows.Count+1);row.gameObject.SetActive(true);row.Text(text);rows.Add(row);return row;
    }
    public void FitTextRows(){
        Canvas.ForceUpdateCanvases();
        foreach(var row in rows){var layout=row.GetComponent<LayoutElement>();if(layout&&row.caption)layout.preferredHeight=Mathf.Max(layout.minHeight,row.caption.preferredHeight+rowVerticalPadding);}
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }
}
}

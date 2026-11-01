using UnityEngine;
using UnityEngine.UI;

namespace TGJ {
// Stable binding IDs separate gameplay from the editable hierarchy and artwork.
[DisallowMultipleComponent]
public class UIElement : MonoBehaviour {
    [Tooltip("程序绑定标识。可以改物体名称、排版、美术；请保留此标识。")]
    public string key;
    public Text caption,functionLabel,noteLabel;
    public Image artwork;
    public Button button;
    [Tooltip("普通动态标签可取消此项以保留手填文案。对话正文、输入框和即时提示始终由剧情/状态提供。")]
    public bool useRuntimeText=true;
    public RectTransform Rect => (RectTransform)transform;
    public void Text(string value){if(caption&&useRuntimeText)caption.text=value;}
    public void Note(string value){if(noteLabel)noteLabel.text=value;}
}
}

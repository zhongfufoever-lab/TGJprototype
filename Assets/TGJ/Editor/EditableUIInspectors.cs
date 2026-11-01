using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TGJ.Editor {
[CustomEditor(typeof(EditableUIRoot))]
public class EditableUIRootInspector:UnityEditor.Editor {
    int preview;
    public override void OnInspectorGUI(){
        var ui=(EditableUIRoot)target;
        EditorGUILayout.HelpBox("停止 Play 后，在 Scene 中用 Rect Tool (T) 拖动 UI。主场景里的修改是本场景覆盖；双击 Prefab 可修改默认排版。预览只改变界面可见性，不推进剧情。",MessageType.Info);
        if(EditorUtility.IsPersistent(ui)&&GUILayout.Button("打开 Canvas 预制体进行排版"))AssetDatabase.OpenAsset(ui.gameObject);
        using(new EditorGUI.DisabledScope(Application.isPlaying||EditorUtility.IsPersistent(ui))){
            var choices=ui.views.Where(v=>v&&v.id!="Backdrop"&&v.id!="BlackBackdrop"&&v.id!="Feedback"&&v.id!="AchievementFX"&&v.id!="ModalShade").ToArray();
            if(choices.Length>0){preview=Mathf.Clamp(preview,0,choices.Length-1);preview=EditorGUILayout.Popup("预览界面",preview,choices.Select(v=>v.name).ToArray());
                if(GUILayout.Button("显示此界面"))Preview(ui,choices[preview].id);
                if(GUILayout.Button("在 Scene 中定位此界面")){Selection.activeGameObject=choices[preview].gameObject;if(SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.in2DMode=true;SceneView.lastActiveSceneView.FrameSelected();}}
            }
        }
        EditorGUILayout.Space();DrawDefaultInspector();
    }
    public static void Preview(EditableUIRoot ui,string id){
        Undo.RegisterFullObjectHierarchyUndo(ui.gameObject,"Preview UI");ui.HideViews();ui.Show(id=="Prologue"||id=="Cinematic"?"BlackBackdrop":"Backdrop");
        if(id.StartsWith("Modal_")){ui.Show("MainMenu");ui.Show("ModalShade");}
        else if(id!="MainMenu"&&id!="Prologue"&&id!="Cinematic"){
            ui.Show("HUD");if(!id.StartsWith("Memory_")&&id!="Hall")ui.Show("Hall");
        }
        var view=ui.Show(id);if(view.previewContent)view.previewContent.SetActive(true);if(view.fade)view.fade.alpha=1;
        ui.ApplyAnnotations();EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);SceneView.RepaintAll();
    }
    [MenuItem("TGJ/UI/Select Scene Canvas")]
    static void SelectCanvas(){var ui=Object.FindFirstObjectByType<EditableUIRoot>();if(ui)Selection.activeGameObject=ui.gameObject;}
    [MenuItem("TGJ/UI/Preview Main Menu")]
    static void MainMenu(){var ui=Object.FindFirstObjectByType<EditableUIRoot>();if(ui&&!Application.isPlaying){Preview(ui,"MainMenu");Selection.activeGameObject=ui.gameObject;}}
}
[CustomEditor(typeof(UIElement))]
public class UIElementInspector:UnityEditor.Editor {
    public override void OnInspectorGUI(){
        EditorGUILayout.HelpBox("位置与大小：编辑 Rect Transform。替换美术：编辑 Image 的 Source Image，并按需要把 Color 调成白色。更换字体：编辑对应 Text。请保留绑定 Key 及组件引用。",MessageType.Info);
        DrawDefaultInspector();var e=(UIElement)target;
        if(e.caption&&GUILayout.Button("选择正文 Text"))Selection.activeGameObject=e.caption.gameObject;
        if(e.artwork&&GUILayout.Button("选择背景 / 美术 Image"))Selection.activeGameObject=e.artwork.gameObject;
    }
}
}

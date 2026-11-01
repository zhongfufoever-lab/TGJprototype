using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ.Editor {
public static class ContentCleanup {
    [Serializable] public class Fields {public string caption,function,note;}
    [Serializable] public class Change {public string prefab,view,key;public Fields old,newText;}
    [Serializable] public class Manifest {public Change[] rows;public Replacement[] replacements;}
    [Serializable] public class Replacement {public string oldText,newText;}
    public static string Apply(string path){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before editing assets.");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
        int count=0;
        Action<Text,string,string> set=(text,oldValue,newValue)=>{
            if(!text||text.text!=oldValue||oldValue==newValue)return;
            text.text=newValue;EditorUtility.SetDirty(text);
            if(PrefabUtility.IsPartOfPrefabInstance(text))PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            count++;
        };
        Action<GameObject> clean=root=>{
            foreach(var e in root.GetComponentsInChildren<UIElement>(true)){
                var view=e.GetComponentInParent<UIView>(true);
                var change=manifest.rows.FirstOrDefault(r=>r.view==(view?view.id:"")&&r.key==e.key);
                if(change!=null){
                    set(e.caption,change.old.caption,change.newText.caption);
                    set(e.functionLabel,change.old.function,change.newText.function);
                    set(e.noteLabel,change.old.note,change.newText.note);
                    if(e.name==e.key+"_"+change.old.caption&&change.old.caption!=change.newText.caption){
                        e.name=e.key+"_"+change.newText.caption;EditorUtility.SetDirty(e.gameObject);
                        if(PrefabUtility.IsPartOfPrefabInstance(e))PrefabUtility.RecordPrefabInstancePropertyModifications(e.gameObject);
                    }
                }else{
                    foreach(var r in manifest.replacements){set(e.caption,r.oldText,r.newText);set(e.functionLabel,r.oldText,r.newText);}
                    if(e.noteLabel)set(e.noteLabel,e.noteLabel.text,"");
                }
                // Names of UI objects should not retain retired item/story captions either.
                foreach(var r in manifest.replacements)if(e.gameObject.name==r.oldText){e.gameObject.name=string.IsNullOrEmpty(r.newText)?e.key:r.newText;EditorUtility.SetDirty(e.gameObject);break;}
            }
        };
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/TGJ/UI/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p.EndsWith("GameUI.prefab")?2:p.Contains("/Views/")?1:0).ToArray();
        foreach(var asset in paths){
            var root=PrefabUtility.LoadPrefabContents(asset);
            try{clean(root);PrefabUtility.SaveAsPrefabAsset(root,asset);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects())if(root.GetComponentInChildren<EditableUIRoot>(true))clean(root);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Updated text fields: "+count+"; preserved transforms, art, and bindings.";
    }
}
}

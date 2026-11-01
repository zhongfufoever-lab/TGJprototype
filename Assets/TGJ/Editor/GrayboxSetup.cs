using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGJ.Editor {
public static class GrayboxSetup {
    [MenuItem("TGJ/Reload Story Content")]
    public static void ReloadContent(){
        var catalog=AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/TGJ/Resources/TGJ/Catalog.asset");
        catalog.content=JsonUtility.FromJson<CatalogSeed>(File.ReadAllText("Assets/TGJ/Resources/TGJ/Content.json"));
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
    [MenuItem("TGJ/Legacy/Build Graybox Scene (resets scene)")]
    public static void Build(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before setup.");
        const string path="Assets/TGJ/Resources/TGJ/Catalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<StoryCatalog>(path);
        if(!catalog){catalog=ScriptableObject.CreateInstance<StoryCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        catalog.content=JsonUtility.FromJson<CatalogSeed>(File.ReadAllText("Assets/TGJ/Resources/TGJ/Content.json"));
        catalog.yarnProject=AssetDatabase.LoadAssetAtPath<Yarn.Unity.YarnProject>("Assets/TGJ/Resources/TGJ/Story.yarnproject");
        if(catalog.yarnProject==null||catalog.yarnProject.Program==null)throw new Exception("Yarn compilation not ready.");
        catalog.placeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TGJ/Resources/TGJ/place.wav");
        catalog.collectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TGJ/Resources/TGJ/collect.wav");
        catalog.clickSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TGJ/Resources/TGJ/click.wav");
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        var oldScene=SceneManager.GetActiveScene();if(oldScene.isDirty)throw new Exception("Current scene has unsaved changes; save it before setup.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.GetComponent<Camera>().orthographic=true;camera.GetComponent<Camera>().backgroundColor=new Color(.04f,.07f,.09f);
        var game=new GameObject("TGJ Game Flow",typeof(GrayboxGame));game.GetComponent<GrayboxGame>().catalog=catalog;
        var uiAsset=AssetDatabase.LoadAssetAtPath<GameObject>(EditableUIBuilder.CanvasPath);
        if(uiAsset)game.GetComponent<GrayboxGame>().ui=((GameObject)PrefabUtility.InstantiatePrefab(uiAsset)).GetComponent<EditableUIRoot>();
        Directory.CreateDirectory("Assets/TGJ/Scenes");EditorSceneManager.SaveScene(scene,"Assets/TGJ/Scenes/Graybox.unity");
        var others=EditorBuildSettings.scenes.Where(s=>s.path!="Assets/TGJ/Scenes/Graybox.unity").ToArray();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/TGJ/Scenes/Graybox.unity",true)}.Concat(others).ToArray();
        PlayerSettings.companyName="TGJ";PlayerSettings.productName="孕育与诞生 Graybox";PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();Debug.Log("TGJ_SETUP_OK: 4 logical scenes, "+catalog.content.nodes.Length+" Yarn nodes, 10 memory items.");
    }
    [MenuItem("TGJ/Run Flow Smoke Tests")]
    public static void Tests(){var game=UnityEngine.Object.FindFirstObjectByType<GrayboxGame>();if(!EditorApplication.isPlaying||game==null)throw new Exception("Enter Play mode first.");game.RunSmokeTests();}
    [MenuItem("TGJ/Build Windows Player")]
    public static void BuildPlayer(){EditorApplication.delayCall+=BuildPlayerNow;}
    static void BuildPlayerNow(){
        Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/TGJ/Scenes/Graybox.unity"},locationPathName="Builds/Windows/TGJ.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        Debug.Log("TGJ_BUILD: "+report.summary.result+" / "+report.summary.totalErrors+" errors");
    }
}
}

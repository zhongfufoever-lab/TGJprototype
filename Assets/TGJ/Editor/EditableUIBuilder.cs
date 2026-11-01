using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TGJ.Editor {
// One-time migration only. Runtime code never calls this builder or overwrites these prefabs.
public static class EditableUIBuilder {
    const string Folder="Assets/TGJ/UI";
    public const string CanvasPath=Folder+"/Prefabs/GameUI.prefab";
    static Font font;
    static readonly Color Ink=new Color(.88f,.91f,.92f),Muted=new Color(.64f,.72f,.75f),Panel=new Color(.10f,.15f,.18f),Accent=new Color(.28f,.48f,.47f);
    static readonly List<UIView> views=new List<UIView>();
    static UIElement historyRow,currentRow,listRow,archiveRow;
    static StoryCatalog catalog;
    static RectTransform R(Transform parent,string name,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(parent)r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Text T(Transform parent,string name,string text,float x,float y,float w,float h,int size=26,Color? color=null,TextAnchor align=TextAnchor.UpperLeft){var r=R(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color??Ink;t.alignment=align;t.supportRichText=false;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
    static void Stretch(RectTransform r,float left=0,float top=0,float right=0,float bottom=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
    static UIElement Group(Transform parent,string key,string name,float x,float y,float w,float h){var e=R(parent,name,x,y,w,h).gameObject.AddComponent<UIElement>();e.key=key;return e;}
    static UIElement L(Transform parent,string key,string text,float x,float y,float w,float h,int size=26,Color? color=null,TextAnchor align=TextAnchor.UpperLeft){var t=T(parent,key+"_文字",text,x,y,w,h,size,color,align);var e=t.gameObject.AddComponent<UIElement>();e.key=key;e.caption=t;return e;}
    static UIElement Box(Transform parent,string key,string name,string function,string note,float x,float y,float w,float h,Color? color=null){
        var e=Group(parent,key,name,x,y,w,h);e.artwork=e.gameObject.AddComponent<Image>();e.artwork.color=color??Panel;
        e.functionLabel=T(e.transform,"功能_右上",function,10,7,w-20,22,14,Muted,TextAnchor.UpperRight);var f=e.functionLabel.rectTransform;f.anchorMin=new Vector2(0,1);f.anchorMax=Vector2.one;f.offsetMin=new Vector2(10,-29);f.offsetMax=new Vector2(-10,-7);
        e.noteLabel=T(e.transform,"备注_右下",note,10,h-27,w-20,22,14,Muted,TextAnchor.LowerRight);var n=e.noteLabel.rectTransform;n.anchorMin=n.anchorMax=Vector2.zero;n.anchorMax=new Vector2(1,0);n.pivot=new Vector2(0,0);n.offsetMin=new Vector2(10,5);n.offsetMax=new Vector2(-10,27);
        return e;
    }
    static UIElement B(Transform parent,string key,string title,string function,string note,float x,float y,float w,float h){
        var e=Box(parent,key,key+"_"+title,function,note,x,y,w,h,Accent);e.caption=T(e.transform,"按钮文字",title,18,26,w-36,h-50,h>90?25:23,Ink,TextAnchor.MiddleLeft);
        Stretch(e.caption.rectTransform,h<75?14:18,h<75?23:26,h<75?14:18,h<75?3:26);if(h<75)e.caption.fontSize=21;
        e.button=e.gameObject.AddComponent<Button>();e.button.targetGraphic=e.artwork;var c=e.button.colors;c.highlightedColor=new Color(1.15f,1.15f,1.15f);c.pressedColor=new Color(.7f,.85f,.85f);c.selectedColor=c.highlightedColor;e.button.colors=c;return e;
    }
    static UIView View(string id,string title){var v=R(null,title,0,0,1920,1080).gameObject.AddComponent<UIView>();v.id=id;views.Add(v);return v;}
    static RectTransform Modal(UIView v,string title,bool close=true){var p=Box(v.transform,"Panel",title,"界面","",210,110,1500,870,new Color(.09f,.135f,.16f));L(p.transform,"Title",title,40,35,1100,65,36);if(close)B(p.transform,"Close","关闭","返回","",1270,35,195,62);return p.Rect;}
    static RectTransform Conversation(UIView v,string title){var p=Box(v.transform,"Panel","右侧对话栏","对话","",1220,202,652,830,new Color(.07f,.12f,.15f));L(p.transform,"Title",title,28,40,596,90,32);return p.Rect;}
    static void Scroll(UIView v,Transform parent,float x,float y,float w,float h,UIElement row){
        var viewport=R(parent,"滚动区域_可调整尺寸",x,y,w,h);viewport.gameObject.AddComponent<Image>().color=new Color(.055f,.085f,.10f);viewport.gameObject.AddComponent<RectMask2D>();
        var content=R(viewport,"Content_列表布局",0,0,w,h);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.sizeDelta=new Vector2(0,h);
        var group=content.gameObject.AddComponent<VerticalLayoutGroup>();group.padding=new RectOffset(8,8,8,12);group.spacing=18;group.childControlWidth=true;group.childControlHeight=true;group.childForceExpandHeight=false;group.childForceExpandWidth=true;
        var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        v.content=content;v.scroll=scroll;v.rowPrefab=row;v.rowVerticalPadding=row==archiveRow?56:24;
        var preview=(GameObject)PrefabUtility.InstantiatePrefab(row.gameObject,content);preview.name="示例条目_仅编辑预览";preview.tag="EditorOnly";preview.GetComponent<UIElement>().caption.text="等待填入";v.previewContent=preview;
    }
    static UIElement MakeRow(string name,Color color,bool button=false){
        UIElement e;if(button){e=B(null,"Row","等待填入","档案条目","",0,0,1300,120);e.button.interactable=false;}
        else{e=Group(null,"Row",name,0,0,560,80);e.caption=T(e.transform,"正文_可改字体与颜色","等待填入",12,12,536,56,28,color);Stretch(e.caption.rectTransform,12,12,12,12);e.caption.lineSpacing=1.25f;}
        var layout=e.gameObject.AddComponent<LayoutElement>();layout.minHeight=button?120:60;
        var asset=PrefabUtility.SaveAsPrefabAsset(e.gameObject,Folder+"/Prefabs/Components/"+name+".prefab").GetComponent<UIElement>();UnityEngine.Object.DestroyImmediate(e.gameObject);return asset;
    }
    [MenuItem("TGJ/UI/Create Editable Canvas (one time)")]
    public static void Create(){
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止 Play。");
        if(File.Exists(CanvasPath)){Debug.Log("GameUI 已存在，未覆盖任何排版。请直接编辑现有 Canvas / Prefab。");return;}
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)throw new InvalidOperationException("请先保存当前场景，再迁移 UI。");
        foreach(var path in new[]{Folder,Folder+"/Prefabs",Folder+"/Prefabs/Views",Folder+"/Prefabs/Components",Folder+"/Fonts"})Directory.CreateDirectory(path);AssetDatabase.Refresh();
        font=AssetDatabase.LoadAssetAtPath<Font>(Folder+"/Fonts/NotoSansCJKsc-Regular.otf");if(!font)throw new InvalidOperationException("项目中文字体文件缺失，请先恢复 UI/Fonts/NotoSansCJKsc-Regular.otf。");
        catalog=AssetDatabase.LoadAssetAtPath<StoryCatalog>("Assets/TGJ/Resources/TGJ/Catalog.asset");views.Clear();
        historyRow=MakeRow("DialogueHistoryLine",Muted);currentRow=MakeRow("DialogueCurrentLine",Ink);listRow=MakeRow("ListTextRow",Ink);archiveRow=MakeRow("ArchiveCard",Ink,true);
        var backdrop=View("Backdrop","背景");var bg=Box(backdrop.transform,"Fill","背景","","",-2000,-2000,6000,5000,new Color(.045f,.075f,.09f));bg.artwork.raycastTarget=false;
        var black=View("BlackBackdrop","全屏黑底");Box(black.transform,"Fill","黑底","","",-2000,-2000,6000,5000,Color.black);
        MakeMain();MakeHUD();MakeWorlds();MakeInventory();MakeDialogue();MakeChoices();
        var shade=View("ModalShade","面板遮罩");Box(shade.transform,"Shade","遮罩","","",-2000,-2000,6000,5000,new Color(0,0,0,.75f));
        MakeModals();
        var feedback=View("Feedback","提示消息");L(feedback.transform,"Notice","",60,1045,1800,34,20,new Color(.85f,.78f,.53f));
        var fx=View("AchievementFX","成就演出");var tint=Box(fx.transform,"Tint","成就颜色","","",0,0,1920,1080,new Color(.45f,.62f,.65f,.1f));tint.artwork.raycastTarget=false;
        var placed=B(null,"PlacedItem","记忆物品","取回","",0,0,230,115);var placedAsset=PrefabUtility.SaveAsPrefabAsset(placed.gameObject,Folder+"/Prefabs/Components/PlacedItem.prefab").GetComponent<UIElement>();UnityEngine.Object.DestroyImmediate(placed.gameObject);
        var drag=B(null,"DragItem","记忆物品","记忆物品","",0,0,230,105);var dragAsset=PrefabUtility.SaveAsPrefabAsset(drag.gameObject,Folder+"/Prefabs/Components/DragItem.prefab").GetComponent<UIElement>();UnityEngine.Object.DestroyImmediate(drag.gameObject);
        var canvasGO=new GameObject("GameUI Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(EditableUIRoot));
        canvasGO.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvasGO.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var ui=canvasGO.GetComponent<EditableUIRoot>();ui.layout=R(canvasGO.transform,"Layout_1920x1080",0,0,1920,1080);ui.layout.anchorMin=ui.layout.anchorMax=new Vector2(.5f,.5f);ui.layout.anchoredPosition=new Vector2(-960,540);
        var instances=new List<UIView>();foreach(var v in views){v.elements=v.GetComponentsInChildren<UIElement>(true).Where(e=>e.key!="Row").ToArray();var asset=PrefabUtility.SaveAsPrefabAsset(v.gameObject,Folder+"/Prefabs/Views/"+v.id+".prefab");UnityEngine.Object.DestroyImmediate(v.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,ui.layout);var bound=instance.GetComponent<UIView>();instance.SetActive(bound.id=="Backdrop"||bound.id=="MainMenu");instances.Add(bound);}
        ui.views=instances.ToArray();ui.placedItemPrefab=placedAsset;ui.dragItemPrefab=dragAsset;
        var canvasAsset=PrefabUtility.SaveAsPrefabAsset(canvasGO,CanvasPath);UnityEngine.Object.DestroyImmediate(canvasGO);
        var canvasInstance=(GameObject)PrefabUtility.InstantiatePrefab(canvasAsset);var game=UnityEngine.Object.FindFirstObjectByType<GrayboxGame>();game.ui=canvasInstance.GetComponent<EditableUIRoot>();EditorUtility.SetDirty(game);
        if(!UnityEngine.Object.FindFirstObjectByType<EventSystem>()){var events=new GameObject("UI EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=canvasInstance;Debug.Log("TGJ_EDITABLE_UI_READY: "+uiCount(instances)+" editable view prefabs, scene Canvas linked.");
    }
    static int uiCount(List<UIView> list)=>list.Count;
    static void MakeMain(){
        var v=View("MainMenu","主菜单");Box(v.transform,"Panel","主菜单背景","主菜单","",80,80,1760,900);
        L(v.transform,"Title","孕育与诞生",160,180,1200,120,70);L(v.transform,"Controls","",166,412,1050,110,25);
        B(v.transform,"NewGame","新游戏","新游戏","",160,560,480,112);B(v.transform,"Continue","继续","自动存档","",680,560,480,112);
        B(v.transform,"Settings","设置","设置","",160,710,300,90);B(v.transform,"Credits","制作人员","名单","",500,710,300,90);B(v.transform,"Quit","退出","退出","",840,710,300,90);
        L(v.transform,"Progress","",1300,590,430,150,26);L(v.transform,"HiddenHint","",1300,765,430,80,26,new Color(.85f,.77f,.55f)).gameObject.SetActive(false);
    }
    static void MakeHUD(){
        var v=View("HUD","游戏顶部导航");L(v.transform,"SceneTitle","音乐厅",48,24,850,60,42);L(v.transform,"Subtitle","",50,85,1000,40,23,Muted);
        B(v.transform,"Previous","←","切换","",1440,30,200,72);B(v.transform,"Next","→","切换","",1664,30,208,72);
        string[] keys={"map","inventory","collection","archive","log","pause"},names={"地图","物品栏","收藏背包","人物档案","Log","暂停"};for(int i=0;i<keys.Length;i++)B(v.transform,keys[i],names[i],"打开","",48+i*205,125,185,62);
        L(v.transform,"Objective","",1300,135,560,60,23,Muted,TextAnchor.MiddleRight);
    }
    static UIElement World(UIView v,string title,bool memory){var e=Box(v.transform,"World",title+"场景","背景",memory?"":"",48,202,1824,540,memory?new Color(.24f,.22f,.18f):new Color(.15f,.21f,.25f));e.gameObject.AddComponent<SceneDrop>();return e;}
    static void MakeWorlds(){
        var v=View("Hall","音乐厅_第一幕");var world=World(v,"音乐厅",false);
        var stage=Box(world.transform,"Stage","舞台","舞台","",520,40,780,105,new Color(.28f,.29f,.27f));L(stage.transform,"StageText","舞台",25,33,720,50,30);
        var phases=new UIElement[5];for(int i=0;i<5;i++){phases[i]=Group(world.transform,"Phase"+i,"阶段"+i,0,0,1824,540);phases[i].gameObject.SetActive(i==1);}
        Box(phases[0].transform,"BagProp","挎包","查看","",60,310,260,145);
        for(int i=0;i<7;i++)B(phases[1].transform,"Bag"+i,catalog.content.bagNames[i],"查看","",35+(i%4)*445,170+(i/4)*170,420,145);
        B(phases[2].transform,"Phone","翻盖手机","对话","",80,230,430,180);
        B(phases[3].transform,"Audience2","前排的人","角色热点","",700,165,340,145);var seat=Box(phases[3].transform,"Seat","空座位","场景","",1070,165,290,145);L(seat.transform,"SeatText","空座位",30,50,230,50,26);
        B(phases[3].transform,"Audience0","甲","角色热点","",300,350,350,145);B(phases[3].transform,"Audience1","乙","角色热点","",1170,350,350,145);
        B(phases[4].transform,"A","A","给A拍张照","",560,170,350,170);var woman=Box(phases[4].transform,"Woman","银发妇人","乐手","",940,170,350,170);L(woman.transform,"WomanText","银发妇人",30,60,290,50,28);
        B(phases[4].transform,"Photo","给A拍张照","给A拍张照","",60,370,400,120);B(phases[4].transform,"Memories","继续","继续","",1320,370,450,120);B(phases[4].transform,"Replay","C","角色热点","",510,370,350,120);B(phases[4].transform,"Rewind","倒带","倒带","",900,370,370,120);
        foreach(var s in catalog.content.scenes){var memory=View("Memory_"+s.id,s.title+"_回忆热点");var bg=World(memory,s.title,true);B(bg.transform,"Character",s.character,"角色热点","",650,100,420,240);B(bg.transform,"Inspect","未命名","查看热点","",70,100,350,150);int n=0;foreach(var c in catalog.content.collectibles.Where(c=>c.scene==s.id)){B(bg.transform,c.id,c.title,"收集热点","",1330,100+n*160,370,135);n++;}}
    }
    static void MakeInventory(){var v=View("InventoryTray","底部物品栏");var tray=Box(v.transform,"Tray","物品栏","物品栏","",48,770,1824,260);int n=0;foreach(var item in catalog.content.items){var e=B(tray.transform,item.id,item.title,"记忆物品","",20+(n%5)*355,40+(n/5)*90,340,86);e.gameObject.AddComponent<ItemDrag>();n++;}}
    static void MakeDialogue(){
        var v=View("Dialogue","右侧对话栏");var p=Conversation(v,"对话");v.GetComponent<RectTransform>();Scroll(v,p,28,94,596,526,historyRow);v.currentRowPrefab=currentRow;
        B(p,"Continue","继续","继续","",28,638,596,72);B(p,"Auto","自动","自动播放","",28,728,140,62);B(p,"Fast","快进","快进","",180,728,140,62);B(p,"Hide","隐藏","隐藏文本框","",332,728,140,62);B(p,"Log","Log","聊天记录","",484,728,140,62);
        var hidden=View("HiddenDialogue","隐藏对话后的恢复按钮");B(hidden.transform,"Show","对话框","对话框","",1450,950,420,90);
        foreach(string id in new[]{"Prologue","Cinematic"}){var c=View(id,id=="Prologue"?"序幕_引文与署名整体":"黑屏过场");var black=Box(c.transform,"Panel","黑底","过场","",0,0,1920,1080,Color.black);var quote=L(black.transform,"Quote",id=="Prologue"?"“外部世界在漠然性的法则下叹息……”\n\n——克尔凯郭尔":"等待填入",240,300,1440,450,38,Ink,TextAnchor.MiddleCenter);if(id=="Prologue")c.fade=quote.gameObject.AddComponent<CanvasGroup>();B(black.transform,"Continue","继续",id=="Prologue"?"继续":"继续","",1550,950,290,70);}
    }
    static void MakeChoices(){
        var phone=View("Choices_phone","电话话题");var p=Conversation(phone,"女声");for(int i=0;i<4;i++)B(p,"Choice"+i,catalog.content.phoneChoices[i],"话题","",28,125+i*160,596,150).caption.fontSize=22;
        foreach(string mode in new[]{"cchoice","rewind"}){var v=View("Choices_"+mode,mode=="rewind"?"倒带选项":"C分支选项");p=Conversation(v,mode=="rewind"?"倒带":"前排的人");for(int i=0;i<2;i++)B(p,"Choice"+i,catalog.content.cChoices[i],mode=="rewind"?"倒带":"选项","",28,170+i*225,596,205);if(mode=="rewind")B(p,"Close","返回","关闭","",28,670,596,85);}
        var ending=View("Choices_ending","结局选项");p=Conversation(ending,"等待填入");L(p,"Tendency","",28,160,596,90,25,Muted);B(p,"Ending1","等待填入","结局 1","",28,280,596,130);B(p,"Ending2","等待填入","结局 2","",28,435,596,130);B(p,"Ending3","收起钱，坐下来听她说完","隐藏结局","",28,590,596,150);
    }
    static void MakeModals(){
        var v=View("Modal_name","取名");var p=Modal(v,"取名",false);L(p,"Help","",55,160,1300,70,27,Muted);var field=Box(p,"NameInput","名字输入框","取名","",60,285,1380,140,new Color(.19f,.26f,.28f));var value=T(field.transform,"输入文字","",24,40,1320,70,38);Stretch(value.rectTransform,24,40,24,30);var input=field.gameObject.AddComponent<InputField>();input.textComponent=value;input.characterLimit=16;input.lineType=InputField.LineType.SingleLine;input.targetGraphic=field.artwork;v.input=input;B(p,"Confirm","确认","确认","",60,530,1380,125);
        v=View("Modal_map","地图");p=Modal(v,"地图");int i=0;foreach(var s in catalog.content.scenes){B(p,s.id,s.title,"切换",s.subtitle,45+(i%2)*720,160+(i/2)*245,690,210);i++;}
        foreach(string mode in new[]{"inventory","archive","log"}){v=View("Modal_"+mode,mode=="inventory"?"物品栏":mode=="archive"?"人物档案":"聊天记录");p=Modal(v,v.name);Scroll(v,p,35,110,1430,620,mode=="archive"?archiveRow:listRow);}
        v=View("Modal_collection","收藏背包");p=Modal(v,"收藏背包");i=0;foreach(var c in catalog.content.collectibles){var card=Box(p,c.id,c.title,"彩蛋物品","",45+(i%4)*355,145+(i/4)*300,330,260);card.caption=T(card.transform,"收藏名称",c.title,20,65,290,140,27);Stretch(card.caption.rectTransform,20,65,20,55);i++;}
        v=View("Modal_pause","暂停");p=Modal(v,"暂停");B(p,"Resume","继续","关闭","",50,145,675,145);B(p,"Save","保存","存档","",775,145,675,145);B(p,"LoadManual","手动槽","手动槽","",50,330,675,145);B(p,"LoadAuto","手动槽","手动槽","",775,330,675,145);B(p,"Settings","设置","设置","",50,515,675,145);B(p,"MainMenu","回到主菜单","主菜单","",775,515,675,145);
        v=View("Modal_settings","设置");p=Modal(v,"设置");Slider(p,"Master","主音量",145,0,1,.75f);Slider(p,"Music","音乐",240,0,1,.4f);Slider(p,"SFX","音效",335,0,1,.65f);Slider(p,"Speed","文字速度",430,10,150,45);B(p,"Fullscreen","全屏","全屏","",55,540,650,95);B(p,"Reduced","减弱闪烁/动效","减弱闪烁/动效","",755,540,690,95);B(p,"Reset","恢复默认","恢复默认","",55,680,650,100);B(p,"Clear","清除跨周目","清除跨周目","",755,680,690,100);
        foreach(string mode in new[]{"newconfirm","menuconfirm","clearconfirm"}){v=View("Modal_"+mode,mode=="newconfirm"?"新游戏":mode=="menuconfirm"?"回到主菜单":"清除跨周目");p=Modal(v,v.name);L(p,"Body",mode=="newconfirm"?"":mode=="menuconfirm"?"":"",55,230,1350,150,29);B(p,"Confirm","确认","确认","",55,480,650,150);B(p,"Cancel","取消","返回","",760,480,680,150);}
        v=View("Modal_credits","制作人员");p=Modal(v,"制作人员");L(p,"Credits","文案：（abc）\n\n程序：（abc）\n\n美术：（abc）",450,220,650,400,38);
        v=View("Modal_cleared","通关");p=Modal(v,"结局");L(p,"Body","",55,260,1340,200,30);B(p,"MainMenu","回到主菜单","主菜单","",55,565,1380,140);
    }
    static void Slider(Transform parent,string key,string title,float y,float min,float max,float value){
        L(parent,key+"Label",title,55,y,450,65,27);var e=Box(parent,key,title,"设置","",550,y,870,65,new Color(.2f,.27f,.29f));
        var track=R(e.transform,"Track",15,35,840,12);track.gameObject.AddComponent<Image>().color=new Color(.1f,.18f,.2f);var fill=R(track,"Fill",0,0,840,12);fill.gameObject.AddComponent<Image>().color=Accent;var handle=R(track,"Handle",0,-9,28,30);var hi=handle.gameObject.AddComponent<Image>();hi.color=Ink;
        var slider=e.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=hi;slider.minValue=min;slider.maxValue=max;slider.value=value;
    }
}
}

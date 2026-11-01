using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ {
public partial class GrayboxGame {
    public EditableUIRoot ui;
    readonly Dictionary<RectTransform,Vector2> worldHomes=new Dictionary<RectTransform,Vector2>();
    readonly Dictionary<RectTransform,Vector3> worldScales=new Dictionary<RectTransform,Vector3>();
    readonly List<GameObject> placedViews=new List<GameObject>();
    Vector2 worldHome;
    void Bind(UIElement element,Action action,bool enabled=true){
        var button=element.button;button.onClick.RemoveAllListeners();button.interactable=enabled;
        button.onClick.AddListener(()=>{Sound(catalog.clickSound,.15f);action?.Invoke();});
    }
    void Bind(UIView view,string key,Action action,bool enabled=true)=>Bind(view.Get(key),action,enabled);
    void Clear(){
        ui.HideViews();foreach(var item in placedViews)if(item){item.SetActive(false);Destroy(item);}placedViews.Clear();
        world=null;dialogueText=null;noticeText=null;achievementImage=null;prologueGroup=null;
    }
    void Render(){
        if(!ui)return;Clear();
        bool cinematic=!mainMenu&&(state.dialogue=="Prologue"||state.dialogue=="Transition"||state.dialogue.StartsWith("Ending"));
        ui.Show(cinematic?"BlackBackdrop":"Backdrop");
        if(mainMenu)RenderMain();else if(!cinematic)RenderGame();else RenderDialogue();
        if(modal!="")RenderModal();
        var feedback=ui.Show("Feedback");noticeText=feedback.Get("Notice").caption;noticeText.text=Time.unscaledTime<noticeUntil?notice:"";
        if(achievementTimer>0){var fx=ui.Show("AchievementFX");achievementImage=fx.Get("Tint").artwork;}
        ui.ApplyAnnotations();
    }
    void RenderMain(){
        var v=ui.Show("MainMenu");
        Bind(v,"NewGame",()=>{if(Read<RunState>("auto")!=null){modal="newconfirm";Render();}else NewGame();});
        Bind(v,"Continue",()=>Load(),Read<RunState>("auto")!=null);
        Bind(v,"Settings",()=>{modal="settings";Render();});Bind(v,"Credits",()=>{modal="credits";Render();});Bind(v,"Quit",()=>Application.Quit());
        v.Get("Progress").Text("已通关结局  "+string.Join(" / ",global.endings.Select(x=>x.ToString()))+"\n成就  "+global.achievements.Count+" / "+catalog.content.achievements.Length);
        v.Get("HiddenHint").gameObject.SetActive(global.Hidden);
    }
    string Objective()=>"";
    void RenderGame(){
        var scene=catalog.Scene(state.scene);var hud=ui.Show("HUD");hud.Get("SceneTitle").Text(scene.title);hud.Get("Subtitle").Text(state.playerName);hud.Get("Objective").Text(Objective());
        Bind(hud,"Previous",()=>SwitchScene(-1),CanWorld&&state.stage>=5&&state.unlocked.Count>1);Bind(hud,"Next",()=>SwitchScene(1),CanWorld&&state.stage>=5&&state.unlocked.Count>1);
        foreach(string mode in new[]{"map","inventory","collection","archive","log","pause"}){string m=mode;Bind(hud,mode,()=>{modal=m;Render();});}
        var view=ui.Show(state.stage<5?"Hall":"Memory_"+state.scene);world=view.Get("World").Rect;world.GetComponent<SceneDrop>().game=this;
        if(!worldHomes.ContainsKey(world)){worldHomes[world]=world.anchoredPosition;worldScales[world]=world.localScale;}
        worldHome=worldHomes[world];world.localScale=worldScales[world]*(Busy||IsConversationChoice?ui.conversationWorldScale:1);
        if(state.stage<5)RenderHall(view);else RenderMemory(view,scene);
        if(Busy)RenderDialogue();else if(!IsConversationChoice)RenderInventory();
        var clip=state.scene=="Hall"?catalog.musicNow:catalog.musicMemory;if(musicSource.clip!=clip){musicSource.clip=clip;if(clip)musicSource.Play();}
    }
    void RenderHall(UIView v){
        for(int n=0;n<5;n++)v.Get("Phase"+n).gameObject.SetActive(state.stage==n);
        if(state.stage==1){for(int n=0;n<7;n++){int i=n;var e=v.Get("Bag"+n);e.Text(catalog.content.bagNames[n]);e.Note(state.bag.Contains(n)?"已读":"");Bind(e,()=>InspectBag(i),!Busy);}return;}
        if(state.stage==2){Bind(v,"Phone",()=>{modal="phone";Render();},!Busy);return;}
        if(state.stage==3){for(int n=0;n<3;n++){int i=n;Bind(v,"Audience"+n,()=>ChooseAudience(i),!Busy&&(state.audience<0||state.audience==n));}return;}
        if(state.stage<4)return;
        Bind(v,"A",()=>{if(!state.photo)Play("Photo","photo");},!Busy);
        v.Get("Photo").Text("给A拍张照");Bind(v,"Photo",()=>Play("Photo","photo"),!Busy&&!state.photo);
        Bind(v,"Memories",()=>Play("Transition","memories"),!Busy);
        v.Get("Replay").gameObject.SetActive(state.audience>=0);v.Get("Replay").Text(state.audience==0?"甲":state.audience==1?"乙":"C");Bind(v,"Replay",ReplayAudience,!Busy);
        v.Get("Rewind").gameObject.SetActive(state.audience==2);Bind(v,"Rewind",()=>{modal="rewind";Render();},!Busy);
    }
    void RenderMemory(UIView v,SceneSpec scene){
        v.Get("Character").Text(scene.character);v.Get("Character").Note(state.talked.Contains(scene.id)?"已读":"新话题");Bind(v,"Character",()=>{state.log.Clear();TalkScene();},!Busy);
        Bind(v,"Inspect",()=>Play("View_"+state.scene),!Busy);
        foreach(var c in catalog.content.collectibles.Where(c=>c.scene==state.scene)){var e=v.Get(c.id);e.Text(c.title);e.gameObject.SetActive(!state.collected.Contains(c.id));string id=c.id;Bind(e,()=>Collect(id),!Busy);}
        foreach(var p in state.positions.Where(p=>p.scene==state.scene)){
            string id=p.item;var e=Instantiate(ui.placedItemPrefab,world,false);e.name="已放置_"+id;e.Text(catalog.Item(id).title);e.Rect.anchoredPosition=new Vector2(p.x,-p.y);Bind(e,()=>Retrieve(id),!Busy);placedViews.Add(e.gameObject);
        }
    }
    void RenderInventory(){
        var v=ui.Show("InventoryTray");v.Get("Tray").Note("");
        foreach(var item in catalog.content.items){var e=v.Get(item.id);bool owned=state.owned.Contains(item.id),placed=state.positions.Any(p=>p.item==item.id);string id=item.id;
            e.Text(owned?item.title:"未获得");e.Note(placed?"取回":selectedItem==id?"已选":"");
            Bind(e,()=>{if(placed){Retrieve(id);return;}selectedItem=id;Render();},owned&&state.stage>=5);
            var drag=e.GetComponent<ItemDrag>();drag.game=this;drag.item=id;drag.enabled=owned&&!placed&&state.stage>=5;
        }
    }
    void RenderDialogue(){
        if(state.dialogue=="Prologue"){
            var v=ui.Show("Prologue");prologueGroup=v.fade;prologueGroup.alpha=PrologueOpacity;dialogueText=v.Get("Quote").caption;dialogueText.text=fullLine;Bind(v,"Continue",Advance);return;
        }
        if(state.dialogue=="Transition"||state.dialogue.StartsWith("Ending")){
            var v=ui.Show("Cinematic");dialogueText=v.Get("Quote").caption;dialogueText.text=fullLine.Substring(0,Mathf.Min(fullLine.Length,Mathf.FloorToInt(reveal)));Bind(v,"Continue",Advance);return;
        }
        if(hiddenText){var hidden=ui.Show("HiddenDialogue");Bind(hidden,"Show",()=>{hiddenText=false;Render();});return;}
        var dialogue=ui.Show("Dialogue");dialogPanel=dialogue.Get("Panel").Rect;dialogue.Get("Panel").Note("");dialogue.Get("Title").Text("对话");
        dialogue.ClearRows();var previous=state.log.ToList();if(previous.Count>0&&previous[previous.Count-1]==fullLine)previous.RemoveAt(previous.Count-1);
        foreach(var line in previous)dialogue.AddRow(line);
        var current=dialogue.AddRow(fullLine,true);dialogue.FitTextRows();dialogue.scroll.verticalNormalizedPosition=0;
        dialogueText=current.caption;dialogueText.text=fullLine.Substring(0,Mathf.Min(fullLine.Length,Mathf.FloorToInt(reveal)));
        Bind(dialogue,"Continue",Advance);dialogue.Get("Auto").Text("自动");dialogue.Get("Auto").Note(auto?"开启":"关闭");Bind(dialogue,"Auto",()=>{auto=!auto;Render();});
        dialogue.Get("Fast").Text("快进");dialogue.Get("Fast").Note(fast?"开启":"关闭");Bind(dialogue,"Fast",()=>{fast=!fast;Render();});Bind(dialogue,"Hide",()=>{hiddenText=true;Render();});Bind(dialogue,"Log",()=>{modal="log";Render();});
    }
    bool IsConversationChoice=>modal=="phone"||modal=="cchoice"||modal=="rewind"||modal=="ending";
    public void BeginDrag(string id,Vector2 screen){if(!CanWorld||state.stage<5)return;selectedItem=id;var e=Instantiate(ui.dragItemPrefab,root,false);e.Text(catalog.Item(id).title);dragGhost=e.gameObject;foreach(var g in dragGhost.GetComponentsInChildren<Graphic>())g.raycastTarget=false;MoveDrag(screen);}
    public void MoveDrag(Vector2 screen){if(!dragGhost)return;RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out var pos);var rect=dragGhost.GetComponent<RectTransform>();rect.anchoredPosition=pos+new Vector2(-rect.rect.width/2,rect.rect.height/2);}
    public void EndDrag(Vector2 screen){if(dragGhost)Destroy(dragGhost);dragGhost=null;if(!CanWorld||world==null)return;if(RectTransformUtility.RectangleContainsScreenPoint(world,screen,null)){RectTransformUtility.ScreenPointToLocalPointInRectangle(world,screen,null,out var pos);if(selectedItem!="")PlaceItem(selectedItem,new Vector2(pos.x,-pos.y));}}
    public void ClickScene(Vector2 screen){if(!CanWorld||selectedItem==""||world==null)return;RectTransformUtility.ScreenPointToLocalPointInRectangle(world,screen,null,out var pos);PlaceItem(selectedItem,new Vector2(pos.x,-pos.y));}
}
}

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TGJ {
public partial class GrayboxGame {
    List<string> checks=new List<string>();
    bool testing;
    void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks.Add(name);}
    public void RunSmokeTests(){if(testing)return;StartCoroutine(SmokeHarness());}
    IEnumerator SmokeHarness(){
        testing=true;testMode=true;checks.Clear();
        foreach(var key in new[]{"auto","manual","global","settings"})PlayerPrefs.DeleteKey(SavePrefix+key);
        global=new GlobalState();state=StoryRules.NewRun(global);settings.speed=10000;
        var steps=SmokeSequence();string failure=null;
        while(true){bool more=false;object current=null;try{more=steps.MoveNext();if(more)current=steps.Current;}catch(Exception e){failure=e.ToString();}
            if(!more||failure!=null)break;yield return current;
        }
        var report="TGJ FLOW SMOKE TESTS\n"+(failure==null?"PASS":"FAIL")+"\n"+string.Join("\n",checks.Select(x=>"PASS: "+x))+"\n"+failure;
        Directory.CreateDirectory(Path.Combine(Application.dataPath,"../TestResults"));File.WriteAllText(Path.Combine(Application.dataPath,"../TestResults/graybox-smoke.txt"),report);
        StopDialogue();testMode=false;global=Read<GlobalState>("global")??new GlobalState();settings=Read<GameSettings>("settings")??new GameSettings();state=StoryRules.NewRun(global);mainMenu=true;modal="";Render();testing=false;
        if(failure!=null)Debug.LogError(report);else Debug.Log(report);
    }
    IEnumerator SmokeSequence(){
        Check(catalog.content.nodes.All(n=>yarn.NodeExists(n.id)),"All 80 authored nodes compiled in Yarn");
        var placeholders=catalog.content.nodes.Where(n=>n.id=="Concert"||n.id=="Photo"||n.id.EndsWith("Talk")||n.id.StartsWith("Ending")||n.id.StartsWith("Item_")||n.id.StartsWith("View_")||n.id.StartsWith("Egg_")).ToArray();
        Check(placeholders.Length==58&&placeholders.All(n=>n.lines.SequenceEqual(new[]{"等待填入"})),"All 58 non-source dialogue nodes use only the requested placeholder");
        Check(catalog.content.scenes.All(s=>s.character=="未命名"&&s.subtitle=="")&&catalog.content.items.Skip(5).All(i=>i.title=="未命名")&&catalog.content.collectibles.All(c=>c.title=="未命名"),"Unspecified characters and items have no invented names");
        Check(!catalog.content.achievements.Any(a=>a.id=="named")&&catalog.content.achievements.Skip(4).All(a=>a.title=="未命名"&&a.quote=="等待填入"),"Invented naming achievement removed; unspecified achievement text neutralized");
        Check(ui.Get("Choices_ending").Get("Ending1").caption.text=="等待填入"&&ui.Get("Choices_ending").Get("Ending2").caption.text=="等待填入"&&ui.Get("Choices_ending").Get("Ending3").caption.text=="收起钱，坐下来听她说完","Ending choices use placeholders except the source hidden option");
        var legacy=new RunState{version=1,stage=5,scene="Motel",dialogue="MotelTalk",line=1,after="talk",arg="Motel",playerName="测试姓名",audience=2,cChoice=1};
        legacy.owned.Add("ticket");legacy.log.Add("retired draft test marker");legacy.log.Add(catalog.Node("Wake").lines[0]);Write("manual",legacy);
        var migrated=Read<RunState>("manual");
        Check(migrated.version==2&&migrated.line==0&&migrated.playerName==legacy.playerName&&migrated.cChoice==1&&migrated.owned.Contains("ticket"),"Legacy save migration preserves progression and clamps shortened dialogue");
        Check(migrated.log.SequenceEqual(new[]{catalog.Node("Wake").lines[0]}),"Legacy log migration removes retired copy and retains source-script text");
        Check(JsonUtility.FromJson<RunState>(PlayerPrefs.GetString(SavePrefix+"manual")).version==2,"Cleaned legacy save is persisted");
        Load(true);yield return null;yield return null;
        Check(Busy&&state.line==0&&fullLine=="等待填入","Legacy mid-dialogue save resumes the replacement placeholder");
        for(int n=0;Busy&&n<20;n++){reveal=99999;Advance();yield return null;}
        Check(state.talked.Contains("Motel")&&state.unlocked.Contains("Diner"),"Migrated placeholder dialogue still grants progression on completion");
        foreach(var slot in new[]{"manual","auto"})PlayerPrefs.DeleteKey(SavePrefix+slot);
        state=StoryRules.NewRun(global);
        Check(ui&&ui.views.Length==34&&ui.views.All(v=>v.elements.All(e=>e)&&v.elements.Select(e=>e.key).Distinct().Count()==v.elements.Length),"Scene Canvas has all 34 prefab views and unique serialized bindings");
        var titleFont=ui.Get("MainMenu").Get("Title").caption.font;Check(titleFont&&titleFont.material&&titleFont.HasCharacter('孕'),"Persistent project font can render Chinese text after domain reload");
        mainMenu=true;modal="";Render();
        var editable=ui.Get("MainMenu").Get("NewGame");var originalPosition=editable.Rect.anchoredPosition;var originalSize=editable.Rect.sizeDelta;int originalFont=editable.caption.fontSize;var originalSprite=editable.artwork.sprite;var originalColor=editable.artwork.color;
        var probeSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,2,2),new Vector2(.5f,.5f));
        editable.Rect.anchoredPosition+=new Vector2(37,-19);editable.Rect.sizeDelta+=new Vector2(13,7);editable.caption.fontSize+=3;editable.artwork.sprite=probeSprite;editable.artwork.color=Color.white;Render();
        Check(editable.Rect.anchoredPosition==originalPosition+new Vector2(37,-19)&&editable.Rect.sizeDelta==originalSize+new Vector2(13,7),"Manual RectTransform position and size survive UI refresh");
        Check(editable.caption.fontSize==originalFont+3&&editable.artwork.sprite==probeSprite&&editable.artwork.color==Color.white,"Edited typography and replacement art survive UI refresh");
        editable.Rect.anchoredPosition=originalPosition;editable.Rect.sizeDelta=originalSize;editable.caption.fontSize=originalFont;editable.artwork.sprite=originalSprite;editable.artwork.color=originalColor;Destroy(probeSprite);
        foreach(string panel in new[]{"settings","map","inventory","collection","archive","log","pause","newconfirm","menuconfirm","clearconfirm","credits","cleared","name"}){modal=panel;Render();}
        Check(true,"Every auxiliary panel binds to editable prefabs without missing controls");
        Directory.CreateDirectory(Path.Combine(Application.dataPath,"../Captures"));
        mainMenu=true;modal="credits";Render();yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"../Captures/revision-credits.png"));yield return new WaitForSecondsRealtime(.2f);
        int runIndex=0;
        foreach(var ending in new[]{1,1,2,3}){
            runIndex++;
            if(runIndex==1){mainMenu=true;modal="";Render();ui.Get("MainMenu").Get("NewGame").button.onClick.Invoke();Check(state.dialogue=="Prologue","Prefab New Game button starts the real story flow");}else NewGame();
            if(runIndex==1){
                yield return null;yield return new WaitForSecondsRealtime(1f);
                Check(state.dialogue=="Prologue"&&fullLine.Contains("克尔凯郭尔")&&fullLine.Contains("外部世界")&&state.line==0,"Prologue quote and attribution appear together on a single step");
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"../Captures/revision-prologue.png"));yield return new WaitForSecondsRealtime(.2f);
                Advance();yield return null;
                Check(state.dialogue=="Prologue"&&prologueGroup!=null,"Continuing prologue begins fade-out before advancing to waking");
            }
            for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return state.dialogue=="Prologue"?new WaitForSecondsRealtime(.01f):null;}
            Check(state.stage==1,"Prologue and waking reach bag inspection");
            for(int i=0;i<7;i++){ui.Get("Hall").Get("Bag"+i).button.onClick.Invoke();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}}
            Check(state.stage==2&&state.bag.Count==7,"Seven bag views trigger the phone");
            for(int i=0;i<4;i++){
                ui.Get("Choices_phone").Get("Choice"+i).button.onClick.Invoke();
                if(runIndex==1&&i==0){yield return null;reveal=99999;Advance();yield return null;reveal=99999;yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"../Captures/revision-dialogue.png"));yield return new WaitForSecondsRealtime(.2f);}
                for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
                if(i==1){Check(modal=="name","Naming prompt appears at scripted moment");ui.Get("Modal_name").input.text="测试旅人";ui.Get("Modal_name").Get("Confirm").button.onClick.Invoke();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}}
            }
            Check(state.stage==3&&state.playerName=="测试旅人"&&state.phone.Count==4,"Phone topics completed; gender-neutral player name retained");
            int branch=ending==1?(runIndex==1?0:1):2;ChooseAudience(branch);
            Check(!StoryRules.ChooseAudience(state,1),"Audience choice locks immediately");
            for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            if(branch==2){ChooseC(ending==2?0:1);for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}Check(!StoryRules.ChooseC(state,1-state.cChoice),"C branch cannot be changed");}
            Check(state.stage==4,"Selected audience conversation reaches concert");
            int oldChoice=state.cChoice;string replayGlobal=JsonUtility.ToJson(global);ReplayAudience();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(state.stage==4&&state.cChoice==oldChoice&&replayGlobal==JsonUtility.ToJson(global),"Clicking selected character replays full conversation without changing choices");
            if(branch==2){string before=JsonUtility.ToJson(global);int choice=state.cChoice;Play("C"+(1-choice),"resume","",true);for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}Check(state.cChoice==choice&&before==JsonUtility.ToJson(global),"Alternate C text replay has no choice or achievement side effects");}
            Play("Photo","photo");for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(state.photo,"Optional photo persists");
            Play("Transition","memories");for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(state.stage==5&&state.scene=="Motel"&&state.owned.Contains("key"),"Transition grants initial memories and unlocks motel");
            var saveBefore=state.unlocked.Count;PlaceItem("ticket",new Vector2(80,340));for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(!state.placed.Contains("ticket")&&state.unlocked.Count==saveBefore&&state.owned.Contains("ticket"),"Wrong placement is non-consuming and cannot unlock scenes");Retrieve("ticket");
            foreach(var scene in new[]{"Motel","Diner","Classroom"}){
                state.scene=scene;Render();TalkScene();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            }
            Check(state.unlocked.Count==4&&catalog.content.items.All(i=>state.owned.Contains(i.id)),"Dialogue alone unlocks all four scenes and ten items");
            state.scene="Hall";Check(StoryRules.NextScene(state,catalog,-1)=="Classroom","Backward arrow wraps through unlocked scenes");
            Check(!StoryRules.CanEnd(state,global,catalog,1),"Endings gated until all required memories restored");
            foreach(var item in catalog.content.items){state.scene=item.home;Render();PlaceItem(item.id,new Vector2(100,360));for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}Retrieve(item.id);}
            Check(StoryRules.Ready(state,catalog)&&state.positions.Count==0,"Correct placements remain recorded after retrieval");
            foreach(var c in catalog.content.collectibles){state.scene=c.scene;Render();Collect(c.id);}
            state.scene="Motel";Render();TalkScene();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(state.eggs.Contains("Motel")&&state.collected.Count==8,"Collectibles and one-shot dialogue insert work");
            Save(true);string snapshot=JsonUtility.ToJson(state);state.photo=false;Load(true);Check(JsonUtility.ToJson(state)==snapshot,"Manual save/load round-trips complete run state");
            Play("ReturnTalk");yield return null;Save(true);int savedLine=state.line;Load(true);for(int n=0;n<3;n++)yield return null;
            Check(state.line==savedLine&&Busy,"Mid-dialogue save/load resumes current line");for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            state.scene="Hall";Render();TalkScene();for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(modal=="ending","Final conversation offers ending choices");
            if(ending==3)Check(state.owned.Contains("hidden_money")&&global.Hidden,"Third run inherits hidden money after ending 1 and 2");
            else Check(!StoryRules.CanEnd(state,global,catalog,3),"Hidden ending unavailable before qualifying new run");
            ChooseEnding(ending);for(int n=0;Busy&&n<400;n++){reveal=99999;Advance();yield return null;}
            Check(global.endings.Contains(ending)&&state.stage==6,"Ending "+ending+" is reached and persists globally");
        }
        var fresh=StoryRules.NewRun(global);Check(fresh.collected.Count==0&&fresh.placed.Count==0&&fresh.owned.Contains("hidden_money"),"New game clears run progress and preserves hidden item eligibility");
    }
}
}
#endif

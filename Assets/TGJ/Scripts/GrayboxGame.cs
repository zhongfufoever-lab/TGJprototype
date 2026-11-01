using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace TGJ {
public partial class GrayboxGame : MonoBehaviour {
    public StoryCatalog catalog;
    public RunState state;
    public GlobalState global;
    public GameSettings settings;
    public bool testMode;
    public string SavePrefix => testMode ? "TGJ_TEST_" : "TGJ_GRAYBOX_V1_";
    Yarn.Dialogue yarn;
    RectTransform root,world,dialogPanel;
    Font font;
    Text dialogueText,noticeText;
    GameObject overlay,dragGhost;
    AudioSource musicSource,soundSource;
    string modal="",notice="",fullLine="",selectedItem="";
    string pendingAfter="",pendingArg="";
    float reveal,autoTimer,noticeUntil,achievementTimer;
    int skipLines,delivered;
    bool mainMenu=true,auto=false,fast=false,hiddenText=false,pump=false,pendingComplete=false;
    Vector2 logScroll;
    Image achievementImage;
    CanvasGroup prologueGroup;
    float prologueElapsed,prologueLeaveAt=6f;
    float PrologueFade => settings.reduced ? .15f : .9f;
    static Color Ink=new Color(.88f,.91f,.92f),Muted=new Color(.64f,.72f,.75f),Panel=new Color(.10f,.15f,.18f),Accent=new Color(.28f,.48f,.47f);
    public bool Busy => !string.IsNullOrEmpty(state?.dialogue);
    public bool CanWorld => !mainMenu && !Busy && modal=="";
    void Awake(){
        if(catalog==null)catalog=Resources.Load<StoryCatalog>("TGJ/Catalog");
        global=Read<GlobalState>("global")??new GlobalState();settings=Read<GameSettings>("settings")??new GameSettings();
        state=StoryRules.NewRun(global);
        Read<RunState>("auto");Read<RunState>("manual");
        var storage=gameObject.AddComponent<Yarn.Unity.InMemoryVariableStorage>();
        yarn=new Yarn.Dialogue(storage);yarn.SetProgram(catalog.yarnProject.Program);
        yarn.LineHandler=line=>Deliver(catalog.Line(line.ID));
        yarn.OptionsHandler=options=>Debug.LogError("Unexpected Yarn options: this catalog uses persistent topic choices.");
        yarn.DialogueCompleteHandler=()=>{pendingComplete=true;};
        yarn.LogErrorMessage=message=>Debug.LogError("Yarn: "+message);
        yarn.CommandHandler=command=>{Debug.Log("Yarn command: "+command.Text);pump=true;};
        musicSource=gameObject.AddComponent<AudioSource>();musicSource.loop=true;
        soundSource=gameObject.AddComponent<AudioSource>();ApplySettings(false);
        if(!ui)ui=FindFirstObjectByType<EditableUIRoot>();
        if(!ui){Debug.LogError("TGJ: 请在场景中放置 GameUI Canvas 预制体，并连接到 TGJ Game Flow 的 UI 字段。");enabled=false;return;}
        root=ui.layout;
        if(FindFirstObjectByType<EventSystem>()==null){var es=new GameObject("UI Input",typeof(EventSystem),typeof(InputSystemUIInputModule));es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
        Render();
    }
    T Read<T>(string slot)where T:class {
        try {
            if(!PlayerPrefs.HasKey(SavePrefix+slot))return null;
            var data=JsonUtility.FromJson<T>(PlayerPrefs.GetString(SavePrefix+slot));
            bool changed=false;
            if(data is RunState run&&run.version<2){
                // Retain source-script history, remove retired draft text, preserve progression.
                var allowedLog=new HashSet<string>(catalog.content.nodes.SelectMany(n=>n.lines).SelectMany(line=>new[]{line,line.Replace("你（主角）",run.playerName=="你"?"你（主角）":run.playerName).Replace("{name}",run.playerName)}).Select(NormalizeLog));
                run.log.RemoveAll(line=>!allowedLog.Contains(NormalizeLog(line)));run.version=2;changed=true;
                if(!string.IsNullOrEmpty(run.dialogue))run.line=Mathf.Clamp(run.line,0,catalog.Node(run.dialogue).lines.Length-1);
            }
            if(data is GlobalState savedGlobal)changed|=savedGlobal.achievements.RemoveAll(id=>!catalog.content.achievements.Any(a=>a.id==id))>0;
            if(changed)Write(slot,data);
            return data;
        }catch(Exception e){Debug.LogWarning("存档读取失败: "+e.Message);return null;}
    }
    static string NormalizeLog(string line)=>System.Text.RegularExpressions.Regex.Replace(line??"",@"\s+"," ").Trim();
    void Write(string slot,object data){PlayerPrefs.SetString(SavePrefix+slot,JsonUtility.ToJson(data));PlayerPrefs.Save();}
    public void Save(bool manual=false){if(mainMenu)return;Write(manual?"manual":"auto",state);if(manual)Toast("存档");}
    public void Load(bool manual=false){var loaded=Read<RunState>(manual?"manual":"auto");if(loaded==null){Toast("等待填入");return;}StopDialogue();state=loaded;global=Read<GlobalState>("global")??global;mainMenu=false;modal="";selectedItem="";hiddenText=false;
        if(Busy){var node=state.dialogue;var after=state.after;var arg=state.arg;var line=state.line;var replay=state.replay;Play(node,after,arg,replay,line);}else Resume();
    }
    public void NewGame(){StopDialogue();global=Read<GlobalState>("global")??global;state=StoryRules.NewRun(global);mainMenu=false;modal="";selectedItem="";auto=false;fast=false;hiddenText=false;Play("Prologue","wake");}
    void Update(){
        if(pump&&yarn!=null){pump=false;yarn.Continue();}
        if(pendingComplete){pendingComplete=false;FinishDialogue();}
        if(Busy&&modal==""){
            if(state.dialogue=="Prologue"){
                if(fullLine.Length>0){
                    prologueElapsed+=Time.unscaledDeltaTime;
                    if(prologueGroup)prologueGroup.alpha=PrologueOpacity;
                    if(prologueElapsed>=prologueLeaveAt+PrologueFade)pump=true;
                }
            }else{
            reveal+=Time.unscaledDeltaTime*(fast?5000:settings.speed);
            if(dialogueText)dialogueText.text=fullLine.Substring(0,Mathf.Min(fullLine.Length,Mathf.FloorToInt(reveal)));
            if(reveal>=fullLine.Length){autoTimer+=Time.unscaledDeltaTime;if((auto&&autoTimer>1.6f)||(fast&&autoTimer>.08f))Advance();}
            }
            var kb=Keyboard.current;
            if(kb!=null&&(kb.spaceKey.wasPressedThisFrame||kb.enterKey.wasPressedThisFrame)&&!hiddenText)Advance();
        }
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&!mainMenu){if(modal=="name"||modal=="phone"||modal=="cchoice"||modal=="ending")return;modal=modal==""?"pause":"";Render();}
        if(noticeText&&Time.unscaledTime>noticeUntil)noticeText.text="";
        if(achievementImage){achievementTimer-=Time.unscaledDeltaTime;var c=achievementImage.color;c.a=Mathf.Sin(Mathf.Clamp01(achievementTimer/1.8f)*Mathf.PI)*.22f;achievementImage.color=c;if(achievementTimer<=0){ui.Get("AchievementFX").gameObject.SetActive(false);achievementImage=null;}}
        if(world&&Mouse.current!=null){var p=Mouse.current.position.ReadValue();var offset=!settings.reduced&&Application.isFocused?new Vector2((p.x/Screen.width-.5f)*ui.parallaxDistance.x,(p.y/Screen.height-.5f)*ui.parallaxDistance.y):Vector2.zero;world.anchoredPosition=Vector2.Lerp(world.anchoredPosition,worldHome+offset,Time.unscaledDeltaTime*10);}
    }
    float PrologueOpacity => Mathf.Clamp01(prologueElapsed<prologueLeaveAt?prologueElapsed/PrologueFade:1-(prologueElapsed-prologueLeaveAt)/PrologueFade);
    public void Advance(){if(!Busy||modal!="")return;if(state.dialogue=="Prologue"){if(fullLine.Length>0)prologueLeaveAt=Mathf.Min(prologueLeaveAt,Mathf.Max(PrologueFade,prologueElapsed));return;}if(reveal<fullLine.Length){reveal=fullLine.Length;return;}autoTimer=0;pump=true;}
    void StopDialogue(){if(yarn!=null&&yarn.IsActive)yarn.Stop();pump=false;pendingComplete=false;}
    public void Play(string node,string after="resume",string arg="",bool replay=false,int resumeLine=-1){
        StopDialogue();state.dialogue=node;state.after=after;state.arg=arg;state.replay=replay;state.line=-1;
        skipLines=node=="Prologue"?0:Mathf.Clamp(resumeLine,0,catalog.Node(node).lines.Length-1);delivered=0;fullLine="";reveal=0;autoTimer=0;hiddenText=false;modal="";
        prologueElapsed=0;prologueLeaveAt=6f;
        yarn.SetNode(node);pump=true;Render();Save();
    }
    void Deliver(string line){
        int current=delivered++;if(current<skipLines){pump=true;return;}
        state.line=current;fullLine=line.Replace("你（主角）",state.playerName=="你"?"你（主角）":state.playerName).Replace("{name}",state.playerName);reveal=0;autoTimer=0;
        if(state.dialogue=="Prologue"){int attribution=fullLine.IndexOf("——");if(attribution>0)fullLine=fullLine.Substring(0,attribution).TrimEnd()+"\n\n"+fullLine.Substring(attribution);reveal=fullLine.Length;prologueElapsed=0;}
        if(skipLines==0||current>skipLines)state.log.Add(fullLine);
        Render();Save();
    }
    void FinishDialogue(){
        string after=state.after,arg=state.arg;bool replay=state.replay;
        state.dialogue="";state.after="";state.arg="";state.line=-1;fullLine="";fast=false;
        if(replay){if(after=="replayC"&&state.cChoice>=0){Play("C"+state.cChoice,"resume","",true);return;}Resume();Save();return;}
        switch(after){
            case "wake": Play("Wake","bag");return;
            case "bag":state.stage=1;break;
            case "bagInspect":if(!state.bag.Contains(int.Parse(arg)))state.bag.Add(int.Parse(arg));if(state.bag.Count==7){state.stage=2;Play("PhoneIntro","phone");return;}break;
            case "phone":state.stage=2;modal="phone";break;
            case "topic":if(!state.phone.Contains(int.Parse(arg)))state.phone.Add(int.Parse(arg));AfterPhone();return;
            case "name":modal="name";break;
            case "named":if(!state.phone.Contains(1))state.phone.Add(1);AfterPhone();return;
            case "audience":state.stage=3;break;
            case "cchoice":modal="cchoice";break;
            case "audienceDone":UnlockAchievement(state.audience==0?"ash":state.audience==1?"wave":state.cChoice==0?"flower":"rain");state.stage=4;Play("Concert","concert");return;
            case "concert":state.stage=4;break;
            case "photo":state.photo=true;break;
            case "memories":state.stage=5;if(!state.unlocked.Contains("Motel"))state.unlocked.Add("Motel");foreach(var id in new[]{"ticket","magazine","alcohol","key","book"})if(!state.owned.Contains(id))state.owned.Add(id);state.scene="Motel";break;
            case "talk":StoryRules.CompleteTalk(state,catalog.Scene(arg));break;
            case "egg":if(!state.eggs.Contains(arg))state.eggs.Add(arg);TalkScene();return;
            case "final":modal="ending";break;
            case "ending":CompleteEnding(int.Parse(arg));return;
        }
        Render();Save();
    }
    void AfterPhone(){if(state.phone.Count==4){Play("AudienceLead","audience");return;}modal="phone";Render();Save();}
    void Resume(){modal="";if(state.stage==2&&!Busy)modal=state.phone.Contains(1)&&state.phone.Count==4?"":"phone";if(state.stage==3&&state.audience==2&&state.cChoice<0)modal="cchoice";Render();}
    public void InspectBag(int index){if(!CanWorld||state.stage!=1)return;Play("Bag"+index,"bagInspect",index.ToString(),state.bag.Contains(index));}
    public void PhoneTopic(int index){if(Busy)return;bool seen=state.phone.Contains(index);modal="";Save();Play("Phone"+index,seen?"resume":index==1?"name":"topic",index.ToString(),seen);}
    public void SetName(string value){value=(value??"").Trim();if(value.Length==0)value="未命名";state.playerName=value.Length>16?value.Substring(0,16):value;Play("Named","named");}
    public void ChooseAudience(int which){if(!CanWorld||state.stage!=3)return;if(!StoryRules.ChooseAudience(state,which)){if(state.audience==which)ReplayAudience();return;}Save();Play(which==0?"AudienceA":which==1?"AudienceB":"AudienceC",which==2?"cchoice":"audienceDone");}
    public void ChooseC(int which){if(!StoryRules.ChooseC(state,which))return;modal="";Save();Play("C"+which,"audienceDone");}
    void ReplayAudience(){state.log.Clear();Play(state.audience==0?"AudienceA":state.audience==1?"AudienceB":"AudienceC",state.audience==2?"replayC":"resume","",true);}
    public void SwitchScene(int direction){if(!CanWorld||state.stage<5)return;state.scene=StoryRules.NextScene(state,catalog,direction);selectedItem="";state.log.Clear();Render();Save();}
    public void TalkScene(){
        if(Busy)return;
        var s=catalog.Scene(state.scene);
        if(state.collected.Any(id=>catalog.content.collectibles.Any(c=>c.id==id&&c.scene==s.id))&&!state.eggs.Contains(s.id)){Play("Egg_"+s.id,"egg",s.id);return;}
        if(s.id=="Hall"){Play(StoryRules.Ready(state,catalog)?"FinalTalk":"ReturnTalk",StoryRules.Ready(state,catalog)?"final":"resume");return;}
        bool seen=state.talked.Contains(s.id);Play(s.talkNode,"talk",s.id,seen);
    }
    public void PlaceItem(string id,Vector2 location){if(!CanWorld||state.stage<5||id=="hidden_money")return;
        StoryRules.Place(state,catalog,id,state.scene,Mathf.Clamp(location.x,0,Mathf.Max(0,world.rect.width-ui.placedItemPrefab.Rect.rect.width)),Mathf.Clamp(location.y,0,Mathf.Max(0,world.rect.height-ui.placedItemPrefab.Rect.rect.height)));selectedItem="";Sound(catalog.placeSound,.4f);
        if(catalog.content.items.Where(i=>i.home!=state.scene).All(i=>state.wrong.Contains(i.id+"_"+state.scene)))UnlockAchievement("wrong");
        Play("Item_"+id+"_"+state.scene);
    }
    public void Retrieve(string id){if(!CanWorld)return;state.positions.RemoveAll(p=>p.item==id);Render();Save();}
    public void Collect(string id){if(!CanWorld||state.collected.Contains(id))return;state.collected.Add(id);Sound(catalog.collectSound,.3f);if(state.collected.Count==catalog.content.collectibles.Length)UnlockAchievement("collect");Render();Save();}
    public void ChooseEnding(int ending){if(!StoryRules.CanEnd(state,global,catalog,ending))return;modal="";Save();Play("Ending"+ending,"ending",ending.ToString());}
    void CompleteEnding(int ending){state.ending=ending;state.stage=6;if(!global.endings.Contains(ending))global.endings.Add(ending);UnlockAchievement("ending"+ending);Write("global",global);Save();mainMenu=true;modal="cleared";Render();}
    void UnlockAchievement(string id){if(global.achievements.Contains(id))return;global.achievements.Add(id);Write("global",global);achievementTimer=1.8f;}
    void Sound(AudioClip clip,float volume=1){if(clip&&soundSource)soundSource.PlayOneShot(clip,settings.sfx*volume);}
    void Toast(string message){notice=message;noticeUntil=Time.unscaledTime+4;if(noticeText)noticeText.text=notice;}
    void ApplySettings(bool save=true){AudioListener.volume=settings.master;if(musicSource)musicSource.volume=settings.music;Screen.fullScreen=settings.fullscreen;if(save)Write("settings",settings);}
}
}

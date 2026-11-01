using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TGJ {
public partial class GrayboxGame {
    void CloseModal(){modal="";if(!mainMenu&&!Busy)Resume();else Render();}
    void RenderModal(){
        if(IsConversationChoice){RenderConversationChoices();return;}
        ui.Show("ModalShade");var v=ui.Show("Modal_"+modal);
        if(modal!="name")Bind(v,"Close",CloseModal);
        switch(modal){
            case "name":
                v.input.SetTextWithoutNotify(state.playerName=="你"?"":state.playerName);
                Bind(v,"Confirm",()=>SetName(v.input.text));v.input.ActivateInputField();break;
            case "map":
                foreach(var scene in catalog.content.scenes){var s=scene;bool open=state.unlocked.Contains(s.id);var e=v.Get(s.id);e.Text(open?s.title:"未解锁");e.Note(state.scene==s.id?"当前位置":open?s.subtitle:"");
                    Bind(e,()=>{if(Busy)return;state.scene=s.id;modal="";selectedItem="";Render();Save();},open&&!Busy&&state.stage>=5);}
                break;
            case "inventory":
                v.ClearRows();foreach(var item in catalog.content.items){bool own=state.owned.Contains(item.id);v.AddRow(own?item.title:"未获得");}
                if(state.owned.Contains("hidden_money"))v.AddRow("没有递出的钱");v.FitTextRows();break;
            case "collection":
                v.Get("Title").Text("收藏背包  "+state.collected.Count+" / "+catalog.content.collectibles.Length);
                foreach(var c in catalog.content.collectibles){bool got=state.collected.Contains(c.id);var e=v.Get(c.id);e.Text(got?c.title+"\n"+catalog.Scene(c.scene).title:"未收集");e.Note("");}break;
            case "archive":RenderArchive(v);break;
            case "log":
                v.ClearRows();foreach(string line in state.log)v.AddRow(line);if(state.log.Count==0)v.AddRow("等待填入");v.FitTextRows();break;
            case "pause":
                Bind(v,"Resume",CloseModal);Bind(v,"Save",()=>Save(true));Bind(v,"LoadManual",()=>Load(true),Read<RunState>("manual")!=null);Bind(v,"LoadAuto",()=>Load());
                Bind(v,"Settings",()=>{modal="settings";Render();});Bind(v,"MainMenu",()=>{modal="menuconfirm";Render();});break;
            case "settings":RenderSettings(v);break;
            case "newconfirm":case "menuconfirm":case "clearconfirm":
                string which=modal;
                Bind(v,"Confirm",()=>{if(which=="newconfirm")NewGame();else if(which=="menuconfirm"){Save();StopDialogue();mainMenu=true;modal="";Render();}else{global=new GlobalState();Write("global",global);state.owned.Remove("hidden_money");modal="settings";Render();}});
                Bind(v,"Cancel",CloseModal);break;
            case "cleared":
                v.Get("Title").Text("结局 "+state.ending);v.Get("Body").Text("");Bind(v,"MainMenu",()=>{modal="";Render();});break;
        }
    }
    void RenderConversationChoices(){
        var v=ui.Show("Choices_"+modal);
        if(modal=="phone"){
            for(int n=0;n<4;n++){int i=n;var e=v.Get("Choice"+i);e.Text(catalog.content.phoneChoices[i]);e.Note(state.phone.Contains(i)?"已读":"新话题");Bind(e,()=>PhoneTopic(i),!Busy);}
        }else if(modal=="cchoice"||modal=="rewind"){
            bool preview=modal=="rewind";
            for(int n=0;n<2;n++){int i=n;var e=v.Get("Choice"+i);e.Text(catalog.content.cChoices[i]);e.Note("");Bind(e,()=>{if(preview)Play("C"+i,"resume","",true);else ChooseC(i);});}
            if(preview)Bind(v,"Close",CloseModal);
        }else{
            v.Get("Tendency").Text("");
            Bind(v,"Ending1",()=>ChooseEnding(1));Bind(v,"Ending2",()=>ChooseEnding(2));v.Get("Ending3").gameObject.SetActive(global.Hidden&&state.owned.Contains("hidden_money"));Bind(v,"Ending3",()=>ChooseEnding(3));
        }
    }
    void RenderArchive(UIView v){
        v.ClearRows();
        string[] names={state.playerName,"女声","A","B","C"};string[] descriptions={"等待填入","等待填入","等待填入","等待填入","等待填入"};
        for(int i=0;i<5;i++){bool known=i==0||i==1&&state.stage>=2||i==2&&state.stage>=4||i==3&&state.talked.Contains("Classroom")||i==4&&(state.audience==2||state.stage>=5);v.AddRow(known?names[i]+"\n"+descriptions[i]:"未解锁");}
        if(state.photo)v.AddRow("等待填入");
        if(state.audience>=0){var e=v.AddRow("重看");e.Note("");Bind(e,ReplayAudience,!Busy);}
        if(state.audience==2&&state.cChoice>=0){var e=v.AddRow("倒带");e.Note("");Bind(e,()=>{modal="rewind";Render();},!Busy);}
        foreach(var a in catalog.content.achievements){bool got=global.achievements.Contains(a.id);var e=v.AddRow(got?a.title+"\n"+a.quote:"未解锁");e.Note("");}v.FitTextRows();
    }
    void BindSlider(UIView v,string key,float value,Action<float> changed){var s=v.Get(key).GetComponent<Slider>();s.onValueChanged.RemoveAllListeners();s.SetValueWithoutNotify(value);s.onValueChanged.AddListener(x=>{changed(x);ApplySettings();});}
    void RenderSettings(UIView v){
        BindSlider(v,"Master",settings.master,x=>settings.master=x);BindSlider(v,"Music",settings.music,x=>settings.music=x);BindSlider(v,"SFX",settings.sfx,x=>settings.sfx=x);BindSlider(v,"Speed",settings.speed,x=>settings.speed=x);
        v.Get("Fullscreen").Text("全屏");v.Get("Fullscreen").Note(settings.fullscreen?"开启":"关闭");Bind(v,"Fullscreen",()=>{settings.fullscreen=!settings.fullscreen;ApplySettings();Render();});
        v.Get("Reduced").Text("减弱闪烁/动效");v.Get("Reduced").Note(settings.reduced?"开启":"关闭");Bind(v,"Reduced",()=>{settings.reduced=!settings.reduced;ApplySettings();Render();});
        Bind(v,"Reset",()=>{settings=new GameSettings();ApplySettings();Render();});Bind(v,"Clear",()=>{modal="clearconfirm";Render();});
    }
}
}

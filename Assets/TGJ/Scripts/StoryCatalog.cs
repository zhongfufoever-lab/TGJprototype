using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TGJ
{
    [Serializable] public class StoryNode { public string id; [TextArea(2,8)] public string[] lines; }
    [Serializable] public class SceneSpec { public string id,title,subtitle,character,talkNode,nextScene; public string[] grant; }
    [Serializable] public class ItemSpec { public string id,title,home,source; }
    [Serializable] public class CollectibleSpec { public string id,title,scene; }
    [Serializable] public class AchievementSpec { public string id,title,owner; [TextArea] public string quote; }
    [Serializable] public class CatalogSeed {
        public StoryNode[] nodes; public SceneSpec[] scenes; public ItemSpec[] items;
        public CollectibleSpec[] collectibles; public AchievementSpec[] achievements;
        public string[] phoneChoices,cChoices,bagNames;
    }
    [CreateAssetMenu(menuName="TGJ/Story Catalog")]
    public class StoryCatalog : ScriptableObject {
        public CatalogSeed content;
        public Yarn.Unity.YarnProject yarnProject;
        public AudioClip musicNow,musicMemory,placeSound,collectSound,clickSound;
        public SceneSpec Scene(string id) => content.scenes.First(s=>s.id==id);
        public ItemSpec Item(string id) => content.items.First(s=>s.id==id);
        public StoryNode Node(string id) => content.nodes.First(s=>s.id==id);
        public string Line(string id) {
            var authored=yarnProject.baseLocalization.GetLocalizedString(id);
            if(!string.IsNullOrEmpty(authored))return authored;
            if(id.StartsWith("line:"))id=id.Substring(5);
            int cut=id.LastIndexOf('_');
            return Node(id.Substring(0,cut)).lines[int.Parse(id.Substring(cut+1))];
        }
    }
    [Serializable] public class Placement { public string item,scene; public float x,y; }
    [Serializable] public class RunState {
        public int version=2,stage=0,audience=-1,cChoice=-1,ending=0;
        public string playerName="你",scene="Hall",dialogue="",after="",arg="";
        public int line=-1;
        public bool replay=false,photo=false;
        public List<int> bag=new List<int>(),phone=new List<int>();
        public List<string> unlocked=new List<string>{"Hall"},owned=new List<string>(),placed=new List<string>(),talked=new List<string>(),collected=new List<string>(),eggs=new List<string>(),wrong=new List<string>();
        public List<Placement> positions=new List<Placement>();
        public List<string> log=new List<string>();
    }
    [Serializable] public class GlobalState { public List<int> endings=new List<int>(); public List<string> achievements=new List<string>(); public bool Hidden => endings.Contains(1)&&endings.Contains(2); }
    [Serializable] public class GameSettings { public float master=.75f,music=.4f,sfx=.65f,speed=45; public bool reduced=false,fullscreen=false; }
    public static class StoryRules {
        public static bool ChooseAudience(RunState s,int choice) { if(s.audience>=0)return false;s.audience=choice;return true; }
        public static bool ChooseC(RunState s,int choice) {if(s.cChoice>=0)return false;s.cChoice=choice;return true;}
        public static bool Place(RunState s,StoryCatalog c,string item,string scene,float x,float y) {
            if(!s.owned.Contains(item)||!s.unlocked.Contains(scene))return false;
            s.positions.RemoveAll(p=>p.item==item);
            s.positions.Add(new Placement{item=item,scene=scene,x=x,y=y});
            bool correct=c.Item(item).home==scene;
            if(correct&&!s.placed.Contains(item))s.placed.Add(item);
            if(!correct&&!s.wrong.Contains(item+"_"+scene))s.wrong.Add(item+"_"+scene);
            return correct;
        }
        public static void CompleteTalk(RunState s,SceneSpec scene) {
            if(!s.talked.Contains(scene.id))s.talked.Add(scene.id);
            foreach(var id in scene.grant)if(!s.owned.Contains(id))s.owned.Add(id);
            if(!string.IsNullOrEmpty(scene.nextScene)&&!s.unlocked.Contains(scene.nextScene))s.unlocked.Add(scene.nextScene);
        }
        public static bool Ready(RunState s,StoryCatalog c)=>c.content.items.All(i=>s.placed.Contains(i.id));
        public static bool CanEnd(RunState s,GlobalState g,StoryCatalog c,int ending)=>s.stage==5&&Ready(s,c)&&(ending==1||ending==2||(ending==3&&g.Hidden&&s.owned.Contains("hidden_money")));
        public static RunState NewRun(GlobalState g) {var s=new RunState();if(g.Hidden)s.owned.Add("hidden_money");return s;}
        public static string NextScene(RunState s,StoryCatalog c,int direction) {
            var available=c.content.scenes.Where(x=>s.unlocked.Contains(x.id)).Select(x=>x.id).ToList();
            int index=available.IndexOf(s.scene);return available[(index+direction+available.Count)%available.Count];
        }
    }
}

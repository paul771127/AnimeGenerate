using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public sealed class GalleryClock
    {
        public double started,deadline;public bool wound;public float finishSeconds;
        public GalleryClock(double now,int minutes){started=now;deadline=now+minutes*60;}
        public double Remaining(double now)=>Math.Max(0,deadline-now);
        public bool Wind(double now){if(wound||now>=deadline)return false;deadline+=120;wound=true;return true;}
        public bool Expired(double now)=>now>=deadline;
    }
    public partial class SoloBattle
    {
        GalleryClock galleryClock;double nextGallerySync,nextGalleryBeep,leverUntil;int galleryChoiceSeen;string galleryLever;
        bool galleryShopOpen,galleryNeutralGranted,galleryKey,galleryMarketBonus,galleryHubBonus;int galleryExtraChests;
        readonly HashSet<int> galleryKeep=new HashSet<int>();readonly List<int> galleryRemove=new List<int>();
        readonly Dictionary<int,GalleryTrapFx> galleryFx=new Dictionary<int,GalleryTrapFx>();
        readonly List<GameObject> galleryColumns=new List<GameObject>(),galleryPeople=new List<GameObject>();
        MechanicalArt galleryBoss;float galleryLastHp;int galleryLayoutCount=-1;
        bool InGallery=>dungeon!=null&&dungeon.id==TrapGallery.Id&&dungeon.traps&&galleryClock!=null;
        double GalleryNow=>SharedClock?.Invoke()??Time.realtimeSinceStartupAsDouble;
        void BeginGallery()
        {
            if(dungeon==null||dungeon.id!=TrapGallery.Id||!dungeon.traps)return;
            galleryClock=new GalleryClock(GalleryNow,dungeon.timeLimit);Ticker(dungeon.before);galleryShopOpen=galleryNeutralGranted=galleryKey=galleryMarketBonus=galleryHubBonus=false;
            galleryLever=null;galleryExtraChests=0;galleryChoiceSeen=0;nextGallerySync=nextGalleryBeep=0;
            battle.OnTrapAdd+=cue=>{if(coop&&NetIsHost)NetSend?.Invoke(new Msg{t="trap",k=JsonUtility.ToJson(cue),c=galleryClock.started,t0=GalleryNow+cue.hit-battle.now});};
        }
        void GalleryEnter()
        {
            if(!InGallery)return;
            battle.ConfigureGallery(drun.node.id,drun.Step,difficulty);battle.galleryStopBossFlame=galleryLever=="correct";battle.galleryFastTraps=galleryLever=="wrong"&&drun.node.id=="core";
            galleryShopOpen=drun.node.id=="market";leverUntil=GalleryNow+20;galleryLayoutCount=-1;
            foreach(var p in galleryPeople)if(p)Destroy(p);galleryPeople.Clear();
            foreach(string id in drun.node.npcs)
            {
                var n=dungeon.Npc(id);if(n==null||n.role=="choice"||n.role=="escort")continue;
                var rig=CharacterRig.Create(n.look);rig.InCombat=false;GalleryResidentArt.Decorate(rig,n.id);rig.transform.position=WorldFrame.FromWorld(new Vector3(-2.2f,0,2.2f));
                if(n.id=="oldsmith")
                {
                    var mat=new Material(Shader.Find("Sprites/Default")){color=new Color(.2f,.75f,1,.35f)};
                    foreach(var rr in rig.GetComponentsInChildren<Renderer>())rr.sharedMaterial=mat;
                    rig.gameObject.AddComponent<GalleryGhostMaterial>().material=mat;
                }
                galleryPeople.Add(rig.gameObject);
            }
            SendGalleryState();
        }
        void SendGalleryState()
        {
            if(!InGallery||!DungeonDecider||!coop)return;
            NetSend?.Invoke(new Msg{t="vault",t0=galleryClock.deadline,c=galleryClock.started,h=leverUntil,ok=galleryClock.wound,k=galleryLever??"",e=galleryKey?1:0});
            NetSend?.Invoke(new Msg{t="trapcfg",hp=battle.galleryEpoch,c=galleryClock.started,k=drun.node.id,pts=battle.flamePillars.ToArray()});
        }
        void ApplyGalleryLever(string result)
        {
            if(string.IsNullOrEmpty(result))return;galleryLever=result;
            battle.galleryStopBossFlame=result=="correct";battle.galleryFastTraps=result=="wrong"&&drun.node.id=="core";
            if(result=="neutral"&&!galleryNeutralGranted){galleryNeutralGranted=true;Economy.AddItem("hp_l",1);Say("老匠留下了大治療藥水 ×1",3);}
        }
        void GalleryTick()
        {
            if(!InGallery||battle==null||phase!=Phase.Fighting)return;
            double now=GalleryNow;
            if(galleryClock.Expired(now)&&!battle.stageCleared)
            {
                battle.FailStage("機關重新上鎖了…");introUntil=-1;readyStep=-1;scrollAsk=false;
                if(DungeonDecider&&coop)NetSend?.Invoke(new Msg{t="bover",ok=false,e=1});
                phase=Phase.Over;ShowResult();return;
            }
            if(galleryClock.Remaining(now)<=180&&now>=nextGalleryBeep){nextGalleryBeep=now+15;Sfx.Play("warn",.7f);}
            if(DungeonDecider&&now>=nextGallerySync){nextGallerySync=now+1;SendGalleryState();}
            if(drun.ChoiceSerial!=galleryChoiceSeen)
            {galleryChoiceSeen=drun.ChoiceSerial;if(drun.lastChoiceId=="oldsmith")ApplyGalleryLever(drun.lastChoiceResult);}
            if(DungeonDecider&&drun.pendingChoice!=null&&drun.pendingChoice.def.role=="lever"&&now>=leverUntil)
            {int option=runRng.Next(drun.pendingChoice.def.options.Count);drun.Choose(option);NetSend?.Invoke(new Msg{t="dchoice",e=option});ApplyGalleryLever(drun.lastChoiceResult);StartNodeFight();SendGalleryState();}
            if(DungeonDecider&&galleryExtraChests>0&&chest!=null&&chest.done&&Time.time>=chest.doneAt+2.5f)
            {galleryExtraChests--;HostOpenChest();}
            UpdateGalleryArt();
        }
        void GalleryNpcDied(string id)
        {if(InGallery&&id=="ark"&&drun.npcs.TryGetValue(id,out var n)&&n.side=="enemy"){galleryKey=true;SendGalleryState();}}
        void GalleryClearedRoom()
        {
            if(!InGallery||!DungeonDecider)return;
            if(drun.node.id=="hub"&&!galleryHubBonus&&drun.npcs.TryGetValue("lingyin",out var n)&&!n.done)
            {galleryHubBonus=true;galleryExtraChests=1;HostOpenChest();}
        }
        void GalleryChestOpened()
        {
            if(!InGallery||!DungeonDecider)return;
            if(drun.node.id=="market"&&!galleryMarketBonus&&drun.npcs.TryGetValue("wheat",out var n)&&n.safe&&!n.dead)
            {galleryMarketBonus=true;galleryExtraChests++;ChatEvent("小麥打開了另一只隱藏寶箱。");}
        }
        bool GalleryRouteReady=>!InGallery||!galleryShopOpen&&galleryExtraChests==0&&(chest==null||chest.done&&Time.time>=chest.doneAt+2.5f);
        void GalleryRewards()
        {
            if(!InGallery)return;
            pendingItems.Add(TrapGallery.Treasure(dropRng,myClass));
            if(galleryKey)pendingItems.Add(ItemGen.Make(dropRng,45,3,myClass));
            galleryClock.finishSeconds=(float)(GalleryNow-galleryClock.started);TrapGallery.Record(galleryClock.finishSeconds);
        }
        bool GalleryMessage(Msg m)
        {
            if(!InGallery||battle==null)return false;
            if(m.t=="trap")
            {
                if(NetIsHost||Math.Abs(m.c-galleryClock.started)>.5)return true;var c=JsonUtility.FromJson<Battle.TrapCue>(m.k);if(c!=null)battle.RemoteTrap(c,battle.now+(float)(m.t0-GalleryNow));return true;
            }
            if(m.t=="vault")
            {
                if(NetIsHost||m.c<galleryClock.started-.5)return true;galleryClock.deadline=m.t0;galleryClock.started=m.c;galleryClock.wound=m.ok;if(m.h>0)leverUntil=m.h;galleryKey=m.e==1;ApplyGalleryLever(m.k);return true;
            }
            if(m.t=="trapcfg")
            {if(!NetIsHost&&Math.Abs(m.c-galleryClock.started)<.5&&m.hp==battle.galleryEpoch&&m.k==drun.node.id){battle.flamePillars.Clear();if(m.pts!=null)battle.flamePillars.AddRange(m.pts);}return true;}
            if(m.t=="vaultshopclose")
            {if(!NetIsHost)galleryShopOpen=false;return true;}
            return false;
        }
        void UpdateGalleryArt()
        {
            foreach(var n in battle.npcs)if(n.npcId=="ark"&&n.npcSide==0)n.npcEscort=true;
            if(bossRig)bossRig.gameObject.SetActive(false);
            if(galleryLayoutCount!=battle.flamePillars.Count)
            {
                foreach(var p in galleryColumns)if(p)Destroy(p);galleryColumns.Clear();
                foreach(var p in battle.flamePillars)galleryColumns.Add(GalleryTrapFx.Pillar(WorldFrame.FromWorld(p)));galleryLayoutCount=battle.flamePillars.Count;
            }
            var keep=galleryKeep;keep.Clear();
            foreach(var c in battle.trapCues)
            {
                if(c.epoch!=battle.galleryEpoch||c.done)continue;keep.Add(c.id);
                if(!galleryFx.TryGetValue(c.id,out var fx)||!fx){fx=GalleryTrapFx.Create(c);galleryFx[c.id]=fx;}
                fx.Tick(battle.now);
            }
            foreach(var kv in galleryFx)if(!keep.Contains(kv.Key)&&kv.Value)Destroy(kv.Value.gameObject);
            var remove=galleryRemove;remove.Clear();foreach(var kv in galleryFx)if(!keep.Contains(kv.Key))remove.Add(kv.Key);foreach(int id in remove)galleryFx.Remove(id);
            if(En.Alive)
            {
                if(!galleryBoss){galleryBoss=MechanicalArt.Create(53);galleryLastHp=En.hp;}
                galleryBoss.transform.SetPositionAndRotation(WorldFrame.FromWorld(En.Feet),Quaternion.LookRotation(WorldFrame.DirFromWorld(En.forward),Vector3.up));
                int ph=En.hp<=En.maxHp*.35f?3:En.hp<=En.maxHp*.7f?2:1;battle.GalleryPhase=ph;galleryBoss.SetPhase(ph);
                galleryBoss.Hit=En.hp<galleryLastHp;galleryLastHp=En.hp;galleryBoss.Wind=En.windupUntil>battle.now?Mathf.Clamp01(1-(En.windupUntil-battle.now)/3.5f):0;galleryBoss.Striking=En.windupUntil<=0&&battle.trapCues.Exists(c=>c.source<0&&battle.now>=c.hit&&battle.now<c.hit+.5f);
            }
            else if(galleryBoss)galleryBoss.Dead=true;
        }
        void ClearGallery()
        {
            foreach(var f in galleryFx.Values)if(f)Destroy(f.gameObject);galleryFx.Clear();
            foreach(var p in galleryColumns)if(p)Destroy(p);galleryColumns.Clear();foreach(var p in galleryPeople)if(p)Destroy(p);galleryPeople.Clear();
            if(galleryBoss)Destroy(galleryBoss.gameObject);galleryBoss=null;galleryClock=null;galleryLayoutCount=-1;
        }
        void DrawGalleryHud()
        {
            if(!InGallery||phase!=Phase.Fighting)return;
            int depth=GUI.depth;GUI.depth=-900;float w=Screen.width,pad=w*.035f;int fs=UiKit.BaseFont;float remaining=(float)galleryClock.Remaining(GalleryNow);
            var color=remaining<=180?new Color(1,.25f,.2f,.7f+.3f*Mathf.Sin(Time.unscaledTime*6)):new Color(.3f,1,.9f);
            var rect=new Rect(pad,Screen.height*.12f,w-pad*2,fs*2.4f);UiKit.Card(rect,.95f,color,.7f);
            UiKit.Text(rect,$"千機迴廊　{Mathf.FloorToInt(remaining/60):00}:{Mathf.FloorToInt(remaining%60):00}",fs,color,TextAnchor.MiddleCenter,true);
            if(drun.npcs.TryGetValue("lingyin",out var n)&&n.done)
            {
                Battle.TrapCue next=null;foreach(var c in battle.trapCues)if(c.source==0&&!c.done&&c.hit>battle.now&&(next==null||c.hit<next.hit))next=c;
                UiKit.Text(new Rect(pad,rect.yMax,w-pad*2,fs*1.5f),next==null?"鈴音：正在聽齒輪……":$"下一個：{TrapName(next.kind)}（約 {Mathf.CeilToInt(next.hit-battle.now)} 秒）",fs-2,color,TextAnchor.MiddleRight);
            }
            if(galleryClock.finishSeconds>0)UiKit.Text(rect,"通關時間 "+galleryClock.finishSeconds.ToString("0.0")+" 秒",fs,color);
            if(galleryShopOpen&&drun.node.id=="market"&&galleryExtraChests==0&&(chest==null||chest.done&&Time.time>=chest.doneAt+2.5f))
            {
                float y=Screen.height*.48f,bh=fs*2.5f;var r=new Rect(pad,y,w-pad*2,bh*4.6f);UiKit.Card(r,.98f,new Color(1,.78f,.3f),.8f);
                if(UiKit.Button(new Rect(r.x,r.y,r.width,bh),"小治療藥水 45 金",fs))GalleryBuy("hp_s",45);
                if(UiKit.Button(new Rect(r.x,r.y+bh,r.width,bh),"大治療藥水 135 金",fs))GalleryBuy("hp_l",135);
                if(UiKit.Button(new Rect(r.x,r.y+bh*2,r.width,bh),galleryClock.wound?"已上發條":"上發條 +2分鐘 2,000金（房主）",fs,false,null,DungeonDecider&&!galleryClock.wound))
                {if(DungeonDecider&&Economy.Gold>=2000&&galleryClock.Wind(GalleryNow)){Economy.Gold-=2000;SendGalleryState();}}
                if(UiKit.Button(new Rect(r.x,r.y+bh*3,r.width,bh),DungeonDecider?"離開商店，繼續前進":"我買好了",fs)){galleryShopOpen=false;if(DungeonDecider)NetSend?.Invoke(new Msg{t="vaultshopclose"});}
            }
            if(drun.pendingChoice!=null&&drun.pendingChoice.def.role=="lever")UiKit.Text(new Rect(pad,rect.yMax+fs*1.5f,w-pad*2,fs*1.5f),$"選拉桿：{Math.Max(0,leverUntil-GalleryNow):0} 秒",fs,Color.yellow,TextAnchor.MiddleCenter);
            GUI.depth=depth;
        }
        static string TrapName(Battle.TrapKind k)=>k==Battle.TrapKind.Rock?"落石":k==Battle.TrapKind.Flame?"旋轉噴火器":k==Battle.TrapKind.Arrows?"飛箭":"橫向滾木";
        void GalleryBuy(string id,int price)
        {if(!InGallery||drun.node.id!="market"||Economy.Gold<price||galleryClock.Expired(GalleryNow))return;Economy.Gold-=price;Economy.AddItem(id,1);ChatEvent("齒輪商人：交易完成。完成、完成。");}
    }
    public sealed class GalleryGhostMaterial:MonoBehaviour{public Material material;void OnDestroy(){if(material)Destroy(material);}}
}

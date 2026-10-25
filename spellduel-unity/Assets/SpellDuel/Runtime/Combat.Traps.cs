using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public static class TrapGallery
    {
        public const string Id="thousand_trap_gallery";
        public static readonly string[] Names={"弩機哨兵","青銅守門人","迴旋刀偶","鍘刀傀儡","銅翼鴉"};
        public static bool IsKind(int k)=>k>=48&&k<=52;
        public static void Register()
        {
            Skills.Classes["boss_clockwork"]=new ClassDef{id="boss_clockwork",name="千機匠魂",color=Skills.C("#d8ac62"),maxHp=4200,maxMp=1000,mpRegen=10,skills=new string[0],defaultLoadout=new string[0],desc="萬象之心的守護匠魂"};
            Skills.All["vault_hit"]=new SkillDef{id="vault_hit",cls="minion",name="機關打擊",color=Skills.C("#ff914d"),damage=15,charge=3.5f,type=SkillType.Projectile};
        }
        public static Item Treasure(System.Random rng,string cls)
        {
            int rarity=rng.NextDouble()<.03?5:4;bool set=rng.NextDouble()<.30;
            var item=ItemGen.Make(rng,45,rarity,cls);item.setId=set?"clockwork":"";
            item.reqLevel=45;
            item.name=(set?"千機":"萬象")+"・"+Item.SlotNames[(int)item.slot];return item;
        }
        public static float BestSeconds
        {
            get{float.TryParse(Progress.Get("sd_vault_best"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n);return n;}
        }
        public static void Record(float seconds){if(seconds>0&&(BestSeconds<=0||seconds<BestSeconds))Progress.Set("sd_vault_best",seconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
    }
    public partial class Battle
    {
        public bool Gallery {get;private set;}
        public int galleryEpoch,GalleryPhase=1;public string galleryNode;
        public bool galleryStopBossFlame,galleryFastTraps;
        public float galleryDifficulty=1;
        public enum TrapKind{Rock,Flame,Arrows,Log,Bolt,Smash,Low,High,Dive,Summon,Volley}
        [Serializable] public sealed class TrapCue
        {
            public int id,epoch,source,mask,count=3;public TrapKind kind;
            public Vector3 center,forward;public float hit,warn=3.5f,duration=.5f,radius=2,halfX=2,halfZ=2,power=.22f;
            [NonSerialized] public bool done,notified,sent;[NonSerialized] public HashSet<Fighter> struck=new HashSet<Fighter>();
            public float Start=>hit-warn;
        }
        public readonly List<TrapCue> trapCues=new List<TrapCue>();
        public readonly List<Vector3> flamePillars=new List<Vector3>();
        public event Action<TrapCue> OnTrapAdd;
        int nextTrapId,lastRandom=-1,repeats,bossActions;float nextRandom,nextGalleryBoss,galleryPause;bool bossFlameAdded,lastRush;
        readonly Dictionary<Fighter,TrapCue> galleryAttacks=new Dictionary<Fighter,TrapCue>();
        readonly float[] flameDue=new float[3];
        bool galleryTrapDamage;
        static T[] GalleryArray<T>(T[] old,Func<int,T> value){var a=new T[53];Array.Copy(old,a,old.Length);for(int i=48;i<53;i++)a[i]=value(i);return a;}
        public void ConfigureGallery(string node,int epoch,int difficulty)
        {
            Gallery=true;bossBaseHp=2400;galleryNode=node;galleryEpoch=epoch;GalleryPhase=1;lastRush=bossFlameAdded=false;bossActions=0;
            trapCues.Clear();galleryAttacks.Clear();flamePillars.Clear();lastRandom=-1;repeats=0;
            galleryDifficulty=difficulty<=0?.7f:difficulty==1?1:1.3f;galleryPause=0;nextRandom=now+12;nextGalleryBoss=now+5;
            if(remoteEnemy)return;
            GalleryFrame(out var center,out float x,out float z);
            int pillars=node=="gate"?1:2;
            for(int i=0;i<pillars;i++)
            {
                Vector3 best=center;float far=-1;
                for(int j=0;j<16;j++)
                {float a=(j+(float)rng.NextDouble())*Mathf.PI/8;float inset=.85f+(float)rng.NextDouble()*.05f;var p=center+new Vector3((Mathf.Cos(a)>=0?1:-1)*x*inset,0,(Mathf.Sin(a)>=0?1:-1)*z*inset);float dist=FlatDistance(p,player.Feet);bool separate=true;foreach(var old in flamePillars)if(FlatDistance(p,old)<1)separate=false;if(dist>far&&separate&&GalleryInside(p)){best=p;far=dist;}}
                if(far>=1.5f)flamePillars.Add(best);
                flameDue[i]=now+8+i*10;
            }
        }
        public void DisableGallery(){Gallery=false;trapCues.Clear();galleryAttacks.Clear();flamePillars.Clear();}
        public void GalleryFrame(out Vector3 center,out float halfX,out float halfZ)
        {
            if(arenaPoly==null||arenaPoly.Count<3){center=Vector3.zero;halfX=halfZ=2;return;}
            float lx=float.MaxValue,lz=lx,hx=float.MinValue,hz=hx;
            foreach(var p in arenaPoly){lx=Mathf.Min(lx,p.x);hx=Mathf.Max(hx,p.x);lz=Mathf.Min(lz,p.z);hz=Mathf.Max(hz,p.z);}
            center=new Vector3((lx+hx)*.5f,0,(lz+hz)*.5f);halfX=(hx-lx)*.5f;halfZ=(hz-lz)*.5f;
        }
        bool GalleryInside(Vector3 p)
        {if(arenaPoly==null||arenaPoly.Count<3)return true;bool inside=false;for(int i=0,j=arenaPoly.Count-1;i<arenaPoly.Count;j=i++){var a=arenaPoly[i];var b=arenaPoly[j];if((a.z>p.z)!=(b.z>p.z)&&p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x)inside=!inside;}return inside;}
        public int GalleryRoster(int ordinal)
        {
            int[] roster=galleryNode=="gate"?new[]{49,48}:galleryNode=="cogway"?new[]{49,48,52}:galleryNode=="arrowhall"?new[]{48,50}:galleryNode=="cells"?new[]{49,51}:galleryNode=="shaft"?new[]{52,48}:galleryNode=="logrun"?new[]{50,49}:galleryNode=="mirror"?new[]{51,50}:new[]{48,49,50,51,52};
            return roster[ordinal%roster.Length];
        }
        public float ReserveGallery(float desired,float duration)
        {
            float at=Mathf.Max(desired,galleryPause+3.5f);
            for(int guard=0;guard<200;guard++)
            {bool overlap=false;foreach(var c in trapCues)if(!c.done&&at<c.hit+c.duration+2&&at+duration+2>c.hit){at=c.hit+c.duration+2;overlap=true;break;}if(!overlap)return at;}
            return at;
        }
        TrapCue QueueTrap(TrapKind kind,Vector3 center,Vector3 forward,int source=0,float desired=0,float radius=2)
        {
            float duration=kind==TrapKind.Flame?1.5f:kind==TrapKind.Arrows||kind==TrapKind.Log?1.2f:.35f;
            var c=new TrapCue{id=++nextTrapId,epoch=galleryEpoch,kind=kind,center=center,forward=forward,source=source,duration=duration,radius=radius,hit=ReserveGallery(Mathf.Max(now+3.5f,desired),duration)};
            GalleryFrame(out var frame,out c.halfX,out c.halfZ);c.mask=1<<rng.Next(4);c.count=rng.Next(3,6);
            if(kind==TrapKind.Rock&&GalleryPhase==3)c.mask|=1<<((MaskIndex(c.mask)+1+rng.Next(3))%4);
            trapCues.Add(c);return c;
        }
        static int MaskIndex(int mask){for(int i=0;i<4;i++)if((mask&(1<<i))!=0)return i;return 0;}
        public void RemoteTrap(TrapCue cue,float localHit)
        {
            if(!Gallery||!remoteEnemy||cue.epoch!=galleryEpoch||cue.id<1)return;
            var old=trapCues.Find(x=>x.id==cue.id);if(old!=null){if(Mathf.Abs(old.hit-localHit)>.2f)old.notified=false;old.hit=localHit;old.mask=cue.mask;return;}
            cue.hit=localHit;cue.sent=true;if(cue.struck==null)cue.struck=new HashSet<Fighter>();trapCues.Add(cue);
        }
        public static string TrapHint(TrapKind k)=>k==TrapKind.Flame||k==TrapKind.Low?"⬆ 跳！":k==TrapKind.Log||k==TrapKind.High?"⬇ 蹲下！":k==TrapKind.Summon?"增援即將啟動！":"⬅ 移開！";
        public static bool TrapContains(TrapCue c,Vector3 feet,float time)
        {
            var p=feet-c.center;float u=Mathf.Clamp01((time-c.hit)/Mathf.Max(.01f,c.duration));
            if(c.kind==TrapKind.Rock){int q=(p.x>=0?1:0)+(p.z>=0?2:0);return Mathf.Abs(p.x)<=c.halfX&&Mathf.Abs(p.z)<=c.halfZ&&(c.mask&(1<<q))!=0;}
            if(c.kind==TrapKind.Flame){float r=new Vector2(p.x,p.z).magnitude;if(r>2)return false;float angle=Mathf.Repeat(Mathf.Atan2(p.x,p.z)*Mathf.Rad2Deg,360);return Mathf.Abs(Mathf.DeltaAngle(angle,u*360))<=22;}
            float along=Vector3.Dot(p,c.forward);var right=new Vector3(c.forward.z,0,-c.forward.x);float side=Vector3.Dot(p,right);
            if(c.kind==TrapKind.Arrows||c.kind==TrapKind.Log)
            {
                float travel=c.radius*2*u;if(Mathf.Abs(along-(-c.radius+travel))>.38f)return false;
                if(c.kind==TrapKind.Log)return Mathf.Abs(side)<=c.halfX;
                // All arrows occupy the negative half of a wall; the positive half leaves >=1m safe on a 3m arena.
                for(int i=0;i<c.count;i++){float lane=-c.halfX*.85f+i*(c.halfX*.75f/Mathf.Max(1,c.count-1));if(Mathf.Abs(side-lane)<.16f)return true;}return false;
            }
            if(c.kind==TrapKind.Bolt||c.kind==TrapKind.Volley)
            {if(along<0||along>c.radius)return false;if(c.kind==TrapKind.Bolt)return Mathf.Abs(side)<.28f;return Mathf.Abs(side)<.24f||Mathf.Abs(side-along*.3f)<.24f||Mathf.Abs(side+along*.3f)<.24f;}
            if(new Vector2(p.x,p.z).magnitude>c.radius)return false;
            return c.kind!=TrapKind.High||p.sqrMagnitude<.001f||Vector3.Angle(c.forward,new Vector3(p.x,0,p.z))<=75;
        }
        void TickGalleryMinion(Fighter m,Fighter target,float distance,float dt,int hold)
        {
            if(hold>0||m.snaredUntil>now||now<galleryPause)return;
            if(galleryAttacks.TryGetValue(m,out var pending)&&!pending.done){m.forward=pending.forward;return;}
            if(m.minionKind==52){m.alt=.75f;m.head.y=1.2f+m.alt;}
            if(now>=m.attackReadyAt&&distance<3&&GalleryInside(m.Feet)&&CanAttackFrom(m.Feet))
            {
                float at=ReserveGallery(now+3.5f,.35f);if(at>now+6)return;
                var kind=m.minionKind==48?TrapKind.Bolt:m.minionKind==49?TrapKind.Smash:m.minionKind==50?TrapKind.Low:m.minionKind==51?TrapKind.High:TrapKind.Dive;
                var fw=Fighter.Flat(target.Feet-m.Feet);if(fw==Vector3.zero)fw=Vector3.forward;
                var c=QueueTrap(kind,kind==TrapKind.Dive?target.Feet:m.Feet,fw,m.minionId,at,kind==TrapKind.Bolt?3:kind==TrapKind.Dive?.8f:2);
                c.power=18*m.dmgMul;m.windupUntil=c.hit;m.lockFwd=fw;galleryAttacks[m]=c;return;
            }
            bool inside=GalleryInside(m.Feet);var goal=target.Feet;if(!inside)GalleryFrame(out goal,out float gx,out float gz);
            float remaining=FlatDistance(goal,m.Feet)-(inside?1.7f:0);
            if(remaining>0){var step=Fighter.Flat(goal-m.Feet)*Mathf.Min(.38f*m.speedMul*dt,remaining);if(GalleryInside(m.Feet+step)||!inside)m.head+=step;}
        }
        void TickGalleryBoss()
        {
            if(!enemy.Alive||!IsBossWave||bossPending)return;
            int phase=enemy.hp<=enemy.maxHp*.35f?3:enemy.hp<=enemy.maxHp*.7f?2:1;
            if(phase!=GalleryPhase){GalleryPhase=phase;Emit("specialwarn",enemy.Feet,Skills.C("#4ae4e7"),"千機匠魂："+(phase==2?"弩臂展開！":"刃臂啟動！"));}
            if(!lastRush&&enemy.hp<=enemy.maxHp*.1f)
            {
                lastRush=true;galleryPause=now+5;nextGalleryBoss=galleryPause+1;
                foreach(var c in trapCues)if(!c.done){if(c.source<0){c.done=true;enemy.windupUntil=0;}else{c.hit=ReserveGallery(galleryPause+3.5f,c.duration);c.notified=false;var minion=minions.Find(m=>m.minionId==c.source);if(minion!=null)minion.windupUntil=c.hit;OnTrapAdd?.Invoke(c);}}
                Emit("specialwarn",enemy.Feet,Color.cyan,"全機關停擺五秒，現在輸出！");return;
            }
            if(now<galleryPause)return;
            if(phase>=2&&!bossFlameAdded&&!galleryStopBossFlame)
            {GalleryFrame(out var p,out float x,out float z);for(int j=0;j<4;j++){var at=p+new Vector3(j%2==0?x*.88f:-x*.88f,0,j<2?z*.88f:-z*.88f);bool ok=GalleryInside(at)&&FlatDistance(at,player.Feet)>=1.5f;foreach(var old in flamePillars)if(FlatDistance(at,old)<.8f)ok=false;if(ok){flamePillars.Add(at);flameDue[flamePillars.Count-1]=now+10;break;}}bossFlameAdded=true;}
            if(now<nextGalleryBoss)return;
            if(galleryAttacks.TryGetValue(enemy,out var pending)&&!pending.done)return;
            var target=player;foreach(var p in Players())if(p.Alive){target=p;break;}
            var fw=Fighter.Flat(target.Feet-enemy.Feet);if(fw==Vector3.zero)fw=Vector3.forward;
            int turn=bossActions++;
            TrapKind kind=phase==1?(turn%2==0?TrapKind.Smash:TrapKind.Summon):phase==2?TrapKind.Volley:(turn%2==0?TrapKind.Low:TrapKind.High);
            var bossCue=QueueTrap(kind,enemy.Feet,fw,-1,now+3.5f,kind==TrapKind.Volley?4:2.5f);bossCue.power=30*DmgMul;enemy.windupUntil=bossCue.hit;enemy.forward=enemy.lockFwd=fw;galleryAttacks[enemy]=bossCue;nextGalleryBoss=bossCue.hit+5;
        }
        void UpdateGalleryTraps()
        {
            if(!Gallery||stageCleared||stageFailed)return;
            if(awaitingNext){foreach(var cue in trapCues)cue.done=true;return;}
            if(!remoteEnemy&&!awaitingNext&&waveToSpawn+minions.Count>0||!remoteEnemy&&enemy.Alive&&IsBossWave&&!bossPending)
            {
                TickGalleryBoss();
                if(now>=nextRandom-8&&now>=galleryPause)
                {
                    int allowed=galleryNode=="gate"?1:galleryNode=="cogway"?2:3;int selected=rng.Next(allowed);
                    if(rng.NextDouble()<.6)selected=galleryNode=="shaft"?0:galleryNode=="arrowhall"?1:galleryNode=="logrun"?2:selected;
                    if(selected==lastRandom&&repeats>=2)selected=(selected+1)%allowed;
                    repeats=selected==lastRandom?repeats+1:1;lastRandom=selected;
                    GalleryFrame(out var center,out float x,out float z);int wall=rng.Next(4);var fw=wall==0?Vector3.forward:wall==1?Vector3.back:wall==2?Vector3.right:Vector3.left;
                    var c=QueueTrap(selected==0?TrapKind.Rock:selected==1?TrapKind.Arrows:TrapKind.Log,center,fw,0,nextRandom+3.5f,Mathf.Abs(fw.z)>.5f?z:x);
                    if(Mathf.Abs(fw.x)>.5f){float swap=c.halfX;c.halfX=c.halfZ;c.halfZ=swap;}
                    bool boss=IsBossWave&&!bossPending;float lo=boss&&!galleryFastTraps?15:10,hi=galleryNode=="hub"||galleryFastTraps?20:30;
                    nextRandom=c.hit+lo+(float)rng.NextDouble()*(hi-lo)-3.5f;
                }
                for(int i=0;i<flamePillars.Count;i++)if(now>=flameDue[i]-8&&now>=galleryPause){var c=QueueTrap(TrapKind.Flame,flamePillars[i],Vector3.forward,0,flameDue[i]+3.5f,2);flameDue[i]=c.hit+20-3.5f;}
            }
            for(int i=0;i<trapCues.Count;i++)
            {
                var c=trapCues[i];if(c.done)continue;
                if(!c.sent){c.sent=true;OnTrapAdd?.Invoke(c);}
                if(!c.notified&&now>=c.Start){c.notified=true;Emit("specialwarn",c.center,Color.red,TrapHint(c.kind));}
                if(now<c.hit)continue;
                if(now>c.hit+c.duration){c.done=true;var expired=c.source<0?enemy:minions.Find(m=>m.minionId==c.source);if(expired!=null){expired.windupUntil=0;expired.attackReadyAt=now+4;}continue;}
                Fighter owner=c.source<0?enemy:c.source==0?null:minions.Find(m=>m.minionId==c.source);
                if(c.source!=0&&(owner==null||!owner.Alive)){c.done=true;continue;}
                if(owner!=null)owner.windupUntil=0;
                if(c.kind==TrapKind.Summon)
                {if(!remoteEnemy){for(int j=0;j<2&&minions.Count<24;j++)SpawnMinion(enemy.Feet+new Vector3(j==0?-.8f:.8f,0,0),0,49,HpMul,DmgMul,SpeedMul);}c.done=true;continue;}
                if(owner==null)GalleryHit(c,player,null);
                else if(remoteEnemy)GalleryHit(c,player,owner);
                else foreach(var v in FoesOf(owner))if(!coopHost||!v.isAlly)GalleryHit(c,v,owner);
                if(now>c.hit+c.duration){c.done=true;if(owner!=null)owner.attackReadyAt=now+4;}
            }
            trapCues.RemoveAll(c=>c.done&&now>c.hit+4);
        }
        void GalleryHit(TrapCue c,Fighter target,Fighter owner)
        {
            if(target==null||!target.Alive||c.struck.Contains(target)||!TrapContains(c,target.Feet,now))return;
            c.struck.Add(target);
            bool evade=(c.kind==TrapKind.Flame||c.kind==TrapKind.Low)&&target.airborneUntil>now||(c.kind==TrapKind.Log||c.kind==TrapKind.High)&&target.crouching;
            if(evade){target.evadeBoostUntil=now+3;Emit("evade",target.Chest,Color.cyan,"閃過機關！");return;}
            if(owner==null)
            {
                // Environment checks only the local physical player, never proxies, NPCs or pets.
                if(target!=player)return;
                galleryTrapDamage=true;try{HazardDamage(target,Mathf.RoundToInt(target.maxHp*c.power*galleryDifficulty),target.Chest,"迴廊機關");}finally{galleryTrapDamage=false;}
            }
            else HitFoe(owner,target,Skills.All["vault_hit"],Mathf.RoundToInt(c.power));
        }
    }
}

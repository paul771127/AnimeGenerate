using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class Battle
    {
        // Extend all kind-indexed stat arrays together, leaving their first 23 entries intact.
        static Battle()
        {
            MinionNames=EliteArray(MinionNames,d=>d.name);
            MinionHp=EliteArray(MinionHp,d=>Mathf.RoundToInt(MinionHp[d.baseKind]*d.hp));
            MinionSpeed=EliteArray(MinionSpeed,d=>MinionSpeed[d.baseKind]);
            MinionRadius=EliteArray(MinionRadius,d=>MinionRadius[d.baseKind]*1.25f);
            MinionHead=EliteArray(MinionHead,d=>1.65f);
            KeepMin=EliteArray(KeepMin,d=>0f);KeepMax=EliteArray(KeepMax,d=>0f);
            MinionReach=EliteArray(MinionReach,d=>2.6f);
            MinionWindup=EliteArray(MinionWindup,d=>1.5f);
            MinionXp=EliteArray(MinionXp,d=>MinionXp[d.baseKind]*4);
            MinionNames=GalleryArray(MinionNames,k=>TrapGallery.Names[k-48]);
            MinionHp=GalleryArray(MinionHp,k=>new[]{45,120,80,90,40}[k-48]);
            MinionSpeed=GalleryArray(MinionSpeed,k=>.8f);MinionRadius=GalleryArray(MinionRadius,k=>k==52?.3f:.42f);
            MinionHead=GalleryArray(MinionHead,k=>k==52?1.2f:1.7f);KeepMin=GalleryArray(KeepMin,k=>0f);KeepMax=GalleryArray(KeepMax,k=>0f);
            MinionReach=GalleryArray(MinionReach,k=>3f);MinionWindup=GalleryArray(MinionWindup,k=>3.5f);MinionXp=GalleryArray(MinionXp,k=>35);
        }
        static T[] EliteArray<T>(T[] old,Func<EliteDef,T> value)
        {var a=new T[48];Array.Copy(old,a,old.Length);foreach(var d in EliteCatalog.All)a[d.kind]=value(d);return a;}

        public bool eliteEncounters;
        public sealed class EliteTelegraph
        {public int slot;public float until,duration;public Vector3 center,forward;public bool striking;public readonly float[] ready=new float[2];public int next;}
        readonly Dictionary<Fighter,EliteTelegraph> eliteStates=new Dictionary<Fighter,EliteTelegraph>();
        readonly List<Fighter> eliteCleanup=new List<Fighter>();
        sealed class EliteWard {public float until,original;}
        readonly Dictionary<Fighter,EliteWard> eliteWards=new Dictionary<Fighter,EliteWard>();
        public event Action<Fighter,SkillDef,Vector3,Vector3,float> OnEliteWarn;
        public EliteTelegraph EliteView(Fighter m)=>eliteStates.TryGetValue(m,out var s)?s:null;
        EliteTelegraph EliteState(Fighter m)
        {if(!eliteStates.TryGetValue(m,out var s)){s=new EliteTelegraph();eliteStates.Add(m,s);}return s;}
        void CleanupElites()
        {
            eliteCleanup.Clear();foreach(var p in eliteStates)if(!p.Key.Alive||!minions.Contains(p.Key))eliteCleanup.Add(p.Key);
            foreach(var m in eliteCleanup)eliteStates.Remove(m);
            eliteCleanup.Clear();foreach(var p in eliteWards)if(now>=p.Value.until||!p.Key.Alive){p.Key.damageTakenMul=p.Value.original;eliteCleanup.Add(p.Key);}
            foreach(var m in eliteCleanup)eliteWards.Remove(m);
        }
        bool EliteOrRareAlive()
        {foreach(var m in minions)if(m.Alive&&(EliteCatalog.IsElite(m.minionKind)||IsSpecial(m.minionKind)))return true;return false;}
        /// <summary>故事關：這一關要出菁英怪的那一波（−1＝這關不出）、出過了沒（一關最多一隻）</summary>
        int eliteWaveAt=-1;bool eliteDone;
        /// <summary>故事關開打時擲一次（使用者要求：普通難度每關 15% 出一隻菁英怪）：中的話在這關隨機一波（有 Boss 的最後一波以外）出現</summary>
        public bool RollStoryElite(float chance,int firstWave,int lastWave)
        {
            eliteWaveAt=-1;eliteDone=false;
            if(chance<=0f||rng.NextDouble()>=chance)return false;
            int last=lastWave>firstWave&&lastWave%BossEvery==0?lastWave-1:lastWave;
            eliteWaveAt=rng.Next(firstWave,Mathf.Max(firstWave,last)+1);
            return true;
        }
        public const float StoryEliteChance=0.15f;
        int EliteSpawnKind(int original,int ordinal,bool forced)
        {
            if(Gallery)return original;
            if(eliteWaveAt>0)
            {
                // 故事關（擲中的那一關）：那一波的第 2 隻（之後）換成這一章的菁英怪；稀有怪、菁英怪活著時先等下一隻
                if(forced||holdSpawns||eliteDone||wave!=eliteWaveAt||ordinal<1||EliteOrRareAlive())return original;
                eliteDone=true;
                return 23+Mathf.Clamp(Theme,0,4)*5+rng.Next(5);
            }
            if(forced||holdSpawns||specialKind>=0||EliteOrRareAlive())return original;
            bool tower=eliteWaves&&eliteLeft>0;
            int local=(wave-1)%10+1;
            if(!tower&&(!(eliteEncounters||FixedDungeonSpawns)||local<3||ordinal!=5||(IsBossWave&&!bossPending)))return original;
            if(tower)eliteLeft=0;
            int role = FixedDungeonSpawns ? (wave/10+local-1)%5 : tower ? ((wave-1)/50+Theme)%5 : rng.Next(5);
            return 23+Mathf.Clamp(Theme,0,4)*5+role;
        }
        void TickElite(Fighter m,Fighter target,float distance,float dt,int hold)
        {
            var def=EliteCatalog.Get(m.minionKind);var s=EliteState(m);
            if(hold>0){m.windupUntil=0;s.until=0;m.attackReadyAt=Mathf.Max(m.attackReadyAt,now+.6f);if(hold==1)return;}
            if(s.until>0)
            {
                m.forward=s.forward;
                if(now<s.until)return;
                s.until=0;m.windupUntil=0;
                var skill=Skills.All[EliteCatalog.SkillId(def.kind,s.slot)];
                s.ready[s.slot]=now+skill.cooldown;s.next=1-s.slot;m.attackReadyAt=now+1.8f;
                EliteStrike(m,skill,s.center,s.forward,Mathf.RoundToInt(skill.damage*m.dmgMul),false);
                return;
            }
            if(m.snaredUntil>now||now<m.attackReadyAt)return;
            int slot=s.ready[s.next]<=now?s.next:1-s.next;
            if(hold==0&&s.ready[slot]<=now&&distance<=2.6f&&CanAttackFrom(m.Feet))
            {
                var move=def.moves[slot];s.slot=slot;s.duration=EliteCatalog.Windup(move);s.until=now+s.duration;
                s.center=move==EliteMove.Burst?target.Feet:m.Feet;s.center.y=0;
                s.forward=Fighter.Flat(target.Feet-m.Feet);if(s.forward==Vector3.zero)s.forward=Vector3.forward;
                m.lockFwd=m.forward=s.forward;m.windupUntil=s.until;
                var skill=Skills.All[EliteCatalog.SkillId(def.kind,slot)];
                Emit("specialwarn",s.center,def.color,def.name+"："+EliteCatalog.Hint(move));
                OnEliteWarn?.Invoke(m,skill,s.center,s.forward,s.duration);
                return;
            }
            float speed=minionSpeed*MinionSpeed[m.minionKind]*m.speedMul*EnemySpeedScale;
            var delta=Fighter.Flat(target.Feet-m.Feet)*Mathf.Min(speed*dt,Mathf.Max(0,distance-1.5f));
            var p=m.Feet+delta;
            if(minionInside==null||minionInside(p)||!minionInside(m.Feet))m.head+=delta;
        }
        public void RemoteEliteWarn(int id,string skillId,Vector3 center,Vector3 forward,float remaining)
        {
            if(!remoteEnemy||!EliteCatalog.TrySkill(skillId,out var def,out int slot)||remaining<=0)return;
            var m=minions.Find(x=>x.minionId==id);
            if(m==null)m=SpawnMinion(center,id,def.kind);
            var s=EliteState(m);bool fresh=s.until<=now||s.slot!=slot;
            s.slot=slot;s.duration=EliteCatalog.Windup(def.moves[slot]);s.until=now+Mathf.Clamp(remaining,.01f,s.duration);
            s.center=center;s.forward=forward;m.lockFwd=m.forward=forward;m.windupUntil=s.until;
            if(fresh)Emit("specialwarn",center,def.color,def.name+"："+EliteCatalog.Hint(def.moves[slot]));
        }
        static bool EliteInside(EliteMove move,Vector3 center,Vector3 forward,Fighter v)
        {
            var to=v.Feet-center;to.y=0;float radius=EliteCatalog.Radius(move);
            if(move==EliteMove.Line){float along=Vector3.Dot(to,forward);var side=to-forward*along;return along>=0&&along<=radius&&side.magnitude<=.28f+v.bodyRadius;}
            if(to.magnitude>radius)return false;
            return move!=EliteMove.Sweep||Vector3.Dot(to,forward)>=0;
        }
        void EliteStrike(Fighter m,SkillDef skill,Vector3 center,Vector3 forward,int damage,bool remote)
        {
            if(!m.Alive||!EliteCatalog.TrySkill(skill.id,out var def,out int slot))return;
            var move=def.moves[slot];var state=EliteState(m);state.until=0;m.windupUntil=0;state.striking=true;
            Emit("minionattack",center,def.color,null);
            if(!remote&&coopHost)OnSpecialStrike?.Invoke(m,skill,center,forward,damage);
            if(move==EliteMove.Quake)
            {shockwaves.Add(new Shockwave{owner=m,skill=skill,center=center,born=now,speed=skill.speed,maxR=skill.rangeMax,dmg=damage});return;}
            if(move==EliteMove.Summon)
            {
                if(!remote)timers.Add(new KeyValuePair<float,Action>(now+.01f,()=>{
                    if(!m.Alive||stageCleared||minions.Count>=24)return;
                    var at=m.Feet+new Vector3(.8f,0,0);if(minionInside!=null&&!minionInside(at))at=m.Feet;
                    SpawnMinion(at,0,def.baseKind,HpMul,DmgMul,SpeedMul);
                }));
                return;
            }
            if(move==EliteMove.Heal||move==EliteMove.Shield)
            {
                if(remote)return;
                foreach(var other in minions)
                {
                    if(!other.Alive||EliteCatalog.IsElite(other.minionKind)||IsSpecial(other.minionKind)||FlatDistance(center,other.Feet)>2)continue;
                    if(move==EliteMove.Heal)other.hp=Mathf.Min(other.maxHp,other.hp+other.maxHp*.12f);
                    else if(eliteWards.TryGetValue(other,out var ward))ward.until=now+4;
                    else {eliteWards.Add(other,new EliteWard{until=now+4,original=other.damageTakenMul});other.damageTakenMul*=.7f;}
                }
                return;
            }
            var foes=remote?new List<Fighter>{player}:FoesOf(m);
            foreach(var v in foes)
            {
                if(v==null||!v.Alive||(!remote&&coopHost&&v.isAlly)||!EliteInside(move,center,forward,v))continue;
                if(move==EliteMove.Sweep&&v.crouching){v.evadeBoostUntil=now+3;Emit("evade",v.Chest,def.color,"蹲下閃過！");continue;}
                HitFoe(m,v,skill,damage,skill.effect,skill.effectDps,skill.effectDur);
            }
        }
    }
}

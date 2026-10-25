#if UNITY_EDITOR
namespace SpellDuel.EditorTools {
using System;using System.Reflection;using System.Collections.Generic;using UnityEngine;using SpellDuel;
public static class EliteCombatAudit {
static BindingFlags P=BindingFlags.NonPublic|BindingFlags.Instance;
static object Call(Battle b,string n,params object[] a)=>typeof(Battle).GetMethod(n,P).Invoke(b,a);
static void Check(bool v,string m){if(!v)throw new Exception(m);}
static Battle Make(bool remote=false){var p=new Fighter("p",Skills.Classes["mage"],new string[0],true);p.head=new Vector3(0,1.7f,1.2f);p.maxHp=10000;p.hp=10000;var e=new Fighter("e",Skills.Classes["boss"],new string[0],false);return new Battle(p,e){remoteEnemy=remote};}
public static void Run(){int count=0;foreach(var d in EliteCatalog.All)for(int slot=0;slot<2;slot++){
 var b=Make();var m=b.SpawnMinion(Vector3.zero,1,d.kind);var st=(Battle.EliteTelegraph)Call(b,"EliteState",m);st.next=slot;b.now=2;int warns=0;b.OnEliteWarn+=(a,c,e,f,g)=>warns++;
 Call(b,"TickElite",m,b.player,1.2f,.016f,0);Check(warns==1&&st.until>=1,"host warn "+d.kind);float until=st.until;Call(b,"TickElite",m,b.player,1.2f,.016f,0);Check(st.until==until,"windup reset");
 b.now=until+.01f;Call(b,"TickElite",m,b.player,1.2f,.016f,0);Check(st.ready[slot]>b.now,"cooldown");
 var skill=Skills.All[EliteCatalog.SkillId(d.kind,slot)];var remote=Make(true);remote.RemoteEliteWarn(1,skill.id,st.center,st.forward,skill.charge);var rm=remote.minions[0];Check(remote.EliteView(rm).slot==slot,"remote slot");remote.RemoteSpecialStrike(1,skill.id,st.center,st.forward,20);
 var move=d.moves[slot];if(move==EliteMove.Line||move==EliteMove.Sweep||move==EliteMove.Burst)Check(remote.player.hp<10000,"remote damage "+d.kind+" "+move);
 if(move==EliteMove.Quake){remote.now=.55f;Call(remote,"UpdateShockwaves");Check(remote.player.hp<10000,"remote quake");var jump=Make(true);jump.player.airborneUntil=3;jump.RemoteSpecialStrike(1,skill.id,Vector3.zero,Vector3.forward,20);jump.now=.55f;Call(jump,"UpdateShockwaves");Check(jump.player.hp==10000,"jump failed");}
 if(move==EliteMove.Sweep){var duck=Make(true);duck.player.crouching=true;duck.RemoteSpecialStrike(1,skill.id,Vector3.zero,Vector3.forward,20);Check(duck.player.hp==10000,"duck failed");}
 var far=Make(true);far.player.head=new Vector3(0,1.7f,3.1f);far.RemoteSpecialStrike(1,skill.id,Vector3.zero,Vector3.forward,20);far.now=1.3f;Call(far,"UpdateShockwaves");Check(far.player.hp==10000,"range leaked");
 Call(b,"HitFoe",b.player,m,Skills.All["fire"],1000000,EffectKind.None,0f,0f);Check(b.killLog.Contains("m"+d.kind),"soul kill key missing");count++;
 }Debug.Log("[SpellDuel] Elite combat audit passed: "+count+" host/client skill paths, cooldowns, jump/crouch/range and soul kill keys");}
}
}
#endif

#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.Reflection;using System.IO;using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static class RoomCoopAudit
 {
  const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
  static object Call(Battle b,string method,params object[] args)=>typeof(Battle).GetMethod(method,F).Invoke(b,args);
  static T Field<T>(Battle b,string name)=>(T)typeof(Battle).GetField(name,F).GetValue(b);
  static void Set(Battle b,string name,object value)=>typeof(Battle).GetField(name,F).SetValue(b,value);
  static int checks;
  static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception("Room coop: "+reason);}
  static Battle Make(int players,string kind)
  {
   var p=new Fighter("host",Skills.Classes["mage"],new string[0],true){pid=0,head=new Vector3(0,1.7f,0)};
   p.maxHp=10000;p.hp=10000;
   var b=new Battle(p,new Fighter("boss",Skills.Classes["boss_clockwork"],new string[0],false));
   for(int i=1;i<players;i++)b.allies.Add(new Fighter("peer"+i,Skills.Classes["mage"],new string[0],false){isAlly=true,pid=i,head=new Vector3(i*2,1.7f,0)});
   b.arenaPoly=new List<Vector3>{new Vector3(-4,0,-4),new Vector3(4,0,-4),new Vector3(4,0,4),new Vector3(-4,0,4)};
   b.edgePoint=()=>new Vector3(3,0,3);b.StartDungeonMap();b.wave=5;b.now=10;b.waveStartAt=10;
   b.StartRoomEvent(kind,new Dictionary<string,string>{{"riddle","海邊的高塔，夜晚替船照亮回家的路。"},{"answer","燈塔"}});
   Field<List<float>>(b,"evQueue").Clear();return b;
  }
  static void Hit(Battle b,Fighter target,int element)
  {
   Set(b,"incoming",new SkillDef{id=element==1?"fire":element==2?"ice":"bolt"});Call(b,"EvPropHit",target,b.player);Set(b,"incoming",null);
  }
  public static void Run()
  {
   checks=0;
   foreach(int n in new[]{1,2,3})
   {
    var b=Make(n,"defend");var guards=b.npcs.FindAll(x=>x.npcId.StartsWith("ev_core"));Check(guards.Count==(n==1?1:2),"guard count "+n);
    guards[guards.Count-1].hp=0;Call(b,"UpdateRoomEvent");Check(b.stageFailed,"either guard fails "+n);
    b=Make(n,"hunt");var targets=Field<List<Fighter>>(b,"evLinked");Check(targets.Count==n,"hunt target count");
    if(n>1)
    {
     targets[0].hp=0;Call(b,"UpdateRoomEvent");b.now+=5.01f;Call(b,"UpdateRoomEvent");Check(targets[0].Alive&&b.evFails==1,"hunt late revival");
     targets[0].hp=0;Call(b,"UpdateRoomEvent");b.now+=4.99f;
    }
    foreach(var f in targets)f.hp=0;Call(b,"UpdateRoomEvent");Check(b.roomEvent==null,"hunt simultaneous success");
    b=Make(n,"totems");targets=Field<List<Fighter>>(b,"evLinked");Check(targets.Count==n+2,"totem count");
    if(n>1){targets[0].hp=0;Call(b,"UpdateRoomEvent");b.now+=5.01f;Call(b,"UpdateRoomEvent");Check(targets[0].Alive&&b.evFails==1,"totem revival");}
    foreach(var f in targets)f.hp=0;b.minions.Clear();Field<List<float>>(b,"evQueue").Clear();Call(b,"UpdateRoomEvent");Check(b.roomEvent==null,"totem completion");
    b=Make(n,"elements");Check(b.evPts.Count==(n==1?6:7),"dual statue count");
    if(n>1)
    {
     var dual=b.npcs.Find(x=>x.evElement==4);Hit(b,dual,1);b.now+=3.01f;Hit(b,dual,2);Check(b.evState[6]==0,"late second attribute does not complete");
     b.now+=.1f;Hit(b,dual,2);Check(b.evState[6]==0,"same attribute does not complete");
     Hit(b,dual,1);Check(b.evState[6]==1,"two distinct attributes within window");
    }
    var statues=b.npcs.FindAll(x=>x.evElement>=1&&x.evElement<=3);foreach(var st in statues)Hit(b,st,st.evElement);Check(b.roomEvent==null,"all statues complete");
    b=Make(n,"elements");var wrong=b.npcs.Find(x=>x.evElement==1);Hit(b,wrong,3);Check(b.evPhase==2&&b.evFails==1&&Field<List<float>>(b,"evQueue").Count>=3,"wrong element punishment");
    b=Make(n,"runes");int[] sequence=Field<int[]>(b,"evSeq");float begin=b.evPhaseAt;
    for(int i=0;i<sequence.Length;i++)
    {
     b.now=begin+.9f*i+.1f;Call(b,"TickRunes");int visible=0;for(int pid=0;pid<n;pid++)if(b.RuneVisibleFor(pid))visible++;
     Check(visible==1,"one rune viewer step "+i);if(n>1)Check(b.evRuneOwner==i%n,"rotating rune owner");
    }
    b.now=begin+.9f*sequence.Length+.1f;Call(b,"TickRunes");
    for(int i=0;i<sequence.Length;i++){b.player.head=b.evPts[sequence[i]]+Vector3.up*1.7f;Call(b,"TickRunes");}Check(b.roomEvent==null,"rune completion");
    b=Make(n,"runes");sequence=Field<int[]>(b,"evSeq");b.now=b.evPhaseAt+.9f*sequence.Length+.1f;Call(b,"TickRunes");b.player.head=b.evPts[(sequence[0]+1)%b.evPts.Count]+Vector3.up*1.7f;Call(b,"TickRunes");Check(b.evFails==1&&b.evPhase==2,"rune wrong punishment");
    b=Make(n,"chant");if(n>1){string joined="";for(int i=0;i<n;i++){joined+=Battle.SplitClue(b.evRiddle,i,n);Check(!b.RoomTextFor(i).Contains(b.evRiddle),"whole riddle leaked to UI");}Check(joined==b.evRiddle,"split clue loses text");}
    Check(!b.EvChant("火山")&&b.evPhase==2,"wrong chant punishment");b.minions.Clear();Field<List<float>>(b,"evQueue").Clear();Call(b,"TickPenaltyOnly");Check(b.evPhase==1,"chant penalty recovery");Check(b.EvChant("燈塔")&&b.roomEvent==null,"chant success");
    b=Make(n,"mimic");for(int i=0;i<n*2;i++){b.now+=6;Call(b,"TickMimic");Check(b.evGlintOwner==(n==1?-1:i%n),"rotating chest viewer");}
    var fake=b.npcs.Find(x=>!x.evReal);Call(b,"EvPropHit",fake,b.player);Check(b.evFails==1&&b.evPhase==2&&b.minions.Count>0,"false chest penalty");
    var real=b.npcs.Find(x=>x.evReal);Call(b,"EvPropHit",real,b.player);Check(b.roomEvent==null,"real chest completion");
    b=Make(n,"escort");var walker=Field<Fighter>(b,"evWalker");var blocks=Field<List<Fighter>>(b,"evBlocks");Check(blocks.Count==(n==1?0:2),"escort blockers count");
    if(n>1){walker.head=blocks[0].head;var pos=walker.head;Call(b,"EscortWalk",walker,.5f);Check(walker.head==pos,"walker passed live barrier");blocks[0].hp=0;Call(b,"EscortWalk",walker,.5f);Check(walker.head!=pos,"walker remains stuck after barrier death");}
    walker.walkPath=null;Call(b,"UpdateRoomEvent");Check(b.roomEvent==null,"escort success");
    b=Make(n,"escort");Field<Fighter>(b,"evWalker").hp=0;Call(b,"UpdateRoomEvent");Check(b.stageFailed,"escort death failure");
    b=Make(n,"dark");Check(!b.EventLitByTeam(new Vector3(20,0,20),2.5f),"unlit remote monster");if(n>1)Check(b.EventLitByTeam(b.allies[0].Feet+Vector3.right*2,2.5f),"ally light not shared");
    b=Make(n,"collapse");if(n>1){var point=b.evPts[b.evPts.Count/2];foreach(var pl in b.AllPlayers)pl.head=point+Vector3.up*1.7f;b.now=30;Call(b,"TickCollapse",.016f);int warned=b.evState.IndexOf(1);Check(warned>=0&&Battle.FlatDistance(b.evPts[warned],point)<1.3f,"collapse ignores cluster");}
    b=Make(n,"collapse");var cell=b.evPts[0];b.evState[0]=2;b.player.head=cell+Vector3.up*1.7f;float hp=b.player.hp;b.now=11;Call(b,"TickCollapse",.1f);Check(b.player.hp<hp,"collapsed cell must damage");
    b=Make(n,"elements");var proxy=new Battle(new Fighter("peer",Skills.Classes["mage"],new string[0],true),new Fighter("boss",Skills.Classes["boss_clockwork"],new string[0],false)){remoteEnemy=true};proxy.SetRoomViewPlayers(string.Join(",",b.evPlayerIds),n>1?1:-1);proxy.evPhase=0;Check(proxy.RoomCoop==(n>1),"remote roster metadata");if(n>1){Check(!proxy.RuneVisibleFor(0)&&proxy.RuneVisibleFor(1),"remote rune owner mask");}
    b=Make(n,"runes");b.remoteEnemy=true;var states=string.Join(",",b.evState);b.now+=20;Call(b,"UpdateRoomEvent");Check(states==string.Join(",",b.evState),"peer ran authority");
    b=Make(n,"mimic");b.remoteEnemy=true;int forwarded=0;b.OnMinionHit+=(id,damage,skill)=>forwarded++;var remoteChest=b.npcs[0];Set(b,"incoming",new SkillDef{id="ice"});Call(b,"EvPropHit",remoteChest,b.player);Check(forwarded==1&&b.evFails==0&&b.roomEvent=="mimic","peer chest evaluated locally");
    b.CleanupRoomEvent();Check(!b.RoomCoop&&b.evPlayerIds.Count==0,"room cleanup retains clue owners");
   }
   Directory.CreateDirectory("snapshots");File.WriteAllText("snapshots/room_coop_audit.json","{\"checks\":"+checks+",\"partySizes\":[1,2,3],\"passed\":true}");
   Debug.Log("[SpellDuel] Room coop audit passed: "+checks);
  }
 }
}
#endif


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
   b.edgePoint=()=>new Vector3(3,0,3);b.StartDungeonMap();b.awaitingNext=false;b.wave=5;b.now=10;b.waveStartAt=10;
   b.StartRoomEvent(kind,new Dictionary<string,string>{{"riddle","海邊的高塔，夜晚替船照亮回家的路。"},{"answer","燈塔"}});
   Field<List<float>>(b,"evQueue").Clear();return b;
  }
  static void Hit(Battle b,Fighter target,int element)
  {
   Set(b,"incoming",new SkillDef{id=element==1?"fire":element==2?"ice":"bolt"});Call(b,"EvPropHit",target,b.player);Set(b,"incoming",null);
  }
  static void Face(Battle b,Fighter target)
  {b.player.head=target.head-Vector3.forward*.6f;b.player.forward=Vector3.forward;b.enemy.head=new Vector3(30,1.7f,30);}
  static void AttackContact(Battle b,Fighter target,SkillDef skill,string route)
  {
   Face(b,target);
   if(route=="sweep")Call(b,"MeleeSweep",b.player,skill,50);
   else if(route=="slash")Call(b,"SlashStrike",b.player,skill,b.player.Chest,Vector3.forward,Vector3.forward,2f,false);
   else if(route=="stab")Call(b,"PlayerStab",b.player,skill,Vector3.forward);
   else if(route=="direct")Call(b,"HitFoe",b.player,target,skill,50,EffectKind.None,0f,0f);
   else if(route=="ambush"){b.player.armed=skill;b.player.armedUntil=b.now+2;Call(b,"UpdateAmbush",b.player);}
   else
   {
    b.burns.Add(new BurnZone{owner=b.player,skill=skill,pos=target.Feet,radius=.1f,until=b.now+3,dps=50});
    Call(b,"UpdateBurns",1.01f);
   }
  }
  static void ContactAudit(int players)
  {
   var sword=Skills.All["na_sword"];
   foreach(string room in new[]{"totems","escort"})
   {
    if(room=="escort"&&players==1)continue;
    foreach(string route in new[]{"sweep","slash","stab","direct","ambush","burn"})
    {
     var host=Make(players,room);var target=host.npcs.Find(x=>x.npcSide==1);float hp=target.hp;
     // Isolate the one target while exercising real selection / attack / host-receive paths.
     host.npcs.RemoveAll(x=>x!=target);host.minions.Clear();AttackContact(host,target,sword,route);
     Check(target.hp<hp,"host contact "+room+"/"+route+"/"+players);
     var peer=Make(players,room);var proxy=peer.npcs.Find(x=>x.npcId==target.npcId);peer.npcs.RemoveAll(x=>x!=proxy);peer.minions.Clear();peer.remoteEnemy=true;
     int sent=0,id=0,damage=0;SkillDef received=null;float peerHp=proxy.hp;
     peer.OnMinionHit+=(i,d,k)=>{sent++;id=i;damage=d;received=k;};AttackContact(peer,proxy,sword,route);
     Check(sent==1&&id==proxy.minionId&&id==target.minionId&&received==sword&&damage>0,"single valid peer contact "+room+"/"+route);
     Check(proxy.hp==peerHp,"peer mutated prop health "+route);
     hp=target.hp;host.ApplyHitOnMinion(id,damage,received);Check(target.hp<hp,"host applies peer contact "+route);
    }
   }
   var b=Make(players,"defend");var guard=b.npcs[0];Face(b,guard);float guardHp=guard.hp;
   var foes=(List<Fighter>)Call(b,"MeleeFoes",b.player);Check(!foes.Contains(guard),"friendly guard selected");
   Call(b,"MeleeSweep",b.player,sword,50);Check(guard.hp==guardHp,"friendly guard damaged");
   b=Make(players,"totems");var prop=b.npcs[0];var npc=b.SpawnNpc("ordinary_enemy","ordinary","priest",1,false,100,0,new Vector3(2,0,2));
   var dead=b.npcs[1];dead.hp=0;foes=(List<Fighter>)Call(b,"MeleeFoes",b.player);
   Check(foes.Contains(prop)&&!foes.Contains(dead)&&!foes.Contains(npc),"prop eligibility changed other NPC rules");
   Check(!((List<Fighter>)Call(b,"MeleeFoes",b.enemy)).Contains(prop)&&!((List<Fighter>)Call(b,"MeleeFoes",npc)).Contains(prop),"enemy acquired allied prop");
   // Existing minion path still forwards once; ordinary NPC behavior remains local.
   b.remoteEnemy=true;var minion=b.SpawnMinion(new Vector3(1,0,1),0,0,1,1,1);int forwarded=0;b.OnMinionHit+=(i,d,k)=>forwarded++;
   float before=minion.hp;Call(b,"HitFoe",b.player,minion,sword,30,EffectKind.None,0f,0f);Check(forwarded==1&&minion.hp==before,"existing minion forwarding regressed");
   before=npc.hp;Call(b,"HitFoe",b.player,npc,sword,30,EffectKind.None,0f,0f);Check(forwarded==1&&npc.hp<before,"ordinary NPC behavior changed");
   b=Make(players,"totems");b.remoteEnemy=true;prop=b.npcs[0];prop.dots.Add(new Fighter.Dot{dps=5,until=b.now+3});before=prop.hp;forwarded=0;
   b.OnMinionHit+=(i,d,k)=>forwarded++;AttackContact(b,prop,Skills.All["plague"],"direct");
   Check(forwarded==1&&prop.hp==before&&prop.dots.Count==1,"peer plague detonated locally");
   var plagueHost=Make(players,"totems");var plagueTarget=plagueHost.npcs[0];plagueTarget.dots.Add(new Fighter.Dot{dps=5,until=plagueHost.now+3});before=plagueTarget.hp;
   plagueHost.ApplyHitOnMinion(plagueTarget.minionId,10,Skills.All["plague"]);Check(plagueTarget.hp<before-10&&plagueTarget.dots.Count==0,"host plague not applied");
   foreach(string room in new[]{"elements","mimic"})
   {
    var host=Make(players,room);var peer=Make(players,room);peer.remoteEnemy=true;var target=host.npcs[0];var proxy=peer.npcs[0];
    host.npcs.RemoveAll(x=>x!=target);peer.npcs.RemoveAll(x=>x!=proxy);proxy.damageTakenMul=1;int sent=0;peer.OnMinionHit+=(i,d,k)=>sent++;
    float hp=target.hp,proxyHp=proxy.hp;string state=string.Join(",",host.evState);
    AttackContact(host,target,Skills.All["fire"],"burn");AttackContact(peer,proxy,Skills.All["fire"],"burn");
    Check(target.hp==hp&&host.evFails==0&&host.roomEvent==room&&state==string.Join(",",host.evState),"host burn puzzle semantics");
    Check(sent==0&&proxy.hp==proxyHp&&peer.evFails==0&&peer.roomEvent==room,"peer burn created puzzle answer");
   }
   // Real peer -> mhit host entry: element validation and chest penalties only on host.
   foreach(string room in new[]{"elements","mimic"})foreach(bool correct in new[]{false,true})
   {
    var host=Make(players,room);var peer=Make(players,room);peer.remoteEnemy=true;
    var target=host.npcs.Find(x=>room=="elements"?x.evElement==1:x.evReal==correct);
    var proxy=peer.npcs.Find(x=>x.npcId==target.npcId);var skill=Skills.All[correct?"fire":"ice"];int sent=0,amount=0;SkillDef incoming=null;
    peer.OnMinionHit+=(i,d,k)=>{Check(i==proxy.minionId,"puzzle contact ID");sent++;amount=d;incoming=k;};float hp=proxy.hp;
    AttackContact(peer,proxy,skill,"direct");Check(sent==1&&peer.evFails==0&&peer.roomEvent==room&&proxy.hp==hp,"peer resolved puzzle locally");
    host.ApplyHitOnMinion(target.minionId,amount,incoming);
    if(!correct)Check(host.evFails==1&&host.evPhase==2,"host puzzle penalty missing");
    else Check(room=="elements"?host.evState[host.evPts.FindIndex(p=>Battle.FlatDistance(p,target.Feet)<.1f)]==1:host.roomEvent==null,"host puzzle success missing "+room);
   }
  }
  public static void Run()
  {
   checks=0;
   foreach(int n in new[]{1,2,3})
   {
    ContactAudit(n);
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
    foreach(string linked in new[]{"totems","hunt"})if(n>1)
    {
     b=Make(n,linked);var group=Field<List<Fighter>>(b,"evLinked");var dead=group[0];int oldId=dead.minionId;dead.hp=0;b.Update(.02f);
     Check(!b.npcs.Contains(dead)&&!b.minions.Contains(dead),"normal frame death cleanup");b.Update(5.02f);
     Check(group[0].Alive&&group[0].minionId!=oldId,"real frame revive after actor removal");
    }
    foreach(string ordinary in new[]{"defend","dark","collapse"})
    {
     b=Make(n,ordinary);b.waveToSpawn=0;b.minions.Clear();b.Update(.02f);Check(b.awaitingNext||b.stageCleared,"ordinary room clear "+ordinary);
    }
    b=Make(n,"mimic");b.remoteEnemy=true;int forwarded=0;b.OnMinionHit+=(id,damage,skill)=>forwarded++;var remoteChest=b.npcs[0];Set(b,"incoming",new SkillDef{id="ice"});Call(b,"EvPropHit",remoteChest,b.player);Check(forwarded==1&&b.evFails==0&&b.roomEvent=="mimic","peer chest evaluated locally");
    b.CleanupRoomEvent();Check(!b.RoomCoop&&b.evPlayerIds.Count==0,"room cleanup retains clue owners");
   }
   Directory.CreateDirectory("snapshots");File.WriteAllText("snapshots/room_coop_audit.json","{\"checks\":"+checks+",\"partySizes\":[1,2,3],\"passed\":true}");
   Debug.Log("[SpellDuel] Room coop audit passed: "+checks);
  }
 }
}
#endif


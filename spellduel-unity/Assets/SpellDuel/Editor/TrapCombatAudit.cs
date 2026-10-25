#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel.EditorTools {
public static class TrapCombatAudit {
static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
static object Call(Battle b,string name,params object[] args)=>typeof(Battle).GetMethod(name,Flags).Invoke(b,args);
static void Check(bool ok,string message){if(!ok)throw new Exception("Trap audit: "+message);}
static Battle Make(bool remote=false){var p=new Fighter("p",Skills.Classes["mage"],new string[0],true);p.head=new Vector3(0,1.7f,0);p.hp=p.maxHp=10000;var e=new Fighter("e",Skills.Classes["boss_clockwork"],new string[0],false);var b=new Battle(p,e){remoteEnemy=remote};b.ConfigureGallery("furnace",1,1);return b;}
public static int Run(){int tests=0;
Check(Battle.MinionKinds==53&&Battle.MinionNames.Length==53,"kind table");
foreach(Battle.TrapKind kind in new[]{Battle.TrapKind.Rock,Battle.TrapKind.Flame,Battle.TrapKind.Arrows,Battle.TrapKind.Log,Battle.TrapKind.Bolt,Battle.TrapKind.Smash,Battle.TrapKind.Low,Battle.TrapKind.High,Battle.TrapKind.Dive,Battle.TrapKind.Volley})
for(int mode=0;mode<4;mode++){
var b=Make(mode%2==1);b.now=10;var c=new Battle.TrapCue{id=1,epoch=1,kind=kind,hit=10,duration=1,radius=3,halfX=2,halfZ=2,mask=15,forward=Vector3.forward,power=.22f};
Vector3 p=kind==Battle.TrapKind.Arrows?new Vector3(-1.7f,0,-3):kind==Battle.TrapKind.Log?new Vector3(0,0,-3):Vector3.forward*.1f;
b.player.head=p+Vector3.up*1.7f;
if(mode==2){b.player.airborneUntil=20;b.player.crouching=true;}if(mode==3)b.player.head=new Vector3(20,1.7f,20);
Call(b,"GalleryHit",c,b.player,null);
bool avoid=mode==3||mode==2&&(kind==Battle.TrapKind.Flame||kind==Battle.TrapKind.Log||kind==Battle.TrapKind.Low||kind==Battle.TrapKind.High);
Check(avoid?b.player.hp==10000:b.player.hp<10000,kind+" player/dodge/range mode "+mode);
float hp=b.player.hp;Call(b,"GalleryHit",c,b.player,null);Check(hp==b.player.hp,"duplicate contact");
var npc=new Fighter("npc",Skills.Classes["mage"],new string[0],false){head=p+Vector3.up*1.7f};float nhp=npc.hp;Call(b,"GalleryHit",c,npc,null);Check(npc.hp==nhp,"environment touched NPC");tests++;
}
var host=Make();host.now=1;
for(int k=48;k<=52;k++){host.now=1+(k-48)*3;var m=host.SpawnMinion(Vector3.forward,1,k);m.attackReadyAt=0;Call(host,"TickGalleryMinion",m,host.player,1f,.016f,0);Check(host.trapCues.Count==k-47,"new enemy attack missing "+k);tests++;}
for(int i=0;i<host.trapCues.Count;i++){var c=host.trapCues[i];Check(c.warn>=3.5f,"warning duration");if(i>0){var prev=host.trapCues[i-1];Check(c.hit-prev.hit-prev.duration>=1.99f,"action overlap");}}
foreach(var p in host.flamePillars)Check(Vector3.Distance(p,host.player.Feet)>=1.5f,"pillar spawn distance");
var peer=Make(true);var cue=new Battle.TrapCue{id=1,epoch=2,kind=Battle.TrapKind.Rock};peer.RemoteTrap(cue,8);Check(peer.trapCues.Count==0,"stale room packet");cue.epoch=1;peer.RemoteTrap(cue,8);Check(peer.trapCues.Count==1&&peer.trapCues[0].hit==8,"remote schedule");peer.RemoteTrap(cue,12);Check(peer.trapCues.Count==1&&peer.trapCues[0].hit==12,"reschedule duplicate");
var reduce=Make();reduce.now=10;reduce.player.trapReduce=.2f;var hit=new Battle.TrapCue{kind=Battle.TrapKind.Rock,mask=15,halfX=2,halfZ=2,hit=10,power=.22f};Call(reduce,"GalleryHit",hit,reduce.player,null);Check(Math.Abs(reduce.player.hp-8240)<1,"trap reduction applied twice/wrong");
Debug.Log("[SpellDuel] Trap combat audit passed: "+tests+" contact/avoidance/new enemy cases, schedule spacing, local-only damage and room epochs");return tests;
}
}}
#endif

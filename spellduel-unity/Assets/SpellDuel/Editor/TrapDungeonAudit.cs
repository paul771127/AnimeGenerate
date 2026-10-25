#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using UnityEngine;
namespace SpellDuel.EditorTools {
public static class TrapDungeonAudit {
[Serializable] public class Report {public string unity;public int combatCases,paths,lootSamples,mythic,setPieces;public bool clock,save,oldSave,exclusive,optionalEscort,lever,hostClientRoutes;}
static void Check(bool v,string m){if(!v)throw new Exception("Gallery integration: "+m);}
public static Report Run(){var report=new Report{unity=Application.unityVersion,combatCases=TrapCombatAudit.Run()};Dungeons.Parse(Resources.Load<TextAsset>("dungeons").text);Check(Dungeons.errors.Count==0,string.Join(";",Dungeons.errors));var d=Dungeons.All.Find(x=>x.id==TrapGallery.Id);Check(d!=null&&d.timeLimit==25&&d.traps&&d.unlock=="4-6","header");
Action<DungeonRoom,List<DungeonRoom>> walk=null;walk=(node,path)=>{var copy=new List<DungeonRoom>(path){node};var exits=d.ExitsOf(node);if(exits.Count==0){int fights=0;foreach(var n in copy)if(!n.IsChest)fights++;Check(fights==10&&node.IsBoss,"path fight count");
foreach(int option in new[]{0,1,2}){var h=new DungeonRun(d);var c=new DungeonRun(d);for(int i=1;i<copy.Count;i++){h.Enter(copy[i]);c.Enter(copy[i]);if(h.pendingChoice!=null){h.Choose(option);c.Choose(option);}h.Cleared();c.Cleared();h.OnKills(40,new System.Random(3));c.OnKills(40,new System.Random(3));if(copy[i].IsChest){h.OnChest();c.OnChest();}}
Check(h.AtEnd&&c.AtEnd&&!h.failed&&!c.failed&&h.path.Count==c.path.Count,"host/client full route");Check(h.lastChoiceResult==c.lastChoiceResult,"lever replication");}
report.paths++;return;}foreach(var e in exits)walk(e,copy);};walk(d.rooms[0],new List<DungeonRoom>());Check(report.paths==2,"fork");report.hostClientRoutes=true;
var run=new DungeonRun(d);run.Enter(d.Node("cells"));run.NpcDied("wheat");Check(!run.failed,"optional escort failed dungeon");report.optionalEscort=true;
foreach(int option in new[]{0,1,2}){var le=new DungeonRun(d);le.Enter(d.Node("antechamber"));Check(le.pendingChoice!=null,"lever not blocking start");le.Choose(option);Check(le.lastChoiceId=="oldsmith"&&le.lastChoiceResult==d.Npc("oldsmith").options[option].result,"lever choice");}report.lever=true;
var clock=new GalleryClock(100,25);Check(clock.Remaining(100)==1500&&clock.Remaining(1500)==100,"clock elapsed through menus");Check(clock.Wind(500)&&!clock.Wind(600)&&clock.deadline==1720,"one wind per team");Check(clock.Expired(1720)&&!clock.Wind(1720),"expiry");report.clock=true;
var myth=ItemGen.Make(new System.Random(21),45,5,"mage");var leg=ItemGen.Make(new System.Random(21),45,4,"mage");Check(myth.mods.Count==leg.mods.Count+1&&Math.Abs(myth.mods[0].Value-leg.mods[0].Value*1.2f)<.001f,"mythic stats");Check(myth.soulSlots<=4&&myth.rarity==5,"socket cap");var saved=Item.Parse(myth.Serialize());Check(saved!=null&&saved.rarity==5&&saved.mods.Count==myth.mods.Count,"mythic roundtrip");report.save=true;
var old=Item.Parse(string.Join("|",leg.Serialize().Split('|'),0,10));Check(old!=null&&old.rarity==4&&old.soulSlots<=4,"old save");report.oldSave=true;
var rng=new System.Random(998);report.lootSamples=10000;for(int i=0;i<report.lootSamples;i++){var it=TrapGallery.Treasure(rng,"mage");if(it.rarity==5)report.mythic++;if(it.setId=="clockwork")report.setPieces++;Check(it.ilvl==45&&it.rarity>=4,"final guarantee");Check(ItemGen.RollRarity(rng,1)<5,"regular mythic leak");Check(ItemGen.Make(rng,45,4,"mage").setId!="clockwork","exclusive set leak");}
Check(report.mythic>220&&report.mythic<380&&report.setPieces>2800&&report.setPieces<3200,"loot distribution");report.exclusive=true;
File.WriteAllText("snapshots/trap_dungeon_audit.json",JsonUtility.ToJson(report,true));Debug.Log("[SpellDuel] Gallery integration audit passed: 2 routes / 3 choices, clock, saves, 10000 loot samples");return report;
}
}}
#endif

#if UNITY_EDITOR
using System;using UnityEngine;
namespace SpellDuel.EditorTools {
public static partial class Snapshot {
static void TrapDungeonShots(Camera cam){TrapDungeonAudit.Run();
foreach(var k in new[]{Battle.TrapKind.Rock,Battle.TrapKind.Flame,Battle.TrapKind.Arrows,Battle.TrapKind.Log}){
var cue=new Battle.TrapCue{id=1,epoch=1,kind=k,center=Vector3.zero,forward=Vector3.forward,hit=5,duration=k==Battle.TrapKind.Flame?1.5f:1.2f,halfX=2,halfZ=2,radius=2,mask=8,count=4};var fx=GalleryTrapFx.Create(cue);GameObject pillar=null;if(k==Battle.TrapKind.Flame)pillar=GalleryTrapFx.Pillar(Vector3.zero);
fx.Tick(4.4f);foreach(var l in fx.GetComponentsInChildren<LineRenderer>())if(l.widthMultiplier>.3f)throw new Exception("trap line lost its authored width");Shot(cam,fx.gameObject,"trap_"+k+"_warn",new Vector3(6,6,7),new Vector3(0,.7f,0),900,800);fx.Tick(5+cue.duration*.45f);Shot(cam,fx.gameObject,"trap_"+k+"_impact",new Vector3(6,6,7),new Vector3(0,.7f,0),900,800);UnityEngine.Object.DestroyImmediate(fx.gameObject);if(pillar)UnityEngine.Object.DestroyImmediate(pillar);
}
for(int k=48;k<=53;k++){if(k<=52){var rig=MinionRig.Create();rig.SetVariant(k);rig.Windup(3.5f);if(!EliteAudit.VertexColorPath(rig.gameObject))throw new Exception("minion rig wrapper colors "+k);UnityEngine.Object.DestroyImmediate(rig.gameObject);}var art=MechanicalArt.Create(k);art.ExternalTick=true;for(int phase=1;phase<=(k==53?3:1);phase++){art.SetPhase(phase);art.Wind=.5f;art.Tick(.2f);if(!EliteAudit.VertexColorPath(art.gameObject))throw new Exception("mechanical vertex colors "+k);if(EliteAudit.Triangles(art.gameObject)>20000)throw new Exception("mechanical triangle budget "+k);float scale=k==53?2:1;Shot(cam,art.gameObject,"vault_enemy_"+k+"_phase"+phase,new Vector3(4,2.8f,7)*scale,new Vector3(0,1,0)*scale,800,900);}UnityEngine.Object.DestroyImmediate(art.gameObject);}
var card=new GameObject("Procedural mythic item card");var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.transform.SetParent(card.transform,false);panel.transform.localPosition=new Vector3(0,1.6f,-.1f);panel.transform.localScale=new Vector3(2.8f,3.2f,.05f);panel.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Sprites/Default")){color=new Color(.08f,.1f,.16f)};
var border=new[]{new Vector3(-1.35f,.08f,.03f),new Vector3(-1.35f,3.12f,.03f),new Vector3(1.35f,3.12f,.03f),new Vector3(1.35f,.08f,.03f)};SpellFx.Line(card.transform,"Mythic red frame",border,true,.025f,Item.RarityColors[5],false,false);
var item=ItemGen.Make(new System.Random(21),45,5,"mage");item.setId="clockwork";
string[] lines={"MYTHIC / CLOCKWORK","ITEM LEVEL 45","MAIN "+item.mods[0].Key+" "+item.mods[0].Value.ToString("0.##"), "AFFIXES "+item.mods.Count+" / SOCKETS "+item.soulSlots, "LEGENDARY MAIN x 1.20"};for(int i=0;i<lines.Length;i++){var t=new GameObject("Card label").AddComponent<TextMesh>();t.transform.SetParent(card.transform,false);t.transform.localPosition=new Vector3(0,2.8f-i*.47f,.01f);t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=48;t.characterSize=.035f;t.color=i==0?Item.RarityColors[item.rarity]:Color.white;t.text=lines[i];t.transform.localRotation=Quaternion.Euler(0,180,0);}
Shot(cam,card,"vault_mythic_card",new Vector3(0,1.6f,7),new Vector3(0,1.6f,0),768,960);UnityEngine.Object.DestroyImmediate(card);
}
}}
#endif

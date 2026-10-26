using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel {
public partial class SoloBattle {
readonly Dictionary<int,string> allyArtGear=new Dictionary<int,string>();
void RefreshAllyArtGear(CharacterRig rig,int pid){
 string gear=peerInfo.TryGetValue(pid,out var p)?p.gear:null;
 var art=AllyPresentation.For(rig);int key=rig.GetInstanceID();
 if(!allyArtGear.TryGetValue(key,out var old)||old!=gear){art.Refresh();allyArtGear[key]=gear;}
}
void DrawAllyLabels(Camera view,float width,float height,int fs){
 if(!view||battle==null)return;var used=new List<Rect>();
 foreach(var a in battle.allies){
  if(!allyRigs.TryGetValue(a.pid,out var rig)||!rig)continue;var art=AllyPresentation.For(rig);if(art.Removing)continue;
  var point=view.WorldToScreenPoint(art.LabelAnchor);if(point.z<=0||point.x<0||point.x>width||point.y<0||point.y>height)continue;
  var rect=AllyPresentation.PlaceLabel(point,width,height,fs,used);float w=rect.width;
  UiKit.Card(rect,.72f,a.Alive?a.cls.color:Color.gray,.5f);
  UiKit.Text(new Rect(rect.x,rect.y,rect.width,fs*1.2f),(a.Alive?"":"倒地 · ")+a.name,Mathf.RoundToInt(fs*.8f),a.Alive?Color.white:Color.gray,TextAnchor.MiddleCenter,true);
  UiKit.Bar(new Rect(rect.x+w*.12f,rect.yMax-fs*.42f,w*.76f,fs*.28f),a.Alive?a.hp/Mathf.Max(1,a.maxHp):0,a.Alive?a.cls.color:Color.gray,"",1);
 }
}
}}

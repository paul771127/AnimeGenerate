using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel {
public partial class WorldUI {
static readonly string[] AlchemistIds={"alchemist_tanyi","alchemist_qingluo","alchemist_xi","alchemist_sailin","alchemist_tuoling"};
readonly System.Random craftArtRandom=new System.Random();
static readonly Dictionary<string,Dictionary<string,List<string>>> craftArtLines=new Dictionary<string,Dictionary<string,List<string>>>();
string craftArtLine,craftArtExpr="normal",craftArtKey,craftArtProduct;float craftArtStart=-100;Color craftArtColor;
void ResetCraftArt(){craftArtLine=null;craftArtExpr="normal";craftArtStart=-100;craftArtKey=null;}
void DrawAlchemy(Rect r,CharacterProfile prof,int fs,int fsS,int fsL,float gap){if(!DrawCraftFacility("alchemist",prof,fs,fsS,fsL,gap))DrawAlchemyBody(r,prof,fs,fsS,fsL,gap);}
void DrawCraft(Rect r,CharacterProfile prof,int fs,int fsS,int fsL,float gap){if(!DrawCraftFacility("crafter",prof,fs,fsS,fsL,gap))DrawCraftBody(r,prof,fs,fsS,fsL,gap);}
bool DrawCraftFacility(string key,CharacterProfile prof,int fs,int fsS,int fsL,float gap){
 var bg=FacilityBg(key);if(!bg)return false;
 if(craftArtKey!=key){craftArtKey=key;craftArtLine=null;craftArtExpr="normal";CraftArtTalk("greeting");}
 bool alchemy=key=="alchemist";var c=alchemy?new Color(.72f,.45f,1):UiKit.Gold;int t=Mathf.Clamp(town,0,4);
 Facility2DFrame(bg,alchemy?"⚗ 藥劑師・"+Towns[town].name:"🛠 巡迴工匠",c,alchemy?AlchemistIds[t]:"crafter_laochui",craftArtExpr,alchemy?AlchemistNames[t]:"工匠・老錘",craftArtLine??"慢慢看，合用的才值得做。",fs,fsS,fsL,false);
 frame2D=true;try{var left=Design(30,190,610,1535);if(alchemy)DrawAlchemyBody(left,prof,fs,fsS,fsL,gap);else DrawCraftBody(left,prof,fs,fsS,fsL,gap);}finally{frame2D=false;}
 DrawCraftAnimation();return true;
}
void CraftArtTalk(string group){
 string key=craftArtKey??(screen==Screen.Alchemy?"alchemist":"crafter");
 if(!craftArtLines.TryGetValue(key,out var groups)){groups=new Dictionary<string,List<string>>();var txt=Resources.Load<TextAsset>("Village2D/lines_"+key);string g=null;foreach(var raw in (txt?txt.text:"").Split('\n')){var l=raw.Trim();if(l.StartsWith("[")&&l.EndsWith("]")){g=l.Substring(1,l.Length-2);groups[g]=new List<string>();}else if(l.Length>0&&g!=null)groups[g].Add(l);}craftArtLines[key]=groups;}
 if(groups.TryGetValue(group,out var lines)&&lines.Count>0)craftArtLine=lines[craftArtRandom.Next(lines.Count)];
 craftArtExpr=group=="shortage"?"serious":group=="greeting"?"normal":"smile";
}
void CraftArtDone(bool ok,string potion,Item item){
 if(!ok){CraftArtTalk("shortage");return;}
 bool quality=item!=null?item.rarity>=3:craftArtRandom.Next(5)==0;CraftArtTalk(quality?"quality":"complete");
 craftArtStart=Time.unscaledTime;craftArtProduct=potion??"mat_soul_smith";craftArtColor=item!=null?Item.RarityColors[Mathf.Clamp(item.rarity,0,Item.RarityColors.Length-1)]:new Color(.72f,.45f,1);
}
void DrawCraftAnimation(){
 float elapsed=Time.unscaledTime-craftArtStart;if(elapsed<0||elapsed>1.65f)return;
 var a=Design(710,1435,290,255);var old=GUI.color;float u=Mathf.Clamp01(elapsed);bool potion=craftArtProduct!=null&&craftArtProduct.StartsWith("bp_");
 if(potion){UiKit.MenuCard(new Rect(a.x,a.y+a.height*.48f,a.width,a.height*.45f),.9f,Color.Lerp(new Color(.2f,.3f,.4f),craftArtColor,u));for(int i=0;i<7;i++){float p=Mathf.Repeat(elapsed*.9f+i*.14f,1);var col=craftArtColor;col.a=(1-p)*(elapsed<1?1:Mathf.Clamp01((1.4f-elapsed)*2));SupplyArt.Disc(new Rect(a.x+a.width*(.12f+(i%4)*.2f),a.y+a.height*(.55f-p*.5f),a.width*.1f,a.width*.1f),col);}}
 else{float pulse=Mathf.Sin(Mathf.Clamp01(elapsed/1.5f)*Mathf.PI);SupplyArt.Disc(new Rect(a.x-a.width*pulse*.2f,a.y,a.width*(1+pulse*.4f),a.width*(1+pulse*.4f)),new Color(craftArtColor.r,craftArtColor.g,craftArtColor.b,pulse*.3f));}
 if(!potion||elapsed>=.95f){float p=Mathf.Clamp01((elapsed-.95f)/.35f);var icon=new Rect(a.center.x-a.width*.21f,a.y+a.height*(.5f-(potion?p*.38f:0)),a.width*.42f,a.width*.42f);GUI.color=new Color(1,1,1,Mathf.Clamp01((1.65f-elapsed)*3));SupplyArt.Draw(icon,craftArtProduct);}
 GUI.color=old;
}
static void DrawCraftNeeds(Rect r,(string mat,int n)[] need,int fs){float w=r.width/Mathf.Max(1,need.Length);for(int i=0;i<need.Length;i++){var n=need[i];string id=n.mat.StartsWith("mat_")?n.mat:"mat_"+n.mat;float size=Mathf.Min(r.height,fs*1.3f);bool drawn=SupplyArt.Draw(new Rect(r.x+w*i,r.y,size,size),id);var m=Crafting.GetMat(id);string label=(drawn?"":m?.icon??"")+(m?.name??n.mat)+" "+Crafting.Have(n.mat)+"/"+n.n;UiKit.Text(new Rect(r.x+w*i+size,r.y,w-size,r.height),label,fs,Crafting.Have(n.mat)>=n.n?new Color(.6f,1,.65f):new Color(1,.6f,.55f),TextAnchor.MiddleLeft,false,.5f);}}
static void DrawCraftMats(Rect r,int fs){int i=0;float row=fs*2.2f,w=r.width/2;foreach(var m in Crafting.Mats){int count=Economy.Count(m.id);if(count<=0)continue;var cell=new Rect(r.x+(i%2)*w,r.y+(i/2)*row,w,row);bool drawn=SupplyArt.Draw(new Rect(cell.x,cell.y,fs*1.7f,fs*1.7f),m.id);UiKit.Text(new Rect(cell.x+fs*1.8f,cell.y,w-fs*1.8f,row),(drawn?"":m.icon)+m.name+" ×"+count,fs,Color.white);i++;}if(i==0)UiKit.Text(r,"還沒有材料：從冒險中收集，再帶來製作。",fs,UiKit.Muted,TextAnchor.UpperLeft);}
}}

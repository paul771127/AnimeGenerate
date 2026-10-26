using UnityEngine;
namespace SpellDuel {
public partial class CharacterPanel {
bool suppliesOpen;Vector2 suppliesScroll;
void DrawSuppliesBag(Rect r,int fs){
 UiKit.Card(r,.9f,UiKit.Gold);var list=new System.Collections.Generic.List<(string id,string name,int count)>();
 foreach(var p in Crafting.Potions){int n=Economy.Count(p.id);if(n>0)list.Add((p.id,p.name,n));}
 foreach(var m in Crafting.Mats){int n=Economy.Count(m.id);if(n>0)list.Add((m.id,(SkillIcons.IsSupply(m.id)?"":m.icon)+m.name,n));}
 var view=new Rect(r.x+fs*.3f,r.y+fs*.4f,r.width-fs*.6f,r.height-fs*.8f);float row=fs*3,w=view.width/2;
 var content=new Rect(0,0,view.width-4,Mathf.Max(view.height,((list.Count+1)/2)*row));suppliesScroll=UiKit.DragScroll(view,suppliesScroll,content);suppliesScroll=GUI.BeginScrollView(view,suppliesScroll,content,false,false,GUIStyle.none,GUIStyle.none);
 for(int i=0;i<list.Count;i++){var a=list[i];float x=(i%2)*w,y=(i/2)*row;SupplyArt.Draw(new Rect(x,y,fs*2.1f,fs*2.1f),a.id);UiKit.Text(new Rect(x+fs*2.2f,y,w-fs*2.2f,fs*2.2f),a.name+"\n×"+a.count,fs,Color.white,TextAnchor.MiddleLeft);}
 if(list.Count==0)UiKit.Text(content,"素材袋目前是空的。",fs,UiKit.Muted,TextAnchor.UpperCenter);GUI.EndScrollView();
}
}}

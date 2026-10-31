using UnityEngine;
namespace SpellDuel {
public partial class WorldUI {
int worldBossNotice=-1;
static readonly string[] worldBossArtIds={"wb_furnace","wb_canopy","wb_leviathan"};
static readonly string[] worldBossArtNames={"晨鐘熔爐泰坦","記名林冠龍","白帆淵鯨"};
static readonly string[] worldBossArtNotes={"山腹深處再度傳來鐘鳴。公會正追查古代熔爐的行蹤。","記名林上方出現移動的樹冠。請將目擊消息留給公會。","白帆航路傳來低沉鯨歌。燈塔守望者正在確認海面異象。"};
static readonly Texture2D[] worldBossPosters=new Texture2D[3];
static Texture2D WorldBossPoster(int i){if(!worldBossPosters[i])worldBossPosters[i]=Resources.Load<Texture2D>("WorldBossNotices/"+worldBossArtIds[i]);return worldBossPosters[i];}
void DrawWorldBossMapMarks(float scale,int fs){
 for(int i=0;i<3;i++){
  if(!wmNodes.TryGetValue("town:"+i,out var t)||!WMNodeOpen(t))continue;
  var pos=new Vector2(t.pos.x,270)*scale;float size=Mathf.Max(fs*2.2f,132*scale);
  var hit=new Rect(pos.x-size*.5f,pos.y-size*.5f,size,size);GUI.DrawTexture(hit,SkillIcons.Get("worldboss_notice"),ScaleMode.ScaleToFit,true);
  UiKit.Text(new Rect(pos.x-fs*3.5f,hit.yMax,fs*7,fs*1.2f),"世界 Boss 出沒",Mathf.RoundToInt(fs*.8f),UiKit.Gold,TextAnchor.MiddleCenter,true);
  if(!UiLock.Blocking&&GUI.Button(hit,GUIContent.none,GUIStyle.none))worldBossNotice=i;
 }
}
void DrawWorldBossBountyCards(Rect r,int fs,int fsS,float gap,ref float y){
 y+=fs*2.7f+gap;
 UiKit.Text(new Rect(r.x+gap,y,r.width-gap*2,fs*1.1f),"世界異象調查 · 點海報閱覽",fsS,UiKit.Gold);y+=fs*1.3f;
 float w=(r.width-gap*4)/3,h=fs*5.7f;
 for(int i=0;i<3;i++){
  var card=new Rect(r.x+gap+i*(w+gap),y,w,h);bool clicked=UiKit.Button(card,"",fsS,false,UiKit.Gold,!UiLock.Blocking);
  var tex=WorldBossPoster(i);if(tex)GUI.DrawTexture(new Rect(card.x+3,card.y+3,card.width-6,card.height-fs*1.4f),tex,ScaleMode.ScaleToFit);
  UiKit.Text(new Rect(card.x,card.yMax-fs*1.5f,card.width,fs*1.4f),worldBossArtNames[i],Mathf.RoundToInt(fsS*.78f),Color.white,TextAnchor.MiddleCenter,true);
  if(clicked)worldBossNotice=i;
 }
}
void DrawWorldBossPoster(int fs,int fsS){
 float w=UnityEngine.Screen.width,h=UnityEngine.Screen.height;var safe=UnityEngine.Screen.safeArea;float top=h-safe.yMax,bottom=h-safe.y;
 UiKit.MenuCard(new Rect(0,0,w,h),1,new Color(.10f,.14f,.23f));float pad=w*.045f;
 UiKit.Text(new Rect(pad,top+pad,w-pad*2,fs*1.6f),"世界 Boss 出沒",fs,UiKit.Gold,TextAnchor.MiddleCenter,true);
 float ph=Mathf.Min((bottom-top)*.74f,(w-pad*2)*1.5f),pw=ph*2/3;var art=new Rect((w-pw)*.5f,top+pad+fs*1.8f,pw,ph);var tex=WorldBossPoster(worldBossNotice);if(tex)GUI.DrawTexture(art,tex,ScaleMode.ScaleToFit);
 UiKit.Text(new Rect(art.x+pw*.12f,art.y+ph*.075f,pw*.76f,ph*.065f),worldBossArtNames[worldBossNotice],fs, new Color(.15f,.19f,.25f),TextAnchor.MiddleCenter,true);
 UiKit.Text(new Rect(art.x+pw*.12f,art.y+ph*.825f,pw*.76f,ph*.085f),worldBossArtNotes[worldBossNotice],fsS,new Color(.15f,.19f,.25f),TextAnchor.MiddleCenter);
 UiKit.Text(new Rect(pad,art.yMax+pad*.25f,w-pad*2,fs*1.4f),"公會調查中 · 尚未開放討伐",fsS,UiKit.Muted,TextAnchor.MiddleCenter);
 if(UiKit.Button(new Rect(w*.25f,bottom-fs*2.4f-pad,w*.5f,fs*2.2f),"收起海報",fs,false,UiKit.Gold))worldBossNotice=-1;
}
}}

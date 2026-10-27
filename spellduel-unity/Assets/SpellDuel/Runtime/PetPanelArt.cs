using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 /// <summary>Presentation only. Preview textures are rendered once per species, never stored as imported assets.</summary>
 public static class PetPanelArt
 {
  static readonly Dictionary<int,Texture2D> portraits=new Dictionary<int,Texture2D>();static GUIStyle measure;
  public static float TextHeight(string text,int font,float width){if(measure==null)measure=new GUIStyle(GUI.skin.label){wordWrap=true};measure.fontSize=font;return measure.CalcHeight(new GUIContent(text),Mathf.Max(40,width));}
  public static Texture2D Portrait(int kind)
  {
   if(portraits.TryGetValue(kind,out var cached)&&cached)return cached;
   var rig=MinionRig.Create();rig.SetVariant(kind);PetDress.Apply(rig,EliteCatalog.IsElite(kind));
   var nodes=rig.GetComponentsInChildren<Transform>(true);foreach(var t in nodes)t.gameObject.layer=31;
   // Isolate the preview camera without modifying the AR camera, lights or global scene state.
   rig.transform.position=new Vector3(0,-200,0);
   var go=new GameObject("Pet portrait camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,0);camera.orthographic=true;
   var bounds=new Bounds(rig.transform.position+Vector3.up*.7f,Vector3.one*.2f);foreach(var f in rig.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(f.bounds);
   camera.orthographicSize=Mathf.Max(.6f,bounds.size.y*.63f,bounds.size.x*.65f);var target=bounds.center;camera.transform.position=target+new Vector3(1.8f,.7f,3);camera.transform.LookAt(target);camera.nearClipPlane=.05f;camera.farClipPlane=20;
   var rt=RenderTexture.GetTemporary(192,192,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(192,192,TextureFormat.RGBA32,false){name="Pet preview "+kind,filterMode=FilterMode.Bilinear};tex.ReadPixels(new Rect(0,0,192,192),0,0);tex.Apply(false,true);portraits[kind]=tex;return tex;}
   finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);go.SetActive(false);rig.gameObject.SetActive(false);SkyArt.Dispose(go);SkyArt.Dispose(rig.gameObject);}
  }
  public static void Hero(Rect r,int kind,string name,string subtitle,int fs,Color color)
  {
   UiKit.Card(r,.88f,color,.65f);float size=r.height*.92f;
   if(Event.current.type==EventType.Repaint)GUI.DrawTexture(new Rect(r.x+4,r.y+4,size,size),Portrait(kind),ScaleMode.ScaleToFit,true);
   UiKit.Text(new Rect(r.x+size+8,r.y+8,r.width-size-16,r.height*.48f),name,fs,color,TextAnchor.MiddleLeft,true);
   UiKit.Text(new Rect(r.x+size+8,r.y+r.height*.5f,r.width-size-16,r.height*.4f),subtitle,Mathf.RoundToInt(fs*.8f),UiKit.Paper,TextAnchor.UpperLeft);
  }
  public static bool ItemButton(Rect r,string icon,string label,int fs,Color color,bool enabled)
  {
   bool clicked=UiKit.Button(r,"",fs,false,color,enabled);float size=Mathf.Min(r.width*.30f,r.height*.62f);
   bool stacked=r.width<fs*10; if(stacked)size=Mathf.Min(r.height*.35f,r.width*.4f);
   Rect iconRect=stacked?new Rect(r.center.x-size*.5f,r.y+4,size,size):new Rect(r.x+5,r.y+(r.height-size)*.5f,size,size);
   Rect textRect=stacked?new Rect(r.x+5,r.y+size+7,r.width-10,r.height-size-11):new Rect(r.x+size+10,r.y+4,r.width-size-15,r.height-8);
   Color old=GUI.color;GUI.color=enabled?Color.white:new Color(1,1,1,.4f);GUI.DrawTexture(iconRect,SkillIcons.Get(icon),ScaleMode.ScaleToFit,true);GUI.color=old;
   UiKit.Text(textRect,label,fs,enabled?UiKit.Paper:UiKit.Muted,TextAnchor.MiddleLeft);
   return clicked;
  }
 }
}

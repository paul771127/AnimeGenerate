using UnityEngine;
namespace SpellDuel {
// Presentation only: never writes Fighter, peer state, equipment or HP.
internal sealed class AllyPresentation:MonoBehaviour {
Renderer[] renderers;Transform head;float headTop=.20f,gray,opacity=1,revive;bool wasAlive=true,dirty=true;MaterialPropertyBlock block;
internal bool Removing{get;private set;}
internal float Opacity=>opacity;
internal Vector3 LabelAnchor=>head?head.TransformPoint(new Vector3(0,headTop,0))+Vector3.up*.14f:transform.position+Vector3.up*2.1f;
internal static AllyPresentation For(CharacterRig rig){var v=rig.GetComponent<AllyPresentation>();if(!v){v=rig.gameObject.AddComponent<AllyPresentation>();v.Refresh();}return v;}
internal static Rect PlaceLabel(Vector3 point,float width,float height,int fs,System.Collections.Generic.List<Rect> used){
 float w=Mathf.Min(width*.30f,fs*8f),h=fs*1.8f;var rect=new Rect(Mathf.Clamp(point.x-w*.5f,0,width-w),height-point.y-h,w,h);
 for(int pass=0;pass<6;pass++){bool overlap=false;foreach(var r in used)if(rect.Overlaps(r)){rect.y=r.y-h-fs*.2f;overlap=true;break;}if(!overlap)break;}
 used.Add(rect);return rect;
}
internal void Refresh(){
 dirty=true;renderers=GetComponentsInChildren<Renderer>(true);if(block==null)block=new MaterialPropertyBlock();
 if(!head)foreach(var t in GetComponentsInChildren<Transform>())if(t.name=="Head"){head=t;break;}
 if(head){headTop=-10;foreach(var f in head.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices)headTop=Mathf.Max(headTop,head.InverseTransformPoint(f.transform.TransformPoint(v)).y);}
}
internal void State(bool alive){if(alive!=wasAlive){dirty=true;if(alive)revive=.8f;}wasAlive=alive;gray=alive?0:.85f;}
internal void BeginRemoval(){if(Removing)return;Removing=true;var rig=GetComponent<CharacterRig>();if(rig)rig.enabled=false;}
void LateUpdate(){Step(Time.unscaledDeltaTime);}
internal void Step(float dt){
 if(!dirty&&!Removing&&revive<=0)return;dirty=false;
 if(Removing)opacity=Mathf.Max(0,opacity-dt/.65f);revive=Mathf.Max(0,revive-dt);
 if(renderers==null)Refresh();
 foreach(var r in renderers){if(!r||!r.sharedMaterial)continue;var m=r.sharedMaterial;r.GetPropertyBlock(block);
  if(m.HasProperty("_ArtFade")){block.SetFloat("_ArtFade",opacity);block.SetFloat("_ArtGray",gray);block.SetFloat("_ArtRevive",revive*.12f);}
  else if(m.HasProperty("_Color")){var c=m.color;float l=c.r*.299f+c.g*.587f+c.b*.114f;c=Color.Lerp(c,new Color(l,l,l,c.a),gray);c.a*=opacity;block.SetColor("_Color",c);}
  r.SetPropertyBlock(block);
 }
 if(Removing&&opacity<=0)Destroy(gameObject);
}
}}

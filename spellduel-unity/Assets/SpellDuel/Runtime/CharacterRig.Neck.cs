using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel {
public partial class CharacterRig {
static readonly Dictionary<string,Mesh> fittedNecks=new Dictionary<string,Mesh>(),raisedCrowns=new Dictionary<string,Mesh>(),collars=new Dictionary<string,Mesh>();
// Called once after the existing head fit. No per-frame mesh work or public API changes.
void FinishNeckAndCollar(){
 string id=advancedId??classId;
 // Store the accepted upper envelope in the actor frame before seating the skull.
 // Fitting uses the old reference haircut, so redesigning the silhouette cannot resize the face.
 MeshFilter skullFilter=null;foreach(var f in head.GetComponentsInChildren<MeshFilter>())if(f.GetComponent<Renderer>().sharedMaterial.HasProperty("_HumanFace")&&f.GetComponent<Renderer>().sharedMaterial.GetFloat("_HumanFace")==1){skullFilter=f;break;}
 var crownTargets=new Dictionary<MeshFilter,float>();
 foreach(var f in head.GetComponentsInChildren<MeshFilter>()){
  bool hair=f.gameObject.name=="整體髮型",hat=f.gameObject.name.StartsWith("Headwear fitted ")||f.gameObject.name=="Player default crown";
  if(!hair&&!hat)continue;
  var source=hair?AnimeFace.CompactPresentationMesh(AnimeHair.MeshFor("player_identity",facePersona),id+"/height reference",facePersona,1):f.sharedMesh;
  var matrix=transform.worldToLocalMatrix*f.transform.localToWorldMatrix;float target=-10;
  foreach(var p in source.vertices)target=Mathf.Max(target,matrix.MultiplyPoint3x4(p).y);crownTargets[f]=target;
 }
 head.localPosition-=Vector3.up*.016f;
 foreach(var pair in crownTargets){
  var f=pair.Key;string key=id+"/"+f.sharedMesh.GetInstanceID();
  if(!raisedCrowns.TryGetValue(key,out var mesh)){
   mesh=Object.Instantiate(f.sharedMesh);mesh.name="Seated crown "+id;var v=mesh.vertices;float top=mesh.bounds.max.y,delta=10;
   var matrix=transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
   // Solve the upper envelope exactly along actor up, rather than approximating
   // it along a tilted head's Y axis. Only the crown above the brows is reshaped.
   for(int i=0;i<v.Length;i++){
    float weight=Mathf.Clamp01((v[i].y-.040f)/Mathf.Max(.001f,top-.040f));
    if(weight>.001f)delta=Mathf.Min(delta,(pair.Value-matrix.MultiplyPoint3x4(v[i]).y)/weight);
   }
   var lift=f.transform.InverseTransformVector(transform.TransformVector(Vector3.up*delta));
   for(int i=0;i<v.Length;i++)v[i]+=lift*Mathf.Clamp01((v[i].y-.040f)/Mathf.Max(.001f,top-.040f));
   if(f.gameObject.name=="整體髮型"&&skullFilter!=null){
    var skull=skullFilter.sharedMesh.vertices;var skullToHair=f.transform.worldToLocalMatrix*skullFilter.transform.localToWorldMatrix;
    for(int i=0;i<skull.Length;i++)skull[i]=skullToHair.MultiplyPoint3x4(skull[i]);
    PlayerHair.SealFinalScalp(v,skull,skullFilter.sharedMesh.triangles);
   }
   mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();raisedCrowns[key]=mesh;
  }f.sharedMesh=mesh;
 }
 var skin=spine.Find("Player default neck");
 if(skin!=null){
  skin.SetParent(neck,false);skin.localPosition=Vector3.zero;skin.localRotation=Quaternion.identity;skin.localScale=Vector3.one;
  if(!fittedNecks.TryGetValue(id,out var mesh)){
   var top=neck.InverseTransformPoint(head.TransformPoint(new Vector3(0,-.080f,-.038f)));
   var bottom=neck.InverseTransformPoint(spine.TransformPoint(new Vector3(0,.525f,-.016f)));
   var path=new[]{top,Vector3.Lerp(top,bottom,.34f),Vector3.Lerp(top,bottom,.68f),bottom};
   var b=new OutfitArt.Builder{Skin=Skin};b.Tube(path,new[]{.032f,.038f,.048f,.060f},new[]{.039f,.043f,.050f,.056f},Skin,16);mesh=b.Finish("Anatomical fitted neck "+id);
   // UV is neck height: a soft submandibular shadow, not a different skin tone.
   var uv=new Vector2[mesh.vertexCount];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(.5f,1-(i/16)/3f);mesh.uv=uv;fittedNecks[id]=mesh;
  }
  skin.GetComponent<MeshFilter>().sharedMesh=mesh;skin.GetComponent<Renderer>().sharedMaterial=AnimeFace.Material(Skin,5);
 }
 if(!collars.TryGetValue(id,out var collar)){
  var b=new OutfitArt.Builder();var d=PlayerClassArt.Design(id);bool high=id=="mage"||id=="archmage"||id=="bishop"||id=="marksman"||id=="swordsman"||id=="paladin"||id=="priest"||id=="shadowdancer"||id=="assassin"||id=="summoner";bool vneck=id=="novice"||id=="archer"||id=="ranger"||id=="berserker"||id=="inquisitor";
  const int sides=24;int start=b.V.Count;
  // Four rings form outer wall, rolled top edge and visible inner lining.
  for(int ring=0;ring<4;ring++)for(int i=0;i<=sides;i++){
   float a=i*2*Mathf.PI/sides,front=Mathf.Max(0,Mathf.Cos(a));bool inner=ring>=2,upper=ring==1||ring==2;
   // Standing collars open at the throat like the 2D coats. The inner
   // wall and gold top edge remain visible from an elevated view.
   float y=upper?(high?.607f:.580f)-(high?front*front*.065f:vneck?front*front*.048f:front*.008f):.522f;
   float rx=inner?.061f:.068f,rz=inner?.064f:.071f;if(!upper){rx+=.013f;rz+=.013f;}
   b.V.Add(new Vector3(Mathf.Sin(a)*rx,y,Mathf.Cos(a)*rz-.016f));b.C.Add(ring==2?d.Accent:inner?PlayerClassArt.Inner(id):d.Main);
   if(ring>0&&i<sides){int n=start+ring*(sides+1)+i,p=n-(sides+1);b.T.AddRange(new[]{p,p+1,n,p+1,n+1,n});}
  }collar=b.Finish("Separate rolled collar "+id);collars[id]=collar;
 }
 AnimeFace.Part(spine,"Separate garment collar",collar,Vector3.zero,AnimeFace.Material(Color.white,4));
}
}}

using UnityEngine;
namespace SpellDuel
{
 public static class PetDress
 {
  public static void Apply(MinionRig rig,bool elite)
  {if(!rig)return;var view=rig.GetComponent<PetDressView>();if(!view)view=rig.gameObject.AddComponent<PetDressView>();view.Apply(rig,elite);}
 }
 public sealed class PetDressView : MonoBehaviour
 {
  MinionRig rig;Transform adorn,ground;bool elite;int kind=-1;
  public void Apply(MinionRig owner,bool isElite)
  {
   if(adorn&&rig==owner&&elite==isElite&&kind==owner.PetArtKind)return;
   SkyArt.Dispose(adorn?adorn.gameObject:null);SkyArt.Dispose(ground?ground.gameObject:null);rig=owner;elite=isElite;kind=owner.PetArtKind;
   var anchor=rig.PetArtAnchor;if(!anchor)return;
   adorn=SkyArt.Node(anchor,"Pet identity ribbon",Vector3.zero);
   // Bell / flower collars go around the stem or canopy edge, rather than across the face.
   float radius=elite?.25f:kind==17?.32f:kind==8?.27f:kind==16?.28f:kind==18?.26f:kind==13?.22f:kind==7||kind==10?.12f:.16f;
   float y=elite?-.21f:kind==17?-.08f:kind==11?-.12f:kind==7||kind==10?-.18f:-.12f;
   var band=SkyArt.Ring(adorn,"Turquoise woven band",radius,SkyArt.Teal,.045f);band.transform.localPosition=new Vector3(0,y,0);
   SkyArt.Oval(adorn,"Ivory identity clasp",new Vector3(radius*.8f,y,radius*.6f),new Vector3(.09f,.09f,.035f),SkyArt.Cream);
   for(int i=-1;i<=1;i+=2){var bow=SkyArt.Oval(adorn,"Ribbon loop",new Vector3(radius+i*.055f,y,0),new Vector3(.11f,.06f,.04f),SkyArt.Teal);bow.localRotation=Quaternion.Euler(0,0,i*25);SkyArt.Sail(adorn,"Ribbon tail",new Vector3(radius+i*.025f,y-.02f,0),new Vector3(.055f,.15f,1),SkyArt.Teal).localRotation=Quaternion.Euler(0,0,180);}
   if(elite)for(int side=-1;side<=1;side+=2)SkyArt.Oval(adorn,"Golden feather clasp",new Vector3(side*.05f,y,.25f),new Vector3(.06f,.16f,.04f),SkyArt.Gold,true).localRotation=Quaternion.Euler(0,0,side*30);
   ground=SkyArt.Node(rig.transform,"Friendly floor rings",new Vector3(0,.025f,0));SkyArt.Ring(ground,"Inner friend line",.34f,new Color(.2f,.85f,.72f,.6f),.018f);SkyArt.Ring(ground,"Outer friend line",.39f,new Color(.6f,1,.87f,.45f),.012f);
   if(elite)for(int j=0;j<8;j++){var line=SkyArt.Line(ground,"Broken gold arc",5,.018f,SkyArt.Gold);for(int i=0;i<5;i++){float a=(j*45+i*6)*Mathf.Deg2Rad;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.47f,0,Mathf.Sin(a)*.47f));}}
  }
  void LateUpdate(){if(!rig)return;if(kind!=rig.PetArtKind||!adorn){Apply(rig,elite);return;}if(ground){ground.gameObject.SetActive(!rig.PetArtDead);if(Application.isPlaying){var p=WorldFrame.ToWorld(rig.transform.position);p.y=.025f;ground.position=WorldFrame.FromWorld(p);ground.rotation=WorldFrame.Marker.rotation;}}}
 }
}

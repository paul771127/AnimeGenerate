using UnityEngine;
namespace SpellDuel
{
 internal static class EnemyShotKindArt
 {
  internal static bool Build(Transform root,string id)
  {
   if(!id.StartsWith("ek_"))return false;
   string slug=id.Substring(id.IndexOf('_',3)+1);var gold=new Color(1,.53f,.08f);var steel=new Color(.64f,.76f,.86f);
   switch(slug)
   {
    case "lowring":
     SpellFx.Line(root,"Rolling flame rim",SpellFx.Circle(32,.19f),true,.055f,gold,true,false);
     SpellFx.Line(root,"White hot wheel",SpellFx.Circle(32,.13f),true,.02f,new Color(1,.95f,.6f),true,false);
     for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);SpellFx.Line(root,"Flame tongues",new[]{p*.17f,p*.26f+Vector3.back*.04f,p*.20f+Vector3.back*.12f},false,.025f,gold,true,false);}break;
    case "rollstone":
     VariantGeometry.Oval(root,"Rolling basalt",Vector3.zero,Vector3.one*.34f,new Color(.45f,.37f,.28f));
     for(int i=0;i<3;i++){var l=SpellFx.Line(root,"Basalt seams",SpellFx.Circle(16,.173f),true,.008f,gold,false,false);l.transform.localRotation=Quaternion.Euler(i*55,i*35,0);}break;
    case "twinmark": Arrow(root,.38f,steel,new Color(.95f,.28f,.12f),false);break;
    case "podburst":
     VariantGeometry.Oval(root,"Split seed husk",new Vector3(0,0,-.05f),new Vector3(.12f,.12f,.22f),new Color(.35f,.63f,.12f));Arrow(root,.30f,new Color(.82f,.95f,.47f),new Color(.27f,.48f,.08f),true);break;
    case "platevolley":
     var plate=VariantGeometry.Blade(root,"Released back plate",new Vector3(0,0,-.14f),.38f,.24f,new Color(.73f,.46f,.23f));plate.localRotation=Quaternion.Euler(90,0,0);
     SpellFx.Line(root,"Plate fracture",new[]{new Vector3(0,.025f,-.1f),new Vector3(.04f,.025f,.04f),new Vector3(0,.025f,.19f)},false,.015f,gold,true,false);break;
    case "twinbolt": Arrow(root,.36f,new Color(.65f,.25f,.10f),gold,false);SpellFx.Line(root,"Molten bolt core",new[]{Vector3.back*.19f,Vector3.forward*.20f},false,.026f,gold,true,false);break;
    case "tripleshot": Arrow(root,.40f,steel,new Color(.62f,.10f,.18f),false);for(int i=0;i<3;i++){var l=SpellFx.Line(root,"Lock collar",SpellFx.Circle(12,.035f),true,.01f,gold,false,false);l.transform.localPosition=Vector3.back*(.05f+i*.04f);}break;
    case "crystalline":
     var gem=VariantGeometry.Blade(root,"Sand crystal lance",Vector3.back*.18f,.40f,.13f,new Color(1,.70f,.30f));gem.localRotation=Quaternion.Euler(90,0,0);
     SpellFx.Line(root,"Crystal lit spine",new[]{new Vector3(0,.016f,-.18f),new Vector3(0,.016f,.22f)},false,.015f,new Color(1,.97f,.69f),true,false);break;
    default:return false;
   }
   root.gameObject.AddComponent<EnemyShotKindMotion>().rolling=slug=="rollstone";return true;
  }
  static void Arrow(Transform root,float length,Color tip,Color tail,bool thorn)
  {
   var axis=VariantGeometry.Node(root,"Flight axis",Vector3.zero);axis.localRotation=Quaternion.Euler(90,0,0);
   VariantGeometry.Rod(axis,"Shaft",Vector3.down*length*.5f,Vector3.up*length*.45f,.025f,VariantGeometry.Metal);
   VariantGeometry.Blade(axis,thorn?"Barbed seed point":"Bolt head",Vector3.up*length*.2f,length*.4f,thorn?.085f:.055f,tip);
   for(int i=0;i<3;i++){var t=VariantGeometry.Blade(axis,"Fletching",Vector3.down*length*.45f,.10f,.07f,tail);t.localRotation=Quaternion.Euler(0,i*120,0);}
  }
 }
 internal sealed class EnemyShotKindMotion:MonoBehaviour
 {
  internal bool rolling;
  // Rotate visible children only: root forward and projectile travel remain caller-owned.
  void Update(){if(rolling)foreach(Transform t in transform)t.Rotate(Vector3.right,Time.deltaTime*180,Space.Self);}
 }
}

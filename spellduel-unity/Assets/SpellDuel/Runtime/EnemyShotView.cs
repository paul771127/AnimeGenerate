using UnityEngine;
namespace SpellDuel
{
 /// <summary>Local +Z is flight direction. Caller owns position, orientation and lifetime; no collision or movement.</summary>
 public sealed class EnemyShotView:MonoBehaviour
 {
  public static EnemyShotView Create(string skillId)
  {
   var v=new GameObject("Enemy projectile "+skillId).AddComponent<EnemyShotView>();v.Build(skillId??"");return v;
  }
  void Build(string id)
  {
   bool arrow=id.Contains("bolt")||id.Contains("arrow")||id.Contains("shortbow")||id.Contains("crossstep")||id.Contains("retreatshot");bool spear=id.Contains("javelin")||id.Contains("highcast");
   if((arrow&&!id.Contains("sandbolt"))||spear)
   {
    float length=spear?.78f:id.Contains("bolt")?.30f:.43f;var shaft=VariantGeometry.Node(transform,"Shaft axis",Vector3.zero);shaft.localRotation=Quaternion.Euler(90,0,0);
    VariantGeometry.Rod(shaft,spear?"Javelin shaft":"Arrow shaft",new Vector3(0,-length*.5f,0),new Vector3(0,length*.5f,0),spear?.025f:.014f,VariantGeometry.Gold);
    VariantGeometry.Blade(shaft,"Steel projectile point",new Vector3(0,length*.5f,0),spear?.18f:.08f,spear?.085f:.045f,VariantGeometry.Metal);
    for(int i=0;i<3;i++){var feather=VariantGeometry.Blade(shaft,"Red enemy fletching",new Vector3(0,-length*.45f,0),.09f,.06f,VariantGeometry.Red);feather.localRotation=Quaternion.Euler(0,i*120,0);}return;
   }
   Color c=id.Contains("ember")?new Color(1,.27f,.06f):id.Contains("water")?new Color(.20f,.53f,.76f):id.Contains("bubble")?new Color(.55f,.29f,.68f):new Color(.75f,.45f,.18f);
   bool water=id.Contains("water"),bubble=id.Contains("bubble");
   var core=VariantGeometry.Oval(transform,bubble?"Pearl bubble":water?"Conch water spindle":id.Contains("ember")?"Brazier ember":"Sand crystal",Vector3.zero,water?new Vector3(.09f,.09f,.34f):Vector3.one*(bubble?.20f:.13f),c);
   if(bubble){var mr=core.GetComponent<Renderer>();mr.sharedMaterial=VariantGeometry.Ghost;var b=new MaterialPropertyBlock();b.SetColor("_Color",new Color(c.r,c.g,c.b,.20f));mr.SetPropertyBlock(b);}
   for(int i=0;i<3;i++){var line=SpellFx.Line(transform,"Element trail",new[]{new Vector3(.045f*Mathf.Cos(i*2),.045f*Mathf.Sin(i*2),0),new Vector3(.08f*Mathf.Cos(i*2),.08f*Mathf.Sin(i*2),-.18f),new Vector3(0,0,-.30f)},false,.015f,c,true,false);}
   if(bubble)SpellFx.Line(transform,"Pearl rim",SpellFx.Circle(24,.1f),true,.015f,c,false,false);
  }
 }
}

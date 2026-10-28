using UnityEngine;
namespace SpellDuel
{
 public sealed partial class EnemySkillView
 {
  // Small source emblems sit inside the locked cue. Only the original boundary denotes damage.
  // Each named skill chooses a concrete readable motif, never another moving target marker.
  void Glyph(string name,Vector3 at,params Vector2[] points)
  {var p=new Vector3[points.Length];for(int i=0;i<p.Length;i++)p[i]=at+new Vector3(points[i].x,points[i].y,0);Line(name,p,false,.018f);}
  void Diamond(string name,Vector3 at,float s)
  {Glyph(name,at,new Vector2(0,-s),new Vector2(-s*.6f,0),new Vector2(0,s),new Vector2(s*.6f,0),new Vector2(0,-s));}
  void Flame(Vector3 p){Glyph("Rising flame",p,new Vector2(-.10f,0),new Vector2(-.12f,.10f),new Vector2(-.03f,.08f),new Vector2(0,.26f),new Vector2(.09f,.12f),new Vector2(.10f,0));}
  void Leaf(Vector3 p){Glyph("Leaf vein",p,new Vector2(-.12f,0),new Vector2(-.08f,.15f),new Vector2(.11f,.22f),new Vector2(.12f,.07f),new Vector2(-.12f,0),new Vector2(.11f,.22f));}
  void Bolt(Vector3 p){Glyph("Lightning fork",p,new Vector2(.07f,.25f),new Vector2(-.08f,.11f),new Vector2(.06f,.10f),new Vector2(-.06f,-.05f));}
  void Wave(Vector3 p){Glyph("Cresting water",p,new Vector2(-.16f,0),new Vector2(-.12f,.10f),new Vector2(-.04f,.14f),new Vector2(.02f,.07f),new Vector2(.12f,.07f),new Vector2(.17f,.13f));}
  void Crown(Vector3 p){Glyph("Crown teeth",p,new Vector2(-.14f,0),new Vector2(-.14f,.20f),new Vector2(-.06f,.10f),new Vector2(0,.25f),new Vector2(.06f,.10f),new Vector2(.14f,.20f),new Vector2(.14f,0),new Vector2(-.14f,0));}
  void Wing(Vector3 p){Glyph("Wing feather edge",p,new Vector2(-.2f,.12f),new Vector2(-.12f,-.03f),new Vector2(-.06f,.07f),new Vector2(0,0),new Vector2(.06f,.07f),new Vector2(.12f,-.03f),new Vector2(.2f,.12f));}
  void Cap(Vector3 p){Glyph("Mushroom cap",p,new Vector2(-.16f,0),new Vector2(-.1f,.15f),new Vector2(0,.21f),new Vector2(.1f,.15f),new Vector2(.16f,0),new Vector2(-.16f,0));Glyph("Mushroom stem",p,new Vector2(0,0),new Vector2(0,-.1f));}
  void Hourglass(Vector3 p){Glyph("Hourglass",p,new Vector2(-.12f,0),new Vector2(.12f,.25f),new Vector2(-.12f,.25f),new Vector2(.12f,0),new Vector2(-.12f,0));}
  void Claw(Vector3 p,int n){for(int i=0;i<n;i++)Glyph("Claw rake "+i,p+Vector3.right*(i-(n-1)*.5f)*.065f,new Vector2(-.025f,0),new Vector2(0,.1f),new Vector2(.015f,.23f));}
  void Marks(Vector3 p,int n,bool crystal){for(int i=0;i<n;i++){var q=p+Vector3.right*(i-(n-1)*.5f)*.14f;if(crystal)Diamond("Loaded plate "+i,q,.095f);else Glyph("Loaded bolt "+i,q,new Vector2(0,-.07f),new Vector2(0,.12f),new Vector2(-.035f,.07f),new Vector2(0,.12f),new Vector2(.035f,.07f));}}
  void BuildKindSignature(string id)
  {
   if(!id.StartsWith("ek_"))return;int end=id.IndexOf('_',3),kind;if(end<0||!int.TryParse(id.Substring(3,end-3),out kind))return;
   var p=new Vector3(0,shape==EsShape.Circle?.38f:.72f,shape==EsShape.Circle?0:Mathf.Min(radius*.18f,.5f));
   switch(kind)
   {
    case 0: Claw(p,3);for(int i=0;i<4;i++)Flame(new Vector3(0,.04f,radius*(i+1)/5));break;
    case 1: Line("Upright fire wheel",SpellFx.Circle(28,.2f),true,.035f);Flame(p);break;
    case 2: Glyph("Raised horn",p,new Vector2(-.12f,0),new Vector2(-.06f,.25f),new Vector2(.02f,.10f),new Vector2(.15f,.22f));break;
    case 3: Diamond("Powder sack",p,.12f);Flame(p+Vector3.up*.12f);break;
    case 4: Diamond("Boulder facets",p,.19f);Claw(p-Vector3.up*.18f,2);break;
    case 5: Marks(p,2,false);break;
    case 6: Diamond("Rift crystal",p,.18f);Hourglass(p);break;
    case 7: Leaf(p);Wave(p-Vector3.up*.13f);break;
    case 8: Leaf(p);Claw(p-Vector3.up*.18f,3);break;
    case 9: Leaf(p-Vector3.right*.08f);Leaf(p+Vector3.right*.08f);break;
    case 10: Glyph("Forked tongue",p,new Vector2(0,-.12f),new Vector2(0,.12f),new Vector2(-.09f,.22f),new Vector2(0,.12f),new Vector2(.09f,.22f));break;
    case 11: Cap(p);Wing(p-Vector3.up*.15f);break;
    case 12: Marks(p,2,true);Leaf(p+Vector3.up*.12f);break;
    case 13: Glyph("Back fin",p,new Vector2(-.18f,0),new Vector2(.08f,.27f),new Vector2(.13f,.08f),new Vector2(.18f,0),new Vector2(-.18f,0));break;
    case 14: Wing(p);Wave(p-Vector3.up*.13f);break;
    case 15: Diamond("Suspended bubble",p,.16f);Wave(p-Vector3.up*.15f);break;
    case 16: Claw(p-Vector3.right*.08f,2);Claw(p+Vector3.right*.08f,2);break;
    case 17: Bolt(p);Diamond("Ground contact",p-Vector3.up*.17f,.07f);break;
    case 18: Crown(p);Wave(p-Vector3.up*.12f);break;
    case 19: for(int i=0;i<5;i++)Claw(new Vector3(0,.035f,radius*(i+1)/6),2);break;
    case 20: Wave(p);Marks(p+Vector3.up*.17f,1,false);break;
    case 21: for(int i=0;i<3;i++)Diamond("Chain link",p+Vector3.up*i*.10f,.055f);Diamond("Chain weight",p-Vector3.up*.12f,.14f);break;
    case 22: Marks(p,3,true);break;
    case 23: Crown(p);Flame(p-Vector3.up*.15f);break;
    case 24: Marks(p,2,false);Flame(p+Vector3.up*.12f);break;
    case 25: for(int i=-1;i<=1;i++)Diamond("Black crystal shard",p+Vector3.right*i*.11f,.12f);break;
    case 26: Flame(p);Crown(p-Vector3.up*.15f);break;
    case 27: Claw(p,1);Flame(p+Vector3.right*.14f);break;
    case 28: Crown(p);Leaf(p+Vector3.up*.1f);break;
    case 29: Marks(p,1,true);Claw(p-Vector3.up*.15f,3);break;
    case 30: Cap(p);Diamond("Dream spore",p+Vector3.up*.29f,.055f);break;
    case 31: Cap(p);Leaf(p-Vector3.up*.13f);break;
    case 32: Wing(p);Leaf(p+Vector3.up*.1f);break;
    case 33: Claw(p,4);Wave(p-Vector3.up*.13f);break;
    case 34: Wing(p);Marks(p+Vector3.up*.15f,1,false);break;
    case 35: Bolt(p);Wave(p-Vector3.up*.12f);break;
    case 36: Wave(p);Glyph("Conch music",p,new Vector2(.1f,.05f),new Vector2(.1f,.29f),new Vector2(.2f,.26f));break;
    case 37: Wave(p);Claw(p+Vector3.up*.12f,1);break;
    case 38: Crown(p);Diamond("Shield boss",p+Vector3.up*.08f,.09f);break;
    case 39: Marks(p,3,false);break;
    case 40: Hourglass(p);Diamond("Falling sand",p-Vector3.up*.1f,.06f);break;
    case 41: Glyph("Drum head",p,new Vector2(-.15f,0),new Vector2(-.15f,.18f),new Vector2(.15f,.18f),new Vector2(.15f,0),new Vector2(-.15f,0));Marks(p,2,false);break;
    case 42: Claw(p,2);Crown(p-Vector3.up*.15f);break;
    case 43: Crown(p);Marks(p+Vector3.up*.13f,3,true);break;
    case 44: Marks(p,2,true);Bolt(p+Vector3.up*.12f);break;
    case 45: Hourglass(p);Wave(p-Vector3.up*.12f);break;
    case 46: Diamond("Stone nest",p,.18f);Leaf(p);break;
    case 47: Wing(p);Claw(p+Vector3.up*.12f,1);break;
   }
  }
 }
}

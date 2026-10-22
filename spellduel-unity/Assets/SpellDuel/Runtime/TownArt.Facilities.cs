using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        static Color FacilityStone(int town)=>town==3?C("bc7760"):Stone(town);
        static bool FacilityBuilding(int town,string kind,Transform p)
        {
            if(kind!="dungeon"&&kind!="training"&&kind!="bounty"&&kind!="watch")return false;
            Color stone=FacilityStone(town),accent=Accent(town),frame=town==0?C("566174"):town==3?C("ba8647"):Wood;
            if(kind=="dungeon")
            {
                Box(p,"Recessed ruin threshold",new Vector3(0,.10f,0),new Vector3(2.65f,.20f,1.9f),stone);
                Box(p,"Unlit descending passage",new Vector3(0,1.03f,.44f),new Vector3(1.62f,1.95f,.10f),C("344052"));
                for(int i=0;i<4;i++)Box(p,"Descending stone stair",new Vector3(0,.30f-i*.065f,-.75f+i*.24f),new Vector3(1.50f,.13f,.25f),i%2==0?stone:Mix(stone,Cream,.16f));
                for(int side=-1;side<=1;side+=2)
                {
                    Box(p,"Ruin jamb",new Vector3(side*.97f,.70f,0),new Vector3(.34f,1.4f,.62f),town==1?Wood:stone);
                    Box(p,"Jamb footing",new Vector3(side*.97f,.15f,0),new Vector3(.52f,.3f,.8f),frame);
                    Beam(p,"Torch bracket",new Vector3(side*1.15f,1.1f,0),new Vector3(side*1.22f,1.23f,-.40f),.08f,frame);
                    Beam(p,"Torch haft",new Vector3(side*1.22f,1.08f,-.40f),new Vector3(side*1.22f,1.60f,-.40f),.065f,Wood);
                    Cone(p,"Amber torch flame",new Vector3(side*1.22f,1.58f,-.40f),new Vector3(.22f,.35f,.22f),Gold);Glow(p,"Torch heart",new Vector3(side*1.22f,1.66f,-.40f),.13f,C("fff0b0"));
                }
                for(int i=0;i<12;i++){float a=(i+.5f)*Mathf.PI/12;var block=Box(p,"Arch wedge",new Vector3(Mathf.Cos(a)*.95f,1.35f+Mathf.Sin(a)*.95f,0),new Vector3(.28f,.34f,.65f),i%2==0?stone:frame);block.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);}
                FacilityRegional(town,p,2.23f,1.15f);FacilitySignPost(p,2.95f,.72f,accent,kind);
            }
            else if(kind=="training")
            {
                Cyl(p,"Packed training circle",new Vector3(0,.02f,0),new Vector3(3.3f,.02f,3.3f),stone);
                for(int i=0;i<5;i++){float x=-1.24f+i*.62f;Box(p,"Training fence post",new Vector3(x,.42f,.83f),new Vector3(.10f,.84f,.10f),frame);}
                for(int level=0;level<2;level++)Beam(p,"Training back rail",new Vector3(-1.24f,.3f+level*.32f,.83f),new Vector3(1.24f,.3f+level*.32f,.83f),.055f,frame);
                for(int side=-1;side<=1;side+=2)Beam(p,"Training side fence",new Vector3(side*1.24f,.45f,.83f),new Vector3(side*1.24f,.45f,-.65f),.055f,frame);
                var target=Group(p,"Layered archery target",new Vector3(-.60f,1.04f,.22f));target.localRotation=Quaternion.Euler(90,0,0);
                Cyl(target,"Straw target rim",Vector3.zero,new Vector3(.91f,.06f,.91f),Gold);Cyl(target,"Painted target ring",new Vector3(0,-.065f,0),new Vector3(.70f,.01f,.70f),Cream);Cyl(target,"Target red ring",new Vector3(0,-.08f,0),new Vector3(.46f,.01f,.46f),accent);Cyl(target,"Target bullseye",new Vector3(0,-.095f,0),new Vector3(.18f,.01f,.18f),Gold);
                for(int s=-1;s<=1;s+=2)Beam(p,"Target timber tripod",new Vector3(-.6f,.9f,.22f),new Vector3(-.6f+s*.28f,.04f,.32f+s*.25f),.085f,Wood);
                Beam(p,"Dummy stake",new Vector3(.35f,0,-.25f),new Vector3(.35f,1.4f,-.25f),.09f,Wood);Ball(p,"Straw dummy head",new Vector3(.35f,1.24f,-.25f),Vector3.one*.30f,Gold);Cyl(p,"Bound straw torso",new Vector3(.35f,.83f,-.25f),new Vector3(.35f,.22f,.29f),C("c7a357"));Beam(p,"Dummy cross arm",new Vector3(-.03f,1.03f,-.25f),new Vector3(.73f,1.03f,-.25f),.085f,Wood);
                for(int i=0;i<3;i++){float x=.84f+i*.15f;Beam(p,"Practice weapon rack",new Vector3(x,.12f,.53f),new Vector3(x,1.1f,.53f),.045f,i==1?Cream:Wood);Beam(p,"Wood sword guard",new Vector3(x-.10f,.75f,.53f),new Vector3(x+.10f,.75f,.53f),.035f,Gold);}
                FacilityRegional(town,p,1.60f,1.12f);FacilitySignPost(p,2.03f,.35f,accent,kind);
            }
            else if(kind=="bounty")
            {
                for(int side=-1;side<=1;side+=2){Box(p,"Notice post",new Vector3(side*.82f,1.03f,0),new Vector3(.15f,2.06f,.18f),frame);Box(p,"Post stone shoe",new Vector3(side*.82f,.13f,0),new Vector3(.30f,.26f,.35f),stone);}
                Box(p,"Carved notice frame",new Vector3(0,1.22f,-.10f),new Vector3(1.65f,1.43f,.18f),frame);Box(p,"Cork notice backing",new Vector3(0,1.22f,-.205f),new Vector3(1.48f,1.24f,.035f),C("9e7549"));
                for(int row=0;row<2;row++)for(int col=0;col<3;col++)
                {
                    float x=(col-1)*.46f,y=.91f+row*.60f;Box(p,"Pinned bounty parchment",new Vector3(x,y,-.24f),new Vector3(.39f,.49f,.015f),Cream);Ball(p,"Bronze parchment tack",new Vector3(x,y+.21f,-.26f),Vector3.one*.045f,Gold);
                    if(col==1&&row==1){Ball(p,"Wanted Boss head",new Vector3(x,y+.02f,-.265f),new Vector3(.17f,.16f,.015f),Ink);for(int side=-1;side<=1;side+=2)Beam(p,"Wanted Boss horns",new Vector3(x+side*.05f,y+.04f,-.28f),new Vector3(x+side*.11f,y+.14f,-.28f),.025f,Ink);Box(p,"Boss silhouette shoulders",new Vector3(x,y-.07f,-.27f),new Vector3(.25f,.08f,.015f),Ink);}
                    else for(int line=0;line<3;line++)Box(p,"Reward inscription",new Vector3(x,y+.10f-line*.075f,-.26f),new Vector3(.23f-line*.03f,.014f,.008f),Ink);
                    Box(p,"Bounty reward seal",new Vector3(x,y-.17f,-.26f),new Vector3(.10f,.025f,.012f),accent);
                }
                Part(p,"Notice rain roof",Shape("roof"),new Vector3(0,2.02f,0),new Vector3(2.05f,.36f,1.08f),accent);FacilityRegional(town,p,2.25f,.86f);FacilitySignPost(p,2.85f,.70f,accent,kind);
            }
            else
            {
                for(int tier=0;tier<3;tier++){float z=-.48f+tier*.43f,y=.12f+tier*.22f;Box(p,"Stepped spectator deck",new Vector3(0,y,z),new Vector3(2.05f,.18f,.42f),town==0||town>=3?stone:Wood);Box(p,"Bench seat",new Vector3(0,y+.16f,z+.04f),new Vector3(1.90f,.10f,.24f),Cream);}
                for(int side=-1;side<=1;side+=2){Beam(p,"Viewing rail",new Vector3(side*.98f,.32f,-.57f),new Vector3(side*.98f,1.04f,.56f),.055f,frame);Beam(p,"Pennant pole",new Vector3(side*.91f,.6f,.51f),new Vector3(side*.91f,2.20f,.51f),.045f,frame);var flag=Box(p,"Spectator pennant",new Vector3(side*1.07f,1.99f,.51f),new Vector3(.34f,.36f,.035f),accent);flag.transform.localRotation=Quaternion.Euler(0,0,side*8);}
                FacilityRegional(town,p,2.10f,.86f);FacilitySignPost(p,1.48f,1.25f,accent,kind);
            }
            return true;
        }
        static void FacilityRegional(int town,Transform p,float y,float half)
        {
            // Each trim piece belongs to a load-bearing timber/stone frame.
            for(int side=-1;side<=1;side+=2)Beam(p,"Regional shelter upright",new Vector3(side*half,.04f,.12f),new Vector3(side*half,y+.12f,.12f),.085f,town==0||town>=3?FacilityStone(town):Wood);
            Beam(p,"Regional shelter lintel",new Vector3(-half,y+.08f,.12f),new Vector3(half,y+.08f,.12f),.09f,town==0?C("566174"):Wood);
            if(town==0)for(int side=-1;side<=1;side+=2){Box(p,"Forged iron reinforcing strap",new Vector3(side*half,y-.12f,0),new Vector3(.09f,.4f,.13f),C("566174"));for(int i=0;i<2;i++)Ball(p,"Copper rivet",new Vector3(side*half,y-.2f+i*.2f,-.08f),Vector3.one*.05f,Gold);}
            else if(town==1)for(int side=-1;side<=1;side+=2){Beam(p,"Living vine brace",new Vector3(side*half,.3f,.20f),new Vector3(side*(half-.18f),y,.10f),.055f,C("5c8c47"));Leaf(p,new Vector3(side*(half-.05f),y-.1f,0),.34f,C("8ebd61"),side*28);Glow(p,"Leaf lamp",new Vector3(side*half,y-.26f,-.02f),.13f,C("dced9e"));}
            else if(town==2)for(int side=-1;side<=1;side+=2){var coil=Group(p,"Coiled ship rope",new Vector3(side*half,y-.16f,-.08f));coil.localRotation=Quaternion.Euler(90,0,0);Ring(coil,"Hemp rope",.12f,.025f,0,Cream,false,12);Ball(p,"Painted mooring buoy",new Vector3(side*half,.20f,.12f),new Vector3(.22f,.31f,.22f),Accent(town));}
            else if(town==3)for(int side=-1;side<=1;side+=2){Box(p,"Sandstone stepped cap",new Vector3(side*half,y+.03f,.05f),new Vector3(.32f,.17f,.28f),FacilityStone(town));Cyl(p,"Bronze cap stud",new Vector3(side*half,y+.15f,.05f),new Vector3(.14f,.04f,.14f),Gold);}
            else{Part(p,"Indigo canvas shade",Shape("roof"),new Vector3(0,y,.15f),new Vector3(half*1.8f,.22f,.72f),Accent(town));for(int side=-1;side<=1;side+=2){Beam(p,"Bell suspension",new Vector3(side*half,y+.05f,0),new Vector3(side*half,y-.18f,0),.02f,Gold);Part(p,"Caravan bell",Shape("bell"),new Vector3(side*half,y-.24f,0),Vector3.one*.13f,Gold);}}
        }
        static void FacilitySignPost(Transform p,float h,float depth,Color color,string kind)
        {
            float z=-depth*.5f-.08f,beamY=kind=="dungeon"?2.31f:kind=="training"?1.68f:kind=="bounty"?2.33f:2.18f;
            Beam(p,"Sign suspension bracket",new Vector3(0,beamY,.12f),new Vector3(0,h+.18f,z),.055f,Wood);
            Beam(p,"Sign short hanger",new Vector3(0,h+.18f,z),new Vector3(0,h-.10f,z),.045f,Wood);Sign(p,h,depth,color,kind);
        }
        static bool FacilitySign(Transform g,string kind)
        {
            const float z=-.09f;
            if(kind=="dungeon"){for(int side=-1;side<=1;side+=2){Box(g,"Castle side tower",new Vector3(side*.20f,.025f,z),new Vector3(.13f,.29f,.035f),Cream);Box(g,"Castle battlement",new Vector3(side*.20f,.18f,z),new Vector3(.18f,.06f,.035f),Gold);}Box(g,"Castle keep",new Vector3(0,-.02f,z),new Vector3(.23f,.25f,.035f),Cream);Box(g,"Castle gate",new Vector3(0,-.07f,z-.03f),new Vector3(.07f,.15f,.02f),Ink);return true;}
            if(kind=="training"){var ring=Group(g,"Target sign",new Vector3(0,0,z));ring.localRotation=Quaternion.Euler(90,0,0);Cyl(ring,"Target white",Vector3.zero,new Vector3(.37f,.015f,.37f),Cream);Cyl(ring,"Target inner",new Vector3(0,-.02f,0),new Vector3(.24f,.01f,.24f),Gold);Cyl(ring,"Target centre",new Vector3(0,-.035f,0),new Vector3(.10f,.01f,.10f),Ink);return true;}
            if(kind=="bounty"){Box(g,"Wanted parchment symbol",new Vector3(0,0,z),new Vector3(.28f,.35f,.025f),Cream);Ball(g,"Wanted head stamp",new Vector3(0,.05f,z-.025f),new Vector3(.10f,.10f,.02f),Ink);Box(g,"Wanted reward stamp",new Vector3(0,-.1f,z-.025f),new Vector3(.18f,.025f,.02f),Ink);return true;}
            if(kind=="watch"){for(int side=-1;side<=1;side+=2){Beam(g,"Eye upper lid",new Vector3(side*.23f,0,z),new Vector3(0,.13f,z),.035f,Cream);Beam(g,"Eye lower lid",new Vector3(side*.23f,0,z),new Vector3(0,-.13f,z),.035f,Cream);}Ball(g,"Eye iris",new Vector3(0,0,z-.015f),new Vector3(.11f,.17f,.025f),Gold);return true;}
            return false;
        }
    }
}

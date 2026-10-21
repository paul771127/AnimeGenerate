using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        internal static void BatchAttachment(Transform p)=>Batch(p);
        internal static GameObject Collar(Transform p,string name,float radius,float height,Color color)
            =>Part(p,name,ChapterArt.Shell(true),Vector3.zero,new Vector3(radius*2,height,radius*2),color);
        static void Arch(Transform p,Vector3 at,float width,float height,Color color)
        {
            for(int side=-1;side<=1;side+=2)Box(p,"Arch jamb",at+new Vector3(side*width*.5f,(height-width*.5f)*.5f,0),new Vector3(.13f,height-width*.5f,.16f),color);
            for(int i=0;i<10;i++){float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;Beam(p,"Arch voussoir",at+new Vector3(Mathf.Cos(a)*width*.5f,height-width*.5f+Mathf.Sin(a)*width*.5f,0),at+new Vector3(Mathf.Cos(b)*width*.5f,height-width*.5f+Mathf.Sin(b)*width*.5f,0),.14f,color);}
        }
        static void RegionalBuilding(int town,string kind,Transform p)
        {
            bool guild=kind=="guild",shop=kind=="shop",smith=kind=="smith";float w=guild?4.1f:2.8f,d=guild?3.2f:2.5f,h=guild?3.15f:2.35f,z=-d*.5f;
            Color stone=Stone(town),accent=Accent(town);
            Box(p,"Building footing",new Vector3(0,.12f,0),new Vector3(w+.10f,.24f,d+.10f),stone);
            if(town==0)
            {
                Box(p,"Stone and timber walls",new Vector3(0,h*.5f,0),new Vector3(w,h,d),C("d0a17e"));
                Part(p,"Deep red tiled gable",Shape("roof"),new Vector3(0,h,0),new Vector3(w+.24f,.85f,d+.2f),accent);
                for(int side=-1;side<=1;side+=2){Box(p,"Timber corner",new Vector3(side*(w*.5f-.08f),h*.5f,z-.025f),new Vector3(.16f,h,.12f),Wood);for(int i=0;i<4;i++)Box(p,"Copper fireproof eave plate",new Vector3(side*(w*.5f+.1f),h-.10f,-d*.35f+i*d*.23f),new Vector3(.12f,.28f,.25f),Gold);}
                for(int j=0;j<3;j++)Box(p,"Basalt masonry course",new Vector3(0,.25f+j*.25f,z-.02f),new Vector3(w,.035f,.035f),stone);
                if(!smith)Box(p,"Village chimney",new Vector3(w*.30f,h+.32f,.40f),new Vector3(.34f,1.25f,.35f),stone);
            }
            else if(town==1)
            {
                Cyl(p,"Round living timber house",new Vector3(0,h*.5f,0),new Vector3(w,h*.5f,d),Wood);
                Ball(p,"Overlapping leaf roof",new Vector3(0,h+.10f,0),new Vector3(w+.4f,1.0f,d+.4f),accent);
                for(int i=0;i<5;i++)Leaf(p,new Vector3((i-2)*w*.16f,h+.32f,z*.72f),.65f,C("77a65b"),i*15-30);
                for(int side=-1;side<=1;side+=2){Beam(p,"Root buttress",new Vector3(side*w*.45f,.1f,z*.8f),new Vector3(side*w*.34f,h*.65f,z*.7f),.23f,Wood);Glow(p,"Leaf lantern",new Vector3(side*w*.42f,1.85f,z-.12f),.18f,C("d9ee87"));}
                if(guild){Ball(p,"Guild tree crown",new Vector3(0,h*1.55f,.40f),new Vector3(w*.85f,1.65f,d*.95f),accent);Cyl(p,"Guild ancient tree",new Vector3(0,h*.80f,.45f),new Vector3(.62f,h*.80f,.65f),Wood);for(int i=0;i<2;i++)Cyl(p,"Spiral guild platform",new Vector3(0,h+i*.62f,0),new Vector3(w+.25f,.10f,d+.25f),C("bca16b"));}
            }
            else if(town==2)
            {
                Box(p,"White harbour plaster",new Vector3(0,h*.5f,0),new Vector3(w,h,d),Cream);
                if(guild)Ball(p,"Upturned guild ship hull",new Vector3(0,h+.10f,0),new Vector3(w+.3f,1.3f,d+.6f),accent);
                else Part(p,"Blue harbour gable",Shape("roof"),new Vector3(0,h,0),new Vector3(w+.25f,.75f,d+.25f),accent);
                if(guild){Beam(p,"Upturned boat keel",new Vector3(0,h+.76f,-d*.48f),new Vector3(0,h+.76f,d*.48f),.10f,Wood);for(int i=-2;i<=2;i++)for(int side=-1;side<=1;side+=2)Beam(p,"Ship hull frame rib",new Vector3(side*w*.40f,h+.38f,i*d*.18f),new Vector3(0,h+.76f,i*d*.18f),.07f,Wood);}
                for(int i=0;i<5;i++)Box(p,"Dock plank",new Vector3(0,.08f,z-.12f-i*.11f),new Vector3(w,.12f,.09f),Wood);
                for(int i=0;i<5;i++){Beam(p,"Hanging net warp",new Vector3(.5f+i*.12f,1.9f,z-.08f),new Vector3(.6f+i*.12f,.85f,z-.08f),.012f,Wood);Beam(p,"Hanging net weft",new Vector3(.48f,1+i*.17f,z-.08f),new Vector3(1.15f,1+i*.17f,z-.08f),.012f,Wood);}
                Ball(p,"Fishing buoy",new Vector3(-w*.35f,.44f,z-.18f),new Vector3(.23f,.34f,.23f),accent);
            }
            else if(town==3)
            {
                if(guild)Cyl(p,"Round sandstone guild temple",new Vector3(0,h*.5f,0),new Vector3(w*.76f,h*.5f,d*.76f),stone);
                else Box(p,"Flat roof sandstone house",new Vector3(0,h*.5f,0),new Vector3(w,h,d),stone);
                if(guild)Cyl(p,"Round temple cornice",new Vector3(0,h+.11f,0),new Vector3(w+.16f,.11f,d+.16f),Cream);
                else Box(p,"Flat parapet",new Vector3(0,h+.11f,0),new Vector3(w+.16f,.22f,d+.16f),Cream);
                if(!guild)for(int side=-1;side<=1;side+=2){Box(p,"Crimson gladiator banner",new Vector3(side*w*.36f,h*.68f,z-.06f),new Vector3(.36f,.95f,.035f),accent);Beam(p,"Banner spear emblem",new Vector3(side*w*.36f,h*.50f,z-.09f),new Vector3(side*w*.36f,h*.83f,z-.09f),.035f,Gold);}
                if(guild)for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Cyl(p,"Temple colonnade",new Vector3(Mathf.Cos(a)*w*.43f,h*.48f,Mathf.Sin(a)*d*.43f),new Vector3(.22f,h*.48f,.22f),Cream);}
                else {var cloth=Box(p,"Red cloth shade",new Vector3(0,h-.2f,z-.25f),new Vector3(w*.85f,.045f,.72f),accent);cloth.transform.localRotation=Quaternion.Euler(-10,0,0);}
            }
            else
            {
                if(guild||smith)
                {
                    Box(p,"Timber post station",new Vector3(0,h*.5f,0),new Vector3(w,h,d),C("b38a52"));
                    for(int side=-1;side<=1;side+=2)Box(p,"Exposed station beam",new Vector3(side*w*.44f,h*.5f,z-.06f),new Vector3(.18f,h,.18f),Wood);
                    Part(p,"Caravan station roof",Shape("roof"),new Vector3(0,h,0),new Vector3(w+.28f,.60f,d+.28f),accent);
                    if(guild){Box(p,"Upper station door",new Vector3(0,2.58f,z-.04f),new Vector3(.62f,.90f,.05f),Wood);for(int side=-1;side<=1;side+=2)Box(p,"Upper paper window",new Vector3(side*.96f,2.65f,z-.04f),new Vector3(.45f,.55f,.05f),Cream);Box(p,"Upper floor balcony",new Vector3(0,1.8f,z-.2f),new Vector3(w+.12f,.14f,.65f),Wood);for(int i=-2;i<=2;i++)Beam(p,"Balcony rail",new Vector3(i*.7f,1.8f,z-.45f),new Vector3(i*.7f,2.35f,z-.45f),.07f,Wood);}
                }
                else if(shop)
                {
                    for(int side=-1;side<=1;side+=2)Box(p,"Caravan tent pole",new Vector3(side*w*.43f,1.15f,0),new Vector3(.09f,2.3f,.09f),Wood);
                    Part(p,"Indigo caravan tent",Shape("roof"),new Vector3(0,1.8f,0),new Vector3(w,.75f,d),accent);
                    Box(p,"Canvas back wall",new Vector3(0,.95f,.85f),new Vector3(w*.90f,1.9f,.04f),Cream);
                }
                else Ball(p,"Loess cave mound",new Vector3(0,h*.48f,.15f),new Vector3(w+.15f,h*1.1f,d+.25f),stone);
            }
            // -Z facade remains at the original doorway/interaction plane.
            Box(p,"Doorway shadow",new Vector3(0,.85f,z-.04f),new Vector3(.73f,1.7f,.035f),Ink);
            Box(p,"Wooden door",new Vector3(0,.79f,z-.075f),new Vector3(.60f,1.55f,.025f),Wood);
            if(town>=3)Arch(p,new Vector3(0,0,z-.09f),.88f,1.96f,Cream);
            else for(int side=-1;side<=1;side+=2)Box(p,"Door jamb",new Vector3(side*.43f,.95f,z-.08f),new Vector3(.10f,1.9f,.1f),Wood);
            if(town<3||kind=="house")for(int side=-1;side<=1;side+=2)
            {
                var window=Cyl(p,"Round window rim",new Vector3(side*w*.32f,1.4f,z-.06f),new Vector3(.54f,.045f,.54f),town==2?accent:Gold);window.transform.localRotation=Quaternion.Euler(90,0,0);
                var glass=Cyl(p,"Round window glass",new Vector3(side*w*.32f,1.4f,z-.12f),new Vector3(.41f,.02f,.41f),Cream);glass.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            if(smith)
            {
                if(town==0){Box(p,"Large basalt furnace",new Vector3(-.85f,.7f,z-.16f),new Vector3(.75f,1.35f,.55f),stone);Box(p,"Tall forge chimney",new Vector3(-.85f,2.4f,.2f),new Vector3(.48f,2.8f,.48f),stone);for(int i=0;i<3;i++)Ball(p,"Ore heap",new Vector3(.9f+i*.13f,.18f,z-.20f),Vector3.one*.3f,Ink);}
                if(town==1){Cyl(p,"Root nestled stone forge",new Vector3(-.9f,.42f,z-.18f),new Vector3(.75f,.42f,.65f),stone);for(int i=0;i<3;i++)Ball(p,"Charcoal heap",new Vector3(-.9f+i*.16f,.14f,z-.30f),Vector3.one*.19f,Ink);}
                if(town<2)Glow(p,"Forge fire",new Vector3(-.85f,.5f,z-.46f),.30f,Gold);
                Box(p,"Anvil base",new Vector3(.8f,.3f,z-.18f),new Vector3(.40f,.6f,.42f),Wood);Box(p,"Anvil face",new Vector3(.8f,.66f,z-.18f),new Vector3(.65f,.15f,.32f),Ink);
                if(town==2){Beam(p,"Ship anchor shank",new Vector3(-.8f,.3f,z-.1f),new Vector3(-.8f,1.8f,z-.1f),.13f,Ink);for(int side=-1;side<=1;side+=2)Beam(p,"Anchor fluke",new Vector3(-.8f,.3f,z-.1f),new Vector3(-.8f+side*.45f,.65f,z-.1f),.12f,Ink);}
                if(town==3){Ball(p,"Armour display helmet",new Vector3(-.86f,1.2f,z-.12f),new Vector3(.42f,.46f,.40f),Gold);var shield=Cyl(p,"Display round shield",new Vector3(-.86f,.65f,z-.12f),new Vector3(.55f,.035f,.55f),accent);shield.transform.localRotation=Quaternion.Euler(90,0,0);}
                if(town==4)for(int i=0;i<5;i++){float a=i*Mathf.PI/4;Box(p,"Horseshoe",new Vector3(-.8f+Mathf.Cos(a)*.19f,1.15f+Mathf.Sin(a)*.22f,z-.10f),new Vector3(.09f,.12f,.04f),Ink);}
            }
            if(shop)
            {
                Box(p,"Merchandise counter",new Vector3(-.65f,.70f,z-.20f),new Vector3(1.0f,.10f,.40f),Wood);
                for(int i=0;i<3;i++)Bottle(p,new Vector3(-1+i*.28f,.76f,z-.20f),i==0?accent:i==1?Gold:Cream,.23f);
                if(town==0||town==1)for(int i=0;i<3;i++)Leaf(p,new Vector3(.7f+i*.12f,1.65f,z-.10f),.34f,Accent(1),15);
                if(town==2)for(int i=0;i<3;i++)Ball(p,"Dried fish",new Vector3(.6f+i*.20f,1.65f,z-.12f),new Vector3(.09f,.32f,.04f),Gold);
                if(town==3){Cyl(p,"Market parasol pole",new Vector3(.8f,1.25f,z-.20f),new Vector3(.055f,1.25f,.055f),Wood);Cone(p,"Crimson market parasol",new Vector3(.8f,2.4f,z-.20f),new Vector3(1.1f,.28f,1.1f),accent);}
                if(town==4)for(int i=0;i<3;i++)ResidentBox(p,"Hanging caravan bundle",new Vector3(.55f+i*.25f,1.35f,z-.10f),new Vector3(.21f,.33f,.21f),Cream);
            }
            if(guild&&town==0){Beam(p,"Miners pick shaft",new Vector3(-.28f,2.9f,z-.12f),new Vector3(.28f,2.35f,z-.12f),.07f,Gold);Beam(p,"Miners pick blade",new Vector3(-.48f,2.65f,z-.12f),new Vector3(-.02f,3.1f,z-.12f),.09f,Gold);}
            if(guild&&town==4)for(int i=-1;i<=1;i++)Part(p,"Caravan guild bell",Shape("bell"),new Vector3(i*.28f,2.65f,z-.14f),Vector3.one*.21f,Gold);
            if(smith||shop||guild)Sign(p,h,d,smith?Gold:shop?Accent(1):Gold,kind);
        }
        static void DesertLandmark(int town,Transform p)
        {
            if(town==3){Arch(p,new Vector3(0,0,0),1.75f,3.3f,Stone(town));var gong=Cyl(p,"Opening gong",new Vector3(0,2.2f,0),new Vector3(1.0f,.07f,1.0f),Gold);gong.transform.localRotation=Quaternion.Euler(90,0,0);Beam(p,"Gong suspension",new Vector3(0,3.1f,0),new Vector3(0,2.7f,0),.055f,Wood);}
            else {for(int side=-1;side<=1;side+=2)Box(p,"Caravan gate post",new Vector3(side*.9f,1.6f,0),new Vector3(.22f,3.2f,.22f),Wood);Box(p,"Caravan gate lintel",new Vector3(0,3.2f,0),new Vector3(2.2f,.24f,.30f),Wood);for(int i=-2;i<=2;i++){Beam(p,"Bell cord",new Vector3(i*.31f,3.1f,0),new Vector3(i*.31f,2.6f-Mathf.Abs(i)*.15f,0),.025f,Wood);Part(p,"Camel bell",Shape("bell"),new Vector3(i*.31f,2.5f-Mathf.Abs(i)*.15f,0),Vector3.one*.26f,Gold);}for(int side=-1;side<=1;side+=2)Cyl(p,"Camel tether post",new Vector3(side*1.15f,.40f,.45f),new Vector3(.13f,.4f,.13f),Wood);}
        }
        static void DesertHorizon(Transform p,Vector3 at,bool canyon)
        {
            if(canyon)for(int j=0;j<3;j++)Box(p,"Distant loess cliff stratum",at+new Vector3(0,1+j*1.4f,j*.7f),new Vector3(8.1f-j*.4f,1.45f,4),j%2==0?Stone(4):C("b38a52"));
            else {Box(p,"Distant arena outer wall",at+Vector3.up*2,new Vector3(8.1f,4,1),Stone(3));for(int i=-1;i<=1;i++)Box(p,"Arena arcade opening",at+new Vector3(i*2,2,-.51f),new Vector3(.9f,2.2f,.04f),Wood);}
        }
    }
}

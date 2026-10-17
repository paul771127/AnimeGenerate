using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        static Color Accent(int town)=>town==0?C("be543a"):town==1?C("45805d"):C("3988bc");
        static Color Stone(int town)=>town==0?C("b58870"):town==1?C("b3b58b"):C("ded7b8");
        public static void BuildScenery(int town,Transform root)
        {
            var p=Group(root,"Town landscape",Vector3.zero);
            // The harbour ground ends at the seawall; the sea stays below the walkable quay.
            Box(p,"Town foundation",new Vector3(0,-.18f,town==2?7:5),new Vector3(32,.3f,town==2?27:32),town==0?C("9b7661"):town==1?C("668d55"):C("c9ba90"));
            Cyl(p,"Paved plaza",new Vector3(0,.005f,5),new Vector3(7,.015f,7),Stone(town));
            var plaza=Group(p,"Plaza mosaic",new Vector3(0,0,5));
            Ring(plaza,"Outer stone border",3.35f,.12f,.025f,Accent(town));Ring(plaza,"Inner inlay",2.95f,.025f,.025f,Gold);
            // Flat seams only: none of the paving or road ornament is a physical obstacle.
            for(int i=-2;i<=2;i++)Box(plaza,"Paver joint",new Vector3(i,.025f,0),new Vector3(.025f,.006f,4.6f),Mix(Stone(town),Wood,.25f));
            void Road(string name,Vector3 a,Vector3 b,float w){var go=Box(p,name,(a+b)*.5f,new Vector3(w,.018f,(b-a).magnitude),Stone(town));go.transform.localRotation=Quaternion.LookRotation(b-a,Vector3.up);}
            Road("Guild avenue",new Vector3(0,.013f,5),new Vector3(0,.013f,11),1.8f);
            Road("Smith avenue",new Vector3(0,.013f,5),new Vector3(-5.2f,.013f,6.4f),1.5f);
            Road("Shop avenue",new Vector3(0,.013f,5),new Vector3(5.2f,.013f,6.4f),1.5f);
            Road("Arrival path",new Vector3(0,.013f,-1.5f),new Vector3(0,.013f,3),1.8f);
            Road("East exit road",new Vector3(3,.013f,5),new Vector3(17,.013f,5),2.3f);
            if(town>0)Road("West exit road",new Vector3(-3,.013f,5),new Vector3(-17,.013f,5),2.3f);
            foreach(var at in new[]{new Vector3(-2.6f,0,1.6f),new Vector3(2.6f,0,1.6f),new Vector3(-3.5f,0,9.8f),new Vector3(3.5f,0,9.8f)})Lamp(p,at,town);
            for(int i=0;i<18;i++)
            {
                float a=i*2*Mathf.PI/18; if(Mathf.Abs(Mathf.Sin(a))<.28f)continue;
                var at=new Vector3(Mathf.Cos(a)*16,0,5+Mathf.Sin(a)*16);
                if(town==2&&at.z<0)continue;
                if(town==0){var rock=Ball(p,"Weathered basalt",at+Vector3.up*.7f,new Vector3(1.7f,1.6f,1.2f),C("805f62"));rock.transform.localRotation=Quaternion.Euler(12,i*47,17);}
                else Tree(p,at,town,2.5f+(i%3)*.55f);
            }
            // Readable distant silhouettes above the boundary, outside all walking routes.
            for(int i=0;i<7;i++)
            {
                float x=-25+i*8;
                if(town==0){Cone(p,"Volcanic ridge",new Vector3(x,-.3f,29+i%2*4),new Vector3(13,7+i%3*2,12),C("9d747a"));Cone(p,"Copper summit",new Vector3(x,5+i%3,29+i%2*4),new Vector3(4,2,4),C("cf8e72"));}
                else if(town==1)Tree(p,new Vector3(x,0,28+i%2*3),town,7+i%3);
                else Cone(p,"Distant sea stack",new Vector3(x,-1,-24-i%2*4),new Vector3(4,2+i%3,4),C("779eae"));
            }
            if(town==2)
            {
                Box(p,"Azure sea",new Vector3(0,-.26f,-20),new Vector3(80,.08f,30),C("3d95b4"));
                for(int i=0;i<20;i++)Box(p,"Foam ribbons",new Vector3(-23+i%5*11,-.20f,-8-i/5*4),new Vector3(3+i%3,.014f,.09f),C("a9e5dd"));
                Box(p,"Wooden pier",new Vector3(5,.07f,-5.2f),new Vector3(1.6f,.14f,5),Wood);
                for(int i=0;i<14;i++)Box(p,"Pier plank seam",new Vector3(5,.147f,-3+i*-.36f),new Vector3(1.5f,.008f,.025f),Cream);
                for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)Cyl(p,"Pier piling",new Vector3(5+side*.85f,-.2f,-3.3f-i*1.8f),new Vector3(.18f,.65f,.18f),Wood);
                var boat=Group(p,"Moored white sail",new Vector3(7.7f,-.1f,-6));
                Ball(boat,"Blue hull",new Vector3(0,.1f,0),new Vector3(1,.5f,2.5f),C("447fa2"));Beam(boat,"Mast",Vector3.zero,new Vector3(0,2.6f,0),.07f,Wood);
                var sail=Cone(boat,"Folded sail",new Vector3(0,.9f,0),new Vector3(.08f,1.5f,1.15f),Cream);sail.transform.localRotation=Quaternion.Euler(0,0,20);
            }
            else if(town==1)
            {
                // Small memorial garden off the western avenue.
                for(int i=0;i<5;i++){var at=new Vector3(-6+i*.5f,0,-2.2f);Box(p,"Memory tablet",at+Vector3.up*.42f,new Vector3(.24f,.64f,.09f),Wood);Box(p,"Inscribed name",at+new Vector3(0,.48f,-.05f),new Vector3(.04f,.26f,.01f),Gold);}
            }
            else
            {
                for(int i=0;i<3;i++){Cyl(p,"Village kiln",new Vector3(-5-i*.7f,.3f,-2.7f),new Vector3(.5f,.3f,.5f),C("bd7550"));Glow(p,"Banked coals",new Vector3(-5-i*.7f,.6f,-2.7f),.22f,C("ffc06b"));}
            }
            Batch(p);
            // One combined moving mesh per ambient color, rather than a particle renderer per mote.
            var motes=Group(root,town==0?"Rising embers":town==1?"Drifting leaves":"Distant gulls",Vector3.zero);
            for(int i=0;i<12;i++)
            {
                float x=-10+(i*7%21),z=town==2?-10-i%4:2+(i*5%13),y=town==2?5+i%3:2+i%4*.6f;
                if(town==1)Leaf(motes,new Vector3(x,y,z),.18f,Gold,i*39);
                else if(town==0)Glow(motes,"Ember",new Vector3(x,y,z),.05f,Gold);
                else {Beam(motes,"Gull wing",new Vector3(x-.25f,y+.1f,z),new Vector3(x,y,z),.035f,Cream);Beam(motes,"Gull wing",new Vector3(x,y,z),new Vector3(x+.25f,y+.1f,z),.035f,Cream);}
            }
            Batch(motes);motes.gameObject.AddComponent<TownArtMotion>().bob=.12f;
        }
        static void Tree(Transform p,Vector3 at,int town,float h)
        {
            Cyl(p,"Trunk",at+Vector3.up*h*.45f,new Vector3(h*.12f,h*.45f,h*.12f),Wood);
            for(int i=0;i<3;i++)Ball(p,"Layered canopy",at+new Vector3((i-1)*h*.15f,h*(.75f+i*.12f),0),new Vector3(h*.7f,h*.52f,h*.65f),i==1?C("77a65b"):C("4b895d"));
        }
        public static void BuildBuilding(int town,string kind,Transform parent)
        {
            var p=Group(parent,kind+" art",Vector3.zero);
            if(kind=="landmark"){Landmark(town,p);Batch(p);return;}
            bool guild=kind=="guild";float w=guild?4.1f:2.8f,d=guild?3.2f:2.5f,h=guild?3.15f:2.35f;
            Color wall=town==0?C("d0a17e"):town==1?C("bca16b"):C("f3ead1");Color roof=town==0?C("a34c3b"):town==1?C("467d58"):C("397eb6");
            Box(p,"Stone footing",new Vector3(0,.13f,0),new Vector3(w+.1f,.26f,d+.1f),Stone(town));
            Box(p,"Plaster walls",new Vector3(0,h*.5f+.12f,0),new Vector3(w,h,d),wall);
            Part(p,"Pitched roof",Shape("roof"),new Vector3(0,h+.12f,0),new Vector3(w+.28f,.9f,d+.25f),roof);
            Box(p,"Ridge cap",new Vector3(0,h+1.02f,0),new Vector3(.12f,.12f,d+.35f),Gold);
            // Regional construction, not just a palette swap: masonry, timber ribs, coastal trim.
            if(town==0)
            {
                for(int row=0;row<4;row++)
                {
                    float y=.38f+row*.48f;
                    Box(p,"Masonry mortar course",new Vector3(0,y,-d*.5f-.012f),new Vector3(w,.025f,.018f),C("b58870"));
                    for(int j=-1;j<=1;j++){float x=j*.85f+(row%2)*.28f;if(Mathf.Abs(x)<.48f)continue;Box(p,"Staggered stone joint",new Vector3(x,y+.22f,-d*.5f-.02f),new Vector3(.025f,.42f,.025f),C("b58870"));}
                }
            }
            else if(town==1)
            {
                for(int sgn=-1;sgn<=1;sgn+=2)
                {
                    for(int row=0;row<5;row++)Box(p,"Timber siding",new Vector3(sgn*(w*.5f+.025f),.35f+row*.44f,0),new Vector3(.065f,.065f,d),Wood);
                    Beam(p,"Forked wooden brace",new Vector3(sgn*w*.47f,.20f,-d*.48f),new Vector3(sgn*w*.38f,1.1f,-d*.48f),.17f,Wood);
                }
                for(int i=0;i<5;i++)
                {
                    float z=-d*.5f+i*d*.25f;
                    Beam(p,"Leaf roof rib",new Vector3(-w*.5f,h+.16f,z),new Vector3(0,h+1.03f,z),.045f,C("8fba67"));
                    Beam(p,"Leaf roof rib",new Vector3(w*.5f,h+.16f,z),new Vector3(0,h+1.03f,z),.045f,C("8fba67"));
                }
            }
            else
            {
                Box(p,"Blue plinth trim",new Vector3(0,.34f,-d*.5f-.03f),new Vector3(w,.17f,.065f),roof);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(p,"White eave",new Vector3(side*w*.5f,h+.15f,0),new Vector3(.17f,.11f,d+.3f),Cream);
                    Box(p,"Blue window shutter",new Vector3(side*(w*.32f+.36f),1.32f,-d*.5f-.08f),new Vector3(.14f,.77f,.06f),roof);
                }
            }

            for(int s=-1;s<=1;s+=2)
            {
                Box(p,"Facade timber",new Vector3(s*(w*.5f-.06f),h*.5f,-d*.5f-.035f),new Vector3(.12f,h,.10f),town==2?roof:Wood);
                Box(p,"Window frame",new Vector3(s*w*.32f,1.32f,-d*.5f-.055f),new Vector3(.62f,.8f,.10f),Wood);
                Box(p,"Honey glass",new Vector3(s*w*.32f,1.32f,-d*.5f-.115f),new Vector3(.48f,.65f,.025f),town==1?C("c4d795"):C("f5ca77"));
                Box(p,"Window mullion",new Vector3(s*w*.32f,1.32f,-d*.5f-.14f),new Vector3(.055f,.69f,.02f),Cream);
                Box(p,"Window sill",new Vector3(s*w*.32f,.9f,-d*.5f-.12f),new Vector3(.73f,.10f,.25f),roof);
            }
            Box(p,"Door frame",new Vector3(0,.95f,-d*.5f-.05f),new Vector3(.94f,1.9f,.14f),Wood);
            Box(p,"Door inset",new Vector3(0,.89f,-d*.5f-.13f),new Vector3(.72f,1.66f,.03f),Accent(town));
            Ball(p,"Brass handle",new Vector3(.22f,.85f,-d*.5f-.17f),Vector3.one*.075f,Gold);
            Box(p,"Lintel",new Vector3(0,2.02f,-d*.5f-.09f),new Vector3(w,.13f,.15f),Wood);
            if(town==1){Leaf(p,new Vector3(-w*.4f,h+.4f,-d*.5f),.6f,C("8fba67"),-30);Leaf(p,new Vector3(w*.4f,h+.4f,-d*.5f),.6f,C("8fba67"),30);}
            if(kind=="smith")
            {
                Box(p,"Basalt chimney",new Vector3(.82f,h+.45f,.5f),new Vector3(.47f,1.75f,.48f),C("847579"));
                Box(p,"Chimney cap",new Vector3(.82f,h+1.35f,.5f),new Vector3(.65f,.15f,.63f),Stone(town));
                Box(p,"Forge surround",new Vector3(-.92f,.48f,-1.25f),new Vector3(.7f,.95f,.6f),C("95665b"));
                Box(p,"Furnace opening",new Vector3(-.92f,.44f,-1.565f),new Vector3(.48f,.48f,.02f),C("bd583c"));
                Glow(p,"Forge flame",new Vector3(-.92f,.45f,-1.60f),.33f,C("ffca65"));
                Cyl(p,"Anvil stump",new Vector3(.9f,.27f,-1.3f),new Vector3(.52f,.27f,.50f),Wood);
                Box(p,"Anvil foot",new Vector3(.9f,.6f,-1.3f),new Vector3(.42f,.14f,.4f),Ink);
                Box(p,"Anvil face",new Vector3(.9f,.77f,-1.3f),new Vector3(.66f,.17f,.33f),C("86a2ad"));
                var horn=Cone(p,"Anvil horn",new Vector3(1.2f,.77f,-1.3f),new Vector3(.24f,.25f,.24f),C("86a2ad"));horn.transform.localRotation=Quaternion.Euler(0,0,-90);
                Sign(p,h,d,Gold,"smith");
                for(int i=0;i<3;i++)Ball(p,"Smoke puff",new Vector3(.82f+i*.1f,h+1.7f+i*.35f,.5f),Vector3.one*(.28f+i*.08f),C("c7b8ae"));
            }
            else if(kind=="shop")
            {
                var awning=Box(p,"Striped canopy",new Vector3(0,1.95f,-1.42f),new Vector3(2.65f,.09f,.75f),Accent(town));awning.transform.localRotation=Quaternion.Euler(-12,0,0);
                for(int i=-2;i<=2;i++)Box(p,"Canvas stripes",new Vector3(i*.48f,1.95f,-1.44f),new Vector3(.20f,.11f,.71f),Cream);
                Box(p,"Potion shelf",new Vector3(-.9f,.72f,-1.42f),new Vector3(.75f,.12f,.42f),Wood);
                for(int i=0;i<3;i++)Bottle(p,new Vector3(-1.12f+i*.22f,.84f,-1.43f),i==0?C("e67985"):i==1?C("62b6d1"):C("a9c66b"),.22f);
                Sign(p,h,d,C("70c9b7"),"shop");
            }
            else if(guild)
            {
                for(int s=-1;s<=1;s+=2)
                {
                    Cyl(p,"Guild pillar",new Vector3(s*1.55f,1.5f,-1.55f),new Vector3(.32f,1.5f,.32f),Cream);
                    Box(p,"Pillar capital",new Vector3(s*1.55f,2.95f,-1.55f),new Vector3(.5f,.16f,.5f),Gold);
                    Box(p,"Swallowtail banner",new Vector3(s*.98f,2.5f,-1.68f),new Vector3(.46f,1.12f,.045f),C("895776"));
                    Cone(p,"Banner point",new Vector3(s*.98f,1.94f,-1.68f),new Vector3(.46f,.25f,.045f),C("895776")).transform.localRotation=Quaternion.Euler(0,0,180);
                    Beam(p,"Banner sword",new Vector3(s*.98f,2.16f,-1.72f),new Vector3(s*.98f,2.83f,-1.72f),.045f,Gold);
                    Box(p,"Sword crossguard",new Vector3(s*.98f,2.38f,-1.74f),new Vector3(.22f,.045f,.03f),Gold);
                }
                Sign(p,h,d,Gold,"guild");
            }
            else
            {
                Box(p,"Window box",new Vector3(.9f,.77f,-1.35f),new Vector3(.65f,.25f,.3f),Wood);
                for(int i=0;i<3;i++)Ball(p,"Planter flowers",new Vector3(.7f+i*.18f,.97f,-1.35f),Vector3.one*.17f,i==1?Gold:C("d1858c"));
            }
            Batch(p);
        }
        static void Sign(Transform p,float h,float d,Color c,string kind)
        {
            var g=Group(p,"Pictogram sign",new Vector3(0,h-.12f,-d*.5f-.14f));
            Box(g,"Sign plaque",Vector3.zero,new Vector3(.72f,.53f,.08f),c);
            if(kind=="shop")Bottle(g,new Vector3(0,-.17f,-.08f),C("528f9b"),.30f);
            else if(kind=="smith"){Beam(g,"Hammer haft",new Vector3(-.12f,-.18f,-.07f),new Vector3(.1f,.1f,-.07f),.055f,Wood);Box(g,"Hammer head",new Vector3(.1f,.12f,-.07f),new Vector3(.3f,.12f,.09f),Cream);}
            else{Beam(g,"Sword",new Vector3(0,-.19f,-.07f),new Vector3(0,.2f,-.07f),.055f,Cream);Box(g,"Guard",new Vector3(0,-.04f,-.08f),new Vector3(.3f,.06f,.06f),Cream);}
        }
        static void Bottle(Transform p,Vector3 at,Color c,float h)
        {
            Ball(p,"Potion body",at+Vector3.up*h*.36f,new Vector3(h*.68f,h*.72f,h*.68f),c);
            Cyl(p,"Bottle cork",at+Vector3.up*h*.88f,new Vector3(h*.27f,h*.16f,h*.27f),Gold);
        }
        static void Landmark(int town,Transform p)
        {
            if(town==0)
            {
                Box(p,"Belfry base",new Vector3(0,.16f,0),new Vector3(1.7f,.32f,1.7f),Stone(town));
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box(p,"Belfry column",new Vector3(x*.58f,1.9f,z*.58f),new Vector3(.20f,3.8f,.20f),Wood);
                Part(p,"Morning bell",Shape("bell"),new Vector3(0,2.9f,0),new Vector3(.85f,.9f,.85f),Gold);
                Ball(p,"Repaired clapper",new Vector3(0,2.35f,0),Vector3.one*.16f,C("cc8550"));
                Box(p,"Bell crossbeam",new Vector3(0,3.5f,0),new Vector3(1.5f,.16f,.2f),Wood);
                Part(p,"Belfry roof",Shape("roof"),new Vector3(0,3.8f,0),new Vector3(1.9f,.75f,1.9f),Accent(town));
                for(int i=0;i<3;i++)Box(p,"Belfry ladder rung",new Vector3(0,.5f+i*.32f,-.65f),new Vector3(.6f,.09f,.1f),Gold);
            }
            else if(town==1)
            {
                Cyl(p,"Ancient trunk",new Vector3(0,1.9f,0),new Vector3(.95f,1.9f,.9f),Wood);
                for(int i=0;i<5;i++){float a=i*2*Mathf.PI/5;Beam(p,"Ancient roots",new Vector3(0,.45f,0),new Vector3(Mathf.Cos(a)*1.1f,.06f,Mathf.Sin(a)*1.1f),.22f,Wood);}
                for(int s=-1;s<=1;s+=2){Beam(p,"Sheltering bough",new Vector3(0,2.2f,0),new Vector3(s*.8f,3.7f,0),.25f,Wood);Ball(p,"Sacred canopy",new Vector3(s*.5f,4.15f,0),new Vector3(2.1f,1.5f,2.1f),s<0?C("4f8f64"):C("7aab68"));}
                for(int i=-1;i<=1;i++){Box(p,"Memorial tablet",new Vector3(i*.52f,.53f,-.88f),new Vector3(.32f,.68f,.075f),C("c99960"));Box(p,"Memorial lettering",new Vector3(i*.52f,.58f,-.93f),new Vector3(.055f,.32f,.015f),Cream);}
                Leaf(p,new Vector3(.58f,2.7f,-.5f),.6f,Gold,18);Glow(p,"Leaf lantern",new Vector3(.58f,2.6f,-.5f),.24f,C("daf3a0"));
            }
            else
            {
                Cyl(p,"Lighthouse shaft",new Vector3(0,2.2f,0),new Vector3(1.2f,2.2f,1.2f),Cream);
                foreach(float y in new[]{1.1f,2.7f})Cyl(p,"Blue lighthouse band",new Vector3(0,y,0),new Vector3(1.23f,.22f,1.23f),Accent(town));
                Cyl(p,"Gallery",new Vector3(0,4.25f,0),new Vector3(1.8f,.10f,1.8f),Gold);
                Cyl(p,"Lantern chamber",new Vector3(0,4.7f,0),new Vector3(.9f,.4f,.9f),C("b9e5e1"));
                Glow(p,"Beacon",new Vector3(0,4.7f,0),.65f,C("fff0af"));
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Beam(p,"Lantern mullion",new Vector3(Mathf.Cos(a)*.48f,4.3f,Mathf.Sin(a)*.48f),new Vector3(Mathf.Cos(a)*.48f,5.1f,Mathf.Sin(a)*.48f),.06f,Wood);}
                Cone(p,"Lighthouse cap",new Vector3(0,5.1f,0),new Vector3(1.5f,.6f,1.5f),Accent(town));
                Box(p,"Lighthouse door",new Vector3(0,.7f,-.61f),new Vector3(.5f,1.4f,.04f),Wood);
            }
        }
        public static void BuildFence(int town,Transform root,Vector3 center,float radius,List<Vector2> gaps,float half)
        {
            var p=Group(root,"Town boundary",Vector3.zero);const int n=64;float outer=radius+.22f;
            bool ClipGap(ref Vector3 x,ref Vector3 y)
            {
                foreach(var g in gaps)
                {
                    var a=new Vector2(x.x-center.x,x.z-center.z);var b=new Vector2(y.x-center.x,y.z-center.z);
                    if(Vector2.Dot(a+b,g)<=0)continue;
                    float ca=g.x*a.y-g.y*a.x,cb=g.x*b.y-g.y*b.x,edge=half+.20f;
                    bool ia=Mathf.Abs(ca)<edge,ib=Mathf.Abs(cb)<edge;
                    if(ia&&ib)return false;
                    if(ia!=ib)
                    {
                        float bound=(ia?cb:ca)>0?edge:-edge;
                        var cut=Vector3.Lerp(x,y,(bound-ca)/(cb-ca));if(ia)x=cut;else y=cut;
                    }
                }
                return true;
            }
            for(int i=0;i<n;i++)
            {
                float a=i*2*Mathf.PI/n,b=(i+1)*2*Mathf.PI/n;
                Vector3 x=center+new Vector3(Mathf.Cos(a)*outer,0,Mathf.Sin(a)*outer),y=center+new Vector3(Mathf.Cos(b)*outer,0,Mathf.Sin(b)*outer);
                if(!ClipGap(ref x,ref y))continue;
                Color c=town==0?C("a28075"):town==1?C("59845a"):Cream;
                var rail=Box(p,"Boundary panel",(x+y)*.5f+Vector3.up*.4f,new Vector3(.24f,.8f,(y-x).magnitude+.03f),c);rail.transform.localRotation=Quaternion.LookRotation(y-x,Vector3.up);
                if(i%2==0){Box(p,"Boundary pier",x+Vector3.up*.56f,new Vector3(.32f,1.12f,.32f),town==1?Wood:Stone(town));Cone(p,"Pier cap",x+Vector3.up*1.12f,new Vector3(.45f,.15f,.45f),Accent(town));}
                if(town==0)Beam(p,"Timber crown",x+Vector3.up*.9f,y+Vector3.up*.9f,.11f,Wood);
                if(town==2)Beam(p,"Seawall coping",x+Vector3.up*.85f,y+Vector3.up*.85f,.17f,Accent(town));
            }
            Batch(p);
        }
        public static void BuildGate(int town,int theme,Transform parent)
        {
            var p=Group(parent,"Open region gateway",Vector3.zero);Color c=Accent(Mathf.Clamp(theme,0,2));
            for(int s=-1;s<=1;s+=2)
            {
                // Inside edge is 1.34m from centre: the full walking aperture remains clear.
                Box(p,"Gate pillar",new Vector3(s*1.52f,1.5f,0),new Vector3(.36f,3,.45f),town==2?Cream:Wood);
                Box(p,"Gate footing",new Vector3(s*1.52f,.15f,0),new Vector3(.36f,.3f,.5f),Stone(town));
                Box(p,"Gate pennant",new Vector3(s*1.85f,2.3f,0),new Vector3(.35f,.8f,.05f),c);
                Cone(p,"Pillar finial",new Vector3(s*1.52f,3.1f,0),new Vector3(.55f,.32f,.65f),c);
            }
            Box(p,"High lintel",new Vector3(0,3.1f,0),new Vector3(3.8f,.23f,.52f),c);
            Box(p,"Carved gate crest",new Vector3(0,2.83f,-.25f),new Vector3(1.1f,.38f,.09f),Gold);
            if(theme==0){for(int s=-1;s<=1;s+=2){Ball(p,"Beyond gate basalt",new Vector3(s*2.6f,.7f,4),new Vector3(1.2f,1.4f,1.3f),C("9e7270"));Glow(p,"Lava fissure",new Vector3(s*2.5f,.5f,3.5f),.3f,C("ffac5a"));}}
            else if(theme==1){Tree(p,new Vector3(-2.8f,0,4),1,3.4f);Tree(p,new Vector3(2.8f,0,5),1,4);}
            else{for(int s=-1;s<=1;s+=2)Cone(p,"Reef beyond gate",new Vector3(s*2.5f,0,4),new Vector3(1.4f,1.3f,1.3f),C("719caa"));}
            Batch(p);
        }
        public static void BuildWarpPad(int town,Transform parent)
        {
            var p=Group(parent,"Warp stone",Vector3.zero);Cyl(p,"Obsidian dais",new Vector3(0,.015f,0),new Vector3(1.6f,.015f,1.6f),C("73628e"));
            Ring(p,"Gold inscription",.76f,.025f,.04f,Gold);
            for(int i=0;i<3;i++){float a=i*2*Mathf.PI/3;Cone(p,"Rune crystal",new Vector3(Mathf.Cos(a)*.92f,.02f,Mathf.Sin(a)*.92f),new Vector3(.17f,.4f,.17f),C("a5cbe6"));}
            Batch(p);
            var rune=Group(parent,"Rotating magic glyph",Vector3.zero);Ring(rune,"Luminous circle",.61f,.025f,.052f,C("b5dfee"),true);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var q=Part(rune,"Rune",Shape("box"),new Vector3(Mathf.Cos(a)*.43f,.055f,Mathf.Sin(a)*.43f),new Vector3(.07f,.012f,.18f),C("b5dfee"),true);q.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
            Batch(rune);rune.gameObject.AddComponent<TownArtMotion>().turn=12;
        }
    }
}

using UnityEngine;
namespace SpellDuel
{
    public static partial class HazardArt
    {
        public static RoomPropArt RoomProp(string id,Color color)
        {
            var v=new GameObject("Event_"+id).AddComponent<RoomPropArt>();v.Id=id;v.Tint=color;
            var body=new GameObject("Body").transform;body.SetParent(v.transform,false);v.Body=body;
            var mb=new ArtKit.MB(3);
            void Pillar(Vector3 at,float h,float r,int mat=0)=>ArtKit.Lathe(mb,mat,at,Quaternion.identity,new[]{0f,.08f,h-.08f,h},new[]{r*1.15f,r,r,r*.85f},10,true,true);
            void Box(Vector3 at,Vector3 size,int mat=0)=>ArtKit.Box(mb,mat,at,size,Quaternion.identity);
            if(id.StartsWith("ev_core"))
            {
                Pillar(Vector3.zero,.16f,.38f,1);
                foreach(float x in new[]{-.29f,.29f})Pillar(new Vector3(x,.15f,0),1.05f,.055f,1);
                Box(new Vector3(0,1.22f,0),new Vector3(.72f,.09f,.13f),2);
                ArtKit.Lathe(mb,2,new Vector3(0,.32f,0),Quaternion.identity,new[]{0f,.06f,.15f,.4f,.56f,.64f},new[]{.29f,.3f,.22f,.19f,.13f,.025f},24,true,false);
                Pillar(new Vector3(0,.21f,0),.2f,.035f,2);
                v.Cracks=new GameObject("Damage cracks");v.Cracks.transform.SetParent(body,false);
                Ribbon(v.Cracks.transform,"Bell fracture",new[]{new Vector3(.02f,.9f,.15f),new Vector3(-.04f,.7f,.2f),new Vector3(.05f,.54f,.23f),new Vector3(.01f,.37f,.3f)},.013f,new Color(.25f,.08f,.04f));v.Cracks.SetActive(false);
                v.Glow=Orb(body,"Glow",new Vector3(0,1.33f,0),.12f,new Color(1,.84f,.32f));
            }
            else if(id.StartsWith("ev_totem"))
            {
                Pillar(Vector3.zero,.14f,.32f,1);Pillar(new Vector3(0,.14f,0),1.17f,.22f);
                for(int i=0;i<4;i++)
                {
                    float y=.35f+i*.22f;
                    Ribbon(body,"Seal rune",new[]{new Vector3(-.12f,y,.23f),new Vector3(0,y+.09f,.24f),new Vector3(.12f,y,.23f),new Vector3(0,y-.04f,.24f)},.019f,new Color(.77f,.35f,1,.8f));
                }
                Pillar(new Vector3(0,1.3f,0),.12f,.29f,2);
                v.Glow=Orb(body,"Glow",new Vector3(0,1.49f,0),.13f,new Color(.78f,.32f,1));
            }
            else if(id.StartsWith("ev_statue"))
            {
                Pillar(Vector3.zero,.12f,.34f,1);Pillar(new Vector3(0,.12f,0),.74f,.2f);Pillar(new Vector3(0,.86f,0),.12f,.29f,2);
                if(id.EndsWith("dual"))
                {
                    v.Glow=new GameObject("Orb").transform;v.Glow.SetParent(body,false);v.Glow.localPosition=new Vector3(0,1.15f,0);
                    Orb(v.Glow,"Fire",new Vector3(-.1f,0,0),.12f,Battle.ElementColor(1));Orb(v.Glow,"Ice",new Vector3(.1f,0,0),.12f,Battle.ElementColor(2));
                }
                else v.Glow=Orb(body,"Orb",new Vector3(0,1.15f,0),.14f,Color.white);
                v.Halo=new GameObject("Lit seal");v.Halo.transform.SetParent(v.transform,false);Ring(v.Halo.transform,"Element seal",.6f,color);v.Halo.SetActive(false);
            }
            else if(id.StartsWith("ev_block"))
            {
                foreach(float x in new[]{-.3f,.3f}){Pillar(new Vector3(x,0,0),.85f,.055f,1);}
                ArtKit.Box(mb,1,new Vector3(0,.4f,0),new Vector3(.85f,.12f,.13f),Quaternion.Euler(0,0,25));
                ArtKit.Box(mb,1,new Vector3(0,.4f,.04f),new Vector3(.85f,.12f,.13f),Quaternion.Euler(0,0,-25));
            }
            else if(id.StartsWith("ev_chest"))
            {
                Box(new Vector3(0,.2f,0),new Vector3(.6f,.36f,.42f),1);
                // Barrel-vault lid, metal straps and an actual lock distinguish it from a box.
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;
                    var q0=new Vector3(-.3f,.36f+Mathf.Sin(a)*.15f,Mathf.Cos(a)*.21f);
                    var q1=new Vector3(-.3f,.36f+Mathf.Sin(b)*.15f,Mathf.Cos(b)*.21f);
                    ArtKit.QuadToward(mb,1,q0,q1,q1+Vector3.right*.6f,q0+Vector3.right*.6f,new Vector3(0,1,0));
                    foreach(float x in new[]{-.22f,.18f})ArtKit.QuadToward(mb,2,new Vector3(x,q0.y+.006f,q0.z*1.02f),new Vector3(x,q1.y+.006f,q1.z*1.02f),new Vector3(x+.04f,q1.y+.006f,q1.z*1.02f),new Vector3(x+.04f,q0.y+.006f,q0.z*1.02f),new Vector3(0,1,0));
                }
                Box(new Vector3(0,.32f,.225f),new Vector3(.09f,.12f,.025f),2);
                Box(new Vector3(0,.32f,.241f),new Vector3(.018f,.035f,.01f));
                v.Glow=Orb(body,"Glimmer",new Vector3(0,.57f,0),.06f,new Color(1,.85f,.25f));v.Glow.gameObject.SetActive(false);
            }
            Part(body,"Crafted structure",mb.Build(id),NatureStone,Wood,Brass);
            return v;
        }
        static Transform Orb(Transform parent,string name,Vector3 at,float radius,Color color)
        {
            var mb=new ArtKit.MB(1);ArtKit.Lathe(mb,0,Vector3.zero,Quaternion.identity,new[]{-radius,-radius*.7f,0f,radius*.7f,radius},new[]{.001f,radius*.72f,radius,radius*.72f,.001f},16,true,true,color);
            var go=Part(parent,name,mb.Build(name),NatureGlow);go.transform.localPosition=at;return go.transform;
        }
        public static RoomPointArt RoomPoint(string kind,int index,Color color)
        {
            var v=new GameObject("EventPoint_"+kind).AddComponent<RoomPointArt>();v.Kind=kind;v.Tint=color;
            var mb=new ArtKit.MB(1);
            if(kind=="runes")
            {
                Ring(v.transform,"Outer rune",.4f,color);Ring(v.transform,"Inner rune",.32f,color);
                for(int i=0;i<3+index%4;i++){float a=i*Mathf.PI*2/(3+index%4);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Ribbon(v.transform,"Glyph",new[]{d*.25f+Vector3.up*.03f,d*.12f+Vector3.up*.03f,Quaternion.Euler(0,65,0)*d*.2f+Vector3.up*.03f},.025f,color);}
            }
            else if(kind=="orb")
            {
                v.Moving=Orb(v.transform,"Element",Vector3.zero,.15f,color);
                Ring(v.Moving,"Orbit",.21f,color);
                if(index==2) v.Moving.localScale=new Vector3(.7f,1.5f,.7f);
                if(index==3)Ribbon(v.Moving,"Lightning",new[]{new Vector3(.07f,.24f,0),new Vector3(-.06f,.02f,.16f),new Vector3(.06f,0,.16f),new Vector3(-.07f,-.24f,0)},.04f,color);
            }
            else if(kind=="chant")
            {
                foreach(float x in new[]{-.75f,.75f})ArtKit.Lathe(mb,0,new Vector3(x,0,0),Quaternion.identity,new[]{0f,.12f,1.85f,2.1f},new[]{.18f,.13f,.13f,.18f},8,true,true);
                ArtKit.Box(mb,0,new Vector3(0,2.05f,0),new Vector3(1.65f,.15f,.28f),Quaternion.identity);
                for(int i=0;i<7;i++){float x=-.6f+i*.2f;Ribbon(v.transform,"Runic curtain",new[]{new Vector3(x,.1f,0),new Vector3(x,1.95f,0)},.06f,new Color(.65f,.4f,1,.35f));}
                for(int i=0;i<4;i++)Ribbon(v.transform,"Door glyph",new[]{new Vector3(-.14f,.35f+i*.4f,.02f),new Vector3(0,.5f+i*.4f,.02f),new Vector3(.14f,.35f+i*.4f,.02f),new Vector3(0,.25f+i*.4f,.02f),new Vector3(-.14f,.35f+i*.4f,.02f)},.027f,color);
            }
            else if(kind=="collapse")
            {
                v.Debris=SpellFx.NewPS(v.transform,"Falling grit",SpellFx.BlendMat(SpellFx.TexSoft),24,false);
                var dm=v.Debris.main;dm.loop=false;dm.startLifetime=.55f;dm.startSpeed=.12f;dm.startSize=.045f;dm.startColor=new Color(.65f,.38f,.18f);dm.gravityModifier=1;
                v.Debris.transform.localPosition=Vector3.up*.18f;SpellFx.Sphere(v.Debris,.34f);
                var de=v.Debris.emission;de.SetBursts(new[]{new ParticleSystem.Burst(0,20)});
                var hole=Part(v.transform,"Hole",ArtKit.FlatDisc(.48f,32,.012f,"Dark opening"),NatureGlow);v.Hole=hole;
                var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.035f,.025f,.045f,.95f));hole.GetComponent<Renderer>().SetPropertyBlock(block);
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Ribbon(v.transform,"Red fracture",new[]{new Vector3(0,.025f,0),new Vector3(Mathf.Cos(a+.2f)*.25f,.025f,Mathf.Sin(a+.2f)*.25f),new Vector3(Mathf.Cos(a)*.48f,.025f,Mathf.Sin(a)*.48f)},.027f,new Color(1,.15f,.04f));}
            }
            else if(kind=="eyes")
            {
                foreach(float x in new[]{-.07f,.07f}){var eye=Orb(v.transform,"Red eye",new Vector3(x,0,0),.035f,new Color(1,.08f,.025f));eye.localScale=new Vector3(1.4f,.5f,.5f);}
            }
            else
            {
                Ring(v.transform,"Destination",kind=="escort"?.55f:.6f,color);
                if(kind=="escort"){ArtKit.Lathe(mb,0,new Vector3(.45f,0,0),Quaternion.identity,new[]{0f,.65f},new[]{.025f,.025f},6,true,true);ArtKit.QuadToward(mb,0,new Vector3(.45f,.62f,0),new Vector3(.75f,.55f,0),new Vector3(.45f,.46f,0),new Vector3(.45f,.62f,0),Vector3.forward,col:color);}
            }
            if(mb.V.Count>0)Part(v.transform,"Stonework",mb.Build("Event point"),NatureStone);
            v.Lines=v.GetComponentsInChildren<LineRenderer>();return v;
        }
    }
}

using UnityEngine;

namespace SpellDuel
{
    public static partial class HazardArt
    {
        static Material natureGlow, natureStone, natureBark;
        static Material NatureGlow => natureGlow ? natureGlow : (natureGlow = new Material(Shader.Find("Sprites/Default")) { name = "Nature glow" });
        static Material NatureStone => Toon(ref natureStone, "Basalt", new Color(.27f,.22f,.32f));
        static Material NatureBark => Toon(ref natureBark, "Living roots", new Color(.48f,.30f,.12f));

        // Each view owns transforms only. Meshes are released with the view; shared materials
        // and SpellFx textures are never cloned or edited by animation.
        public static NatureHazardArt Nature(Battle.HazardKind kind, float radius)
        {
            var v = new GameObject("Nature_" + kind).AddComponent<NatureHazardArt>();
            v.Kind = kind;
            v.Warning = new GameObject("Warning").transform; v.Warning.SetParent(v.transform, false);
            v.Active = new GameObject("Active").transform; v.Active.SetParent(v.transform, false);
            Color tint = kind == Battle.HazardKind.Lava ? new Color(1,.24f,.04f) : kind == Battle.HazardKind.Roots ? new Color(.38f,1,.18f) : kind == Battle.HazardKind.Spore ? new Color(.8f,.28f,1) : new Color(.2f,.8f,1);
            v.Tint = tint;
            if (kind == Battle.HazardKind.Tide)
            {
                Ribbon(v.Warning, "Shore warning", new[]{new Vector3(-4.5f,.025f,0),new Vector3(4.5f,.025f,0)}, .16f, tint);
                var mb = new ArtKit.MB(1);
                for(int i=0;i<36;i++)
                {
                    float x=-4.5f+i*.25f, y=.38f+.055f*Mathf.Sin(i*1.7f);
                    ArtKit.QuadToward(mb,0,new Vector3(x,.02f,-radius),new Vector3(x+.25f,.02f,-radius),new Vector3(x+.25f,y,.08f),new Vector3(x,y,.08f),Vector3.up, col:new Color(.08f,.54f,.85f,.66f));
                    ArtKit.QuadToward(mb,0,new Vector3(x,y,.08f),new Vector3(x+.25f,y,.08f),new Vector3(x+.25f,.07f,radius),new Vector3(x,.07f,radius),Vector3.up, col:new Color(.24f,.8f,1,.6f));
                }
                Part(v.Active,"Curled wave",mb.Build("Tide crest"),NatureGlow);
                var foam = new Vector3[73]; for(int i=0;i<foam.Length;i++)foam[i]=new Vector3(-4.5f+i*.125f,.4f+.035f*Mathf.Sin(i*.85f),.08f);
                Ribbon(v.Active,"Foam lip",foam,.065f,new Color(.85f,1,1,.85f));
            }
            else
            {
                Ring(v.Warning,"Boundary",radius,tint);
                if(kind == Battle.HazardKind.Lava || kind == Battle.HazardKind.Roots)
                {
                    var stone=new ArtKit.MB(1);
                    for(int i=0;i<10;i++)
                    {
                        float a=i*Mathf.PI/5; var p=new Vector3(Mathf.Cos(a)*radius*.85f,.045f,Mathf.Sin(a)*radius*.85f);
                        ArtKit.Lathe(stone,0,p,Quaternion.identity,new[]{0f,.07f,.11f},new[]{.13f,.14f,.07f},6,true,true);
                        var path=new Vector3[6];for(int j=0;j<6;j++){float r=radius*j/6;path[j]=new Vector3(Mathf.Cos(a+j*.12f)*r,.02f,Mathf.Sin(a+j*.12f)*r);}
                        Ribbon(v.Warning,"Fissure",path,.025f,tint);
                    }
                    Part(v.transform,"Cracked rim",stone.Build("Nature rim"),NatureStone);
                    if(kind==Battle.HazardKind.Roots)
                    {
                        var mb=new ArtKit.MB(1);
                        for(int i=0;i<7;i++)
                        {
                            float a=i*Mathf.PI*2/7; int start=mb.V.Count;
                            for(int j=0;j<=9;j++)
                            {
                                float t=j/9f, r=.105f*(1-t)+.002f;
                                var c=new Vector3(Mathf.Cos(a+t*1.4f)*radius*(.7f-.35f*t),.025f+t*(.8f+.15f*Mathf.Sin(i)),Mathf.Sin(a+t*1.4f)*radius*(.7f-.35f*t));
                                for(int k=0;k<8;k++){float ph=k*Mathf.PI/4;var n=new Vector3(Mathf.Cos(ph),0,Mathf.Sin(ph));mb.Add(c+n*r,n,new Vector2(k/8f,t));}
                            }
                            for(int j=0;j<9;j++)for(int k=0;k<8;k++){int a0=start+j*8+k,b=start+j*8+(k+1)%8;mb.Tri(0,a0,a0+8,b);mb.Tri(0,b,a0+8,b+8);}
                        }
                        Part(v.Active,"Seven twisting roots",mb.Build("Root tuft"),NatureBark);
                    }
                    else
                    {
                        var mb=new ArtKit.MB(1);
                        ArtKit.Lathe(mb,0,Vector3.zero,Quaternion.identity,new[]{.02f,.25f,.7f,1.3f,1.8f,2f},new[]{radius*.72f,radius*.66f,radius*.42f,radius*.49f,radius*.27f,.03f},14,true,false,new Color(1,.4f,.035f,.87f));
                        Part(v.Active,"Molten column",mb.Build("Lava jet"),NatureGlow);
                        for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Ribbon(v.Active,"White hot stream",new[]{new Vector3(Mathf.Cos(a)*.25f,.03f,Mathf.Sin(a)*.25f),new Vector3(Mathf.Cos(a)*.18f,.8f,Mathf.Sin(a)*.18f),new Vector3(Mathf.Cos(a)*.1f,1.7f,Mathf.Sin(a)*.1f)},.055f,new Color(1,.9f,.35f,.8f));}
                    }
                }
                if(kind==Battle.HazardKind.Whirl)
                    for(int j=0;j<4;j++)
                    {
                        var pts=new Vector3[40];for(int i=0;i<40;i++){float t=i/39f,a=t*5+j*Mathf.PI/2,r=radius*(.12f+.85f*t);pts[i]=new Vector3(Mathf.Cos(a)*r,.025f+.008f*j,Mathf.Sin(a)*r);}
                        Ribbon(v.Active,"Spiral foam",pts,.055f,new Color(.48f,.9f,1,.72f));
                    }
            }
            if(kind==Battle.HazardKind.Lava)
            {
                v.Smoke=SpellFx.NewPS(v.Warning,"Vent smoke",SpellFx.BlendMat(SpellFx.TexSoft),20,false);
                var sm=v.Smoke.main;sm.startLifetime=1.3f;sm.startSize=.23f;sm.startColor=new Color(.38f,.3f,.3f,.24f);sm.startSpeed=.15f;
                SpellFx.Sphere(v.Smoke,radius*.5f);v.Smoke.transform.localPosition=Vector3.up*.3f;
                var sv=v.Smoke.velocityOverLifetime;sv.enabled=true;sv.y=.35f;
                var se=v.Smoke.emission;se.rateOverTime=8;
            }
            v.Motes=SpellFx.NewPS(v.Active,"Local motes",SpellFx.BlendMat(SpellFx.TexSoft),48,false);
            var main=v.Motes.main;main.startColor=new Color(tint.r,tint.g,tint.b,.5f);main.startLifetime=1.2f;main.startSize=kind==Battle.HazardKind.Spore?.3f:.055f;main.startSpeed=.18f;
            SpellFx.Sphere(v.Motes,kind==Battle.HazardKind.Spore?radius*.75f:.25f);
            v.Motes.transform.localPosition=Vector3.up*(kind==Battle.HazardKind.Spore?.5f:.25f);
            if(kind==Battle.HazardKind.Spore){var cloud=v.Motes.shape;cloud.scale=new Vector3(1,.5f,1);}
            if(kind==Battle.HazardKind.Tide){var shape=v.Motes.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(9,.1f,.4f);}
            var fade=v.Motes.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var emission=v.Motes.emission;emission.rateOverTime=kind==Battle.HazardKind.Spore?24:14;
            if(kind==Battle.HazardKind.Lava){main.startSpeed=1.7f;main.gravityModifier=.18f;}
            var vel=v.Motes.velocityOverLifetime;vel.enabled=true;vel.y=.22f;
            v.WarningLines=v.Warning.GetComponentsInChildren<LineRenderer>();
            v.Tick(0,0); return v;
        }
        static void Ring(Transform p,string name,float radius,Color c)
        {
            var pts=new Vector3[65];for(int i=0;i<65;i++){float a=i*Mathf.PI/32;pts[i]=new Vector3(Mathf.Cos(a)*radius,.026f,Mathf.Sin(a)*radius);}Ribbon(p,name,pts,.035f,c);
        }
        static void Ribbon(Transform p,string name,Vector3[] pts,float width,Color c)
        {
            var l=new GameObject(name).AddComponent<LineRenderer>();l.transform.SetParent(p,false);l.useWorldSpace=false;l.sharedMaterial=NatureGlow;l.positionCount=pts.Length;l.SetPositions(pts);l.widthMultiplier=width;l.startColor=l.endColor=c;l.numCornerVertices=2;l.numCapVertices=2;
        }
    }
}

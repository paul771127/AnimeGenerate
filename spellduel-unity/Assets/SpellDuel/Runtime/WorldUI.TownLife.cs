using System;
using UnityEngine;
namespace SpellDuel
{
    public partial class WorldUI
    {
        [Serializable] sealed class TownAmbientAnchor { public string kind; public float x,y,w,h; }
        [Serializable] sealed class TownAmbientLayout { public TownAmbientAnchor[] ambient; }
        static readonly System.Collections.Generic.Dictionary<TownMapData,TownAmbientAnchor[]> townLife=new System.Collections.Generic.Dictionary<TownMapData,TownAmbientAnchor[]>();
        static void ReadTownLife(string json,TownMapData data){townLife[data]=JsonUtility.FromJson<TownAmbientLayout>(json).ambient;}
        static void TownLine(Vector2 a,Vector2 b,float width,Color colour)
        {
            var delta=b-a;var matrix=GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            UiKit.Divider(new Rect(a.x,a.y-width*.5f,delta.magnitude,width),colour);GUI.matrix=matrix;
        }
        static bool ClearOfTownPeople(TownMapData d,Vector2 p)
        {
            foreach(var point in d.npc.Values)if((point-p).sqrMagnitude<150*150)return false;
            foreach(var point in d.label.Values)if(Mathf.Abs(point.x-p.x)<190&&Mathf.Abs(point.y-p.y)<75)return false;
            return true;
        }
        static void DrawTownLife(int town,TownMapData d,float scale,Rect visible,float time)
        {
            if(!townLife.TryGetValue(d,out var anchors)||anchors==null)return;
            GUI.color=Color.white;
            int paths=0;foreach(var anchor in anchors)if(anchor.kind=="path")paths++;
            // The route is authored in map pixels. Pedestrians never register a GUI control.
            if(paths>1)for(int person=0;person<4;person++)
            {
                float travel=Mathf.Repeat(time/9+person*paths/4f,(paths-1)*2);
                bool reverse=travel>paths-1;float q=reverse?(paths-1)*2-travel:travel;
                int segment=Mathf.Min(paths-2,Mathf.FloorToInt(q));float u=Mathf.Clamp01((q-segment)/.8f);
                Vector2 a=Vector2.zero,b=Vector2.zero;int n=0;
                foreach(var anchor in anchors)if(anchor.kind=="path"){if(n==segment)a=new Vector2(anchor.x,anchor.y);if(n==segment+1)b=new Vector2(anchor.x,anchor.y);n++;}
                var at=Vector2.Lerp(a,b,Mathf.SmoothStep(0,1,u));if(!ClearOfTownPeople(d,at))continue;
                var pixel=at*scale;float unit=scale*25;var bounds=new Rect(pixel.x-unit*2,pixel.y-unit*3,unit*4,unit*4);if(!bounds.Overlaps(visible))continue;
                float step=u>=1?0:Mathf.Sin(time*6+person)*unit*.22f;
                var ink=new Color(.12f,.14f,.18f);var cloth=person%2==0?new Color(.42f,.57f,.63f):new Color(.65f,.35f,.23f);
                UiKit.Disc(new Rect(pixel.x-unit*.8f,pixel.y-unit*.15f,unit*1.6f,unit*.45f),new Color(0,0,0,.2f));
                if(town==4&&person<2)
                {
                    var tan=new Color(.65f,.42f,.22f);UiKit.Disc(new Rect(pixel.x-unit,pixel.y-unit*1.45f,unit*2,unit),tan);
                    UiKit.Disc(new Rect(pixel.x-unit*.65f,pixel.y-unit*1.8f,unit*.7f,unit*.65f),tan);
                    float facing=reverse?-1:1;TownLine(pixel+new Vector2(unit*facing,-unit),pixel+new Vector2(unit*1.2f*facing,-unit*2),unit*.3f,tan);
                    UiKit.Disc(new Rect(pixel.x+unit*facing-unit*.25f,pixel.y-unit*2.1f,unit*.6f,unit*.4f),tan);
                    for(int leg=0;leg<4;leg++){float x=pixel.x+(leg-1.5f)*unit*.42f;TownLine(new Vector2(x,pixel.y-unit*.7f),new Vector2(x+step*(leg%2==0?1:-1),pixel.y),unit*.12f,ink);}
                    UiKit.Gem(new Rect(pixel.x+unit*facing,pixel.y-unit*.85f+step*.2f,unit*.22f,unit*.26f),UiKit.Gold);
                }
                else
                {
                    TownLine(pixel+new Vector2(-unit*.22f,-unit*.65f),pixel+new Vector2(-unit*.25f+step,0),unit*.22f,ink);
                    TownLine(pixel+new Vector2(unit*.22f,-unit*.65f),pixel+new Vector2(unit*.25f-step,0),unit*.22f,ink);
                    UiKit.Disc(new Rect(pixel.x-unit*.55f,pixel.y-unit*1.8f,unit*1.1f,unit*1.3f),cloth);
                    UiKit.Disc(new Rect(pixel.x-unit*.33f,pixel.y-unit*2.4f,unit*.66f,unit*.72f),new Color(.86f,.67f,.48f));
                    UiKit.Disc(new Rect(pixel.x-unit*.47f,pixel.y-unit*2.45f,unit*.94f,unit*.34f),new Color(.35f,.26f,.19f));
                    UiKit.Gem(new Rect(pixel.x+(reverse?-1:1)*unit*.3f,pixel.y-unit*1.3f,unit*.35f,unit*.5f),new Color(.46f,.30f,.2f));
                }
            }
            int index=0;
            foreach(var a in anchors)
            {
                int seed=index++;if(a.kind=="path")continue;
                var rect=new Rect(a.x*scale,a.y*scale,a.w*scale,a.h*scale);if(!new Rect(rect.x-60*scale,rect.y-180*scale,rect.width+120*scale,rect.height+240*scale).Overlaps(visible))continue;
                float pulse=.5f+.5f*Mathf.Sin(time*1.8f+seed);
                switch(a.kind)
                {
                    case "chimney":
                        for(int k=0;k<6;k++){float age=Mathf.Repeat(time/5+k/6f+seed*.11f,1);float size=(14+age*42)*scale;var p=rect.center+new Vector2(Mathf.Sin(age*4+seed)*18*scale,-age*150*scale);UiKit.Disc(new Rect(p.x-size/2,p.y-size/2,size,size),new Color(.63f,.66f,.7f,(1-age)*.20f));}break;
                    case "lamp":case "furnace":
                        UiKit.Disc(rect,new Color(1,.65f,.2f,.10f+pulse*.12f));
                        if(a.kind=="furnace"&&town==0)for(int k=0;k<7;k++){float life=Mathf.Repeat(time*.4f+k/7f,1);UiKit.Disc(new Rect(rect.center.x+Mathf.Sin(k*4+life)*rect.width*.4f,rect.y-life*rect.height,3*scale,5*scale),new Color(1,.7f,.24f,1-life));}break;
                    case "leaflamp":
                        var pivot=new Vector2(rect.center.x,rect.y);float sway=Mathf.Sin(time*1.3f+seed)*rect.width*.18f;TownLine(pivot,new Vector2(rect.center.x+sway,rect.yMax),scale*2,new Color(.25f,.34f,.15f));UiKit.Disc(new Rect(rect.x+sway,rect.y+rect.height*.5f,rect.width,rect.height*.6f),new Color(.8f,1,.4f,.18f+pulse*.15f));break;
                    case "water":
                        for(int k=0;k<8;k++){float phase=Mathf.Repeat(time*.13f+k*.137f,1);float y=rect.y+rect.height*phase;float x=rect.x+Hash01(k,seed)*rect.width*.55f;UiKit.Disc(new Rect(x,y,rect.width*(.18f+.08f*Mathf.Sin(time+k)),Mathf.Max(1,2*scale)),new Color(.8f,.97f,1,.16f*Mathf.Sin(phase*Mathf.PI)));}break;
                    case "flag":
                        // Reuse the exact illustrated cloth in four gently flexing strips.
                        for(int strip=0;strip<4;strip++){float sw=rect.width/4,dx=Mathf.Sin(time*2+strip*.6f+seed)*scale*2*strip;var part=new Rect(rect.x+strip*sw,rect.y+dx,sw+.5f,rect.height);GUI.DrawTextureWithTexCoords(part,d.tex,new Rect((a.x+a.w*strip/4)/MapW,1-(a.y+a.h)/MapH,a.w/4/MapW,a.h/MapH),true);}break;
                    case "bell":
                        var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Sin(time*3)*Mathf.Pow(Mathf.Max(0,Mathf.Cos(time*.12f)),12)*5,new Vector2(rect.center.x,rect.y));GUI.DrawTextureWithTexCoords(rect,d.tex,new Rect(a.x/MapW,1-(a.y+a.h)/MapH,a.w/MapW,a.h/MapH),true);GUI.matrix=matrix;break;
                    case "beacon":
                        float angle=time*.35f;var endpoint=rect.center+new Vector2(Mathf.Cos(angle)*rect.width*.5f,Mathf.Sin(angle)*rect.height*.5f);TownLine(rect.center,endpoint,4*scale,new Color(1,.93f,.62f,.18f));UiKit.Disc(new Rect(rect.center.x-5*scale,rect.center.y-5*scale,10*scale,10*scale),new Color(1,.94f,.7f,.5f));break;
                    case "boat":
                        float bob=Mathf.Sin(time*1.6f+seed)*3*scale;var hull=new Rect(rect.x,rect.y+bob+rect.height*.55f,rect.width,rect.height*.35f);UiKit.Disc(hull,new Color(.32f,.21f,.14f));TownLine(hull.center,hull.center-Vector2.up*rect.height*.6f,2*scale,new Color(.28f,.23f,.18f));UiKit.Gem(new Rect(hull.center.x,rect.y+bob,rect.width*.28f,rect.height*.65f),new Color(.92f,.89f,.74f));break;
                    case "windmill":
                        for(int blade=0;blade<4;blade++){float rotation=(time*.5f+blade*Mathf.PI*.5f);var direction=new Vector2(Mathf.Cos(rotation),Mathf.Sin(rotation));TownLine(rect.center+direction*rect.width*.1f,rect.center+direction*rect.width*.48f,rect.width*.10f,new Color(.47f,.33f,.19f,.85f));}UiKit.Disc(new Rect(rect.center.x-4*scale,rect.center.y-4*scale,8*scale,8*scale),new Color(.3f,.23f,.16f));break;
                    case "dust":
                        for(int k=0;k<5;k++){float life=Mathf.Repeat(time*.15f+k*.2f,1);float size=(12+life*26)*scale;UiKit.Disc(new Rect(rect.x+rect.width*k/5,rect.y-life*rect.height,size,size*.6f),new Color(.85f,.67f,.4f,.12f*(1-life)));}break;
                }
            }
        }
    }
}

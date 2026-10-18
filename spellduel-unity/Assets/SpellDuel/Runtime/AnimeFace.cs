using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>人類專用的截面頭模與曲面五官；只在建立時產生，網格與材質均快取。</summary>
    internal static class AnimeFace
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        // 下巴到頭頂的半寬、前臉深度、後腦深度；前臉不使用球體經緯變形。
        static readonly float[] ys = {-.157f,-.140f,-.122f,-.106f,-.085f,-.072f,-.055f,-.030f,-.010f,.013f,.048f,.087f,.123f,.147f,.157f};
        static readonly float[] widths = {.0308f,.050f,.074f,.094f,.116f,.126f,.133f,.139f,.140f,.137f,.133f,.126f,.107f,.067f,.003f};
        static readonly float[] fronts = {.107f,.110f,.112f,.114f,.116f,.119f,.122f,.122f,.120f,.116f,.124f,.118f,.104f,.061f,.004f};
        static readonly float[] backs = {.030f,.050f,.075f,.096f,.118f,.130f,.140f,.148f,.152f,.153f,.151f,.145f,.125f,.087f,.005f};
        static readonly float[] frontAngles={0,7,16,30,48,68,90};
        static float FrontAngle(int column)=>frontAngles[column<0?-column:column]*(column<0?-1:1)*Mathf.Deg2Rad;
        static float Bell(float x,float w) => Mathf.Exp(-x*x/(w*w));
        static void Section(float y,Portraits.Persona p,out float width,out float front,out float back)
        {
            y=Mathf.Clamp(y/p.Length,ys[0],ys[ys.Length-1]);int k=0;
            while(k<ys.Length-2&&y>ys[k+1])k++;
            float t=Mathf.Clamp01((y-ys[k])/(ys[k+1]-ys[k]));
            float jaw=Mathf.Lerp(.70f+.30f*p.Jaw,1,Mathf.Clamp01((y+.12f)/.12f));
            width=Mathf.Lerp(widths[k],widths[k+1],t)*p.Width*jaw;
            front=Mathf.Lerp(fronts[k],fronts[k+1],t);back=Mathf.Lerp(backs[k],backs[k+1],t);
        }
        static float SculptSurface(float x,float y,Portraits.Persona p)
        {
            Section(y,p,out var width,out var front,out _);
            float side=Mathf.Clamp01(Mathf.Abs(x)/Mathf.Max(.001f,width));
            // 平臉面、顴骨轉折、整合鼻梁與鼻尖，嘴周稍往後收。
            var plan=new Portraits.FacePlan(p);
            return front*(1-Mathf.Pow(side,4))+.020f*Bell(x,.016f)*Bell(y-(plan.NoseY+.014f),.017f)
                +.007f*Bell(x,.013f)*Bell(y+.015f,.047f)-.003f*Bell(x,.038f)*Bell(y-plan.MouthY,.014f);
        }
        static bool OnTriangle(float x,float y,Vector3 a,Vector3 b,Vector3 c,out float z)
        {
            float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
            if(Mathf.Abs(den)<.00000001f){z=0;return false;}
            float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den;
            float v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;float w=1-u-v;
            z=u*a.z+v*b.z+w*c.z;return u>=-.00001f&&v>=-.00001f&&w>=-.00001f;
        }
        static Vector3 FrontVertex(int row,int column,Portraits.Persona p)
        {
            float y=ys[row]*p.Length,a=FrontAngle(column);
            Section(y,p,out var width,out _,out _);float x=Mathf.Sin(a)*width;
            return new Vector3(x,y,SculptSurface(x,y,p));
        }
        internal static float Surface(float x,float y,Portraits.Persona p)
        {
            // 取實際三角面而非理想曲面，薄片才不會陷入低面數頭模。
            int row=0;while(row<ys.Length-2&&y>ys[row+1]*p.Length)row++;
            for(int i=-6;i<6;i++)
            {
                var a=FrontVertex(row,i,p);var b=FrontVertex(row,i+1,p);
                var c=FrontVertex(row+1,i,p);var d=FrontVertex(row+1,i+1,p);
                if(OnTriangle(x,y,a,b,c,out var z)||OnTriangle(x,y,b,d,c,out z))return z;
            }
            return SculptSurface(x,y,p);
        }
        internal static Material Material(Color color,int kind=0)
        {
            string key=ColorUtility.ToHtmlStringRGBA(color)+"/"+kind;
            if(materials.TryGetValue(key,out var found)&&found!=null)return found;
            var m=new Material(kind==3?Shader.Find("Sprites/Default"):ToonStyle.LitShader()){color=color,name="Human "+key};
            if(kind!=3&&m.shader.name=="SpellDuel/Toon"){m.EnableKeyword("SD_HUMAN");m.SetFloat("_HumanFace",kind);m.SetFloat("_HumanOutline",kind==1?.60f:.85f);}
            materials[key]=m;return m;
        }
        internal static GameObject Part(Transform parent,string name,Mesh mesh,Vector3 at,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go;
        }
        static Mesh Finish(string key,List<Vector3> v,List<int> t,List<Color> colors,List<Vector2> uv=null)
        {
            var m=new Mesh{name=key};m.SetVertices(v);m.SetTriangles(t,0);m.colors=colors.ToArray();
            if(uv!=null)m.uv=uv.ToArray();m.RecalculateNormals();m.RecalculateBounds();meshes[key]=m;return m;
        }
        internal static Mesh Head(Portraits.Persona p,string identity)
        {
            string key="截面頭型/"+identity;if(meshes.TryGetValue(key,out var mesh))return mesh;
            const int sides=24;var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();var uv=new List<Vector2>();
            for(int j=0;j<ys.Length;j++)for(int i=0;i<=sides;i++)
            {
                float a=i<=6?FrontAngle(i):i<18?(90+(i-6)*15)*Mathf.Deg2Rad:FrontAngle(i-24),y=ys[j]*p.Length;
                Section(y,p,out var width,out _,out var back);float x=Mathf.Sin(a)*width,c=Mathf.Cos(a);
                float z=c>=0?SculptSurface(x,y,p):-back*(-c);
                v.Add(new Vector3(x,y,z));uv.Add(new Vector2(.5f+x/(.32f*p.Width),.5f+y/(.32f*p.Length)));
                float outline=c>0?Mathf.Lerp(.18f,.65f,Mathf.Clamp01(Mathf.Abs(x)/.13f)):.85f;
                colors.Add(new Color(outline,c>0?1:0,0,1));
                if(j==ys.Length-1||i==sides)continue;
                int k=j*(sides+1)+i,n=k+sides+1;t.AddRange(new[]{k,k+1,n,k+1,n+1,n});
            }
            // 封住頂端與下巴，讓背面擠出外框保持封閉。
            for(int cap=0;cap<2;cap++)
            {
                int row=cap==0?0:ys.Length-1,center=v.Count;
                v.Add(new Vector3(0,ys[row]*p.Length,(fronts[row]-backs[row])*.5f));colors.Add(new Color(.18f,0,0,1));uv.Add(new Vector2(.5f,cap));
                for(int i=0;i<sides;i++){int k=row*(sides+1)+i;t.AddRange(cap==0?new[]{center,k+1,k}:new[]{center,k,k+1});}
            }
            mesh=Finish(key,v,t,colors,uv);
            // 接縫法線一致，避免後腦及頭頂外框裂縫。
            var normals=mesh.normals;for(int j=0;j<ys.Length;j++){int k=j*(sides+1);var n=(normals[k]+normals[k+sides]).normalized;normals[k]=normals[k+sides]=n;}mesh.normals=normals;return mesh;
        }
        static Mesh Patch(string identity,Portraits.Persona p,Vector3 anchor,Vector2 center,Vector2 radius,float angle,float depth,Color top,Color bottom,bool almond=false)
        {
            string key=$"薄片/{identity}/{anchor.x}/{anchor.y}/{anchor.z}/{center.x}/{center.y}/{radius.x}/{radius.y}/{angle}/{depth}/{almond}/{ColorUtility.ToHtmlStringRGBA(top)}/{ColorUtility.ToHtmlStringRGBA(bottom)}";
            if(meshes.TryGetValue(key,out var mesh))return mesh;
            var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();float a=angle*Mathf.Deg2Rad,ca=Mathf.Cos(a),sa=Mathf.Sin(a);
            for(int i=-1;i<24;i++)
            {
                float q=i<0?0:i*2*Mathf.PI/24,dx=i<0?0:Mathf.Cos(q)*radius.x;
                float dy=i<0?0:Mathf.Sin(q)*radius.y*(almond?Mathf.Pow(Mathf.Abs(Mathf.Sin(q)),.35f):1);
                float x=center.x+dx*ca-dy*sa,y=center.y+dx*sa+dy*ca;
                // 眼神由上眼皮遮蓋控制，虹膜也裁到同一眼型，避免露出眼線外。
                if(center.y>-.02f&&center.y<.04f&&Mathf.Abs(center.x)>.02f)
                {
                    var plan=new Portraits.FacePlan(p);float ex=center.x<0?-plan.EyeX:plan.EyeX;
                    float nx=Mathf.Clamp((x-ex)/(plan.EyeWidth*.5f),-1,1),cover=p.Lid+(identity=="marksman"&&center.x<0?.04f:0);
                    float upper=plan.EyeY+plan.EyeHeight*.5f*Mathf.Pow(Mathf.Max(0,1-nx*nx),.675f)*(1-cover)+(x-ex)*Mathf.Sin(angle*Mathf.Deg2Rad);
                    y=Mathf.Min(y,upper);
                }
                v.Add(new Vector3(x,y,Surface(x,y,p)+depth)-anchor);colors.Add(Color.Lerp(top,bottom,Mathf.Clamp01(.5f-dy/Mathf.Max(.001f,radius.y*2))));
            }
            for(int i=1;i<=24;i++)t.AddRange(new[]{0,i,i==24?1:i+1});return Finish(key,v,t,colors);
        }
        static void Paint(Transform parent,string label,string identity,Portraits.Persona p,Vector3 anchor,Vector2 center,Vector2 radius,float angle,float depth,Color top,Color bottom,bool almond=false)
            =>Part(parent,label,Patch(identity,p,anchor,center,radius,angle,depth,top,bottom,almond),Vector3.zero,Material(Color.white,3));
        static void Stroke(Transform parent,string identity,Portraits.Persona p,Vector3 anchor,Vector2 a,Vector2 b,float width,Color color,float depth=.0025f)
            =>Part(parent,"曲面線 "+identity,LineMesh(identity,p,anchor,a,b,width,color,depth),Vector3.zero,Material(Color.white,3));
        static Mesh LineMesh(string identity,Portraits.Persona p,Vector3 anchor,Vector2 a,Vector2 b,float width,Color color,float depth)
        {
            string key=$"線/{identity}/{a.x}/{a.y}/{b.x}/{b.y}/{width}/{anchor.x}/{anchor.y}/{anchor.z}/{depth}";
            if(!meshes.TryGetValue(key,out var mesh))
            {
                var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
                var d=b-a;float len=Mathf.Max(.0001f,Mathf.Sqrt(d.x*d.x+d.y*d.y));var normal=new Vector2(-d.y/len,d.x/len)*width*.5f;
                for(int i=0;i<=8;i++)for(int side=-1;side<=1;side+=2){var q=a+d*(i/8f)+normal*side;v.Add(new Vector3(q.x,q.y,Surface(q.x,q.y,p)+depth)-anchor);colors.Add(color);}
                for(int i=0;i<8;i++){int k=i*2;t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}mesh=Finish(key,v,t,colors);
            }
            return mesh;
        }
        internal static void Detail(Transform head,string identity,Portraits.Persona p,Vector2 a,Vector2 b,float width,Color color)
            =>Stroke(head,identity,p,Vector3.zero,a,b,width,color,.004f);
        internal sealed class Features
        {
            internal readonly List<Transform> Eyes=new List<Transform>(),Lids=new List<Transform>();
            internal Transform Mouth;
        }
        internal static Features Build(Transform head,string identity,Portraits.Persona p,Color skin,Color brow,Color iris,bool precise=false,bool elder=false,bool elf=false)
        {
            var plan=new Portraits.FacePlan(p);var result=new Features();Part(head,"立面頭型",Head(p,identity),Vector3.zero,Material(skin,1));
            Color ink=new Color(.13f,.07f,.12f),white=new Color(1,.97f,.90f);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*plan.EyeX,eyeHeight=plan.EyeHeight*.5f,angle=side*p.Tilt;float cover=p.Lid+(precise&&side<0?.04f:0);
                var anchor=new Vector3(x,.013f,.118f);var eye=new GameObject("EyePaint").transform;eye.SetParent(head,false);eye.localPosition=anchor;result.Eyes.Add(eye);
                Paint(eye,"眼白",identity,p,anchor,new Vector2(x,.013f),new Vector2(plan.EyeWidth*.5f,eyeHeight),angle,.0015f,white,white,true);
                float gx=x+p.Gaze*.018f,gy=plan.EyeY-p.Pitch*.00015f;
                Paint(eye,"虹膜漸層",identity,p,anchor,new Vector2(gx,gy),new Vector2(plan.EyeWidth*.235f,eyeHeight*.88f),angle,.0022f,Color.Lerp(iris,ink,.62f),Color.Lerp(iris,white,.40f));
                Paint(eye,"瞳孔",identity,p,anchor,new Vector2(gx,gy+.001f),new Vector2(plan.EyeWidth*.09f*p.Pupil,eyeHeight*.61f),angle,.0029f,ink,ink);
                Paint(eye,"主高光",identity,p,anchor,new Vector2(gx-.004f,gy+eyeHeight*.34f),new Vector2(.0035f,.0035f),0,.0035f,white,white);
                Paint(eye,"副高光",identity,p,anchor,new Vector2(gx+.004f,gy-eyeHeight*.34f),new Vector2(.002f,.002f),0,.0035f,white,white);
                var lidAnchor=new Vector3(x,.037f,.122f);var lid=new GameObject("UpperLid").transform;lid.SetParent(head,false);lid.localPosition=lidAnchor;result.Lids.Add(lid);
                float half=plan.EyeWidth*.5f,tilt=Mathf.Sin(angle*Mathf.Deg2Rad)*half;
                float lidY=plan.EyeY+eyeHeight*(1-cover);
                Stroke(lid,identity,p,lidAnchor,new Vector2(x-half,plan.EyeY-tilt),new Vector2(x,lidY),.0035f,ink,.004f);
                Stroke(lid,identity,p,lidAnchor,new Vector2(x,lidY),new Vector2(x+half,plan.EyeY+tilt),.0030f,ink,.004f);
                Stroke(head,identity,p,Vector3.zero,new Vector2(x-half*.8f,plan.EyeY-eyeHeight*.8f),new Vector2(x+half*.8f,plan.EyeY-eyeHeight*.8f),.001f,Color.Lerp(skin,ink,.26f));
                float inner=x-side*half,outer=inner+side*plan.EyeWidth*1.1f;
                float by=plan.BrowY+(side>0?p.Asym*.0006f:0),bend=p.Brow*Mathf.Deg2Rad*plan.EyeWidth;
                Stroke(head,identity,p,Vector3.zero,new Vector2(inner,by),new Vector2(x,by+.0015f+bend*.5f),elder?.005f:.0035f,brow);
                Stroke(head,identity,p,Vector3.zero,new Vector2(x,by+.0015f+bend*.5f),new Vector2(outer,by+bend),.003f,brow);
            }
            // 鼻子融入頭模，只留一筆暖色鼻底，不再另外黏三角錐。
            Stroke(head,identity,p,Vector3.zero,new Vector2(-.007f,plan.NoseY),new Vector2(.008f,plan.NoseY-.001f),.0018f,Color.Lerp(skin,new Color(.65f,.29f,.26f),.45f));
            float corner=plan.MouthY+p.Smile*.004f,mw=plan.MouthWidth*.5f;
            Stroke(head,identity,p,Vector3.zero,new Vector2(-mw,corner),new Vector2(0,plan.MouthY),.0025f,new Color(.53f,.24f,.25f));
            Stroke(head,identity,p,Vector3.zero,new Vector2(0,plan.MouthY),new Vector2(mw,corner+p.Asym*.0004f),.0020f,new Color(.65f,.32f,.31f));
            // 單位薄片保留原有 mouthOpen 縮放契約，施法時仍可張嘴。
            if(!meshes.TryGetValue("張嘴單位片",out var mouth))
            {
                var v=new List<Vector3>{Vector3.zero};var t=new List<int>();var colors=new List<Color>{Color.white};
                for(int i=0;i<24;i++){float a=i*2*Mathf.PI/24;v.Add(new Vector3(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f,0));colors.Add(Color.white);}
                for(int i=1;i<=24;i++)t.AddRange(new[]{0,i,i==24?1:i+1});mouth=Finish("張嘴單位片",v,t,colors);
            }
            result.Mouth=Part(head,"mouthOpen",mouth,new Vector3(0,plan.MouthY,Surface(0,plan.MouthY,p)+.003f),Material(new Color(.30f,.10f,.14f),3)).transform;
            result.Mouth.localScale=new Vector3(plan.MouthWidth,.001f+.016f*p.Open,.003f);
            if(p.Open>0)Paint(head,"笑容齒線",identity,p,Vector3.zero,new Vector2(0,plan.MouthY+.003f),new Vector2(plan.MouthWidth*.36f,.002f),0,.004f,white,white);
            Ears(head,identity,p,skin,elf);return result;
        }
        static void Ears(Transform head,string identity,Portraits.Persona p,Color skin,bool elf)
        {
            var plan=new Portraits.FacePlan(p);float h=(plan.EarTop-plan.EarBottom)*.5f,cy=(plan.EarTop+plan.EarBottom)*.5f;
            for(int side=-1;side<=1;side+=2)
            {
                string key="耳廓/"+identity+"/"+side;
                if(!meshes.TryGetValue(key,out var mesh))
                {
                    var v=new List<Vector3>{new Vector3(side*(.14f*p.Width+.007f),cy+h,-.025f)};var t=new List<int>();var c=new List<Color>{Color.white};
                    for(int j=1;j<6;j++)for(int i=0;i<8;i++)
                    {
                        float theta=j*Mathf.PI/6,a=i*Mathf.PI/4;
                        float x=Mathf.Sin(theta)*Mathf.Cos(a)*.019f;
                        if(elf&&j==2&&i==(side>0?0:4))x*=1.4f;
                        v.Add(new Vector3(side*(.14f*p.Width+.007f)+x,cy+Mathf.Cos(theta)*h,-.025f+Mathf.Sin(theta)*Mathf.Sin(a)*.017f));c.Add(Color.white);
                    }
                    int bottom=v.Count;v.Add(new Vector3(side*(.14f*p.Width+.007f),cy-h,-.025f));c.Add(Color.white);
                    for(int i=0;i<8;i++)
                    {
                        int n=(i+1)%8;t.AddRange(new[]{0,1+n,1+i,bottom,33+i,33+n});
                        for(int j=0;j<4;j++){int a=1+j*8+i,b=1+j*8+n;t.AddRange(new[]{a,b,a+8,b,b+8,a+8});}
                    }
                    mesh=Finish(key,v,t,c);
                }
                Part(head,"解剖耳廓",mesh,Vector3.zero,Material(skin,2));
            }
        }
        internal static void Hair(Transform parent,string identity,Portraits.Persona p,Color color)
        {
            // 六束髮片合成一個 Renderer，高光帶再合一個；僅第一次建立網格。
            string lockKey="整組髮束/"+identity,shineKey="整組髮光/"+identity;
            if(meshes.TryGetValue(lockKey,out var locksMesh)&&meshes.TryGetValue(shineKey,out var shineMesh))
            {Part(parent,"成束瀏海",locksMesh,Vector3.zero,Material(color));Part(parent,"天使環",shineMesh,Vector3.zero,Material(Color.white,3));return;}
            var locks=new List<CombineInstance>();var shine=new List<CombineInstance>();
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                string key=$"髮束/{identity}/{side}/{i}";float x=side*(.035f+i*.036f),y=.113f-i*.009f;
                if(!meshes.TryGetValue(key,out var mesh))
                {
                    var v=new List<Vector3>{new Vector3(x-.026f,y+.028f,.073f),new Vector3(x+.024f,y+.027f,.072f),new Vector3(x+.018f,y-.008f,.123f),new Vector3(x+side*.008f,y-.062f,.124f),new Vector3(x-.025f,y-.009f,.123f),new Vector3(x,y+.009f,.145f)};
                    var t=new List<int>{0,1,5,1,2,5,2,3,5,3,4,5,4,0,5,0,4,3,0,3,2,0,2,1};
                    for(int j=0;j<t.Count;j+=3){int swap=t[j+1];t[j+1]=t[j+2];t[j+2]=swap;}
                    mesh=Finish(key,v,t,new List<Color>{Color.white,Color.white,Color.white,Color.white,Color.white,Color.white});
                }
                locks.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});
                string glintKey=key+"/高光";
                if(!meshes.TryGetValue(glintKey,out var glint))
                {
                    var gv=new List<Vector3>();var gt=new List<int>();var gc=new List<Color>();var vertices=mesh.vertices;
                    for(int k=0;k<=8;k++)for(int side0=-1;side0<=1;side0+=2)
                    {
                        float gx=x-.012f+k*.022f/8,gy=y+.005f+side0*.002f,gz=.12f;
                        for(int f=0;f<15;f+=3)if(OnTriangle(gx,gy,vertices[mesh.triangles[f]],vertices[mesh.triangles[f+1]],vertices[mesh.triangles[f+2]],out var hit))gz=Mathf.Max(gz,hit);
                        gv.Add(new Vector3(gx,gy,gz+.0008f));gc.Add(Color.Lerp(color,Color.white,.45f));
                    }
                    for(int k=0;k<8;k++){int at=k*2;gt.AddRange(new[]{at,at+2,at+1,at+1,at+2,at+3});}
                    glint=Finish(glintKey,gv,gt,gc);
                }
                shine.Add(new CombineInstance{mesh=glint,transform=Matrix4x4.identity});
            }
            locksMesh=new Mesh{name=lockKey};locksMesh.CombineMeshes(locks.ToArray(),true,true);locksMesh.RecalculateBounds();meshes[lockKey]=locksMesh;
            shineMesh=new Mesh{name=shineKey};shineMesh.CombineMeshes(shine.ToArray(),true,true);shineMesh.RecalculateBounds();meshes[shineKey]=shineMesh;
            Part(parent,"成束瀏海",locksMesh,Vector3.zero,Material(color));Part(parent,"天使環",shineMesh,Vector3.zero,Material(Color.white,3));
        }
    }
}

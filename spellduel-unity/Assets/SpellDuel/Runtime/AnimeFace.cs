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
        static readonly float[] ys = {-0.15700f,-0.14000f,-0.12200f,-0.11500f,-0.11000f,-0.10600f,-0.10100f,-0.09600f,-0.08500f,-0.08100f,-0.07200f,-0.06500f,-0.05500f,-0.04800f,-0.04000f,-0.03000f,-0.01000f,0.01300f,0.04800f,0.08700f,0.12300f,0.14700f,0.15700f};
        static readonly float[] fittingWidths = {0.03080f,0.05000f,0.07400f,0.08275f,0.08900f,0.09400f,0.09924f,0.10448f,0.11600f,0.11908f,0.12600f,0.12888f,0.13300f,0.13468f,0.13660f,0.13900f,0.14000f,0.13700f,0.13300f,0.12600f,0.10700f,0.06700f,0.00300f};
        static readonly float[] widths = {0.009000f,0.051479f,0.071005f,0.076529f,0.079996f,0.082521f,0.085400f,0.087999f,0.092840f,0.094330f,0.097204f,0.099011f,0.100985f,0.101960f,0.102680f,0.103000f,0.102500f,0.101500f,0.099000f,0.093000f,0.080000f,0.047000f,0.003000f};
        // Convex Bezier jaw: outward cheek arcs end in a short, rounded point.
        static readonly float[] feminineWidths = {0.005000f,0.041486f,0.062515f,0.068839f,0.072905f,0.075918f,0.079412f,0.082624f,0.088784f,0.090736f,0.094593f,0.097096f,0.099926f,0.101379f,0.102488f,0.103000f,0.102500f,0.101500f,0.099000f,0.093000f,0.080000f,0.047000f,0.003000f};
        static readonly float[] fronts = {0.08600f,0.09300f,0.10100f,0.10688f,0.11100f,0.11400f,0.11448f,0.11495f,0.11600f,0.11692f,0.11900f,0.12024f,0.12200f,0.12200f,0.12200f,0.12200f,0.12000f,0.11600f,0.12400f,0.11800f,0.10400f,0.06100f,0.00400f};
        static readonly float[] backs = {0.03000f,0.05000f,0.07500f,0.08419f,0.09075f,0.09600f,0.10124f,0.10648f,0.11800f,0.12169f,0.13000f,0.13412f,0.14000f,0.14224f,0.14480f,0.14800f,0.15200f,0.15300f,0.15100f,0.14500f,0.12500f,0.08700f,0.00500f};
        static readonly float[] meshYs=HeadRows();
        static float[] HeadRows(){var rows=new float[43];rows[0]=-.157f;rows[1]=-.155f;rows[2]=-.152f;for(int i=3;i<43;i++)rows[i]=Mathf.Lerp(-.148f,.157f,(i-3)/39f);return rows;}
        static readonly float[] frontAngles={0,7,16,30,48,68,90};
        static float FrontAngle(int column)=>frontAngles[column<0?-column:column]*(column<0?-1:1)*Mathf.Deg2Rad;
        static float Bell(float x,float w) => Mathf.Exp(-x*x/(w*w));
        static float ChinBlend(float normalizedY,Portraits.Persona p)
        {
            if(p.Masculine||p.Elder)return 0;
            float t=Mathf.Clamp01((-.118f-normalizedY)/.039f);return t*t*(3-2*t);
        }
        static void Section(float y,Portraits.Persona p,out float width,out float front,out float back)
        {
            y=Mathf.Clamp(y/p.Length,ys[0],ys[ys.Length-1]);int k=0;
            while(k<ys.Length-2&&y>ys[k+1])k++;
            float t=Mathf.Clamp01((y-ys[k])/(ys[k+1]-ys[k]));
            float jaw=Mathf.Lerp(.70f+.30f*p.Jaw,1,Mathf.Clamp01((y+.12f)/.12f));
            float lower=Mathf.Clamp01((-.045f-y)/.112f);lower=lower*lower*(3-2*lower);
            bool mature=p.Masculine||p.Elder;float pointed=1-(mature?.10f:0)*lower;
            width=Profile(mature?widths:feminineWidths,k,t)*p.Length*Mathf.Lerp(.984f,1.012f,(p.Width-.95f)/.10f)*jaw*pointed;

            front=Profile(fronts,k,t);back=Mathf.Lerp(Profile(backs,k,t),-.072f,ChinBlend(y,p));
        }
        static float SculptSurface(float x,float y,Portraits.Persona p)
        {
            Section(y,p,out var width,out var front,out _);
            float side=Mathf.Clamp01(Mathf.Abs(x)/Mathf.Max(.001f,width));
            // 平臉面、顴骨轉折、整合鼻梁與鼻尖，嘴周稍往後收。
            var plan=new Portraits.FacePlan(p);
            // Low bridge and tiny integrated tip; no protruding nose cone or lip ridge.
            float nose=.006f*Bell(x,.010f)*Bell(y-(plan.NoseY+.006f),.009f)
                +.0015f*Bell(x,.009f)*Bell(y-(plan.NoseY+.020f),.019f)
                -.001f*Bell(x,.013f)*Bell(y-(plan.NoseY-.004f),.005f);
            float lips=.0012f*Bell(x,plan.MouthWidth*.6f)*Bell(y-(plan.MouthY-.003f),.005f)
                -.001f*Bell(x,.023f)*Bell(y-(plan.MouthY-.013f),.006f);
            float offset=.075f*ChinBlend(y/p.Length,p);
            return offset+(front-offset)*(1-Mathf.Pow(side,4))+nose+lips;
        }
        // Monotone cubic: a continuous tangent through cheek/jaw sections, without overshoot.
        static float Profile(float[] values,int k,float t)
        {
            float h=ys[k+1]-ys[k],d=(values[k+1]-values[k])/h;
            float left=k==0?d:(values[k]-values[k-1])/(ys[k]-ys[k-1]);
            float right=k==ys.Length-2?d:(values[k+2]-values[k+1])/(ys[k+2]-ys[k+1]);
            float a=left*d<=0?0:2*left*d/(left+d),b=right*d<=0?0:2*right*d/(right+d);
            return (2*t*t*t-3*t*t+1)*values[k]+(t*t*t-2*t*t+t)*h*a
                +(-2*t*t*t+3*t*t)*values[k+1]+(t*t*t-t*t)*h*b;
        }
        // Original head coordinates are used ONLY by sizing and existing fringe anchors.
        // This keeps every accepted hair/hat mesh unchanged as the visible face narrows.
        static readonly Dictionary<string,Vector3[]> fittingPoints=new Dictionary<string,Vector3[]>();
        static void FittingSection(float y,Portraits.Persona p,out float width,out float front,out float back)
        {
            y=Mathf.Clamp(y/p.Length,ys[0],ys[ys.Length-1]);int k=0;
            while(k<ys.Length-2&&y>ys[k+1])k++;
            float t=Mathf.Clamp01((y-ys[k])/(ys[k+1]-ys[k]));
            float jaw=Mathf.Lerp(.70f+.30f*p.Jaw,1,Mathf.Clamp01((y+.12f)/.12f));
            width=Mathf.Lerp(fittingWidths[k],fittingWidths[k+1],t)*p.Width*jaw;
            if(p.Story&&y<-.030f)width=Mathf.Max(.003f,.14f*Mathf.Pow(Mathf.Clamp01((y+.157f)/.127f),.72f)*p.Width);
            front=Mathf.Lerp(fronts[k],fronts[k+1],t);back=Mathf.Lerp(backs[k],backs[k+1],t);
        }
        static float FittingSculpt(float x,float y,Portraits.Persona p)
        {
            FittingSection(y,p,out var width,out var front,out _);
            float side=Mathf.Clamp01(Mathf.Abs(x)/Mathf.Max(.001f,width));
            // 平臉面、顴骨轉折、整合鼻梁與鼻尖，嘴周稍往後收。
            float d=.013f+.157f*p.Length,noseY=.013f-.50f*d,mouthY=.013f-.70f*d;
            // Reference only: retain the old fringe attachment, never render this surface.
            float lower=Mathf.Clamp01((-.030f-y)/.012f);
            float tip=noseY+.009f;
            float nose=.025f*Bell(x,.014f)*Bell(y-tip,.011f)
                +.004f*Bell(x,.011f)*Bell(y-(tip+.025f),.024f)
                -.003f*Bell(x,.016f)*Bell(y-(noseY-.005f),.005f)
                -.010f*Bell(x,.020f)*Bell(y-(noseY+.035f),.020f);
            float upper=(Bell(x-.008f,.012f)+Bell(x+.008f,.012f))*.0026f;
            float lips=upper*Bell(y-(mouthY+.004f),.004f)
                +.005f*Bell(x,.026f)*Bell(y-(mouthY-.005f),.0045f)
                -.0015f*Bell(x,.030f)*Bell(y-mouthY,.002f)
                -.002f*Bell(x,.030f)*Bell(y-(mouthY-.015f),.006f);
            float legacy=.020f*Bell(x,.016f)*Bell(y-(noseY+.014f),.017f)
                +.007f*Bell(x,.013f)*Bell(y+.015f,.047f)-.003f*Bell(x,.038f)*Bell(y-mouthY,.014f);
            // Legacy fringe anchor below the eyelid.
            float acceptedNose=.025f*Bell(x,.014f)*Bell(y-tip,.011f)
                +.004f*Bell(x,.011f)*Bell(y-(tip+.025f),.024f)
                -.003f*Bell(x,.016f)*Bell(y-(noseY-.005f),.005f);
            float acceptedLips=.0015f*Bell(x,.028f)*Bell(y-(mouthY-.003f),.006f)
                -.0015f*Bell(x,.030f)*Bell(y-(mouthY-.015f),.006f);
            float accepted=Mathf.Lerp(legacy,acceptedNose+acceptedLips,lower);
            float detail=Mathf.Lerp(accepted,nose+lips,Mathf.Clamp01((-.040f-y)/.012f));
            return front*(1-Mathf.Pow(side,4))+detail*(p.SoftNose?.18f:1);
        }
        // Construction-only legacy envelope. Narrowing the face must not auto-widen the
        // whole fitted head (including hair/hat) or change the accepted body/head proportions.
        internal static Vector3[] FittingVertices(Portraits.Persona p)
        {
            string key=$"{p.Width}/{p.Jaw}/{p.Length}/{p.Story}/{p.SoftNose}";
            if(fittingPoints.TryGetValue(key,out var cached))return cached;
            var points=new Vector3[ys.Length*25];
            for(int row=0;row<ys.Length;row++)for(int i=0;i<=24;i++)
            {
                float y=ys[row],a=i<=6?FrontAngle(i):i<18?(90+(i-6)*15)*Mathf.Deg2Rad:FrontAngle(i-24);
                float jaw=Mathf.Lerp(.70f+.30f*p.Jaw,1,Mathf.Clamp01((y+.12f)/.12f));
                float w=fittingWidths[row]*p.Width*jaw;
                if(p.Story&&y<-.030f)w=Mathf.Max(.003f,.14f*Mathf.Pow(Mathf.Clamp01((y+.157f)/.127f),.72f)*p.Width);
                float x=Mathf.Sin(a)*w,c=Mathf.Cos(a);
                points[row*25+i]=new Vector3(x,y*p.Length,c>=0?FittingSculpt(x,y*p.Length,p):-backs[row]*(-c));
            }
            fittingPoints[key]=points;return points;
        }
        internal static float HairSeat(float x,float y,Portraits.Persona p)
        {
            var points=FittingVertices(p);int row=0;while(row<ys.Length-2&&y>ys[row+1]*p.Length)row++;
            for(int i=-6;i<6;i++)
            {
                int left=i<0?24+i:i,right=i+1<0?25+i:i+1;
                var a=points[row*25+left];var b=points[row*25+right];var c=points[(row+1)*25+left];var d=points[(row+1)*25+right];
                if(OnTriangle(x,y,a,b,c,out var z)||OnTriangle(x,y,b,d,c,out z))return z;
            }
            return FittingSculpt(x,y,p);
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
            float y=meshYs[row]*p.Length,a=FrontAngle(column);
            Section(y,p,out var width,out _,out _);float x=Mathf.Sin(a)*width;
            float z=SculptSurface(x,y,p);return new Vector3(x,UpperY(y,p)+Dome(x,y,z,p),z);
        }
        internal static float Surface(float x,float y,Portraits.Persona p)
        {
            // 取實際三角面而非理想曲面，薄片才不會陷入低面數頭模。
            int row=0;while(row<meshYs.Length-2&&y>UpperY(meshYs[row+1]*p.Length,p))row++;
            int first=y>UpperY(.070f*p.Length,p)?Mathf.Max(0,row-3):row;
            for(int r=first;r<=row;r++)for(int i=-6;i<6;i++)
            {
                var a=FrontVertex(r,i,p);var b=FrontVertex(r,i+1,p);
                var c=FrontVertex(r+1,i,p);var d=FrontVertex(r+1,i+1,p);
                if(OnTriangle(x,y,a,b,c,out var z)||OnTriangle(x,y,b,d,c,out z))return z;
            }
            return SculptSurface(x,y,p);
        }
        // Physical upper-head correction. It is applied AFTER the old body/head fitting,
        // so automatic height normalisation cannot stretch the compact head tall again.
        internal static float UpperSeam(Portraits.Persona p)
        {var plan=new Portraits.FacePlan(p);return plan.BrowY+plan.EyeWidth*.18f;}
        internal static float UpperY(float y,Portraits.Persona p)
        {float seam=UpperSeam(p);return y<=seam?y:seam+(y-seam)*.65f;}
        internal static float Dome(float x,float originalY,float z,Portraits.Persona p)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.070f*p.Length,.145f*p.Length,originalY));
            float radius=x*x/(.100f*.100f*p.Width*p.Width)+(z+.009f)*(z+.009f)/(.120f*.120f);
            return .014f*p.Length*t*Mathf.Exp(-1.4f*radius);
        }
        static float CrownCrest(float x,float y,float z,Portraits.Persona p)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.100f*p.Length,.160f*p.Length,y));
            float r=x*x/(.055f*.055f*p.Width*p.Width)+(z+.009f)*(z+.009f)/(.085f*.085f);
            return .018f*p.Length*t*Mathf.Exp(-1.6f*r);
        }
        internal static float HeadwearDrop(Portraits.Persona p)=>UpperY(.177f*p.Length,p)+Dome(0,.177f*p.Length,-.009f,p)+CrownCrest(0,.177f*p.Length,-.009f,p)-.177f*p.Length;
        internal static Mesh CompactPresentationMesh(Mesh source,string identity,Portraits.Persona p,int kind)
        {
            string key="Compact upper/"+identity+"/"+kind+"/"+source.GetInstanceID();if(meshes.TryGetValue(key,out var found))return found;
            var mesh=Object.Instantiate(source);mesh.name=key;var vertices=mesh.vertices;float seam=UpperSeam(p),drop=HeadwearDrop(p);
            for(int i=0;i<vertices.Length;i++)
            {
                var v=vertices[i];float y=v.y;
                if(kind==2&&identity!="艾琳")v.y=y+drop; // Rigid hats move to the hair; their design stays intact.
                else
                {
                    v.y=UpperY(y,p)+Dome(v.x,y,v.z,p);
                    if(kind==1)v.y+=CrownCrest(v.x,y,v.z,p)-.015f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(seam+.020f,.160f*p.Length,y)));
                }
                vertices[i]=v;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;return mesh;
        }
        internal static float[] Outline(Portraits.Persona p)
        {
            var points=new List<float>();
            for(int side=-1;side<=1;side+=2)for(int step=0;step<=88;step++)
            {
                float y=Mathf.Lerp(ys[0],ys[ys.Length-1],side<0?1-step/88f:step/88f)*p.Length;
                Section(y,p,out var w,out _,out _);points.Add(side*w*3);points.Add((UpperY(y,p)+Dome(side*w,y,0,p))*3+.09f);
            }
            return points.ToArray();
        }
        // Old rigs fit hats/hair with a nonuniform head scale. Compensate only facial
        // children, once, so their visible oval has the same aspect as the 2D portrait.
        internal static void AlignOval(Transform head,string identity,Portraits.Persona p)
        {
            if(head.Find("Oval facial frame")!=null)return;
            var parts=new List<Transform>();
            for(int i=0;i<head.childCount;i++)
            {
                var child=head.GetChild(i);
                string n=child.gameObject.name;
                if(n=="立面頭型"||n=="EyePaint"||n=="UpperLid"||n=="BrowPivot"||n=="HumanLip"||n=="mouthOpen"
                    ||n=="解剖耳廓"||n=="Soft cheek blush"||n.StartsWith("曲面線 ")||n=="Clerk round spectacles"
                    ||n=="Pointed beard"||n=="Moustache"||n=="Golden earring")parts.Add(child);
            }
            var frame=new GameObject("Oval facial frame").transform;frame.SetParent(head,false);
            foreach(var part in parts)part.SetParent(frame,false);
            var fitted=head.lossyScale;frame.localScale=new Vector3(fitted.y/Mathf.Max(.001f,fitted.x),1,1);
            foreach(var filter in head.GetComponentsInChildren<MeshFilter>(true))
            {
                string n=filter.gameObject.name;
                int kind=n=="立面頭型"?0:n=="整體髮型"?1:n.StartsWith("Headwear fitted ")?2:-1;
                if(kind>=0)filter.sharedMesh=CompactPresentationMesh(filter.sharedMesh,identity,p,kind);
            }
            var halo=head.Find("Halo");if(halo!=null)halo.localPosition+=Vector3.up*HeadwearDrop(p);
        }
        // Reference-based skin palette, shared by face, exposed body and portrait.
        internal static Color SkinFor(string identity)
        {
            string id=Portraits.Key(identity);
            return CharacterLook.Hex(id=="澪"?"cdebf5":id=="艾琳"?"f7d8cc":id=="凪"?"f7d8ca":id=="翠"?"f3ddcf":
                id=="海斗"?"f1cebb":id=="老焰"?"edc6aa":id=="小葉"?"f8ddce":id=="鐵匠"?"e6bea4":
                id=="阿潮"?"e7c5b0":id=="教官"?"ecc9b4":id=="塔守"?"e9d4c8":"f4d2bf");
        }
        internal static Material Material(Color color,int kind=0,string identity=null)
        {
            string key=ColorUtility.ToHtmlStringRGBA(color)+"/"+kind+(identity??"");
            if(materials.TryGetValue(key,out var found)&&found!=null)return found;
            var m=new Material(kind==3?Shader.Find("Sprites/Default"):ToonStyle.LitShader()){color=color,name="Human "+key};
            if(kind!=3&&m.shader.name=="SpellDuel/Toon"){m.EnableKeyword("SD_HUMAN");m.SetFloat("_HumanFace",kind==4?0:kind);m.SetFloat("_HumanHair",kind==4?1:0);m.SetFloat("_HumanOutline",kind==1?.60f:.85f);}
            if(kind==1&&identity!=null)
            {
                var look=CharacterLook.Get(identity);m.SetFloat("_HumanPart",look.Part);m.SetFloat("_HumanWave",look.Seed*.71f);
                m.SetFloat("_HumanFringe",look.Hair==CharacterLook.HairKind.Crew||look.Hair==CharacterLook.HairKind.Slick?.82f:look.Hair==CharacterLook.HairKind.Bob?.65f:.71f);
            }
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
            for(int j=0;j<meshYs.Length;j++)for(int i=0;i<=sides;i++)
            {
                float a=i<=6?FrontAngle(i):i<18?(90+(i-6)*15)*Mathf.Deg2Rad:FrontAngle(i-24),y=meshYs[j]*p.Length;
                Section(y,p,out var width,out _,out var back);float x=Mathf.Sin(a)*width,c=Mathf.Cos(a);
                float offset=.075f*ChinBlend(y/p.Length,p);
                float z=c>=0?SculptSurface(x,y,p):offset-(back+offset)*(-c);
                v.Add(new Vector3(x,y,z));uv.Add(new Vector2(.5f+x/(.32f*p.Width),.5f+y/(.32f*p.Length)));
                float outline=c>0?Mathf.Lerp(.18f,.65f,Mathf.Clamp01(Mathf.Abs(x)/.13f)):.85f;
                colors.Add(new Color(outline,c>0?1:0,0,1));
                if(j==meshYs.Length-1||i==sides)continue;
                int k=j*(sides+1)+i,n=k+sides+1;t.AddRange(new[]{k,k+1,n,k+1,n+1,n});
            }
            // 封住頂端與下巴，讓背面擠出外框保持封閉。
            for(int cap=0;cap<2;cap++)
            {
                int row=cap==0?0:meshYs.Length-1,center=v.Count;
                Section(meshYs[row]*p.Length,p,out _,out var capFront,out var capBack);v.Add(new Vector3(0,meshYs[row]*p.Length,(capFront-capBack)*.5f));colors.Add(new Color(.18f,0,0,1));uv.Add(new Vector2(.5f,cap));
                for(int i=0;i<sides;i++){int k=row*(sides+1)+i;t.AddRange(cap==0?new[]{center,k+1,k}:new[]{center,k,k+1});}
            }
            mesh=Finish(key,v,t,colors,uv);
            // 接縫法線一致，避免後腦及頭頂外框裂縫。
            var normals=mesh.normals;for(int j=0;j<meshYs.Length;j++){int k=j*(sides+1);var n=(normals[k]+normals[k+sides]).normalized;normals[k]=normals[k+sides]=n;}mesh.normals=normals;return mesh;
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
                v.Add(new Vector3(x,y,Surface(x,y,p)+depth)-anchor);var tint=Color.Lerp(top,bottom,Mathf.Clamp01(.5f-dy/Mathf.Max(.001f,radius.y*2)));if(identity.Contains("/blush/"))tint.a=i<0?.07f:0;colors.Add(tint);
            }
            for(int i=1;i<=24;i++)t.AddRange(new[]{0,i,i==24?1:i+1});return Finish(key,v,t,colors);
        }
        static void Paint(Transform parent,string label,string identity,Portraits.Persona p,Vector3 anchor,Vector2 center,Vector2 radius,float angle,float depth,Color top,Color bottom,bool almond=false)
            =>Part(parent,label,Patch(identity,p,anchor,center,radius,angle,depth,top,bottom,almond),Vector3.zero,Material(Color.white,3));
        static void Stroke(Transform parent,string identity,Portraits.Persona p,Vector3 anchor,Vector2 a,Vector2 b,float width,Color color,float depth=.0046f)
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
            internal MeshFilter Lip;internal readonly List<Transform> Brows=new List<Transform>();
            internal HumanExpressionMotion Motion;
        }
        internal static Color BrowColor(string identity)
        {return Color.Lerp(new Color(.15f,.08f,.12f),CharacterLook.Get(identity).HairColor,.32f);}
        internal static float BrowThickness(Portraits.Persona p)=>p.Elder?.0068f:p.Masculine?.0048f:.0032f;
        internal static float BrowCurve(float t,Portraits.Persona p)
        {
            float arch=p.Elder?.0022f:p.Masculine?.0012f:.0035f;
            float bend=p.Elder?-.004f:p.Masculine?0:(p.Brow*.25f-1)*Mathf.Deg2Rad*new Portraits.FacePlan(p).EyeWidth;
            return arch*Mathf.Sin(t*Mathf.PI)+bend*t;
        }
        static Mesh BrowMesh(string identity,Portraits.Persona p,Vector3 anchor,int side)
        {
            string key="Brow curve/"+identity+"/"+side;if(meshes.TryGetValue(key,out var mesh))return mesh;
            var plan=new Portraits.FacePlan(p);var v=new List<Vector3>();var tris=new List<int>();var colors=new List<Color>();
            for(int layer=0;layer<(p.Elder?2:1);layer++)
            {
                int start=v.Count;float inner=side*(plan.EyeX-plan.EyeWidth*.5f),outer=inner+side*plan.EyeWidth*1.1f;
                for(int i=0;i<=16;i++)
                {
                    float t=i/16f,x=Mathf.Lerp(inner,outer,t),y=anchor.y+BrowCurve(t,p);
                    float half=BrowThickness(p)*.5f*(1-.45f*t)*(layer==0?1:.43f);
                    for(int edge=-1;edge<=1;edge+=2){float py=y+edge*half;v.Add(new Vector3(x,py,Surface(x,py,p)+.0046f+layer*.0004f)-anchor);colors.Add(layer==0?BrowColor(identity):Color.Lerp(CharacterLook.Get(identity).HairColor,Color.white,.55f));}
                    if(i<16){int k=start+i*2;tris.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
                }
            }
            return Finish(key,v,tris,colors);
        }
        internal static Features Build(Transform head,string identity,Portraits.Persona p,Color skin,Color brow,Color iris,bool precise=false,bool elder=false,bool elf=false)
        {
            var plan=new Portraits.FacePlan(p);var result=new Features();Part(head,"立面頭型",Head(p,identity),Vector3.zero,Material(skin,1,identity));
            Color ink=new Color(.13f,.07f,.12f),white=new Color(1,.97f,.90f);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*plan.EyeX,eyeHeight=plan.EyeHeight*.5f,angle=side*p.Tilt;float cover=p.Lid+(precise&&side<0?.04f:0);
                var anchor=new Vector3(x,plan.EyeY,.118f);var eye=new GameObject("EyePaint").transform;eye.SetParent(head,false);eye.localPosition=anchor;result.Eyes.Add(eye);
                Part(eye,"眼睛圖層",EyeMesh(identity,p,anchor,side),Vector3.zero,EyeMaterial(identity));
                var lidAnchor=new Vector3(x,plan.EyeY+.024f,.122f);var lid=new GameObject("UpperLid").transform;lid.SetParent(head,false);lid.localPosition=lidAnchor;result.Lids.Add(lid);
                float half=plan.EyeWidth*.5f;
                Stroke(lid,identity,p,lidAnchor,new Vector2(x-half,plan.EyeY),new Vector2(x,plan.EyeY+.003f),.0025f,ink,.004f);
                Stroke(lid,identity,p,lidAnchor,new Vector2(x,plan.EyeY+.003f),new Vector2(x+half,plan.EyeY),.0025f,ink,.004f);
                MergeInk(lid,identity+"/lid/"+side);
                foreach(var renderer in lid.GetComponentsInChildren<MeshRenderer>())renderer.enabled=false;
                float by=plan.BrowY+(side>0?p.Asym*.0006f:0);
                var browPivot=new GameObject("BrowPivot").transform;browPivot.SetParent(head,false);browPivot.localPosition=new Vector3(x,by,.12f);result.Brows.Add(browPivot);
                Part(browPivot,"Brow "+identity,BrowMesh(identity,p,browPivot.localPosition,side),Vector3.zero,Material(Color.white,3));
            }
            // 鼻子融入頭模，只留一筆暖色鼻底，不再另外黏三角錐。
            Stroke(head,identity,p,Vector3.zero,new Vector2(-.0035f,plan.NoseY),new Vector2(.0015f,plan.NoseY-.0007f),.0008f,Color.Lerp(skin,new Color(.65f,.29f,.26f),.28f));
            for(int side=-1;side<=1;side+=2)
            {
                float cx=side*plan.EyeX*1.22f,cy=plan.EyeY-plan.EyeHeight*.72f;
                Paint(head,"Soft cheek blush",identity+"/blush/"+side,p,Vector3.zero,new Vector2(cx,cy),new Vector2(.017f,.007f),0,.001f,
                    new Color(.96f,.45f,.52f,.045f),new Color(.96f,.45f,.52f,0));
                if(p.Elder)
                {
                    var crease=Color.Lerp(skin,new Color(.53f,.31f,.28f),.30f);
                    Detail(head,identity,p,new Vector2(side*(plan.EyeX-.018f),plan.EyeY-.012f),new Vector2(side*(plan.EyeX+.022f),plan.EyeY-.014f),.0009f,crease);
                    Detail(head,identity,p,new Vector2(side*.016f,plan.NoseY-.004f),new Vector2(side*.022f,plan.MouthY+.001f),.0008f,crease);
                }
            }
            result.Lip=Part(head,"HumanLip",MouthLine(identity,p),Vector3.zero,Material(Color.white,3)).GetComponent<MeshFilter>();
            // 單位薄片保留原有 mouthOpen 縮放契約，施法時仍可張嘴。
            if(!meshes.TryGetValue("張嘴單位片",out var mouth))
            {
                var v=new List<Vector3>{Vector3.zero};var t=new List<int>();var colors=new List<Color>{Color.white};
                for(int i=0;i<24;i++){float a=i*2*Mathf.PI/24;v.Add(new Vector3(Mathf.Cos(a)*.5f,Mathf.Sin(a)*.5f,Mathf.Sin(a)*.25f));colors.Add(Color.white);}
                for(int i=1;i<=24;i++)t.AddRange(new[]{0,i,i==24?1:i+1});mouth=Finish("張嘴單位片",v,t,colors);
            }
            result.Mouth=Part(head,"mouthOpen",mouth,new Vector3(0,plan.MouthY,Surface(0,plan.MouthY,p)+.003f),Material(new Color(.30f,.10f,.14f),3)).transform;
            result.Mouth.localScale=new Vector3(plan.MouthWidth,.001f+.016f*p.Open,.003f);
            Part(result.Mouth,"UpperTeeth",mouth,new Vector3(0,.23f,.15f),Material(white,3)).transform.localScale=new Vector3(.72f,.15f,.15f);
            result.Motion=head.gameObject.AddComponent<HumanExpressionMotion>();result.Motion.Initialize(identity,p,result);
            Ears(head,identity,p,skin,elf);return result;
        }
        static void MergeInk(Transform pivot,string key)
        {
            var parts=pivot.GetComponentsInChildren<MeshFilter>();if(parts.Length<2)return;
            var material=parts[0].GetComponent<Renderer>().sharedMaterial;
            if(!meshes.TryGetValue("InkPair/"+key,out var mesh))
            {
                var pieces=new CombineInstance[parts.Length];for(int i=0;i<parts.Length;i++)pieces[i]=new CombineInstance{mesh=parts[i].sharedMesh,transform=pivot.worldToLocalMatrix*parts[i].transform.localToWorldMatrix};
                mesh=new Mesh{name="InkPair "+key};mesh.CombineMeshes(pieces,true,true);mesh.RecalculateBounds();meshes["InkPair/"+key]=mesh;
            }
            for(int i=0;i<parts.Length;i++){var go=parts[i].gameObject;go.transform.SetParent(null,false);Object.Destroy(go);}
            Part(pivot,"InkPair "+key,mesh,Vector3.zero,material);
        }
        // One cached curved strip: tapered corners and a subtle cupid bend, not a two-line V.
        internal static Mesh MouthShape(string identity,Portraits.Persona p,int state)
        {HumanPersonality.Mouth(ref p,identity,state);return MouthLine(identity+"/expression/"+state,p);}
        static Mesh MouthLine(string identity,Portraits.Persona p)
        {
            string key="唇縫/"+identity;if(meshes.TryGetValue(key,out var mesh))return mesh;
            var plan=new Portraits.FacePlan(p);var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
            for(int i=0;i<=16;i++)
            {
                float q=i/8f-1,x=q*plan.MouthWidth*.5f;
                float y=plan.MouthY+p.Smile*.004f*q*q+p.Asym*.0009f*q
                    +.0006f*Mathf.Cos(q*Mathf.PI*2)*(1-q*q);
                float half=.00085f*Mathf.Lerp(.25f,1,1-q*q);
                for(int side=-1;side<=1;side+=2)
                {
                    float py=y+side*half;v.Add(new Vector3(x,py,Surface(x,py,p)+.0025f));
                    colors.Add(new Color(.60f,.32f,.32f));
                }
                if(i<16){int k=i*2;t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
            }
            return Finish(key,v,t,colors);
        }
        static void Ears(Transform head,string identity,Portraits.Persona p,Color skin,bool elf)
        {
            var plan=new Portraits.FacePlan(p);float h=(plan.EarTop-plan.EarBottom)*.5f,cy=(plan.EarTop+plan.EarBottom)*.5f;
            for(int side=-1;side<=1;side+=2)
            {
                string key="耳廓/"+identity+"/"+side;
                if(!meshes.TryGetValue(key,out var mesh))
                {
                    var v=new List<Vector3>{new Vector3(side*(new Portraits.FacePlan(p).FaceWidth*.5f+.006f),cy+h,-.025f)};var t=new List<int>();var c=new List<Color>{Color.white};
                    for(int j=1;j<6;j++)for(int i=0;i<8;i++)
                    {
                        float theta=j*Mathf.PI/6,a=i*Mathf.PI/4;
                        float x=Mathf.Sin(theta)*Mathf.Cos(a)*.014f;
                        if(elf&&j==2&&i==(side>0?0:4))x*=1.4f;
                        v.Add(new Vector3(side*(new Portraits.FacePlan(p).FaceWidth*.5f+.006f)+x,cy+Mathf.Cos(theta)*h,-.025f+Mathf.Sin(theta)*Mathf.Sin(a)*.013f));c.Add(Color.white);
                    }
                    int bottom=v.Count;v.Add(new Vector3(side*(new Portraits.FacePlan(p).FaceWidth*.5f+.006f),cy-h,-.025f));c.Add(Color.white);
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
        internal static void Hair(Transform parent,string identity,Portraits.Persona p,Color color)=>AnimeHair.Build(parent,identity,p);
        internal static Material EyeMaterial(string identity,int glance=0)
        {
            string key="眼睛圖集/"+identity+"/"+glance;if(materials.TryGetValue(key,out var mat))return mat;
            var px=new Color32[EyeArt.W*2*EyeArt.H];
            for(int side=0;side<2;side++)
            {
                var eye=EyeArt.Render(identity,side==0?-1:1,glance);
                for(int y=0;y<EyeArt.H;y++)for(int x=0;x<EyeArt.W;x++)px[y*EyeArt.W*2+side*EyeArt.W+x]=eye[y*EyeArt.W+x];
            }
            var texture=new Texture2D(EyeArt.W*2,EyeArt.H,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(px);texture.Apply(false,true);mat=new Material(Shader.Find("Sprites/Default")){name=key,color=Color.white,mainTexture=texture};materials[key]=mat;return mat;
        }
        internal static Mesh LidPose(string identity,Portraits.Persona p,int side,int blink)
        {
            string key="Fitted blink/"+identity+"/"+side+"/"+blink;if(meshes.TryGetValue(key,out var mesh))return mesh;
            var plan=new Portraits.FacePlan(p);float cx=side*plan.EyeX;var anchor=new Vector3(cx,plan.EyeY+.024f,.122f);
            var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();
            for(int i=0;i<=16;i++)
            {
                float q=i/8f-1,x=cx+q*plan.EyeWidth*.5f,y=plan.EyeY+.003f*(1-Mathf.Abs(q));
                for(int edge=-1;edge<=1;edge+=2)
                {
                    float py=y+edge*.00125f;v.Add(new Vector3(x,py,Surface(x,py-plan.EyeHeight*.45f*blink/8f,p)+.0035f)-anchor);c.Add(new Color(.13f,.07f,.12f));
                }
                if(i<16){int k=i*2;t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
            }
            for(int lash=0;lash<2;lash++)
            {
                float x=cx+side*(plan.EyeWidth*.5f-.003f+lash*.003f),y=plan.EyeY+.001f;
                var points=new[]{new Vector2(x-side*.002f,y-.001f),new Vector2(x+side*.0015f,y),new Vector2(x+side*(.006f+lash*.001f),y+.004f+lash*.001f)};
                int start=v.Count;foreach(var q in points){v.Add(new Vector3(q.x,q.y,Surface(q.x,q.y-plan.EyeHeight*.45f*blink/8f,p)+.0035f)-anchor);c.Add(identity=="澪"?CharacterLook.Hex("56879d"):new Color(.13f,.07f,.12f));}t.AddRange(new[]{start,start+1,start+2});
            }
            return Finish(key,v,t,c);
        }
        internal static Mesh EyePose(string identity,Portraits.Persona p,int side,int blink,bool combat)
        {
            var plan=new Portraits.FacePlan(p);return EyeMesh(identity,p,new Vector3(side*plan.EyeX,plan.EyeY,.118f),side,blink,combat);
        }
        static Mesh EyeMesh(string identity,Portraits.Persona p,Vector3 anchor,int side,int blink=0,bool combat=false)
        {
            string key="曲面眼皮/"+identity+side+"/"+blink+"/"+combat;if(meshes.TryGetValue(key,out var mesh))return mesh;
            var plan=new Portraits.FacePlan(p);var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();var uv=new List<Vector2>();
            const int columns=10,rows=6;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                float u=x/(float)columns,w=y/(float)rows,px=anchor.x+(u*2-1)*plan.EyeWidth/1.44f,py=plan.EyeY+(w*2-1)*plan.EyeHeight/.90f;
                v.Add(new Vector3(px,py,Surface(px,plan.EyeY+(py-plan.EyeY)*(1-p.Lid*1.6f-(combat?.045f:0))*(1-.995f*blink/8f)-plan.EyeHeight*.485f*blink/8f,p)+.0043f)-anchor);c.Add(Color.white);uv.Add(new Vector2((u+(side>0?1:0))*.5f,w));
                if(x==columns||y==rows)continue;int k=y*(columns+1)+x;t.AddRange(new[]{k,k+1,k+columns+1,k+1,k+columns+2,k+columns+1});
            }
            return Finish(key,v,t,c,uv);
        }
    }
}

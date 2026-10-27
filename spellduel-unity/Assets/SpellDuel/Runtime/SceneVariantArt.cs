using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Built once with each arena. All opaque scenery stays outside the authored arena.
    public sealed class SceneVariantArt : MonoBehaviour
    {
        readonly GameObject[] groups=new GameObject[3];
        readonly List<Material> owned=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        static readonly Dictionary<int,Texture2D> floors=new Dictionary<int,Texture2D>();
        public int Current {get;private set;}
        public static readonly string[,] Names={{"Lava valley","Crater rim","Abandoned mine"},{"Ancient woods","Mist marsh","Forest ruins"},{"Rocky reef","Wreck deck","Tide cavern"},{"Grand arena","Underground pit","Training yard"},{"Ochre canyon","Caravan station","Cliff walkway"}};
        public static SceneVariantArt Install(Transform parent,List<Vector3> polygon,float floorY,int theme)
        {
            var art=parent.gameObject.AddComponent<SceneVariantArt>();
            var originals=new List<Transform>();foreach(Transform t in parent)originals.Add(t);
            art.groups[0]=new GameObject(Names[theme,0]);art.groups[0].transform.SetParent(parent,false);
            foreach(var t in originals)t.SetParent(art.groups[0].transform,true);
            ArtKit.CenterAndRadius(polygon,floorY,out var center,out float radius);
            for(int v=1;v<=2;v++)art.Build(polygon,center,radius,theme,v);
            art.Set(0);return art;
        }
        public void Set(int v){Current=Mathf.Clamp(v,0,2);for(int i=0;i<3;i++)if(groups[i].activeSelf!=(i==Current))groups[i].SetActive(i==Current);}
        public static Color Palette(int theme,int variant)
        {
            Color[] c={new Color(.36f,.31f,.38f),new Color(.3f,.5f,.43f),new Color(.26f,.48f,.6f),new Color(.47f,.4f,.33f),new Color(.65f,.48f,.29f)};
            return Color.Lerp(c[Mathf.Clamp(theme,0,4)],variant==2?new Color(.55f,.43f,.32f):new Color(.28f,.4f,.45f),.25f);
        }
        public static Texture2D Floor(int theme,int variant)
        {
            int key=theme*3+variant;if(floors.TryGetValue(key,out var tex))return tex;
            const int n=128;var px=new Color32[n*n];var c=Palette(theme,variant);
            bool planks=(theme==0&&variant==2)||(theme==2&&variant==1)||(theme==4&&variant==2);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float noise=Mathf.PerlinNoise(x*.13f+theme*7,y*.13f+variant*5)*.1f;
                bool seam=planks?(y%24<2 || (x+(y/24%2)*32)%64<2):(x%32<2 || y%32<2);
                float k=seam?.7f:.9f+noise;px[y*n+x]=new Color(c.r*k,c.g*k,c.b*k,.62f);
            }
            tex=new Texture2D(n,n,TextureFormat.RGBA32,false){name=Names[theme,variant]+" floor",wrapMode=TextureWrapMode.Repeat};tex.SetPixels32(px);tex.Apply(false,true);floors[key]=tex;return tex;
        }
        void Build(List<Vector3> poly,Vector3 center,float radius,int theme,int variant)
        {
            var root=new GameObject(Names[theme,variant]);groups[variant]=root;root.transform.SetParent(transform,false);
            var tint=Palette(theme,variant);
            Material Mat(string name,Color c,bool transparent=false){var m=new Material(transparent?Shader.Find("Sprites/Default"):ToonStyle.LitShader()){name=name,color=c};owned.Add(m);return m;}
            var stone=Mat("Weathered stone",tint);var wood=Mat("Weathered timber",new Color(.52f,.33f,.18f));var iron=Mat("Dark iron",new Color(.28f,.3f,.36f));var cloth=Mat("Canvas",theme==4?new Color(.8f,.42f,.24f):new Color(.35f,.65f,.73f));var glow=Mat("Lamps and water",theme==0||theme==3?new Color(1,.64f,.22f,.7f):theme==1?new Color(.42f,.9f,.58f,.65f):theme==4?new Color(1,.8f,.45f,.65f):new Color(.42f,.83f,.9f,.65f),true);
            void Put(string name,ArtKit.MB mb,Material mat){var mesh=mb.Build(name);meshes.Add(mesh);ArtKit.MeshObj(root.transform,name,mesh,Vector3.zero,mat);}
            var floor=Mat("Quiet arena floor",Color.white,true);floor.mainTexture=Floor(theme,variant);floor.renderQueue=2910;
            if(poly!=null&&poly.Count>=3)
            {
                var mb=new ArtKit.MB(1);int c=mb.Add(center+Vector3.up*.008f,Vector3.up,new Vector2(center.x,center.z)*.5f);
                for(int i=0;i<poly.Count;i++){var p=poly[i];p.y=center.y+.008f;mb.Add(p,Vector3.up,new Vector2(p.x,p.z)*.5f);}
                for(int i=0;i<poly.Count;i++){int a=i+1,b=(i+1)%poly.Count+1;if(Vector3.Cross(mb.V[a]-mb.V[c],mb.V[b]-mb.V[c]).y>0)mb.Tri(0,c,a,b);else mb.Tri(0,c,b,a);}Put("Arena surface",mb,floor);
            }
            if((theme==1&&variant==1)||(theme==2&&variant==2))
            {
                var water=new ArtKit.MB(1);float inside=radius+.5f,outside=radius+8;
                for(int i=0;i<64;i++)
                {
                    float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Vector3 P(float angle,float r)=>center+new Vector3(Mathf.Cos(angle)*r,.012f,Mathf.Sin(angle)*r);
                    ArtKit.QuadToward(water,0,P(a,inside),P(a,outside),P(b,outside),P(b,inside),center+Vector3.up*20,col:new Color(.3f,.65f,.7f,.3f));
                }
                Put("Shallow luminous water",water,glow);
            }
            var rocks=new ArtKit.MB(1);var timbers=new ArtKit.MB(1);var metal=new ArtKit.MB(1);var fabric=new ArtKit.MB(1);var lights=new ArtKit.MB(1);
            void Box(ArtKit.MB mb,Vector3 at,Vector3 size,float yaw=0)=>ArtKit.Box(mb,0,at,size,Quaternion.Euler(0,yaw,0));
            void Pole(ArtKit.MB mb,Vector3 at,float h,float r)=>ArtKit.Lathe(mb,0,at,Quaternion.identity,new[]{0f,h*.4f,h},new[]{r,r*.9f,r*.65f},10,true,true);
            float edge=radius+3;
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var outwards=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var p=center+outwards*edge;float yaw=90-i*30;
                if(theme==0&&variant==1)
                {
                    Pole(rocks,p,1.4f+i%3*.6f,.7f);Pole(lights,p+Vector3.up*.05f,.09f,.45f);
                }
                else if(theme==0&&variant==2)
                {
                    if(i%3==0){foreach(float dx in new[]{-.85f,.85f})Pole(timbers,p+Quaternion.Euler(0,yaw,0)*Vector3.right*dx,2.5f,.12f);Box(timbers,p+Vector3.up*2.5f,new Vector3(2.1f,.2f,.25f),yaw);Pole(lights,p+Vector3.up*2,.22f,.12f);}
                    else if(i%3==1){Box(timbers,p+Vector3.up*.5f,new Vector3(1,.55f,.8f),yaw);foreach(float dx in new[]{-.4f,.4f})Pole(metal,p+Vector3.right*dx,.22f,.16f);}
                    foreach(float dx in new[]{-.45f,.45f})Box(metal,p+Vector3.right*dx+Vector3.up*.04f,new Vector3(.045f,.04f,2));
                }
                else if(theme==1&&variant==1)
                {
                    for(int j=0;j<5;j++){var q=p+new Vector3(j*.15f,0,Mathf.Sin(j)*.4f);Pole(timbers,q,.8f+j%2*.3f,.025f);Pole(lights,q+Vector3.up*.85f,.1f,.065f);}
                    Pole(timbers,p+outwards,.32f,.055f);ArtKit.Lathe(lights,0,p+outwards+Vector3.up*.3f,Quaternion.identity,new[]{0f,.06f,.16f,.24f},new[]{.32f,.38f,.22f,.01f},16,true,true);
                }
                else if(theme==1&&variant==2)
                {
                    if(i%2==0){Pole(rocks,p,1.6f+i%3*.4f,.3f);Box(rocks,p+Vector3.up*(1.65f+i%3*.4f),new Vector3(.8f,.18f,.7f),yaw);}else Box(rocks,p+Vector3.up*.3f,new Vector3(1.7f,.45f,.5f),yaw+20);
                    Box(lights,p+Vector3.up*.02f,new Vector3(.7f,.025f,.4f),yaw);
                }
                else if(theme==2&&variant==1)
                {
                    Box(timbers,p+Vector3.up*.4f,new Vector3(2,.12f,.18f),yaw);if(i%3==0){Pole(timbers,p,3,.13f);Box(timbers,p+Vector3.up*2.65f,new Vector3(1.9f,.1f,.1f),yaw);Box(fabric,p+Vector3.up*2,new Vector3(1.6f,1.1f,.025f),yaw);}
                }
                else if(theme==2&&variant==2)
                {
                    Pole(rocks,p+outwards,4.5f,.8f);Pole(rocks,p+outwards*2+Vector3.up*4.1f,.45f,2.2f);
                    for(int j=0;j<4;j++)Pole(lights,p+Vector3.right*j*.13f,.3f+j*.1f,.05f);
                }
                else if(theme==3&&variant==1)
                {
                    Box(rocks,p+Vector3.up*1.4f,new Vector3(2.5f,2.8f,.45f),yaw);
                    for(int j=0;j<7;j++)Pole(metal,p-outwards*.3f+Quaternion.Euler(0,yaw,0)*Vector3.right*(j-3)*.22f,2.3f,.028f);
                    if(i%2==0){Pole(timbers,p-outwards*.6f,.9f,.07f);Pole(lights,p-outwards*.6f+Vector3.up*.9f,.28f,.13f);}
                }
                else if(theme==3&&variant==2)
                {
                    if(i%2==0){Pole(timbers,p,1.6f,.14f);Box(timbers,p+Vector3.up*1.15f,new Vector3(.9f,.13f,.13f),yaw);}else{Box(timbers,p+Vector3.up*.9f,new Vector3(1.3f,.1f,.3f),yaw);for(int j=0;j<4;j++)Pole(metal,p+Vector3.right*(j-.5f)*.2f,1.65f,.024f);}
                    if(i%3==0){Pole(metal,p+outwards,3,.025f);Box(fabric,p+outwards+Vector3.up*2.5f,new Vector3(.65f,.8f,.025f),yaw);}
                }
                else if(theme==4&&variant==1)
                {
                    if(i%3==0){Box(rocks,p+outwards+Vector3.up*1.2f,new Vector3(2.3f,2.4f,1.8f),yaw);Box(timbers,p-outwards*.02f+Vector3.up*.7f,new Vector3(.65f,1.4f,.1f),yaw);}
                    else if(i%3==1){foreach(float dx in new[]{-.7f,.7f})Pole(timbers,p+Vector3.right*dx,1.9f,.05f);Box(fabric,p+Vector3.up*1.9f,new Vector3(1.7f,.08f,1.5f),yaw);}
                    else{Pole(timbers,p,.6f,.18f);ArtKit.Lathe(fabric,0,p+Vector3.up*.6f,Quaternion.identity,new[]{0f,.15f,.24f},new[]{.4f,.32f,.2f},12,true,false);}
                }
                else
                {
                    Pole(timbers,p,1.1f,.055f);Box(timbers,p+Vector3.up*.9f,new Vector3(2.4f,.07f,.07f),yaw);
                    if(i<6)Pole(rocks,p+outwards*3,5+i%3,1.4f);
                }
            }
            Put("Landforms",rocks,stone);Put("Timber structures",timbers,wood);Put("Metal details",metal,iron);Put("Canvas and flags",fabric,cloth);Put("Water lamps mushrooms",lights,glow);
            // Distant landform and atmosphere provide a different horizon for each variant.
            var far=new ArtKit.MB(1);
            if(theme==0&&variant==1)ArtKit.Lathe(far,0,center+new Vector3(0,0,edge+12),Quaternion.identity,new[]{0f,3f,6f,6.3f},new[]{7f,4.2f,2.3f,2.1f},32,false,false);
            else if(theme==1)for(int i=0;i<10;i++)Pole(far,center+new Vector3(Mathf.Cos(i)*20,0,Mathf.Sin(i)*20),7,.5f);
            else if(theme==4&&variant==2)for(int i=0;i<8;i++)Pole(far,center+new Vector3(-16+i*4,-3,25),2+i%3,2.5f);
            else for(int i=0;i<10;i++)Pole(far,center+new Vector3(Mathf.Cos(i)*22,0,Mathf.Sin(i)*22),theme==2?3:5,1.5f);
            Put("Distant skyline",far,stone);
            if(theme==0&&variant==1)
            {
                var smoke=SpellFx.NewPS(root.transform,"Crater plume",SpellFx.BlendMat(SpellFx.TexSoft),32,false);smoke.transform.position=center+new Vector3(0,6,edge+12);
                var sm=smoke.main;sm.startLifetime=7;sm.startSize=3;sm.startSpeed=.1f;sm.startColor=new Color(.35f,.29f,.32f,.35f);
                var sv=smoke.velocityOverLifetime;sv.enabled=true;sv.y=.7f;var se=smoke.emission;se.rateOverTime=3;SpellFx.Sphere(smoke,1.4f);smoke.Play();
            }
            var dust=SpellFx.NewPS(root.transform,"Ambient motes",SpellFx.BlendMat(SpellFx.TexSoft),64,false);dust.transform.position=center+Vector3.up*2;
            var main=dust.main;main.startLifetime=5;main.startSpeed=.04f;main.startSize=(theme==1&&variant==1)||(theme==4&&variant==2)?2:.05f;main.startColor=new Color(tint.r+.2f,tint.g+.2f,tint.b+.2f,.18f);
            SpellFx.Sphere(dust,edge+3);var em=dust.emission;em.rateOverTime=8;dust.Play();
        }
        void OnDestroy(){foreach(var m in meshes)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}foreach(var m in owned)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}
    }
}

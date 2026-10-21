using UnityEngine;
namespace SpellDuel
{
    /// <summary>Procedural charge HUD. Coordinates and gameplay progress come only from the caller.</summary>
    public static class ChargeReticle
    {
        static Texture2D[] frames;
        static bool wasReady;
        static float readyAt;
        static readonly Color Back=new Color(.025f,.035f,.065f,.88f);
        /// <summary>classId is an optional visual hint; existing callers retain their exact size and position.</summary>
        public static void Draw(Vector2 center,float size,bool charging,float progress,Color color,float overcharge=0f,string classId=null)
        {
            var oldColor=GUI.color;var oldMatrix=GUI.matrix;
            try
            {
                EnsureFrames();size=Mathf.Max(1,size);bool ready=charging&&progress>=1;
                if(ready&&!wasReady)readyAt=Time.time;wasReady=ready;
                float age=Mathf.Max(0,Time.time-readyAt),pulse=.5f+.5f*Mathf.Sin(Time.time*5.5f);
                float impact=ready?Mathf.Clamp01(1-age/.24f):0;
                var ink=ready?Color.Lerp(color,Color.white,.35f+.45f*pulse):color;
                float radius=size*1.75f,thickness=Mathf.Max(4,size*.28f);
                // Four separated sight arms leave the exact target unobstructed.
                float gap=size*.32f,arm=size*.9f;
                Cross(center,arm,Mathf.Max(4,size*.16f)+2,gap-1,Back);
                Cross(center,arm,Mathf.Max(2,size*.13f),gap,charging?ink:Color.white);
                Rect(center,4,4,Back);Rect(center,2,2,Color.white);
                if(!charging)return;
                int style=Style(classId,color);
                GUI.color=new Color(1,1,1,.95f);GUI.DrawTexture(new Rect(center.x-size*2.25f,center.y-size*2.25f,size*4.5f,size*4.5f),frames[style]);
                // Continuous partial segment at the leading edge: no 32-step quantisation.
                Ring(center,radius,thickness,Mathf.Clamp01(progress),ink);
                if(ready)
                {
                    Ring(center,radius+thickness*.75f+impact*size*.3f,Mathf.Max(1.5f,size*.08f),1,new Color(ink.r,ink.g,ink.b,.28f+.42f*pulse+impact*.25f));
                    if(impact>0)Ring(center,radius+size*.6f*(1-impact),2,1,new Color(1,1,1,impact*.75f));
                }
                if(overcharge>0)
                {
                    float outer=radius+size*.72f;
                    Ring(center,outer,thickness*.75f,1,Back);
                    Ring(center,outer,thickness*.50f,Mathf.Clamp01(overcharge),new Color(1,.78f,.22f));
                    for(int i=0;i<4;i++)Tick(center,i*90,outer,size*.23f,2,new Color(1,.94f,.65f));
                }
            }
            finally{GUI.matrix=oldMatrix;GUI.color=oldColor;}
        }
        static int Style(string id,Color c)
        {
            if(id=="swordsman")return 0;if(id=="archer")return 1;if(id=="mage")return 2;if(id=="assassin")return 3;if(id=="priest")return 4;
            // Existing calls only pass skill colour. This is a visual fallback, never a class/gameplay lookup.
            if(c.r>.85f&&c.g>.7f&&c.b<.7f)return 4;
            if(c.g>c.r&&c.g>c.b)return 1;
            if(c.r>c.g*1.3f&&c.b>c.g*1.3f)return c.r>c.b?3:2;
            return 0;
        }
        static void Ring(Vector2 c,float r,float width,float progress,Color color)
        {
            const int n=64;float amount=progress*n;
            for(int i=0;i<n;i++)
            {
                float fraction=Mathf.Clamp01(amount-i);if(fraction<=0)break;
                // A tangential strip with a tiny overlap reads as a thick continuous arc.
                float a=(i+fraction*.5f)*360/n;
                Tick(c,a,r,width,Mathf.Max(.3f,r*2*Mathf.PI/n*fraction+.6f),color);
            }
        }
        static void Cross(Vector2 c,float arm,float width,float gap,Color color)
        {
            float length=arm-gap;
            Rect(new Vector2(c.x-(arm+gap)*.5f,c.y),length,width,color);Rect(new Vector2(c.x+(arm+gap)*.5f,c.y),length,width,color);
            Rect(new Vector2(c.x,c.y-(arm+gap)*.5f),width,length,color);Rect(new Vector2(c.x,c.y+(arm+gap)*.5f),width,length,color);
        }
        static void Rect(Vector2 c,float w,float h,Color color){GUI.color=color;GUI.DrawTexture(new Rect(c.x-w*.5f,c.y-h*.5f,w,h),Texture2D.whiteTexture);}
        static void Tick(Vector2 c,float angle,float radius,float length,float width,Color color)
        {
            var m=GUI.matrix;GUIUtility.RotateAroundPivot(angle,c);GUI.color=color;
            GUI.DrawTexture(new Rect(c.x-width*.5f,c.y-radius-length*.5f,width,length),Texture2D.whiteTexture);GUI.matrix=m;
        }
        static void EnsureFrames()
        {
            if(frames!=null)return;frames=new Texture2D[5];
            const int n=256;
            for(int style=0;style<5;style++)
            {
                var pixels=new Color[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float dx=(x+.5f-n*.5f)/(n/4.5f),dy=(y+.5f-n*.5f)/(n/4.5f),r=Mathf.Sqrt(dx*dx+dy*dy);
                    float angle=Mathf.Atan2(dx,dy),teeth=Mathf.Abs(Mathf.Sin(angle*(style==1?8:style==2?6:style==3?4:style==4?12:3)));
                    float a=Mathf.Clamp01((.29f-Mathf.Abs(r-1.75f))*n/4.5f);
                    Color col=new Color(.025f,.035f,.065f,a*.86f);
                    if(Mathf.Abs(r-1.75f)<.12f)col=new Color(.75f,.83f,.96f,.18f);
                    // Cached silhouette vocabulary: blades / feathers / rune gates / needles / halo beads.
                    bool motif=style==4?Mathf.Abs(r-2.08f)<.04f&&teeth>.87f:style==2?r>2&&r<2.17f&&teeth>.90f:r>2&&r<2.05f+.12f*teeth&&teeth>.94f;
                    if(motif)col=new Color(.88f,.94f,1,.85f);
                    pixels[y*n+x]=col;
                }
                var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Charge frame "+style,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};t.SetPixels(pixels);t.Apply(false,true);frames[style]=t;
            }
        }
    }
}

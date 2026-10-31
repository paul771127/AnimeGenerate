using UnityEngine;
namespace SpellDuel
{
    public static partial class UiKit
    {
        public static class Theme
        {
            public static readonly Color Ink = new Color(.025f,.035f,.065f,.97f);
            public static readonly Color Health = new Color(.96f,.27f,.37f);
            public static readonly Color Mana = new Color(.26f,.65f,1f);
            public static readonly Color Positive = new Color(.34f,.92f,.63f);
            public static readonly Color Negative = new Color(1f,.35f,.33f);
            public static readonly Color Warning = new Color(1f,.73f,.28f);
        }
        // One state per displayed resource, owned by the view; never modifies a Fighter.
        public sealed class ResourceTrail
        {
            float previous, trail, holdUntil, healedAt = -10f, lastSeen = -10f;
            int frame = -1;
            public void Draw(Rect r,float value,Color colour,string label,int font)
            {
                value=Mathf.Clamp01(value);
                if(Event.current.type==EventType.Repaint && frame!=Time.frameCount)
                {
                    frame=Time.frameCount;
                    float now=Time.unscaledTime;
                    if(now-lastSeen>1f){previous=trail=value;healedAt=-10f;}
                    if(value<previous)holdUntil=now+.28f;
                    if(value>previous+.002f)healedAt=now;
                    trail=Mathf.Max(value,now>holdUntil?Mathf.MoveTowards(trail,value,Time.unscaledDeltaTime*.65f):trail);
                    previous=value;lastSeen=now;
                }
                Init();Paint(r,panelStyle,Theme.Ink);var inner=Inset(r,3);
                Paint(new Rect(inner.x,inner.y,inner.width*trail,inner.height),panelStyle,new Color(.93f,.95f,1f,.85f));
                Paint(new Rect(inner.x,inner.y,inner.width*value,inner.height),panelStyle,colour);
                Paint(r,bevelStyle,new Color(1,1,1,.35f));Paint(r,frameStyle,Color.Lerp(colour,Paper,.6f));
                float pulse=Mathf.Clamp01(1f-(Time.unscaledTime-healedAt)/.65f);
                if(pulse>0)Paint(r,frameStyle,new Color(.4f,1f,.64f,pulse));
                Text(Inset(r,2),label,font,Paper,TextAnchor.MiddleCenter,true,.5f);
            }
        }
        public static void SkillCard(Rect r,Color accent,bool usable,bool charging)
        {
            Card(r,usable?.94f:.98f,accent,usable?.95f:.4f);
            Init();Paint(Inset(r,3),bevelStyle,new Color(accent.r,accent.g,accent.b,usable?.4f:.12f));
            if(!usable)Paint(Inset(r,5),panelStyle,new Color(.02f,.03f,.05f,.38f));
            if(charging)Halo(r,accent,.65f+.25f*Mathf.Sin(Time.unscaledTime*6f));
        }
        public static void Cooldown(Rect r,float fraction,float seconds,int font)
        {
            fraction=Mathf.Clamp01(fraction);
            Divider(new Rect(r.x,r.yMax-r.height*fraction,r.width,r.height*fraction),new Color(.015f,.025f,.05f,.8f));
            Divider(new Rect(r.x,r.yMax-r.height*fraction,r.width,Mathf.Max(1,r.height*.025f)),Muted);
            Text(r,seconds>=10?seconds.ToString("0"):seconds.ToString("0.0"),font,Paper,TextAnchor.MiddleCenter,true);
        }
        public static void StatusFrame(Rect r,Texture2D texture,bool good)
        {
            var c=good?Theme.Positive:Theme.Negative;
            Card(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),1,c,1);
            var keep=GUI.color;GUI.color=Color.white;GUI.DrawTexture(Inset(r,2),texture,ScaleMode.ScaleToFit,true);GUI.color=keep;
            // Shape cue as well as colour: positive diamond, negative underline.
            if(good)Gem(new Rect(r.xMax-7,r.y-3,10,10),c);
            else Divider(new Rect(r.x+3,r.yMax-2,r.width-6,3),c);
        }
        public static void ChargeRail(Rect r,float progress,bool ready,Color colour)
        {
            Bar(r,progress,colour,ready?"READY":"",Mathf.Max(10,Mathf.RoundToInt(r.height*.72f)));
            if(ready)Halo(r,colour,.65f+.3f*Mathf.Sin(Time.unscaledTime*6f));
        }
        public static void SpeechBubble(Rect r,float tailX,float alpha)
        {
            Card(r,alpha,Paper,.9f*alpha);
            Gem(new Rect(Mathf.Clamp(tailX-6,r.x+8,r.xMax-20),r.yMax-5,12,12),new Color(.18f,.24f,.35f,alpha));
        }
        public static void MapFrame(Rect r,int font)
        {
            Halo(r,Gold,1);
            var north=new Rect(r.center.x-font*.65f,r.y-font*.75f,font*1.3f,font*1.3f);
            Card(north,1,Gold,1);Text(north,"N",font,Paper,TextAnchor.MiddleCenter,true);
        }
    }
}

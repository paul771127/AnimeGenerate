using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        /// <summary>Visual only. Linear radius 0..radius over seconds; caller owns damage timing.</summary>
        public static void GroundRingFx(Vector3 position, float radius, Color color, float seconds)
        { AreaEnemyFx.Spawn(position,Vector3.forward,radius,360,0.035f,color,seconds,true); }
        /// <summary>Visual only, angle centred on horizontal direction, height measured above position.</summary>
        public static void SweepArcFx(Vector3 position, Vector3 direction, float radius, float angle, float height, Color color)
        { AreaEnemyFx.Spawn(position,direction,radius,angle,height,color,.45f,false); }
    }
    // Uses SpellFx's cached additive material and line utility. No collider or battle references.
    public sealed class AreaEnemyFx : MonoBehaviour
    {
        LineRenderer outer, inner;
        Color tint;
        float age, duration, radius;
        bool expanding;
        public static void Spawn(Vector3 position,Vector3 direction,float radius,float angle,float height,Color color,float seconds,bool expanding)
        {
            if(radius<=0||seconds<=0)return;
            direction.y=0;if(direction.sqrMagnitude<.0001f)direction=Vector3.forward;
            var root=new GameObject(expanding?"Ground wave ring (visual)":"Chest sweep (visual)");
            root.transform.SetPositionAndRotation(position+Vector3.up*height,Quaternion.LookRotation(direction,Vector3.up));
            var fx=root.AddComponent<AreaEnemyFx>();fx.tint=color;fx.duration=seconds;fx.radius=radius;fx.expanding=expanding;
            angle=Mathf.Clamp(angle,1,360);var points=new Vector3[65];
            for(int i=0;i<points.Length;i++)
            {float a=(-angle*.5f+angle*i/(points.Length-1))*Mathf.Deg2Rad;points[i]=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));}
            fx.outer=SpellFx.Line(root.transform,"Bright leading edge",points,false,expanding?.07f:.13f,color,true,false);
            color.a*=.32f;
            fx.inner=SpellFx.Line(root.transform,"Soft luminous band",points,false,expanding?.20f:.32f,color,true,false);
            fx.Tick(0);
        }
        void Tick(float dt)
        {
            age+=dt;float t=Mathf.Clamp01(age/duration);
            // Keep physical radius linear, rather than FxAnim's cubic easing.
            transform.localScale=Vector3.one*(expanding?Mathf.Max(.001f,radius*t):radius);
            Color c=tint;c.a*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.68f)/.32f));
            outer.startColor=outer.endColor=c;c.a*=.32f;inner.startColor=inner.endColor=c;
            if(age>=duration)Destroy(gameObject);
        }
        void Update(){Tick(Time.deltaTime);}
    }
}

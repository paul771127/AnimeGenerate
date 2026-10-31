using UnityEngine;
namespace SpellDuel
{
    // Presentation only. Does not stop a projectile, alter a collider, or own damage timing.
    [DefaultExecutionOrder(900)]
    public sealed class SpellReadability : MonoBehaviour
    {
        public const int ParticleBudget=192,PerSystemBudget=64;
        Renderer[] renderers;ParticleSystem[] particles;Color[] colour,tint,lastColour,lastTint;
        MaterialPropertyBlock block;Camera view;float age;bool transientImpact;
        static readonly int ColourId=Shader.PropertyToID("_Color"),TintId=Shader.PropertyToID("_TintColor");
        public static bool IsImpact(string name)
        {
            return name.IndexOf("impact",System.StringComparison.OrdinalIgnoreCase)>=0 ||
                name.IndexOf(" hit",System.StringComparison.OrdinalIgnoreCase)>=0 ||
                name.IndexOf("implosion",System.StringComparison.OrdinalIgnoreCase)>=0 || name=="SpellFx Falcon claw";
        }
        public static SpellReadability Attach(GameObject root,bool impact=false)
        {
            var fx=root.GetComponent<SpellReadability>();if(!fx)fx=root.AddComponent<SpellReadability>();fx.transientImpact=impact;return fx;
        }
        public void Prepare()
        {
            if(renderers!=null)return;
            view=Camera.main;renderers=GetComponentsInChildren<Renderer>(true);particles=GetComponentsInChildren<ParticleSystem>(true);
            colour=new Color[renderers.Length];tint=new Color[renderers.Length];lastColour=new Color[renderers.Length];lastTint=new Color[renderers.Length];block=new MaterialPropertyBlock();
            int remaining=ParticleBudget;
            for(int i=0;i<particles.Length;i++)
            {
                var main=particles[i].main;
                int reserve=Mathf.Min(remaining,particles.Length-i-1);
                int cap=Mathf.Max(0,Mathf.Min(main.maxParticles,PerSystemBudget,remaining-reserve));
                main.maxParticles=cap;remaining-=cap;
            }
            for(int i=0;i<renderers.Length;i++)
            {
                var mat=renderers[i].sharedMaterial;
                renderers[i].GetPropertyBlock(block);lastColour[i]=block.GetColor(ColourId);lastTint[i]=block.GetColor(TintId);
                colour[i]=mat&&mat.HasProperty(ColourId)?mat.GetColor(ColourId):Color.white;
                tint[i]=mat&&mat.HasProperty(TintId)?mat.GetColor(TintId):Color.white;
                if(lastColour[i]!=Color.clear)colour[i]=lastColour[i];if(lastTint[i]!=Color.clear)tint[i]=lastTint[i];
            }
        }
        public static float NearFade(float distance)=>Mathf.SmoothStep(.08f,1f,Mathf.InverseLerp(.2f,1f,distance));
        public void Evaluate(Camera camera,float elapsed)
        {
            Prepare();float impact=1;
            // Mathf.SmoothStep takes an interpolation value, so normalise the time explicitly.
            if(transientImpact)impact=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.5f,elapsed));
            for(int i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i];if(!renderer)continue;
                float fade=impact;
                if(camera)fade*=NearFade(Vector3.Distance(camera.transform.position,renderer.bounds.ClosestPoint(camera.transform.position)));
                renderer.GetPropertyBlock(block);
                var current=block.GetColor(ColourId);if(current!=lastColour[i])colour[i]=current;
                current=block.GetColor(TintId);if(current!=lastTint[i])tint[i]=current;
                var c=colour[i];c.a*=fade;block.SetColor(ColourId,c);lastColour[i]=c;
                c=tint[i];c.a*=fade;block.SetColor(TintId,c);lastTint[i]=c;renderer.SetPropertyBlock(block);
                if(transientImpact&&elapsed>=.5f)renderer.enabled=false;
            }
        }
        void Start(){Prepare();}
        void LateUpdate(){age+=Time.deltaTime;if(!view)view=Camera.main;Evaluate(view,age);}
    }
}

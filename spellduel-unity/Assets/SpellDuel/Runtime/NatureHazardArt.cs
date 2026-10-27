using UnityEngine;
namespace SpellDuel
{
    public sealed class NatureHazardArt : MonoBehaviour
    {
        public Battle.HazardKind Kind;
        public Transform Warning, Active;
        public ParticleSystem Motes, Smoke;
        public LineRenderer[] WarningLines;
        public Color Tint;
        public void Tick(float state,float clock)
        {
            bool on=state>=1;
            if(Smoke){bool smoking=state>0 && !on;if(smoking && !Smoke.isPlaying)Smoke.Play();if(!smoking && Smoke.isPlaying)Smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
            Active.gameObject.SetActive(on || (Kind==Battle.HazardKind.Whirl && state>0));
            Warning.gameObject.SetActive(Kind!=Battle.HazardKind.Tide || (state>0 && !on));
            var c=Tint;c.a=state<=0?.18f:.35f+.45f*state*(.8f+.2f*Mathf.Sin(clock*9));
            foreach(var l in WarningLines)l.startColor=l.endColor=c;
            if(Kind==Battle.HazardKind.Whirl)Active.localRotation=Quaternion.Euler(0,clock*(on?240:45),0);
            if(Kind==Battle.HazardKind.Lava)Active.localScale=new Vector3(1,1+.04f*Mathf.Sin(clock*19),1);
            if(on && !Motes.isPlaying)Motes.Play();
            if(!on && Motes.isPlaying)Motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDestroy(){foreach(var f in GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh){if(Application.isPlaying)Destroy(f.sharedMesh);else DestroyImmediate(f.sharedMesh);}}
    }
}

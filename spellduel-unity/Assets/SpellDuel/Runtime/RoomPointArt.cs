using UnityEngine;
namespace SpellDuel
{
    public sealed class RoomPointArt : MonoBehaviour
    {
        public string Kind;public Color Tint;public Transform Moving;public GameObject Hole;public LineRenderer[] Lines;public ParticleSystem Debris;int previous=-1;
        public void Tick(int state,float clock)
        {
            if(Debris && state==2 && previous!=2){Debris.Play();}previous=state;
            var c=Tint;
            if(Kind=="runes")c=state==1?new Color(1,.9f,.2f):state==2?new Color(.3f,1,.5f):state==3?new Color(1,.12f,.06f):new Color(.55f,.35f,1,.45f);
            if(Kind=="collapse"){c=state==2?new Color(.3f,.13f,.05f,.7f):new Color(1,.15f,.04f,.5f+.3f*Mathf.Sin(clock*8));if(Hole)Hole.SetActive(state==2);}
            foreach(var l in Lines)l.startColor=l.endColor=c;
            if(Moving)Moving.localRotation=Quaternion.Euler(0,clock*35,0);
        }
        void OnDestroy(){foreach(var f in GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh){if(Application.isPlaying)Destroy(f.sharedMesh);else DestroyImmediate(f.sharedMesh);}}
    }
}

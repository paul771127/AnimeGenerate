using UnityEngine;
namespace SpellDuel
{
    public sealed class RoomPropArt : MonoBehaviour
    {
        public string Id; public Color Tint; public Transform Body,Glow; public GameObject Cracks,Halo;
        float lastHp=-1,hitUntil,flashUntil;
        public void Pulse(float clock){flashUntil=clock+.7f;if(Glow)Glow.gameObject.SetActive(true);}
        public void Tick(float hp,float maxHp,bool lit,float clock)
        {
            if(lastHp>=0 && hp<lastHp)hitUntil=clock+.4f;lastHp=hp;
            Body.localRotation=Quaternion.Euler(0,0,clock<hitUntil?Mathf.Sin(clock*50)*6:0);
            if(Cracks)Cracks.SetActive(hp<maxHp*.65f);
            if(Halo)Halo.SetActive(lit);
            if(Glow){float s=lit?1.5f:1; if(clock<flashUntil)s*=1.6f;Glow.localScale=Vector3.one*s*(1+.04f*Mathf.Sin(clock*3));if(Id.StartsWith("ev_chest"))Glow.gameObject.SetActive(clock<flashUntil);}
        }
        void OnDestroy(){foreach(var f in GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh){if(Application.isPlaying)Destroy(f.sharedMesh);else DestroyImmediate(f.sharedMesh);}}
    }
}

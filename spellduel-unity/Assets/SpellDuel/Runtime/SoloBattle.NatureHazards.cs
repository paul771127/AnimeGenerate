using UnityEngine;
namespace SpellDuel
{
    public partial class SoloBattle
    {
        HazardView MakeNatureView(Battle.Hazard h)
        {
            var art=HazardArt.Nature(h.kind,h.radius);
            return new HazardView { go=art.gameObject, part=art.Active, nature=art };
        }
        void UpdateNatureView(Battle.Hazard h,HazardView v,float now)
        {
            var art=v.nature;
            float state=0;
            Vector3 pos=h.a;
            Quaternion rot=Quaternion.identity;
            switch(h.kind)
            {
                case Battle.HazardKind.Lava:
                case Battle.HazardKind.Roots:
                    state=now<h.start?0:Battle.SpikeState(h,now);
                    if(state>=1 && v.spikeY<.5f && Battle.FlatDistance(h.a,Me.Feet)<6)
                        Sfx.Play(h.kind==Battle.HazardKind.Lava?"boom":"trap",.5f,h.kind==Battle.HazardKind.Lava?.7f:1.4f);
                    v.spikeY=state>=1?1:0;
                    break;
                case Battle.HazardKind.Spore:
                    pos=Battle.SporeCenter(h,now);state=now<h.start?0:1;break;
                case Battle.HazardKind.Whirl:
                    state=now<h.start?0:Battle.WhirlState(h,now);break;
                case Battle.HazardKind.Tide:
                    float warn=now<h.start?0:Battle.TideWarn(h,now);
                    float front=now<h.start?-1:Battle.TideFront(h,now);
                    state=front>=0?1:Mathf.Min(.999f,warn);
                    pos=h.a+h.b*(front>=0?front:.3f);
                    rot=Quaternion.LookRotation(h.b,Vector3.up);
                    if(front>=0 && v.spikeY<.5f){Sfx.Play("dodge",.7f,.5f);Say("🌊 浪來了！跳起來！",1.2f);}
                    v.spikeY=front>=0?1:0;break;
            }
            pos.y=0;
            v.go.transform.SetPositionAndRotation(WorldFrame.FromWorld(pos),WorldFrame.RotFromWorld(rot));
            art.Tick(state,now);
        }
    }
}

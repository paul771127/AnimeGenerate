using UnityEngine;

namespace SpellDuel
{
    // Visual-only area attackers. World movement, spawn limits and damage belong to Combat.
    public partial class MinionRig
    {
        static Mesh areaSail;
        Transform areaFrontL, areaFrontR, areaTail, areaWeapon;
        readonly Transform[] areaLegs = new Transform[4];
        Vector3 areaSpineAt;
        float areaSpin;

        void BuildAreaEnemy(int k)
        {
            InitSea(); sea = plant = false; variantId = kind = k;
            // Existing chapter palettes also drive hit, charge and death colours.
            v = Variants[k == 19 ? 8 : k == 20 ? 13 : k == 21 ? 2 : 4];
            // Per-instance visual palette; never mutate the shared ordinary-minion presets.
            Color light=k==19?new Color(.83f,.98f,.18f):k==20?new Color(.19f,.88f,.91f):new Color(1,.40f,.08f);
            v=new Variant{size=v.size,lava=light,lavaDim=light*.52f,hot=Color.Lerp(light,Color.white,.6f),eye=k==19?new Color(1,.89f,.22f):k==20?new Color(1,.72f,.18f):new Color(1,.47f,.14f)};
            rockList.Clear(); rockCols.Clear(); rockGlows.Clear();
            lavaList.Clear(); hotList.Clear(); eyeList.Clear(); swayT.Clear(); swayQ.Clear();
            flame = ball = core = boulder = tip = null; flameR = null; embers = null;
            hipL = hipR = shL = shR = wrL = wrR = head = null;
            areaFrontL = areaFrontR = areaTail = areaWeapon = null;
            for (int i = 0; i < areaLegs.Length; i++) areaLegs[i] = null;
            phase = PhaseNone; atkT = areaSpin = 0; clock = Random.Range(0f, 10f);
            body = Pivot("Area enemy body", transform, Vector3.zero);
            spine = Pivot("Area spine", body, Vector3.zero);
#if UNITY_EDITOR
            if(rarePreviewLegacy){switch(k){case 19:BuildAncientRootBeastLegacy();break;case 20:BuildTidewhipEelLegacy();break;case 21:BuildChainGladiatorLegacy();break;case 22:BuildRiftplateLizardLegacy();break;}}
            else
#endif
            switch (k)
            {
                case 19: BuildAncientRootBeast(); break;
                case 20: BuildTidewhipEel(); break;
                case 21: BuildChainGladiator(); break;
                case 22: BuildRiftplateLizard(); break;
            }
            areaSpineAt = spine.localPosition;
            rockR = rockList.ToArray(); rockC = rockCols.ToArray(); rockG = rockGlows.ToArray();
            lavaR = lavaList.ToArray(); hotR = hotList.ToArray(); eyeR = eyeList.ToArray();
            AnimateAreaEnemy(0);
        }

        Transform AreaPart(Transform at, Vector3 pos, Vector3 scale, Color color, Mesh mesh = null, Vector3 euler = default(Vector3))
        { return Part(at, mesh == null ? pBlob : mesh, pos, scale, euler, color); }
        void AreaEyes(Transform at, float width, float y, float z, Color color)
        {
            for (int s = -1; s <= 1; s += 2)
                AreaPart(at, new Vector3(s * width, y, z), new Vector3(.105f,.065f,.045f), color, pBlob, new Vector3(0,0,s*18));
        }
        void AreaQuadruped(Color hide, Color feet)
        {
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var leg = areaLegs[i] = Pivot("Articulated leg", spine, new Vector3((i%2==0?-1:1)*.43f,-.04f,front?.49f:-.48f));
                AreaPart(leg,new Vector3(0,-.24f,0),new Vector3(.32f,.54f,.34f),hide);
                AreaPart(leg,new Vector3(0,-.47f,.09f),new Vector3(.39f,.20f,.45f),feet);
            }
            areaFrontL = areaLegs[0]; areaFrontR = areaLegs[1];
        }
        void AnimateAreaEnemy(float dt)
        {
            clock+=dt;
            if(hitT>0)hitT=Mathf.Max(0,hitT-dt);
            if(dead)deadT+=dt;
            float w=0, strike=0;
            if(!dead && phase==PhaseWindup){atkT+=dt;w=Mathf.SmoothStep(0,1,Mathf.Clamp01(atkT/atkDur));plantW=w;}
            else if(!dead && phase==PhaseStrike)
            {
                atkT+=dt;w=snapW*(1-Mathf.Clamp01(atkT/.16f));
                strike=Mathf.Sin(Mathf.Clamp01(atkT/.65f)*Mathf.PI);
                if(atkT>=.65f)phase=PhaseNone;
            }
            plantW=w;
            float move=dead||phase!=PhaseNone?0:Mathf.Clamp01(MoveSpeed/.4f);
            walkPhase+=dt*Mathf.Max(.4f,MoveSpeed)*5;
            float death=Mathf.SmoothStep(0,1,Mathf.Clamp01(deadT/DieDur));
            body.localPosition=new Vector3(0,-death*.35f,0);
            body.localScale=Vector3.one*(1-death*.55f);
            body.localRotation=Quaternion.Euler(death*78,0,-Mathf.Clamp01(hitT/HitDur)*7);
            float lift=(kind==19||kind==22)?w*.30f-strike*.12f:0;
            spine.localPosition=areaSpineAt+Vector3.up*(lift+Mathf.Sin(clock*2)*.015f+Mathf.Abs(Mathf.Sin(walkPhase))*.025f*move);
            spine.localRotation=Quaternion.Euler(-w*(kind==20?32:kind==21?6:24)+strike*15,kind==20?strike*100:kind==21?strike*140:0,0);
            for(int i=0;i<areaLegs.Length;i++)if(areaLegs[i]!=null)
                areaLegs[i].localRotation=Quaternion.Euler(Mathf.Sin(walkPhase+(i==0||i==3?0:Mathf.PI))*24*move-(i<2&&kind!=21?w*65:0),0,0);
            if(kind==21)
            {
                areaFrontR.localRotation=Quaternion.Euler(-w*165-strike*85,0,-15);
                areaFrontL.localRotation=Quaternion.Euler(-20-w*45,0,18);
                areaSpin+=dt*(phase==PhaseWindup?Mathf.Lerp(120,850,w):phase==PhaseStrike?950:0);
                areaWeapon.rotation=transform.rotation*Quaternion.Euler(0,areaSpin,phase==PhaseNone?-20:0);
            }
            if(areaTail!=null)areaTail.localRotation=Quaternion.Euler(0,Mathf.Sin(clock*2)*8+(kind==20?-w*45+strike*160:0),0);
            UpdatePlantColors(Mathf.Clamp01(hitT/HitDur),death,.5f+.5f*Mathf.Sin(clock*5),w);
        }
    }
}

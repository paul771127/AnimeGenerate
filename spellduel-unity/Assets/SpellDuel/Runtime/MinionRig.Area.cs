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
            rockList.Clear(); rockCols.Clear(); rockGlows.Clear();
            lavaList.Clear(); hotList.Clear(); eyeList.Clear(); swayT.Clear(); swayQ.Clear();
            flame = ball = core = boulder = tip = null; flameR = null; embers = null;
            hipL = hipR = shL = shR = wrL = wrR = head = null;
            areaFrontL = areaFrontR = areaTail = areaWeapon = null;
            for (int i = 0; i < areaLegs.Length; i++) areaLegs[i] = null;
            phase = PhaseNone; atkT = areaSpin = 0; clock = Random.Range(0f, 10f);
            body = Pivot("Area enemy body", transform, Vector3.zero);
            spine = Pivot("Area spine", body, Vector3.zero);
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
        void BuildAncientRootBeast()
        {
            spine.localPosition = new Vector3(0,.64f,0);
            var bark = new Color(.49f,.28f,.14f); var moss = new Color(.35f,.69f,.20f);
            AreaPart(spine,Vector3.zero,new Vector3(1.12f,.84f,1.65f),bark);
            AreaQuadruped(bark,new Color(.59f,.65f,.48f));
            for (int i=0;i<5;i++)
            {
                float z=-.58f+i*.27f;
                AreaPart(spine,new Vector3(0,.35f,z),new Vector3(.98f,.32f,.40f),new Color(.56f,.65f,.47f),pBlob,new Vector3(0,0,i%2==0?12:-12));
                AreaPart(spine,new Vector3(-.16f,.49f,z),new Vector3(.55f,.12f,.32f),moss);
            }
            head=Pivot("Root crown",spine,new Vector3(0,.12f,.82f));
            AreaPart(head,Vector3.zero,new Vector3(.70f,.62f,.62f),bark);
            AreaPart(head,new Vector3(0,-.12f,.29f),new Vector3(.57f,.25f,.30f),new Color(.75f,.53f,.28f));
            AreaEyes(head,.19f,.09f,.30f,new Color(.95f,1,.29f));
            for(int s=-1;s<=1;s+=2)
            {
                AreaPart(head,new Vector3(s*.27f,.31f,0),new Vector3(.15f,.48f,.15f),bark,pCone,new Vector3(0,0,-s*30));
                AreaPart(head,new Vector3(s*.38f,.45f,0),new Vector3(.25f,.11f,.35f),moss,pLeaf);
            }
        }
        void BuildTidewhipEel()
        {
            spine.localPosition=new Vector3(0,.16f,0);
            AreaPart(body,new Vector3(0,.025f,0),new Vector3(1.65f,.045f,1.40f),new Color(.16f,.67f,.80f));
            Transform section=spine;
            for(int i=0;i<5;i++)
            {
                section=Pivot("Curved eel segment",section,new Vector3(0,i==0?0:.26f,i==0?0:.09f));
                AreaPart(section,Vector3.zero,new Vector3(.52f-i*.025f,.48f,.52f-i*.025f),new Color(.13f,.47f,.76f));
                AreaPart(section,new Vector3(0,0,.20f),new Vector3(.30f,.34f,.12f),new Color(.68f,.94f,.91f));

            }
            if(areaSail==null)
            {
                var mesh=new PMesh(1);
                for(int i=0;i<6;i++)
                {
                    float t=i/6f,u=(i+1)/6f;
                    var a=new Vector3(0,.12f+t*1.22f,-.24f+t*.34f);
                    var b=new Vector3(0,.12f+u*1.22f,-.24f+u*.34f);
                    var c=b+new Vector3(0,0,-Mathf.Sin(u*Mathf.PI)*.48f);
                    var d=a+new Vector3(0,0,-Mathf.Sin(t*Mathf.PI)*.48f);
                    mesh.Quad(a,b,c,d,Color.white);mesh.Quad(a,d,c,b,Color.white);
                }
                areaSail=mesh.Build("Eel continuous sail",true);
            }
            AreaPart(spine,Vector3.zero,Vector3.one,new Color(.28f,.84f,.90f),areaSail);
            head=Pivot("Eel jaw",section,new Vector3(0,.22f,.16f));
            AreaPart(head,Vector3.zero,new Vector3(.56f,.40f,.70f),new Color(.18f,.57f,.82f));
            AreaPart(head,new Vector3(0,-.13f,.23f),new Vector3(.41f,.08f,.37f),new Color(.77f,.96f,.92f));
            AreaEyes(head,.18f,.07f,.30f,new Color(1,.65f,.22f));
            areaTail=Pivot("Whip tail",spine,new Vector3(0,.15f,-.15f));
            for(int i=0;i<7;i++)
                AreaPart(areaTail,new Vector3(.10f*i,.05f*i,-.20f-i*.18f),new Vector3(.39f-i*.044f,.30f-i*.032f,.40f),new Color(.12f,.43f,.73f));
            AreaPart(areaTail,new Vector3(.70f,.35f,-1.48f),new Vector3(.32f,.04f,.36f),new Color(.31f,.87f,.91f),pLeaf,new Vector3(0,180,90));
        }
        void BuildChainGladiator()
        {
            spine.localPosition=new Vector3(0,.86f,0);
            var skin=new Color(.77f,.46f,.29f); var iron=new Color(.40f,.50f,.64f); var cloth=new Color(.72f,.20f,.26f);
            AreaPart(spine,new Vector3(0,.27f,0),new Vector3(.94f,.86f,.53f),skin);
            AreaPart(spine,new Vector3(0,-.10f,0),new Vector3(.69f,.32f,.50f),cloth);
            for(int s=-1;s<=1;s+=2)
            {
                var leg=areaLegs[s<0?0:1]=Pivot("Gladiator leg",body,new Vector3(s*.24f,.77f,0));
                AreaPart(leg,new Vector3(0,-.27f,0),new Vector3(.32f,.63f,.36f),skin);
                AreaPart(leg,new Vector3(0,-.65f,.07f),new Vector3(.35f,.23f,.48f),iron);
                var arm=Pivot("Sloping deltoid",spine,new Vector3(s*.47f,.52f,0));
                AreaPart(arm,new Vector3(s*.06f,-.16f,0),new Vector3(.34f,.49f,.35f),skin);
                AreaPart(arm,new Vector3(s*.08f,-.44f,.06f),new Vector3(.28f,.34f,.30f),iron);
                if(s>0)areaFrontR=arm;else areaFrontL=arm;
            }
            head=Pivot("Iron mask",spine,new Vector3(0,.91f,0));
            AreaPart(head,Vector3.zero,new Vector3(.47f,.54f,.45f),iron);
            AreaPart(head,new Vector3(0,-.12f,.23f),new Vector3(.35f,.23f,.08f),new Color(.68f,.76f,.83f));
            AreaEyes(head,.12f,.06f,.225f,new Color(1,.57f,.20f));
            areaWeapon=Pivot("Chain rotating from fist",areaFrontR,new Vector3(.08f,-.56f,.06f));
            for(int i=0;i<9;i++)AreaPart(areaWeapon,new Vector3(.12f+i*.115f,0,0),new Vector3(.14f,.065f,.065f),iron);
            AreaPart(areaWeapon,new Vector3(1.23f,0,0),Vector3.one*.37f,iron);
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3;
                AreaPart(areaWeapon,new Vector3(1.23f+Mathf.Cos(a)*.21f,Mathf.Sin(a)*.21f,0),new Vector3(.10f,.22f,.10f),new Color(.78f,.83f,.87f),pCone,new Vector3(0,0,i*60-90));
            }
        }
        void BuildRiftplateLizard()
        {
            spine.localPosition=new Vector3(0,.59f,0);
            var hide=new Color(.76f,.43f,.18f); var stone=new Color(.88f,.66f,.33f);
            AreaPart(spine,Vector3.zero,new Vector3(1.05f,.69f,1.72f),hide);
            AreaQuadruped(hide,stone);
            for(int i=0;i<6;i++)
                for(int s=-1;s<=1;s+=2)
                    AreaPart(spine,new Vector3(s*.29f,.31f,-.65f+i*.24f),new Vector3(.49f,.27f,.36f),stone,pBlob,new Vector3(0,s*15,s*24));
            head=Pivot("Lizard head",spine,new Vector3(0,-.02f,.92f));
            AreaPart(head,Vector3.zero,new Vector3(.65f,.39f,.66f),hide);
            AreaPart(head,new Vector3(0,.14f,0),new Vector3(.63f,.14f,.56f),stone);
            AreaEyes(head,.22f,.02f,.29f,new Color(.61f,1,.29f));
            areaTail=Pivot("Tapering stone tail",spine,new Vector3(0,0,-.72f));
            for(int i=0;i<7;i++)
                AreaPart(areaTail,new Vector3(.07f*i,-.035f*i,-.15f-i*.19f),new Vector3(.46f-i*.055f,.34f-i*.038f,.40f),i%2==0?stone:hide);
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

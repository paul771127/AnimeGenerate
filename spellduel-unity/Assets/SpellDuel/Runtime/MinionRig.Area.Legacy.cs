#if UNITY_EDITOR
using UnityEngine;
namespace SpellDuel { public partial class MinionRig {
static bool rarePreviewLegacy;
        void BuildAncientRootBeastLegacy()
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
        void BuildTidewhipEelLegacy()
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
        void BuildChainGladiatorLegacy()
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
        void BuildRiftplateLizardLegacy()
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

}}
#endif

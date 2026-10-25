using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        Transform eliteArmL,eliteArmR,eliteLegL,eliteLegR,eliteCrown,eliteTool;
        int eliteSlot;
        Renderer[] eliteRenderers;MaterialPropertyBlock eliteTint;
        public void EliteSkill(int slot){eliteSlot=Mathf.Clamp(slot,0,1);}
        void BuildElite(int k)
        {
            variantId=kind=k;sea=plant=false;dead=false;phase=PhaseNone;atkT=hitT=deadT=clock=0;
            var d=EliteCatalog.Get(k);v=Variants[d.baseKind];embers=null;
            body=EnemyArt.Pivot(transform,"Elite "+d.name,Vector3.zero);spine=EnemyArt.Pivot(body,"Articulated torso",new Vector3(0,.77f,0));
            Color c=d.color,armor=Color.Lerp(c,new Color(.25f,.29f,.42f),.45f),gold=new Color(1,.8f,.29f);
            float width=d.role==0?.92f:d.role==1?.57f:.62f;
            EnemyArt.Part(spine,"Curved chest armor",new Vector3(0,.18f,0),new Vector3(width,.75f,.49f),armor);
            EnemyArt.Part(spine,"Elite heart crest",new Vector3(0,.28f,.26f),new Vector3(.18f,.24f,.09f),gold,true);
            head=EnemyArt.Pivot(spine,"Head",new Vector3(0,.72f,.02f));
            EnemyArt.Part(head,"Mask or beast skull",Vector3.zero,new Vector3(.43f,.39f,.38f),c);
            for(int s=-1;s<=1;s+=2)EnemyArt.Part(head,"Luminous slit",new Vector3(s*.11f,.03f,.18f),new Vector3(.13f,.045f,.035f),gold);
            eliteCrown=EnemyArt.Pivot(head,"Elite crown",new Vector3(0,.2f,0));
            for(int i=-1;i<=1;i++)EnemyArt.Part(eliteCrown,"Crown tooth",new Vector3(i*.15f,.08f,0),new Vector3(.12f,.22f+(i==0?.1f:0),.12f),gold,true,new Vector3(0,0,-i*20));
            eliteArmL=EnemyArt.Pivot(spine,"Left shoulder",new Vector3(-width*.52f,.4f,0));
            eliteArmR=EnemyArt.Pivot(spine,"Right shoulder",new Vector3(width*.52f,.4f,0));
            for(int side=0;side<2;side++)
            {
                var arm=side==0?eliteArmL:eliteArmR;
                EnemyArt.Part(arm,"Sloping shoulder",new Vector3(0,-.07f,0),new Vector3(.34f,.28f,.38f),c);
                EnemyArt.Part(arm,"Tapered forelimb",new Vector3(0,-.35f,0),new Vector3(.22f,.59f,.26f),armor);
                EnemyArt.Part(arm,"Claw gauntlet",new Vector3(0,-.62f,.05f),new Vector3(.29f,.21f,.3f),gold);
            }
            eliteLegL=EnemyArt.Pivot(spine,"Left hip",new Vector3(-.21f,-.15f,0));eliteLegR=EnemyArt.Pivot(spine,"Right hip",new Vector3(.21f,-.15f,0));
            foreach(var leg in new[]{eliteLegL,eliteLegR}){EnemyArt.Part(leg,"Jointed thigh",new Vector3(0,-.23f,0),new Vector3(.26f,.48f,.29f),armor);EnemyArt.Part(leg,"Planted foot",new Vector3(0,-.53f,.08f),new Vector3(.32f,.22f,.4f),c);}
            eliteTool=EnemyArt.Pivot(eliteArmR,"Distinctive weapon",new Vector3(0,-.57f,.1f));
            if(d.role==0)
            {
                EnemyArt.Part(eliteArmL,"Great curved shield",new Vector3(-.1f,-.3f,.24f),new Vector3(.60f,.87f,.20f),c);
                EnemyArt.Beam(eliteTool,"Hammer shaft",new Vector3(0,-.15f,0),new Vector3(0,.52f,0),.09f,gold);
                EnemyArt.Part(eliteTool,"Hammer head",new Vector3(0,.54f,0),new Vector3(.55f,.26f,.32f),armor);
            }
            else if(d.role==1)
            {
                EnemyArt.Part(eliteTool,"Twin launcher stock",new Vector3(0,.1f,.28f),new Vector3(.45f,.24f,.65f),armor);
                for(int s=-1;s<=1;s+=2)EnemyArt.Part(eliteTool,"Paired barrel",new Vector3(s*.13f,.14f,.6f),new Vector3(.12f,.12f,.7f),gold);
                EnemyArt.Part(spine,"Back sail",new Vector3(0,.4f,-.3f),new Vector3(.65f,.9f,.17f),c,true);
            }
            else if(d.role==2)
            {
                for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;EnemyArt.Part(head,"Radial cowl petal",new Vector3(Mathf.Cos(a)*.3f,Mathf.Sin(a)*.3f,-.1f),new Vector3(.2f,.4f,.14f),c,true,new Vector3(0,0,-a*Mathf.Rad2Deg));}
                EnemyArt.Part(eliteTool,"Spell focus",new Vector3(0,.5f,0),new Vector3(.36f,.4f,.36f),gold,true);
            }
            else if(d.role==3)
            {
                EnemyArt.Part(spine,"Hollow nursery back frame",new Vector3(0,.24f,-.35f),new Vector3(.75f,.8f,.32f),armor);
                for(int s=-1;s<=1;s+=2)EnemyArt.Part(spine,"Nest lantern",new Vector3(s*.22f,.4f,-.56f),new Vector3(.21f,.34f,.22f),gold);
                EnemyArt.Beam(eliteTool,"Conductor staff",new Vector3(0,-.4f,0),new Vector3(0,.85f,0),.08f,gold);
                EnemyArt.Part(eliteTool,"Summoning bell",new Vector3(0,.8f,0),new Vector3(.33f,.27f,.33f),c);
            }
            else
            {
                foreach(var a in new[]{eliteArmL,eliteArmR})EnemyArt.Part(a,"Long hunting blade",new Vector3(0,-.55f,.37f),new Vector3(.14f,.16f,.8f),gold,true,new Vector3(90,0,0));
                EnemyArt.Part(spine,"Hunter tail",new Vector3(0,-.05f,-.47f),new Vector3(.20f,.22f,.85f),c,true,new Vector3(-90,0,0));
            }
            // Theme anatomy: each chapter changes the physical crest/back structure, rather than only tint.
            for(int side=-1;side<=1;side+=2)
            {
                if(d.chapter==0)EnemyArt.Part(head,"Obsidian furnace horn",new Vector3(side*.25f,.22f,0),new Vector3(.19f,.43f,.19f),armor,true,new Vector3(0,0,-side*25));
                if(d.chapter==1)EnemyArt.Part(spine,"Root branch",new Vector3(side*.35f,.72f,-.20f),new Vector3(.17f,.63f,.23f),c,true,new Vector3(0,0,-side*35));
                if(d.chapter==2)EnemyArt.Part(head,"Coral fin",new Vector3(side*.28f,.01f,-.04f),new Vector3(.12f,.55f,.34f),c,true,new Vector3(0,0,-side*40));
                if(d.chapter==3)EnemyArt.Part(head,"Arena mask cheek",new Vector3(side*.13f,-.08f,.16f),new Vector3(.15f,.31f,.1f),gold);
                if(d.chapter==4)EnemyArt.Part(spine,"Sand crystal plate",new Vector3(side*.31f,.56f,-.27f),new Vector3(.24f,.68f,.25f),gold,true,new Vector3(0,0,-side*18));
            }
            EnemyArt.Bake(body,"elite/"+k);eliteRenderers=body.GetComponentsInChildren<Renderer>();eliteTint=new MaterialPropertyBlock();AnimateElite(0);
        }
        void AnimateElite(float dt)
        {
            clock+=dt;atkT+=dt;hitT=Mathf.Max(0,hitT-dt);if(dead)deadT+=dt;
            float wind=phase==PhaseWindup?Mathf.Clamp01(atkT/Mathf.Max(.05f,atkDur)):0;
            float strike=phase==PhaseStrike?Mathf.Sin(Mathf.Clamp01(atkT/.5f)*Mathf.PI):0;
            if(phase==PhaseStrike&&atkT>.5f)phase=PhaseNone;
            var move=EliteCatalog.Get(kind).moves[eliteSlot];float walk=dead?0:Mathf.Clamp01(MoveSpeed)*Mathf.Sin(clock*5);
            spine.localPosition=new Vector3(0,.77f+Mathf.Sin(clock*2)*.018f,0);
            spine.localRotation=Quaternion.Euler(hitT*90+(move==EliteMove.Burst?-wind*25:wind*8)+strike*16,move==EliteMove.Sweep?-wind*40+strike*95:0,0);
            eliteArmL.localRotation=Quaternion.Euler(move==EliteMove.Quake?-wind*155+strike*70:-wind*65+strike*40,0,wind*20);
            eliteArmR.localRotation=Quaternion.Euler(-wind*(move==EliteMove.Line?80:145)+strike*100,move==EliteMove.Sweep?wind*65:0,-wind*18);
            eliteLegL.localRotation=Quaternion.Euler(walk*24,0,0);eliteLegR.localRotation=Quaternion.Euler(-walk*24,0,0);
            head.localRotation=Quaternion.Euler(-wind*15,Mathf.Sin(clock)*5,0);
            if(dead){float t=Mathf.Clamp01(deadT/.9f);body.localRotation=Quaternion.Euler(t*85,0,0);body.localScale=Vector3.one*(1-t*.65f);}
            else {body.localRotation=Quaternion.identity;body.localScale=Vector3.one;}
            var tint=Color.Lerp(Color.white,new Color(.28f,.27f,.34f),dead?Mathf.Clamp01(deadT/.9f):0);
            tint=Color.Lerp(tint,new Color(2,2,2,1),Mathf.Clamp01(hitT/HitDur));eliteTint.SetColor("_Color",tint);
            foreach(var r in eliteRenderers)if(r)r.SetPropertyBlock(eliteTint);
        }
    }
}

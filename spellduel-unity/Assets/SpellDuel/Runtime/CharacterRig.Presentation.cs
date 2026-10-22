using UnityEngine;
namespace SpellDuel
{
    public partial class CharacterRig
    {
        /// <summary>Visual stance only. Caller integration may set false in lobbies/towns/dialogue.</summary>
        public bool InCombat { get; set; } = true;
        HumanExpressionMotion expressionMotion;float combatAmount=1;Vector3[] leftSole,rightSole;
        HumanPersonality.Pose idlePresentation,battlePresentation;
        void SetupPresentation()
        {
            string id=advancedId??classId;idlePresentation=HumanPersonality.Stance(id,false);battlePresentation=HumanPersonality.Stance(id,true);
            float variation=(CharacterLook.Get(id).Seed%3)-1;
            idlePresentation.L=new Vector3(-4-variation,6,-10);idlePresentation.R=new Vector3(-5+variation,7,-11);
            expressionMotion=head.GetComponent<HumanExpressionMotion>();expressionMotion.External=true;
            HumanProportions.Fit(head,id,1.75f);Vector3 keep=head.position;
            // Lengthen the rib-cage/neck region into the space freed by the smaller head.
            // Counter-scale arms/head so muscles, hands, weapons and hair do not stretch.
            spine.localScale=new Vector3(1,1.10f,1);shL.localScale=shR.localScale=new Vector3(1,1/1.10f,1);
            head.localScale=new Vector3(head.localScale.x,head.localScale.y/1.10f,head.localScale.z);head.position=keep;
            leftSole=OutfitArt.MeshFor(id,"calfL",Skin).vertices;rightSole=OutfitArt.MeshFor(id,"calfR",Skin).vertices;
        }
        void UpdatePresentation(float dt)
        {
            // The explicit mode is authoritative, including walking through a non-combat town.
            // Existing walk/guard/charge/cast animations remain overlays on the chosen base stance.
            combatAmount=Mathf.MoveTowards(combatAmount,InCombat?1:0,dt/.4f);
        }
        Arm PresentationArm(bool left)
        {
            if(phoneDriven)return left?baseL:baseR;   // 真人分身：姿勢照手機（CharacterRig.Body.cs），不套預設站姿

            float t=combatAmount*combatAmount*(3-2*combatAmount);
            var v=Vector3.Lerp(left?idlePresentation.L:idlePresentation.R,left?battlePresentation.L:battlePresentation.R,t);
            return new Arm(v.x,v.y,v.z,classId=="assassin"?Mathf.Lerp(-45,65,t):0);
        }
        void PosePresentation(float deadE)
        {
            if(phoneDriven)return;

            float t=combatAmount*combatAmount*(3-2*combatAmount),still=(1-walkAmt)*(1-deadE);
            float shift=Mathf.Lerp(idlePresentation.Shift,battlePresentation.Shift,t);
            float roll=Mathf.Lerp(idlePresentation.Roll,battlePresentation.Roll,t);
            float twist=Mathf.Lerp(idlePresentation.Twist,battlePresentation.Twist,t);
            float lean=Mathf.Lerp(idlePresentation.Lean,battlePresentation.Lean,t);
            bool coat=OutfitArt.Get(advancedId??classId).Hem>.15f;
            // Long garments use a compact stance so the unchanged cloth never intersects the thighs.
            shift*=coat?.4f:.65f;if(coat){roll*=.3f;twist*=.3f;lean*=.5f;}
            // A seeded 6..12 second cycle, with a short smooth adjustment once per cycle.
            float period=6+CharacterLook.Get(advancedId??classId).Seed%7,phase=clock%period;
            float gesture=phase<1.6f?Mathf.Sin(phase/1.6f*Mathf.PI):0;
            float drop=Mathf.Lerp(idlePresentation.Drop,battlePresentation.Drop,t);
            body.localPosition+=new Vector3(shift*still,-drop*still,0);
            spine.localRotation=spine.localRotation*Quaternion.Euler(lean*still,twist*still,(roll+gesture*(1-t))*still);
            neck.localRotation=neck.localRotation*Quaternion.Euler(0,-twist*.40f*still,-roll*.5f*still);
            float bend=Mathf.Lerp(4, classId=="assassin"?22:12,t)*still*(coat?.35f:1);
            hipL.localRotation=hipL.localRotation*Quaternion.Euler(-bend*.5f,0,0);
            hipR.localRotation=hipR.localRotation*Quaternion.Euler(-bend*.5f,0,0);
            knL.localRotation=knL.localRotation*Quaternion.Euler(bend,0,0);knR.localRotation=knR.localRotation*Quaternion.Euler(bend,0,0);
            hipL.localPosition=new Vector3(-.085f*(classId=="assassin"?.85f:1)-shift*still,.85f,Mathf.Lerp(idlePresentation.Step,battlePresentation.Step,t)*still*(coat?.15f:1));
            hipR.localPosition=new Vector3(.085f*(classId=="assassin"?.85f:1)-shift*still,.85f,-Mathf.Lerp(idlePresentation.Step,battlePresentation.Step,t)*still*(coat?.15f:1));
            if(deadE<.001f){PlantSole(hipL,knL,leftSole);PlantSole(hipR,knR,rightSole);}
        }
        void PlantSole(Transform hip,Transform knee,Vector3[] verts)
        {
            // Cached calf vertices; one matrix evaluation, no allocation, includes actual shoe geometry.
            var m=transform.worldToLocalMatrix*knee.localToWorldMatrix;float low=10;
            for(int i=0;i<verts.Length;i++)low=Mathf.Min(low,m.MultiplyPoint3x4(verts[i]).y);
            hip.localPosition+=Vector3.up*(-low);
        }
    }
}

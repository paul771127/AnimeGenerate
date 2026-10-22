using UnityEngine;
namespace SpellDuel
{
    internal sealed partial class HumanResidentMotion
    {
        sealed class Leg
        {
            internal Transform hip,knee,ankle;internal Vector3 rest,anchor,takeoff,lastGoal,landing;internal Quaternion contactRotation;
            internal float upper,lower,footHeight,lift;internal bool swinging;internal float pivot=-1;
            internal Leg(Transform h,Transform k)
            {
                hip=h;knee=k;ankle=k.Find("Resident ankle");rest=h.localPosition;upper=-k.localPosition.y;lower=-ankle.localPosition.y;
                var mesh=k.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;float low=0;foreach(var v in mesh.vertices)low=Mathf.Min(low,v.y);footHeight=ankle.localPosition.y-low;
            }
        }
        Leg legL,legR;bool feetReady;Vector3 lastRoot;
        void ResetFeet()
        {
            foreach(var leg in new[]{legL,legR})
            {
                leg.anchor=figure.TransformPoint(new Vector3(leg.rest.x,0,0));leg.takeoff=leg.lastGoal=leg.landing=leg.anchor;
                leg.contactRotation=figure.rotation;leg.swinging=false;leg.lift=0;leg.pivot=-1;
            }
            feetReady=true;lastRoot=figure.position;
        }
        void UpdateFeet(float dt,float strideWorld)
        {
            if(!feetReady||(figure.position-lastRoot).magnitude>1)ResetFeet();lastRoot=figure.position;
            StepFoot(legL,0,dt,strideWorld);StepFoot(legR,.5f,dt,strideWorld);
        }
        void StepFoot(Leg leg,float offset,float dt,float strideWorld)
        {
            float phase=(walkPhase+offset)%1,scale=figure.lossyScale.y,localStride=strideWorld/Mathf.Max(.01f,scale);
            float support=child?.48f:.62f;bool walking=walkAmt>.03f,air=walking&&phase>support;Vector3 goal;
            if(idleSpin!=0)
            {
                // A deliberately lifted dance step, separate from planted locomotion.
                goal=figure.TransformPoint(new Vector3(leg.rest.x,0,0));leg.anchor=goal;leg.lift=.018f+Mathf.Abs(Mathf.Sin(clock*8+offset*Mathf.PI*2))*.025f;
                leg.contactRotation=figure.rotation;leg.swinging=false;
            }
            else if(air)
            {
                float u=Ease(Mathf.Clamp01((phase-support)/(1-support)));
                if(!leg.swinging){leg.takeoff=leg.anchor;leg.swinging=true;}
                leg.landing=figure.TransformPoint(new Vector3(leg.rest.x,0,localStride*(child?.24f:.31f)));
                goal=Vector3.Lerp(leg.takeoff,leg.landing,u);leg.lift=Mathf.Sin(u*Mathf.PI)*(child?.085f:elder?.027f:coat?.040f:.060f);
                leg.contactRotation=Quaternion.Slerp(leg.contactRotation,figure.rotation,Mathf.Clamp01(dt*10));
            }
            else if(leg.swinging)
            {
                if(walking){leg.anchor=leg.landing;leg.lift=0;leg.swinging=false;}
                else
                {
                    leg.lift=Mathf.MoveTowards(leg.lift,0,dt*.22f);
                    if(leg.lift<.0001f){leg.anchor=leg.lastGoal;leg.swinging=false;}
                }
                goal=walking?leg.anchor:leg.lastGoal;
            }
            else
            {
                goal=leg.anchor;leg.lift=0;
                // Replant, rather than drag, a foot while TownWalk turns the actor.
                bool otherPivot=(leg==legL?legR:legL).pivot>=0;
                if(!walking&&!otherPivot&&leg.pivot<0&&Quaternion.Angle(leg.contactRotation,figure.rotation)>52)
                {leg.pivot=0;leg.takeoff=leg.anchor;leg.landing=figure.TransformPoint(new Vector3(leg.rest.x,0,0));}
                if(leg.pivot>=0)
                {
                    leg.pivot+=dt/.32f;float u=Ease(Mathf.Clamp01(leg.pivot));goal=Vector3.Lerp(leg.takeoff,leg.landing,u);leg.lift=Mathf.Sin(u*Mathf.PI)*.035f;
                    leg.contactRotation=Quaternion.Slerp(leg.contactRotation,figure.rotation,u);
                    if(leg.pivot>=1){leg.anchor=leg.landing;leg.pivot=-1;leg.lift=0;}
                }
            }
            leg.lastGoal=goal;
            Vector3 local=figure.InverseTransformPoint(goal);local.y=leg.footHeight+leg.lift+idleHop;
            Vector3 hip=leg.rest;hip.y+=idleHop-walkAmt*.022f;
            float horizontal=(new Vector2(local.x-hip.x,local.z-hip.z)).magnitude,total=leg.upper+leg.lower-.003f;
            hip.y=Mathf.Min(hip.y,local.y+Mathf.Sqrt(Mathf.Max(.02f,total*total-horizontal*horizontal)));
            leg.hip.localPosition=hip;
            Vector3 delta=local-hip;float length=Mathf.Clamp(delta.magnitude,.06f,total);Vector3 axis=delta.normalized,bend=Vector3.forward-axis*Vector3.Dot(Vector3.forward,axis);bend=bend.normalized;
            float along=(leg.upper*leg.upper-leg.lower*leg.lower+length*length)/(2*length),height=Mathf.Sqrt(Mathf.Max(0,leg.upper*leg.upper-along*along));Vector3 middle=hip+axis*along+bend*height;
            leg.hip.rotation=Quaternion.FromToRotation(Vector3.down,figure.TransformDirection(middle-hip));
            leg.knee.rotation=Quaternion.FromToRotation(Vector3.down,figure.TransformDirection(hip+axis*length-middle));leg.ankle.rotation=leg.contactRotation;
        }
    }
}

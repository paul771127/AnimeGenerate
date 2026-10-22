using UnityEngine;
namespace SpellDuel
{
    internal sealed partial class HumanResidentMotion
    {
        bool artBound,stationCaptured;Transform station,sparks,turningPage,stationFrame,purse,reel;Vector3 stationPosition;Quaternion stationRotation;
        internal void BindActionArt()
        {
            foreach(var tf in figure.GetComponentsInChildren<Transform>())
            {
                if(tf.gameObject.name=="Resident workstation")station=tf;
                if(tf.gameObject.name=="Resident forge sparks")sparks=tf;
                if(tf.gameObject.name=="Resident spinning reel")reel=tf;
                if(tf.gameObject.name=="Resident counting purse")purse=tf;
                if(tf.gameObject.name=="Resident turning page")turningPage=tf;
            }
            // Inactive sparks are deliberately excluded by GetComponentsInChildren.
            if(station!=null&&sparks==null)sparks=station.Find("Resident forge sparks");
            artBound=true;feetReady=false;
        }
        bool StationNearby()=>station!=null&&(!stationCaptured||(new Vector2(figure.position.x-station.position.x,figure.position.z-station.position.z)).magnitude<.70f*figure.lossyScale.y);
        void TickActionArt()
        {
            if(station!=null)
            {
                if(!stationCaptured){stationFrame=figure.parent==null?null:figure.parent.parent;stationPosition=stationFrame==null?station.position:stationFrame.InverseTransformPoint(station.position);stationRotation=stationFrame==null?station.rotation:Quaternion.Inverse(stationFrame.rotation)*station.rotation;stationCaptured=true;}
                station.position=stationFrame==null?stationPosition:stationFrame.TransformPoint(stationPosition);station.rotation=stationFrame==null?stationRotation:stationFrame.rotation*stationRotation;
            }
            if(sparks!=null)
            {
                bool strike=IdleIndex==0&&idleWeight>.75f&&StationNearby()&&Mathf.Sin(idleProgress*Mathf.PI*8)<-.92f&&walkAmt<.01f;
                if(sparks.gameObject.activeSelf!=strike)sparks.gameObject.SetActive(strike);
                sparks.localRotation=Quaternion.Euler(0,idleProgress*300,0);
            }
            if(reel!=null)reel.localRotation=Quaternion.Euler(90,0,0)*Quaternion.Euler(0,IdleIndex==1?idleProgress*720*idleWeight:0,0);
            if(purse!=null)purse.rotation=figure.rotation;
            if(turningPage!=null)turningPage.localRotation=Quaternion.Euler(0,Mathf.Max(0,Mathf.Sin(idleProgress*Mathf.PI*4))*135*idleWeight,0);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class CharacterRig
    {
        static readonly Dictionary<string,Mesh> playerWeaponMeshes=new Dictionary<string,Mesh>();
        static void ReferenceSegment(PMesh m,Vector3 a,Vector3 b,float radius,Color color)
        {
            var d=b-a;m.Lathe(new[]{0f,radius*.80f,d.magnitude*.12f,radius,d.magnitude*.88f,radius,d.magnitude,radius*.65f},8,(z,i)=>color,1,1,0,null,a,Quaternion.LookRotation(d));
        }
        static Mesh ReferenceWeapon(string id,string type)
        {
            string key=id+"/"+type;if(playerWeaponMeshes.TryGetValue(key,out var mesh))return mesh;
            var m=new PMesh(1);var d=PlayerClassArt.Design(id);Color gold=d.Accent,silver=new Color(.76f,.84f,.91f),leather=new Color(.30f,.23f,.20f);
            if(type=="Sword"||type=="Dagger"||type=="CrescentBlade")
            {
                bool knife=type!="Sword",kunai=id=="ninja";float end=knife?.41f:id=="novice"?.60f:.78f;
                Color blade= id=="assassin"?new Color(.79f,.21f,.27f):id=="shadowdancer"?new Color(.70f,.32f,.68f):silver;
                m.Lathe(new[]{-.12f,.018f,.055f,.018f},8,(z,i)=>leather);
                m.Lathe(new[]{.065f,.028f,.14f,knife?.054f:.062f,end-.09f,knife?.022f:.035f,end,0f},4,(z,i)=>blade,1,.20f,45);
                ReferenceSegment(m,new Vector3(knife?-.058f:-.11f,0,.065f),new Vector3(knife?.058f:.11f,0,.065f),.015f,gold);
                if(kunai)
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;ReferenceSegment(m,new Vector3(Mathf.Cos(a)*.04f,0,-.16f+Mathf.Sin(a)*.04f),new Vector3(Mathf.Cos(b)*.04f,0,-.16f+Mathf.Sin(b)*.04f),.008f,silver);}
                else m.Lathe(new[]{-.15f,0f,-.12f,.025f,-.10f,.02f},8,(z,i)=>gold);
                ReferenceSegment(m,new Vector3(0,.011f,.17f),new Vector3(0,.006f,end-.06f),.005f,id=="swordsman"?new Color(.34f,.65f,.95f):gold);
            }
            else if(type=="Bow")
            {
                bool precise=id=="marksman";
                for(int i=0;i<16;i++)
                {
                    float a=-1+i/8f,b=-1+(i+1)/8f;
                    ReferenceSegment(m,BowPoint(a,.57f,.16f),BowPoint(b,.57f,.16f),.018f,precise?gold:leather);
                    if(Mathf.Abs(a)>.24f)ReferenceSegment(m,BowPoint(a,.57f,.16f)+new Vector3(.023f,-.008f,0),BowPoint(b,.57f,.16f)+new Vector3(.023f,-.008f,0),.008f,gold);
                }
                ReferenceSegment(m,new Vector3(0,0,-.065f),new Vector3(0,0,.065f),.023f,leather);
                if(precise)for(int side=-1;side<=1;side+=2)
                {
                    ReferenceSegment(m,new Vector3(0,.03f,side*.17f),new Vector3(.10f,.06f,side*.35f),.009f,gold);
                    ReferenceSegment(m,new Vector3(.10f,.06f,side*.35f),new Vector3(0,.12f,side*.49f),.009f,gold);
                }
            }
            else if(type=="Staff"||type=="HolyLance")
            {
                bool holy=id=="priest"||id=="bishop"||id=="inquisitor";
                m.Lathe(new[]{-.30f,.014f,.79f,.014f},10,(z,i)=>holy?gold:leather);
                for(int band=0;band<3;band++){float z=-.25f+band*.50f;m.Lathe(new[]{z,.019f,z+.035f,.019f},10,(h,i)=>gold);}
                int ringSides=holy?16:12;
                for(int i=0;i<ringSides;i++)
                {
                    float a=i*Mathf.PI*2/ringSides,b=(i+1)*Mathf.PI*2/ringSides;float r=holy?.14f:.11f;
                    // Ornament in the local XZ plane faces the same way as the existing staff head.
                    ReferenceSegment(m,new Vector3(Mathf.Cos(a)*r,0,.96f+Mathf.Sin(a)*r),new Vector3(Mathf.Cos(b)*r,0,.96f+Mathf.Sin(b)*r),holy?.012f:.009f,gold);
                    if(holy&&i%2==0)ReferenceSegment(m,new Vector3(Mathf.Cos(a)*r,0,.96f+Mathf.Sin(a)*r),new Vector3(Mathf.Cos(a)*(r+.055f),0,.96f+Mathf.Sin(a)*(r+.055f)),.009f,gold);
                }
                if(holy)
                {
                    ReferenceSegment(m,new Vector3(0,0,.79f),new Vector3(0,0,1.15f),.015f,gold);
                    ReferenceSegment(m,new Vector3(-.10f,0,.96f),new Vector3(.10f,0,.96f),.013f,gold);
                    if(id=="inquisitor")for(int s=-1;s<=1;s+=2)
                        m.Tri(new Vector3(s*.13f,0,.94f),new Vector3(s*.22f,0,1.13f),new Vector3(s*.10f,0,1.08f),gold);
                }
                else for(int s=-1;s<=1;s+=2)
                {
                    ReferenceSegment(m,new Vector3(0,0,.78f),new Vector3(s*.13f,0,.94f),.020f,gold);
                    ReferenceSegment(m,new Vector3(s*.13f,0,.94f),new Vector3(s*.09f,0,1.12f),.014f,gold);
                }
            }
            else if(type=="Shield")
            {
                for(int layer=0;layer<2;layer++)
                {
                    float w=layer==0?.23f:.195f,top=layer==0?.35f:.31f,bottom=layer==0?-.46f:-.40f,y=.025f+layer*.015f;
                    Vector3 a=new Vector3(-w,y,top),b=new Vector3(w,y,top),c=new Vector3(w*.88f,y,-.14f),e=new Vector3(0,y,bottom),f=new Vector3(-w*.88f,y,-.14f);
                    m.Tri(a,c,b,layer==0?gold:new Color(.91f,.91f,.84f));m.Tri(a,f,c,layer==0?gold:new Color(.91f,.91f,.84f));m.Tri(f,e,c,layer==0?gold:new Color(.91f,.91f,.84f));
                    m.Tri(a,b,c,layer==0?gold:d.Sub);m.Tri(a,c,f,layer==0?gold:d.Sub);m.Tri(f,c,e,layer==0?gold:d.Sub);
                }
                ReferenceSegment(m,new Vector3(0,.055f,-.25f),new Vector3(0,.055f,.25f),.014f,gold);
                ReferenceSegment(m,new Vector3(-.12f,.055f,.06f),new Vector3(.12f,.055f,.06f),.014f,gold);
            }
            mesh=m.Build("Player reference weapon "+key,true);playerWeaponMeshes[key]=mesh;return mesh;
        }
        void ReplaceReferenceProp(Transform root,string id,string type)
        {
            if(root==null)return;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                if(orb!=null&&f.transform.IsChildOf(orb))continue;
                if(f.transform==root){f.GetComponent<Renderer>().enabled=false;continue;}
                f.transform.SetParent(null,false);RemoveVisual(f.gameObject);
            }
            root.localScale=Vector3.one;
            AnimeFace.Part(root,"Reference weapon surface",ReferenceWeapon(id,type),Vector3.zero,AnimeFace.Material(Color.white,4));
            if(type=="Bow")Seg(root,BowPoint(-1,.57f,.16f),BowPoint(1,.57f,.16f),.003f,Glow(new Color(.94f,.89f,.63f,.7f)));
        }
        void MatchDefaultWeapons()
        {
            string id=advancedId??classId;
            if(halo!=null){RemoveVisual(halo.gameObject);halo=null;}
            if(id!="paladin"&&shield!=null){RemoveVisual(shield.gameObject);shield=null;}
            if(id=="paladin")ReplaceReferenceProp(shield,id,"Shield");
            foreach(var hand in new[]{handL,handR})
                for(int i=0;i<hand.childCount;i++)
                {
                    var prop=hand.GetChild(i);string type=prop.gameObject.name;
                    if(type=="Sword"&&id=="berserker")continue; // Actual jian/qi components used by 劍來 remain intact.
                    if(type=="Sword"||type=="Dagger"||type=="CrescentBlade"||type=="Bow"||type=="Staff"||type=="HolyLance")ReplaceReferenceProp(prop,id,type);
                }
            if(orb!=null)
            {
                var color=id=="summoner"?new Color(1,.44f,.10f):id=="mage"||id=="archmage"?new Color(.68f,.35f,1):new Color(1,.83f,.38f);
                orb.GetComponent<Renderer>().sharedMaterial=Glow(color);orbBaseScale=Vector3.one*(id=="archmage"?.16f:.12f);orb.localScale=orbBaseScale;
            }
            // Cloaks are existing animated pivots; coat hems are attached to the unmodified spine.
            bool cloak=id=="novice"||id=="swordsman"||id=="assassin"||id=="priest"||id=="paladin"||id=="marksman"||id=="ranger"||id=="archer"||id=="archmage"||id=="bishop";
            if(cloak&&cape==null)cape=Pivot("Cape",spine,new Vector3(0,.51f,-.14f));
            if(id=="summoner"){foreach(var mote in motes)if(mote!=null)RemoveVisual(mote.gameObject);motes.Clear();}
            if(id=="ninja")RemoveNamed(spine,"NinjaSword");
            if(id=="bishop")Book(handL,new Vector3(0,-.05f,.10f),PlayerClassArt.Design(id).Sub,1.15f);
            if(id=="ranger")
            {
                var falcon=shR.Find("PerchedFalcon");if(falcon!=null){falcon.SetParent(elR,false);falcon.localPosition=new Vector3(.07f,-.11f,.025f);falcon.localRotation=Quaternion.Euler(0,0,-75);}
            }
        }
    }
}

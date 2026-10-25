using UnityEngine;
namespace SpellDuel
{
    // Persistent warning geometry. Only transform/color changes each frame; SpellFx owns shared FX materials.
    public sealed class EliteEnemyFx : MonoBehaviour
    {
        LineRenderer edge,guide;float age,life;bool transient;EliteMove move;
        public static EliteEnemyFx Create(Transform parent,EliteMove move)
        {
            var go=new GameObject("Elite "+move+" telegraph");if(parent)go.transform.SetParent(parent,false);
            var fx=go.AddComponent<EliteEnemyFx>();fx.move=move;
            var points=new Vector3[65];float radius=EliteCatalog.Radius(move);
            for(int i=0;i<65;i++)
            {
                float a=(move==EliteMove.Sweep?-90+i*180f/64:i*360f/64)*Mathf.Deg2Rad;
                points[i]=new Vector3(Mathf.Sin(a)*radius,.035f,Mathf.Cos(a)*radius);
            }
            if(move==EliteMove.Line)points=new[]{new Vector3(-.28f,.04f,0),new Vector3(-.28f,.04f,2.6f),new Vector3(.28f,.04f,2.6f),new Vector3(.28f,.04f,0),new Vector3(-.28f,.04f,0)};
            if(move==EliteMove.Shield){points=new Vector3[7];for(int i=0;i<7;i++){float a=i*Mathf.PI/3;points[i]=new Vector3(Mathf.Sin(a)*radius,.035f,Mathf.Cos(a)*radius);}}
            fx.edge=SpellFx.Line(go.transform,"Locked attack boundary",points,false,.06f,new Color(1,.16f,.12f,.85f),true,false);
            fx.guide=SpellFx.Line(go.transform,"Visible attack direction",new[]{Vector3.up*.045f,new Vector3(0,.045f,radius)},false,.035f,Color.white,true,false);
            if(move==EliteMove.Heal)
            {
                SpellFx.Line(go.transform,"Healing cross across",new[]{new Vector3(-.3f,.09f,0),new Vector3(.3f,.09f,0)},false,.12f,new Color(.3f,1,.5f),true,false);
                SpellFx.Line(go.transform,"Healing cross upright",new[]{new Vector3(0,.09f,-.3f),new Vector3(0,.09f,.3f)},false,.12f,new Color(.3f,1,.5f),true,false);
            }
            if(move==EliteMove.Summon)
            {
                var star=new Vector3[6];for(int i=0;i<6;i++){float a=i*Mathf.PI*4/5;star[i]=new Vector3(Mathf.Sin(a)*.65f,.07f,Mathf.Cos(a)*.65f);}
                SpellFx.Line(go.transform,"Summoning pentacle",star,false,.05f,new Color(1,.8f,.3f),true,false);
            }
            return fx;
        }
        public void Place(Vector3 at,Vector3 direction,Color color,float progress,bool impact=false)
        {
            if(direction.sqrMagnitude<.001f)direction=Vector3.forward;
            transform.SetPositionAndRotation(at,Quaternion.LookRotation(direction,Vector3.up));
            color=move==EliteMove.Heal?new Color(.3f,1,.5f):move==EliteMove.Shield?new Color(.3f,.75f,1):impact?color:new Color(1,.2f,.15f);
            color.a=(impact?1-progress:.55f+.4f*progress);edge.startColor=edge.endColor=color;guide.startColor=guide.endColor=color;
        }
        public static void Impact(Vector3 at,Vector3 forward,EliteMove move,Color color)
        {
            if(move==EliteMove.Quake)return; // The actual Battle.Shockwave view owns its single expanding ring.
            if(move==EliteMove.Sweep){MinionRig.SweepArcFx(at,forward,2.4f,180,1.2f,color);return;}
            var fx=Create(null,move);fx.transient=true;fx.life=.55f;fx.Place(at,forward,color,0,true);
        }
        void Update(){if(!transient)return;age+=Time.deltaTime;float a=Mathf.Clamp01(age/life);var c=edge.startColor;c.a=1-a;edge.startColor=edge.endColor=c;guide.startColor=guide.endColor=c;if(age>=life)Destroy(gameObject);}
    }
}

using System.Globalization;
using UnityEngine;
namespace SpellDuel
{
    public partial class SoloBattle
    {
        string EliteWire(Fighter m)
        {
            if(!EliteCatalog.IsElite(m.minionKind))return "";var s=battle.EliteView(m);if(s==null||s.until<=battle.now)return ",0";
            return ",1,"+s.slot+","+(s.until-battle.now).ToString("0.000",CultureInfo.InvariantCulture)+","+s.center.x.ToString("R",CultureInfo.InvariantCulture)+","+s.center.z.ToString("R",CultureInfo.InvariantCulture)+","+s.forward.x.ToString("R",CultureInfo.InvariantCulture)+","+s.forward.z.ToString("R",CultureInfo.InvariantCulture);
        }
        void ReadEliteWire(Fighter m,string[] p)
        {
            if(!EliteCatalog.IsElite(m.minionKind)||p.Length<8)return;
            if(p[7]!="1")return; // spx owns attack completion; a late state packet cannot inflict damage.
            if(p.Length<14||!int.TryParse(p[8],out int slot))return;
            if(!float.TryParse(p[9],NumberStyles.Float,CultureInfo.InvariantCulture,out float remain)||!float.TryParse(p[10],NumberStyles.Float,CultureInfo.InvariantCulture,out float x)||!float.TryParse(p[11],NumberStyles.Float,CultureInfo.InvariantCulture,out float z)||!float.TryParse(p[12],NumberStyles.Float,CultureInfo.InvariantCulture,out float fx)||!float.TryParse(p[13],NumberStyles.Float,CultureInfo.InvariantCulture,out float fz))return;
            battle.RemoteEliteWarn(m.minionId,EliteCatalog.SkillId(m.minionKind,slot),new Vector3(x,0,z),new Vector3(fx,0,fz),remain);
        }
        void UpdateEliteVisual(Fighter m,MinionRig rig)
        {
            if(!EliteCatalog.IsElite(m.minionKind))return;
            var state=battle.EliteView(m);if(state==null)return;
            rig.EliteSkill(state.slot);var def=EliteCatalog.Get(m.minionKind);var move=def.moves[state.slot];
            var view=rig.GetComponent<EliteVisualState>();if(!view)view=rig.gameObject.AddComponent<EliteVisualState>();
            if(state.until>battle.now)
            {
                if(!view.warning||view.slot!=state.slot){if(view.warning)Destroy(view.warning.gameObject);view.warning=EliteEnemyFx.Create(rig.transform,move);view.slot=state.slot;}
                view.warning.gameObject.SetActive(true);view.warning.transform.localScale=Vector3.one;
                view.warning.Place(WorldFrame.FromWorld(state.center),WorldFrame.DirFromWorld(state.forward),def.color,Mathf.Clamp01(1-(state.until-battle.now)/state.duration));
            }
            else if(view.warning)view.warning.gameObject.SetActive(false);
            if(state.striking){state.striking=false;EliteEnemyFx.Impact(WorldFrame.FromWorld(state.center),WorldFrame.DirFromWorld(state.forward),move,def.color);}
        }
    }
    public sealed class EliteVisualState:MonoBehaviour {public EliteEnemyFx warning;public int slot=-1;}
}

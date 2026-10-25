using UnityEngine;
namespace SpellDuel {
 public enum EliteMove { Sweep, Quake, Line, Burst, Summon, Heal, Shield }
 public sealed class EliteDef { public int kind,chapter,role,baseKind; public string name; public float hp,damage; public Color color; public EliteMove[] moves; }
 public static class EliteCatalog {
 public const int First=23,Last=47; public static bool IsElite(int kind)=>kind>=First&&kind<=Last;
 public static EliteDef Get(int kind)=>IsElite(kind)?All[kind-First]:null;
 public static readonly EliteDef[] All={
 new EliteDef {kind=23,chapter=0,role=0,baseKind=2,name="熔冠衛士",hp=7f,damage=1.5f,color=new Color(1.0f,0.5059f,0.2431f),moves=new[]{EliteMove.Sweep,EliteMove.Quake}},
 new EliteDef {kind=24,chapter=0,role=1,baseKind=5,name="焰弩督軍",hp=4.5f,damage=1.6f,color=new Color(1.0f,0.5059f,0.2431f),moves=new[]{EliteMove.Line,EliteMove.Burst}},
 new EliteDef {kind=25,chapter=0,role=2,baseKind=6,name="黑曜縛咒師",hp=5f,damage=1.3f,color=new Color(1.0f,0.5059f,0.2431f),moves=new[]{EliteMove.Burst,EliteMove.Line}},
 new EliteDef {kind=26,chapter=0,role=3,baseKind=4,name="爐心喚靈者",hp=5.5f,damage=1.4f,color=new Color(1.0f,0.5059f,0.2431f),moves=new[]{EliteMove.Summon,EliteMove.Heal}},
 new EliteDef {kind=27,chapter=0,role=4,baseKind=0,name="灰燼獵首",hp=4f,damage=1.8f,color=new Color(1.0f,0.5059f,0.2431f),moves=new[]{EliteMove.Line,EliteMove.Sweep}},
 new EliteDef {kind=28,chapter=1,role=0,baseKind=8,name="苔甲守根者",hp=7f,damage=1.5f,color=new Color(0.5098f,0.8118f,0.3961f),moves=new[]{EliteMove.Sweep,EliteMove.Quake}},
 new EliteDef {kind=29,chapter=1,role=1,baseKind=12,name="棘莢狙擊手",hp=4.5f,damage=1.6f,color=new Color(0.5098f,0.8118f,0.3961f),moves=new[]{EliteMove.Line,EliteMove.Burst}},
 new EliteDef {kind=30,chapter=1,role=2,baseKind=9,name="夢孢司祭",hp=5f,damage=1.3f,color=new Color(0.5098f,0.8118f,0.3961f),moves=new[]{EliteMove.Burst,EliteMove.Line}},
 new EliteDef {kind=31,chapter=1,role=3,baseKind=11,name="菌冠育巢者",hp=5.5f,damage=1.4f,color=new Color(0.5098f,0.8118f,0.3961f),moves=new[]{EliteMove.Summon,EliteMove.Heal}},
 new EliteDef {kind=32,chapter=1,role=4,baseKind=10,name="翠翼巡獵者",hp=4f,damage=1.8f,color=new Color(0.5098f,0.8118f,0.3961f),moves=new[]{EliteMove.Line,EliteMove.Sweep}},
 new EliteDef {kind=33,chapter=2,role=0,baseKind=16,name="珊瑚重鉗",hp=7f,damage=1.5f,color=new Color(0.2745f,0.7882f,0.9294f),moves=new[]{EliteMove.Sweep,EliteMove.Quake}},
 new EliteDef {kind=34,chapter=2,role=1,baseKind=15,name="帆鰭砲手",hp=4.5f,damage=1.6f,color=new Color(0.2745f,0.7882f,0.9294f),moves=new[]{EliteMove.Line,EliteMove.Burst}},
 new EliteDef {kind=35,chapter=2,role=2,baseKind=17,name="雷傘織潮者",hp=5f,damage=1.3f,color=new Color(0.2745f,0.7882f,0.9294f),moves=new[]{EliteMove.Burst,EliteMove.Line}},
 new EliteDef {kind=36,chapter=2,role=3,baseKind=18,name="螺笛領航者",hp=5.5f,damage=1.4f,color=new Color(0.2745f,0.7882f,0.9294f),moves=new[]{EliteMove.Shield,EliteMove.Heal}},
 new EliteDef {kind=37,chapter=2,role=4,baseKind=13,name="浪刃追獵者",hp=4f,damage=1.8f,color=new Color(0.2745f,0.7882f,0.9294f),moves=new[]{EliteMove.Line,EliteMove.Sweep}},
 new EliteDef {kind=38,chapter=3,role=0,baseKind=2,name="銅盾百夫長",hp=7f,damage=1.5f,color=new Color(0.8745f,0.6588f,0.2627f),moves=new[]{EliteMove.Sweep,EliteMove.Quake}},
 new EliteDef {kind=39,chapter=3,role=1,baseKind=5,name="鎖弩監軍",hp=4.5f,damage=1.6f,color=new Color(0.8745f,0.6588f,0.2627f),moves=new[]{EliteMove.Line,EliteMove.Burst}},
 new EliteDef {kind=40,chapter=3,role=2,baseKind=6,name="砂鐘縛鬥士",hp=5f,damage=1.3f,color=new Color(0.8745f,0.6588f,0.2627f),moves=new[]{EliteMove.Burst,EliteMove.Line}},
 new EliteDef {kind=41,chapter=3,role=3,baseKind=4,name="戰鼓旗手",hp=5.5f,damage=1.4f,color=new Color(0.8745f,0.6588f,0.2627f),moves=new[]{EliteMove.Shield,EliteMove.Heal}},
 new EliteDef {kind=42,chapter=3,role=4,baseKind=0,name="赤刃角鬥士",hp=4f,damage=1.8f,color=new Color(0.8745f,0.6588f,0.2627f),moves=new[]{EliteMove.Line,EliteMove.Sweep}},
 new EliteDef {kind=43,chapter=4,role=0,baseKind=2,name="晶甲守隘獸",hp=7f,damage=1.5f,color=new Color(0.8745f,0.6078f,0.3412f),moves=new[]{EliteMove.Sweep,EliteMove.Quake}},
 new EliteDef {kind=44,chapter=4,role=1,baseKind=4,name="砂晶砲蜥",hp=4.5f,damage=1.6f,color=new Color(0.8745f,0.6078f,0.3412f),moves=new[]{EliteMove.Line,EliteMove.Burst}},
 new EliteDef {kind=45,chapter=4,role=2,baseKind=6,name="流砂咒巫",hp=5f,damage=1.3f,color=new Color(0.8745f,0.6078f,0.3412f),moves=new[]{EliteMove.Burst,EliteMove.Line}},
 new EliteDef {kind=46,chapter=4,role=3,baseKind=8,name="石巢牧者",hp=5.5f,damage=1.4f,color=new Color(0.8745f,0.6078f,0.3412f),moves=new[]{EliteMove.Summon,EliteMove.Heal}},
 new EliteDef {kind=47,chapter=4,role=4,baseKind=0,name="裂風獵蜥",hp=4f,damage=1.8f,color=new Color(0.8745f,0.6078f,0.3412f),moves=new[]{EliteMove.Line,EliteMove.Sweep}},
 };
 public static string SkillId(int kind,int slot)=>"elite_"+kind+"_"+slot;
 public static float Windup(EliteMove m)=>m==EliteMove.Line?1.2f:m==EliteMove.Sweep?1.5f:m==EliteMove.Quake?1.6f:m==EliteMove.Summon||m==EliteMove.Heal?2f:1.8f;
 public static float Cooldown(EliteMove m)=>m==EliteMove.Summon?14:m==EliteMove.Heal?12:m==EliteMove.Shield?13:m==EliteMove.Line?6:m==EliteMove.Sweep?7:8;
 public static float Radius(EliteMove m)=>m==EliteMove.Burst?.75f:m==EliteMove.Sweep?2.4f:m==EliteMove.Quake||m==EliteMove.Line?2.6f:2f;
 public static string Hint(EliteMove m)=>m==EliteMove.Sweep?"⬇ 蹲下或退開！":m==EliteMove.Quake?"⬆ 跳起來或退開！":m==EliteMove.Line?"左右側步！":m==EliteMove.Burst?"離開紅圈！":"優先擊倒施術者！";
 public static bool TrySkill(string id,out EliteDef d,out int slot){ d=null;slot=0;if(id==null)return false;var p=id.Split('_');if(p.Length!=3||p[0]!="elite"||!int.TryParse(p[1],out int k)||!int.TryParse(p[2],out slot)||slot<0||slot>1)return false;d=Get(k);return d!=null;}
 public static void RegisterSkills(){foreach(var d in All)for(int i=0;i<2;i++){var m=d.moves[i];string id=SkillId(d.kind,i);Skills.All[id]=new SkillDef{id=id,cls="minion",name=d.name+"・"+m,type=SkillType.Projectile,color=d.color,cost=0,damage=10,charge=Windup(m),cooldown=Cooldown(m),rangeMax=Radius(m),speed=Radius(m)/1.2f,radius=.28f};}}
 }
}

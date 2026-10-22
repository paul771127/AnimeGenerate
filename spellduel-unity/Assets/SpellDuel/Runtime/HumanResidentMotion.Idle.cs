using UnityEngine;
namespace SpellDuel
{
    internal sealed partial class HumanResidentMotion
    {
        float idlePeriod,idleOffset,idleWeight,idleHop,idleSpin,idleDrop,idleLean,idleTwist,idleRoll,idleNod,idleLook,idleEyes,idleMouth,idleProgress;
        Vector3 idleL,idleR;Quaternion idlePalmL,idlePalmR;bool idleUseL,idleUseR;
        internal int IdleIndex{get;private set;}
        internal int IdleActionCount=>identity=="鐵匠"||identity=="小鐵"||identity=="鐵娘"||identity=="店員"||identity=="米菈"||identity=="小葉"?3:2;
        internal string IdleLabel=>ActionLabel(identity,IdleIndex);
        internal float IdleKeyTime(int index,float phase=.38f){float t=idlePeriod*(index+phase)-idleOffset;return t<0?t+idlePeriod*IdleActionCount:t;}
        internal static string ActionLabel(string id,int index)
        {
            switch(id)
            {
                case "鐵匠":case "小鐵":case "鐵娘":return index==0?"敲砧鉚接":index==1?"擦拭額汗":"端詳刀刃";
                case "店員":case "米菈":return index==0?"整理貨架":index==1?"清點零錢":"招呼路人";
                case "公會長":return index==0?"翻閱名冊":"撫鬚思考";
                case "老焰":return index==0?"拄杖望山":"輕敲後背";
                case "翠":return index==0?"合十閉目":"輕扶枝冠";
                case "小葉":return index==0?"開心小跳":index==1?"舉牌講故事":"抱牌轉圈";
                case "楓":return index==0?"擦拭木弓":"遮陽巡林";
                case "海斗":return index==0?"望遠鏡巡海":"扶正船帽";
                case "凪":return index==0?"擦拭茶杯":"哼歌搖擺";
                case "阿潮":return index==0?"拋竿試線":"轉動捲線器";
                case "雷歐":return index==0?"轉肩鬆筋":"檢查練習劍";
                case "小沙":return index==0?"撕下票根":"核對票簿";
                case "老駝":return index==0?"拄杖望路":"清點驛站鑰鈴";
                case "阿鈴":return index==0?"搖響駝鈴":"向旅人招手";
                case "石先生":return index==0?"展卷說書":"指向遠山";
                case "嚮導":return index==0?"檢查燈油":"整理背包指路";
                case "教官":return index==0?"緩揮木劍":"示範防守";
                case "懸賞官":return index==0?"張貼新告示":"羽筆核對賞金";
                case "播報員":return index==0?"舉筒播報":"揮旗致意";
                default:return index==0?"翻看旅行筆記":"抬頭觀望";
            }
        }
        void SampleIdle(float t,float weight)
        {
            float cycle=(t+idleOffset)/idlePeriod;IdleIndex=((int)Mathf.Floor(cycle)%IdleActionCount+IdleActionCount)%IdleActionCount;
            float u=cycle-Mathf.Floor(cycle);idleProgress=u;
            idleWeight=Ease(Mathf.Clamp01(u/.13f))*Ease(Mathf.Clamp01((.82f-u)/.16f))*weight;
            if(!artBound)idleWeight=0;
            if(waveT>=0)idleWeight*=.10f;
            idleUseL=idleUseR=false;idlePalmL=idlePalmR=Quaternion.identity;
            idleHop=idleSpin=idleDrop=idleLean=idleTwist=idleRoll=idleNod=idleLook=idleEyes=idleMouth=0;
            float beat=Mathf.Sin(u*Mathf.PI*8),slow=Mathf.Sin(u*Mathf.PI*2);int i=IdleIndex;
            switch(identity)
            {
                case "鐵匠":case "小鐵":case "鐵娘":
                    if(i==0&&StationNearby())
                    {
                        L(-.10f,.96f,.27f);R(.18f,.88f+Mathf.Max(0,beat)*.21f,.15f);idlePalmR=Quaternion.Euler(90,0,0);idleDrop=-.035f;idleLean=9;idleNod=8;
                    }
                    else if(i==1){R(.17f,1.54f,.15f);idlePalmR=Quaternion.Euler(0,0,-25);idleLook=-9;}
                    else{L(-.14f,1.13f,.28f);R(.17f,1.10f,.27f);idleNod=9;idleTwist=slow*6;}
                    break;
                case "店員":case "米菈":
                    L(-.15f,1.07f,.28f);R(.15f,1.12f,.28f);
                    if(i==0){idleL+=new Vector3(slow*.06f,slow*.025f,0);if(StationNearby())idleR.x=.27f+slow*.025f;idleR.y+=Mathf.Max(0,beat)*.035f;idleNod=8;}
                    else if(i==1){idleR.x+=Mathf.Sin(u*22)*.025f;idleR.z-=.02f;idleNod=12;}
                    else{R(.27f,1.42f,.20f);idlePalmR=Quaternion.Euler(0,0,180+beat*14);idleLook=slow*12;}
                    break;
                case "公會長":
                    L(-.13f,1.12f,.27f);if(i==0){R(.07f,1.13f,.28f);idleNod=10;}else{R(.065f,1.45f,.24f);idleR.y+=slow*.015f;idleLook=12;}
                    break;
                case "老焰":case "老駝":
                    R(.26f,.95f,.18f);
                    if(i==0){L(-.19f,1.25f,.12f);idleLook=18+slow*7;idleNod=-7;}
                    else if(identity=="老焰"){L(-.20f,1.02f,-.17f);idleL.y+=beat*.018f;idleLean=5;idleNod=6;}
                    else{L(-.16f,1.10f,.27f);idleNod=10;idleL.y+=beat*.01f;}
                    break;
                case "翠":
                    if(i==0){L(-.045f,1.18f,.30f);R(.045f,1.18f,.30f);idlePalmL=Quaternion.Euler(0,90,180);idlePalmR=Quaternion.Euler(0,-90,180);idleEyes=1;idleNod=4;}
                    else{L(-.14f,1.59f,.06f);idlePalmL=Quaternion.Euler(0,0,165);R(.10f,1.09f,.20f);idleLook=-10;}
                    break;
                case "小葉":
                    L(-.16f,1.08f+(i==1?.20f:0),.28f);R(.15f,1.08f+(i==1?.20f:0),.28f);
                    if(i==0)idleHop=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI*4)),2)*.070f;
                    if(i==1){idleMouth=.32f;idleNod=-5;idleRoll=slow*3;}
                    if(i==2&&u<.82f){idleSpin=360*Ease(Mathf.Clamp01(u/.80f));idleRoll=slow*2;}
                    break;
                case "楓":
                    L(-.18f,1.03f,.23f);
                    if(i==0){R(-.04f,1.07f+slow*.08f,.30f);idleNod=8;}
                    else{R(.12f,1.57f,.19f);idlePalmR=Quaternion.Euler(0,0,85);idleLook=18+slow*8;idleNod=-4;}
                    break;
                case "海斗":
                    if(i==0){R(.07f,1.59f,.23f);L(-.05f,1.53f,.34f);idleLook=slow*14;}
                    else{R(.17f,1.64f,.08f);idlePalmR=Quaternion.Euler(0,0,165);idleNod=-4;idleLook=-10;}
                    break;
                case "凪":
                    L(-.14f,1.11f,.27f);
                    if(i==0){R(-.015f,1.15f+beat*.02f,.28f);idleNod=10;}
                    else{R(.12f,1.19f,.20f);idleRoll=slow*4;idleMouth=.20f;idleEyes=.35f;}
                    break;
                case "阿潮":
                    L(-.25f,1.04f,.16f);R(-.11f,1.13f,.26f);idlePalmR=Quaternion.Euler(0,0,-90);
                    if(i==0){idlePalmL=Quaternion.Euler(-18+slow*22,0,0);idleLean=slow*3;idleLook=-8;}
                    else{idleR+=new Vector3(Mathf.Cos(u*25)*.025f,Mathf.Sin(u*25)*.025f,0);idleNod=7;}
                    break;
                case "雷歐":
                    if(i==0){R(.08f,1.31f,.22f);L(.11f,1.30f,.20f);idleTwist=slow*10;idleRoll=slow*2;}
                    else{R(.20f,1.02f,.23f);L(-.10f,1.12f,.30f);idleNod=10;}
                    break;
                case "小沙":
                    R(.13f,1.11f,.27f);L(-.12f,1.12f,.28f);
                    if(i==0){idleL.x-=Mathf.Max(0,slow)*.10f;idleNod=6;}else{idleR.y+=.04f;idleNod=12;idleLook=-4;}
                    break;
                case "阿鈴":
                    L(-.15f,1.15f,.25f);
                    if(i==0){idleL.y+=beat*.045f;R(.14f,1.10f,.22f);idleRoll=slow*3;}
                    else{R(.26f,1.43f,.22f);idlePalmR=Quaternion.Euler(0,0,180+beat*15);idleLook=-8;}
                    break;
                case "石先生":
                    L(-.14f,1.13f,.28f);
                    if(i==0){R(.04f,1.14f,.30f);idleNod=9;idleMouth=.22f;}
                    else{R(.29f,1.32f,.36f);idleLook=18;idleMouth=.25f;}
                    break;
                case "嚮導":
                    if(i==0){L(-.16f,1.18f,.25f);R(.10f,1.12f,.28f);idleNod=12;}
                    else{R(.23f,1.25f,-.10f);L(-.18f,1.03f,.24f);idleLook=-16;}
                    break;
                case "教官":
                    L(-.15f,1.15f,.26f);R(.28f,1.16f+slow*.12f,.28f+Mathf.Max(0,slow)*.17f);
                    idlePalmR=Quaternion.Euler(-10+slow*20,slow*24,0);idleTwist=slow*7;
                    if(i==1){idleR=new Vector3(.16f,1.20f,.29f);idleL.y+=.10f;idleNod=-3;}
                    break;
                case "懸賞官":
                    if(i==0&&StationNearby()){L(-.10f,1.28f,.40f);R(.17f,1.34f,.39f);idleLean=3;idleNod=-3;}
                    else{L(-.12f,1.12f,.28f);R(.10f,1.16f,.29f);idleR.x+=beat*.02f;idleNod=10;}
                    break;
                case "播報員":
                    if(i==0){R(.12f,1.52f,.23f);L(-.23f,1.12f,.23f);idleMouth=.65f;idleNod=-4;}
                    else{L(-.24f,1.40f,.20f);R(.19f,1.12f,.28f);idlePalmL=Quaternion.Euler(0,0,slow*23);idleRoll=slow*3;}
                    break;
                default:
                    if(i==0){R(.08f,1.17f,.29f);idleNod=10;}
                    else{R(.23f,1.35f,.25f);idleLook=-16;idleNod=-5;}
                    break;
            }
            if(walkAmt>.01f||waveT>=0||!artBound)idleSpin=0;idleHop*=idleWeight;idleDrop*=idleWeight;idleLean*=idleWeight;idleTwist*=idleWeight;idleRoll*=idleWeight;idleNod*=idleWeight;idleLook*=idleWeight;idleEyes*=idleWeight;idleMouth*=idleWeight;
        }
        void L(float x,float y,float z){idleUseL=true;idleL=new Vector3(x,y,z);}
        void R(float x,float y,float z){idleUseR=true;idleR=new Vector3(x,y,z);}
    }
}

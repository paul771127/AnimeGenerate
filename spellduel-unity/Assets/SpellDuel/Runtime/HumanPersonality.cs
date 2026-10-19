using UnityEngine;
namespace SpellDuel
{
    internal static class HumanPersonality
    {
        internal enum Temper { Confident,Cheerful,Calm,Gentle,Fierce,Cunning,Shy,Mature }
        internal static Temper For(string id)
        {
            switch(id)
            {
                case "swordsman":case "mage":case "paladin":case "小鐵":return Temper.Confident;
                case "ranger":case "summoner":case "小葉":case "海斗":case "鐵匠":return Temper.Cheerful;
                case "archer":case "marksman":case "archmage":case "ninja":case "楓":case "澪":return Temper.Calm;
                case "priest":case "bishop":case "凪":case "艾琳":return Temper.Gentle;
                case "berserker":case "inquisitor":return Temper.Fierce;
                case "assassin":case "shadowdancer":case "米菈":return Temper.Cunning;
                case "店員":return Temper.Shy;
                default:return Temper.Mature;
            }
        }
        internal static void Apply(ref Portraits.Persona p,string id)
        {
            switch(For(id))
            {
                case Temper.Confident:p.Smile=.7f;p.Asym=2.5f;p.Open=0;p.Brow=3;p.Lid=.06f;break;
                case Temper.Cheerful:p.Smile=1;p.Asym=.3f;p.Open=.40f;p.Brow=-2;p.Lid=.05f;break;
                case Temper.Calm:p.Smile=-.12f;p.Asym=0;p.Open=0;p.Brow=0;p.Lid=.12f;break;
                case Temper.Gentle:p.Smile=.55f;p.Asym=0;p.Open=0;p.Brow=-3;p.Lid=.05f;break;
                case Temper.Fierce:p.Smile=-.45f;p.Asym=.5f;p.Open=.16f;p.Brow=6;p.Lid=.08f;p.Pupil=.85f;break;
                case Temper.Cunning:p.Smile=.8f;p.Asym=3;p.Open=0;p.Brow=3;p.Gaze=.18f;p.Lid=.09f;break;
                case Temper.Shy:p.Smile=-.3f;p.Asym=0;p.Open=.12f;p.Brow=-5;p.Gaze=-.18f;p.Lid=0;break;
                case Temper.Mature:p.Smile=.12f;p.Asym=0;p.Open=0;p.Brow=-1;p.Lid=.12f;break;
            }
        }
        internal static void Mouth(ref Portraits.Persona p,string id,int state)
        {
            if(state==1){p.Smile=Mathf.Min(1,p.Smile+.35f);p.Asym+=.5f;}
            if(state==2)
            {
                switch(For(id))
                {
                    case Temper.Cheerful:case Temper.Confident:p.Smile=.6f;p.Asym=1.5f;break;
                    case Temper.Cunning:p.Smile=.4f;p.Asym=3;break;
                    case Temper.Shy:p.Smile=-.55f;p.Asym=0;break;
                    case Temper.Gentle:p.Smile=.12f;p.Asym=0;break;
                    case Temper.Mature:p.Smile=0;p.Asym=0;break;
                    case Temper.Fierce:p.Smile=-.55f;p.Asym=0;break;
                    default:p.Smile=-.18f;p.Asym=0;break;
                }
            }
            if(state==3){p.Smile=0;p.Asym=0;}
            if(state==4){p.Smile=-.6f;p.Asym=0;}
        }
        internal struct Pose
        {
            internal Vector3 L,R;internal float Roll,Twist,Lean,Shift,Drop,Step;
            internal Pose(Vector3 l,Vector3 r,float roll,float twist,float lean,float shift,float drop,float step)
            {L=l;R=r;Roll=roll;Twist=twist;Lean=lean;Shift=shift;Drop=drop;Step=step;}
        }
        static Vector3 A(float x,float z,float e)=>new Vector3(x,z,e);
        static Pose P(Vector3 l,Vector3 r,float roll,float twist,float lean,float shift,float drop,float step)=>new Pose(l,r,roll,twist,lean,shift,drop,step);
        internal static Pose Stance(string id,bool combat)
        {
            if(combat)switch(id)
            {
                case "swordsman":return P(A(-20,22,-70),A(-32,8,-62),-2,-9,3,-.008f,.025f,.060f);
                case "paladin":return P(A(-36,18,-65),A(-22,5,-66),-1,-6,2,-.005f,.018f,.045f);
                case "berserker":return P(A(-28,32,-42),A(-40,18,-38),3,12,6,.012f,.040f,.070f);
                case "archer":return P(A(-58,10,-16),A(-35,-22,-102),-2,24,3,-.015f,.018f,.055f);
                case "marksman":return P(A(-64,7,-14),A(-36,-25,-108),-1,28,2,-.012f,.023f,.065f);
                case "ranger":return P(A(-48,15,-24),A(-27,-15,-90),3,18,4,.015f,.027f,.048f);
                case "mage":return P(A(-32,14,-66),A(-27,6,-52),-2,-9,3,-.012f,.016f,.045f);
                case "archmage":return P(A(-40,22,-75),A(-17,8,-64),1,-14,1,.008f,.012f,.036f);
                case "summoner":return P(A(-48,26,-42),A(-20,16,-60),-3,13,4,-.015f,.022f,.052f);
                case "assassin":return P(A(-42,22,-65),A(-38,14,-72),3,-15,8,.012f,.065f,.090f);
                case "ninja":return P(A(-48,12,-54),A(-36,18,-72),-3,20,7,-.012f,.055f,.085f);
                case "shadowdancer":return P(A(-35,35,-50),A(-55,12,-40),5,-22,5,.022f,.045f,.075f);
                case "priest":return P(A(-48,18,-28),A(-12,5,-76),-1,-5,1,-.006f,.010f,.028f);
                case "bishop":return P(A(-52,22,-22),A(-8,5,-82),1,7,1,.006f,.009f,.025f);
                case "inquisitor":return P(A(-30,28,-60),A(-29,7,-57),-2,12,5,-.01f,.030f,.060f);
            }
            switch(id)
            {
                case "swordsman":return P(A(-10,35,-75),A(12,8,-24),-3,-5,-1,-.022f,.004f,.015f);
                case "paladin":return P(A(-8,20,-38),A(4,5,-42),2,4,-1,.018f,.003f,.012f);
                case "berserker":return P(A(-8,38,-74),A(-12,24,-50),4,10,2,.025f,.007f,.020f);
                case "archer":return P(A(-8,7,-48),A(-25,32,-85),-3,17,1,-.020f,.005f,.018f);
                case "marksman":return P(A(4,8,-50),A(-12,26,-96),2,20,0,.017f,.004f,.016f);
                case "ranger":return P(A(-6,16,-44),A(-18,32,-70),4,-12,2,.026f,.006f,.025f);
                case "mage":return P(A(-26,-12,-70),A(-5,4,-82),-2,-7,-1,-.018f,.003f,.010f);
                case "archmage":return P(A(-18,30,-100),A(4,8,-82),2,9,-2,.018f,.003f,.008f);
                case "summoner":return P(A(-16,34,-65),A(-4,8,-72),-4,12,2,-.024f,.006f,.022f);
                case "assassin":return P(A(-35,-22,-105),A(-28,-18,-98),4,-14,2,.024f,.009f,.030f);
                case "ninja":return P(A(-29,-20,-98),A(-37,-22,-90),-3,12,1,-.020f,.007f,.025f);
                case "shadowdancer":return P(A(-10,38,-82),A(-24,12,-45),5,-20,-1,.028f,.008f,.032f);
                case "priest":return P(A(-20,-15,-74),A(-5,4,-83),-1,-3,0,-.012f,.002f,.008f);
                case "bishop":return P(A(-16,-12,-80),A(-3,6,-84),1,4,-1,.012f,.002f,.006f);
                case "inquisitor":return P(A(-8,34,-68),A(-5,8,-58),-2,8,1,-.018f,.004f,.016f);
                case "老焰":return P(A(-9,9,-40),A(-6,18,-34),-3,-4,4,-.018f,.004f,.010f);
                case "小鐵":return P(A(-8,36,-78),A(-14,28,-65),4,9,3,.020f,.004f,.020f);
                case "米菈":return P(A(-18,14,-70),A(-8,34,-75),-4,-13,1,-.022f,.004f,.022f);
                case "翠":return P(A(-28,-8,-78),A(-18,-12,-85),2,5,-1,.012f,.002f,.008f);
                case "楓":return P(A(-3,10,-35),A(-18,30,-75),3,-12,2,.019f,.004f,.020f);
                case "小葉":return P(A(-10,23,-48),A(-26,18,-60),-5,9,1,-.020f,.005f,.018f);
                case "海斗":return P(A(-10,36,-80),A(-13,34,-73),4,13,-1,.023f,.004f,.018f);
                case "凪":return P(A(-20,-15,-72),A(-22,-12,-68),-2,-8,1,-.014f,.002f,.008f);
                case "阿潮":return P(A(-4,7,-27),A(-6,22,-52),3,8,5,.019f,.004f,.012f);
                case "鐵匠":return P(A(-5,35,-78),A(-16,10,-56),-4,-9,2,-.024f,.005f,.020f);
                case "店員":return P(A(-21,-9,-72),A(-24,-13,-78),-3,10,3,-.015f,.003f,.009f);
                default:return P(A(-8,24,-54),A(-13,18,-64),2,-5,-1,.014f,.002f,.010f);
            }
        }
    }
}

using UnityEngine;
namespace SpellDuel
{
    public static partial class UiKit
    {
        public static void RankMedallion(Rect r,string rank,Color accent,int font)
        {
            Badge(r,null,accent,2);
            Gem(new Rect(r.center.x-r.width*.1f,r.y+r.height*.08f,r.width*.2f,r.height*.12f),Paper);
            Text(Inset(r,r.width*.14f),rank,font,Paper,TextAnchor.MiddleCenter,true);
        }
        public static bool RewardIcon(Rect r,string id,string label,Color colour)
        {
            if(!string.IsNullOrEmpty(id))
            {
                Badge(r,SkillIcons.Get(id),colour);return true;
            }
            if(string.IsNullOrEmpty(label))return false;
            if(label.StartsWith("💠")){Card(r,.96f,colour);Gem(Inset(r,r.width*.23f),colour);return true;}
            if(label.StartsWith("⬆")){Card(r,.96f,colour);Gem(new Rect(r.center.x-r.width*.14f,r.y+r.height*.1f,r.width*.28f,r.height*.35f),colour);Divider(new Rect(r.center.x-r.width*.07f,r.y+r.height*.4f,r.width*.14f,r.height*.4f),colour);return true;}
            if(label.StartsWith("🎁")){Card(r,.96f,colour);return TrySymbol(r,"🛡",Mathf.RoundToInt(r.height*.75f),colour);}
            string symbol=label.Contains("金幣")||label.Contains("💰")?"💰":
                label.Contains("配方")?"📜":label.Contains("材料")?"🧪":null;
            if(symbol==null)return false;
            Card(r,.9f,colour);return TrySymbol(r,symbol,Mathf.RoundToInt(r.height*.7f),colour);
        }
        public static void LoadingRail(Rect r,float progress,string label,int font)
        {
            Bar(r,progress,Gold,label,font);
            float x=r.x+Mathf.Clamp01(progress)*r.width;
            if(progress>0&&progress<1)Gem(new Rect(x-4,r.center.y-4,8,8),Paper);
        }
    }
}

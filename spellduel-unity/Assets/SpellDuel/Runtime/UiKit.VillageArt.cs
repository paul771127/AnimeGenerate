using UnityEngine;
namespace SpellDuel
{
    public static partial class UiKit
    {
        public static void TowerArt(Rect rect,Color color)=>GUI.DrawTexture(rect,VillageIllustrations.Get(0,color),ScaleMode.StretchToFill,true);
        public static void PracticeArt(Rect rect,Color color)=>GUI.DrawTexture(rect,VillageIllustrations.Get(1,color),ScaleMode.StretchToFill,true);
        public static void SettingsArt(Rect rect,Color color)=>GUI.DrawTexture(rect,VillageIllustrations.Get(2,color),ScaleMode.StretchToFill,true);
    }
}

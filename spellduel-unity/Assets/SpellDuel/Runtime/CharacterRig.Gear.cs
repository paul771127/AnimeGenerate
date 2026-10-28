using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 身上裝備顯示在角色上（遠端隊友／對手、觀戰、角色畫面的預覽）。
    /// 這裡只負責「穿／脫」：清掉舊的裝備部件、記住現在穿什麼；部件長什麼樣子由美術在 BuildGearArt 裡做
    /// （CharacterRig.GearArt.cs；規格見 FACE_3D_BRIEF.md 第十五輪）。
    /// </summary>
    public partial class CharacterRig
    {
        string gearCode = "";
        GearLook gear;
        readonly List<GameObject> gearParts = new List<GameObject>();

        /// <summary>現在穿的裝備（Encode 後的字串；沒穿＝空）</summary>
        public string GearCode => gearCode;
        public GearLook Gear => gear;

        /// <summary>換裝：和現在一樣就不動；不一樣就拆掉舊部件、重新掛上</summary>
        public void SetGear(GearLook g)
        {
            string code = g == null || g.Empty ? "" : g.Encode();
            if (code == gearCode) return;
            gearCode = code; gear = g;
            foreach (var go in gearParts) if (go) { var lease=go.GetComponent<GearVisualLease>();if(lease)lease.Release();RemoveVisual(go); }
            gearParts.Clear();
            if (code.Length == 0 || classId == DummyId) return;
            BuildGearArt(g);
        }

        /// <summary>美術掛上的裝備部件要用這個登記，換裝時才拆得掉</summary>
        GameObject GearPart(GameObject go) { if (go) gearParts.Add(go); return go; }

        /// <summary>美術實作：依 g 的每個欄位（GearLook.PieceColors 取色）在對應關節掛部件</summary>
        partial void BuildGearArt(GearLook g);
    }
}

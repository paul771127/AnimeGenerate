using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 身上裝備的「外觀」：每個欄位（武器、頭部、身體、鞋子、飾品）穿了什麼稀有度、哪一套。
    /// 角色外觀用它掛裝備（CharacterRig.SetGear），連線時編成一行字送給隊友／對手／觀眾（Msg.g）。
    /// 強化等級不影響外觀（先不做強化光）。
    /// </summary>
    public class GearLook
    {
        public struct Piece
        {
            public int rarity;      // −1＝空欄位；0 普通 … 4 傳說
            public string set;      // 套裝 id（Economy.Sets）；空＝不是套裝
            public bool Has => rarity >= 0;
        }

        public const int Slots = 5;
        public readonly Piece[] pieces = new Piece[Slots];

        public GearLook() { for (int i = 0; i < Slots; i++) pieces[i] = new Piece { rarity = -1, set = "" }; }

        public Piece this[Slot s] => pieces[(int)s];

        public static GearLook Of(Item[] equipped)
        {
            var g = new GearLook();
            if (equipped == null) return g;
            for (int i = 0; i < Slots && i < equipped.Length; i++)
                if (equipped[i] != null) g.pieces[i] = new Piece { rarity = Mathf.Clamp(equipped[i].rarity, 0, 5), set = equipped[i].setId ?? "" };
            return g;
        }

        /// <summary>同一套穿了幾件</summary>
        public int SetCount(string setId)
        {
            int n = 0;
            if (string.IsNullOrEmpty(setId)) return 0;
            foreach (var p in pieces) if (p.Has && p.set == setId) n++;
            return n;
        }

        /// <summary>穿了 4 件以上的那一套（套裝效果全開；外觀可以整套換色）；沒有＝null</summary>
        public string FullSet
        {
            get
            {
                foreach (var p in pieces) if (p.Has && !string.IsNullOrEmpty(p.set) && SetCount(p.set) >= 4) return p.set;
                return null;
            }
        }

        public bool Empty { get { foreach (var p in pieces) if (p.Has) return false; return true; } }

        // ---------------------------------------------------------------- 配色
        /// <summary>套裝的主色與點綴色（外觀用）</summary>
        public static void SetColors(string setId, out Color main, out Color accent)
        {
            switch (setId)
            {
                case "clockwork": main=new Color(.45f,.31f,.16f);accent=new Color(.2f,.95f,.9f);return;
                case "skyweave": main = new Color(0.16f, 0.62f, 0.6f); accent = new Color(0.95f, 0.78f, 0.35f); return;   // 天穹航裝：青綠＋金扣
                case "flame": main = new Color(0.78f, 0.18f, 0.1f); accent = new Color(1f, 0.62f, 0.15f); return;       // 炎龍：赤紅＋熔金
                case "grove": main = new Color(0.2f, 0.52f, 0.25f); accent = new Color(0.72f, 0.9f, 0.35f); return;      // 森羅：深綠＋嫩葉
                case "tide": main = new Color(0.12f, 0.38f, 0.62f); accent = new Color(0.45f, 0.9f, 0.9f); return;       // 海神：深海藍＋青
                case "gladiator": main = new Color(0.62f, 0.38f, 0.18f); accent = new Color(0.72f, 0.1f, 0.12f); return; // 鬥魂：青銅＋深紅
                case "loess": main = new Color(0.78f, 0.62f, 0.36f); accent = new Color(0.5f, 0.3f, 0.16f); return;      // 黃沙：沙黃＋赭
                case "gale": main = new Color(0.9f, 0.94f, 0.96f); accent = new Color(0.35f, 0.82f, 0.95f); return;      // 疾風：白＋天青
                case "guard": main = new Color(0.7f, 0.74f, 0.8f); accent = new Color(0.22f, 0.36f, 0.72f); return;      // 守護者：銀＋寶藍
            }
            main = new Color(0.55f, 0.5f, 0.45f); accent = new Color(0.8f, 0.8f, 0.8f);
        }

        /// <summary>一件裝備的顏色：套裝用套裝色；非套裝依稀有度（皮革→鐵→鋼藍→紫→金）</summary>
        public static void PieceColors(Piece p, out Color main, out Color accent)
        {
            if (!string.IsNullOrEmpty(p.set)) { SetColors(p.set, out main, out accent); return; }
            switch (p.rarity)
            {
                case 0: main = new Color(0.55f, 0.36f, 0.2f); accent = new Color(0.7f, 0.55f, 0.35f); return;
                case 1: main = new Color(0.5f, 0.53f, 0.56f); accent = new Color(0.4f, 0.95f, 0.45f); return;
                case 2: main = new Color(0.45f, 0.58f, 0.75f); accent = new Color(0.4f, 0.65f, 1f); return;
                case 5: main=new Color(.65f,.13f,.18f);accent=new Color(1,.27f,.27f);return;
                case 3: main = new Color(0.36f, 0.22f, 0.5f); accent = new Color(0.78f, 0.45f, 1f); return;
                default: main = new Color(0.85f, 0.65f, 0.25f); accent = new Color(1f, 0.85f, 0.4f); return;
            }
        }

        // ---------------------------------------------------------------- 連線：一行字
        /// <summary>「稀有度:套裝」五段用 / 分開；空欄位是空字串。例如 "2:flame/3:/-1:/..."</summary>
        public string Encode()
        {
            var parts = new string[Slots];
            for (int i = 0; i < Slots; i++) parts[i] = pieces[i].Has ? pieces[i].rarity + ":" + pieces[i].set : "";
            return string.Join("/", parts);
        }

        public static GearLook Parse(string s)
        {
            var g = new GearLook();
            if (string.IsNullOrEmpty(s)) return g;
            var parts = s.Split('/');
            for (int i = 0; i < Slots && i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                int c = parts[i].IndexOf(':');
                string r = c >= 0 ? parts[i].Substring(0, c) : parts[i];
                if (!int.TryParse(r, out var rarity) || rarity < 0) continue;
                string set = c >= 0 ? parts[i].Substring(c + 1) : "";
                if (Economy.SetOf(set) == null) set = "";
                g.pieces[i] = new Piece { rarity = Mathf.Clamp(rarity, 0, 5), set = set };
            }
            return g;
        }
    }
}

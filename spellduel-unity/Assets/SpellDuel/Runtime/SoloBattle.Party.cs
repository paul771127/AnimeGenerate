using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 連線副本的隊伍畫面（選技能畫面裡）：等玩家到齊、大家按「準備好了」，房主按「開始副本」才一起開打。
    ///   房主：選副本、選難度；其他人都準備好才能按開始（一個人也能開始）。
    ///   隊員：按「準備好了」後等房主開始（房主開始時，大家的副本與難度都跟房主一樣）。
    /// </summary>
    public partial class SoloBattle
    {
        void DrawDungeonParty(ref float y, float pad, float gap, float u, int fs, int fsS, float W)
        {
            var d = Dungeons.Get(netDungeonId);
            int n = PeerIds.Count + 1, max = d != null ? d.maxPlayers : 4, min = d != null ? d.minPlayers : 2;
            var col = new Color(0.4f, 0.85f, 0.75f);
            float rowH = u * 0.95f;
            int slots = Mathf.Max(max, n);
            var card = new Rect(pad, y, W - pad * 2, u * 1.1f + rowH * slots + u * 1.9f);
            UiKit.Card(card, 0.96f, col, 0.95f);
            float x = card.x + gap, w = card.width - gap * 2, cy = card.y + gap * 0.4f;
            UiKit.Text(new Rect(x, cy, w, u), $"🏰 {(d != null ? d.name : "副本")} 隊伍（{n}/{max} 人）", fs, Color.white, TextAnchor.MiddleLeft, true);
            cy += u * 1.05f;
            // 自己
            string myCls = Skills.Classes[myClass].name;
            UiKit.Text(new Rect(x, cy, w, rowH), $"⭐ 你{(NetIsHost ? "（房主）" : "")}：{myCls}　" + (NetIsHost ? "由你開始副本" : localReady ? "✔ 已準備" : "還沒準備"), fsS, NetIsHost || localReady ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
            cy += rowH;
            foreach (var pid in PeerIds)
            {
                bool rr = peerInfo.TryGetValue(pid, out var pi) && pi.ready;
                string cn = pi != null && Skills.Classes.TryGetValue(pi.cls ?? "", out var rc) ? rc.name : "（選職業中）";
                bool hostRow = pid == 0;
                UiKit.Text(new Rect(x, cy, w, rowH), $"👤 {PeerName(pid)}：{cn}　" + (hostRow ? "等你準備好後開始" : rr ? "✔ 已準備" : "還沒準備"), fsS, hostRow || rr ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
                cy += rowH;
            }
            for (int i = n; i < max; i++) { UiKit.Text(new Rect(x, cy, w, rowH), "　（空位：把房間代碼傳給朋友）", fsS, UiKit.Muted); cy += rowH; }
            // 提示
            string hint = NetIsHost
                ? (PeerIds.Count == 0 ? "等待玩家加入…（一個人也可以直接開始）" : PartyReady ? "大家都準備好了：按下方「開始副本」" : "等所有人按「準備好了」，再由你按「開始副本」")
                : localReady ? "已準備：等房主按開始（副本與難度跟房主一樣）" : "選好技能後按下方「準備好了」";
            hint += $"\n👥 {n} 人：敵人血量、攻擊 ×2，數量、Boss 血量再 ×{Dungeons.PartyScale(n)}・掉寶率 ×{Dungeons.LootMul(n):0.##}";   // 浮動難度（最少以 2 人計算）
            UiKit.Text(new Rect(x, cy + gap * 0.3f, w, u * 1.6f), hint, fsS, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperLeft);
            y = card.yMax + gap;
        }
    }
}

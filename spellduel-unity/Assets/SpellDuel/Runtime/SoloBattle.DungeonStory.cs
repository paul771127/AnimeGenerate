using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本的劇情字幕卡（不暫停戰鬥，多人連線時每支手機各自顯示）：
    ///   進地點（enter）、清完地點（clear）、劇情人物的台詞、首領剩一半血（boss_half），多人開場時的副本開場白（before）。
    ///   畫面上方一張卡：頭像＋名字＋台詞，依字數自動換下一句。台詞寫在 Resources/dungeons.txt。
    /// </summary>
    public partial class SoloBattle
    {
        readonly Queue<StoryLine> ticker = new Queue<StoryLine>();
        StoryLine tickerLine; float tickerUntil;
        int dungeonHalfSaid = -1;

        void Ticker(IEnumerable<StoryLine> lines) { if (lines != null) foreach (var l in lines) { ticker.Enqueue(l); ChatLine(l); } }
        void ClearTicker() { ticker.Clear(); tickerLine = null; dungeonHalfSaid = -1; }

        /// <summary>每幀（副本戰鬥中）：地點的台詞由 DungeonRun 送進來（SoloBattle.DungeonMap.cs）；首領剩一半血 → boss_half；換下一句</summary>
        void UpdateDungeonStory()
        {
            if (dungeon == null || battle == null) return;
            UpdateDungeonMap();
            // 首領剩一半血
            if (drun != null && dungeonHalfSaid != drun.Step && drun.node.IsBoss && battle.IsBossWave && En.Alive && En.maxHp > 0 && En.hp <= En.maxHp * 0.5f)
            {
                dungeonHalfSaid = drun.Step;
                foreach (var l in drun.node.bossHalf) { ticker.Enqueue(l); ChatLine(l); }
            }
            if (tickerLine == null || Time.time >= tickerUntil)
            {
                tickerLine = ticker.Count > 0 ? ticker.Dequeue() : null;
                if (tickerLine != null) tickerUntil = Time.time + Mathf.Clamp(1.6f + (tickerLine.text ?? "").Length * 0.09f, 2.6f, 6f);
            }
        }

        /// <summary>字幕卡：畫面上方（敵人資訊卡下面）</summary>
        void DrawDungeonTicker(float W, float H, float pad, int fs, int fsS)
        {
            if (tickerLine == null || dungeon == null) return;
            float h = fs * 3.4f, y = H - Screen.safeArea.yMax + H * 0.17f;
            var r = new Rect(pad, y, W - pad * 2, h);
            float a = Mathf.Clamp01((tickerUntil - Time.time) * 3f);
            bool narr = string.IsNullOrEmpty(tickerLine.speaker);
            var col = Story.TagColor(tickerLine.tag);
            UiKit.Card(r, 0.82f * a, narr ? (Color?)null : col, 0.8f * a);
            float x = r.x + fs * 0.5f;
            if (!narr)
            {
                float ps = h - fs * 0.6f;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(x, r.y + fs * 0.3f, ps, ps), Portraits.Get(tickerLine.speaker, tickerLine.tag), ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
                x += ps + fs * 0.4f;
                UiKit.Text(new Rect(x, r.y + fs * 0.15f, r.xMax - x - fs * 0.4f, fs * 1.1f), tickerLine.speaker, fsS, new Color(col.r, col.g, col.b, a), TextAnchor.MiddleLeft, true);
            }
            UiKit.Text(new Rect(x, r.y + (narr ? fs * 0.2f : fs * 1.2f), r.xMax - x - fs * 0.4f, narr ? h - fs * 0.4f : h - fs * 1.3f),
                Story.Fill(tickerLine.text, myClass), fsS, new Color(narr ? 0.92f : 1f, narr ? 0.92f : 1f, narr ? 0.85f : 1f, a), narr ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);
        }
    }
}

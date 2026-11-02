using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的小隊戰術與場地機關的畫面、連線（邏輯在 Combat.ArenaProps.cs）：
    ///   ・草叢、水窪：地上的區域模型（房主每 0.3 秒送 azones 給隊友）
    ///   ・爆炸桶、石柱：劇情人物（npc pr_*），用 PropVisual 換成道具模型；石柱倒下另外播一段倒下的動畫
    ///   ・隊長頭上的旗子、增援來的方向箭頭
    ///   ・一次性的效果（爆炸、導電、結凍、著火、倒下）走 ev 開頭的事件（OnRoomEventFx），房主順便送給隊友
    /// </summary>
    public partial class SoloBattle
    {
        readonly List<ArenaZoneArt> zoneGo = new List<ArenaZoneArt>();
        readonly List<(GameObject go, Vector3 axis, float born)> fallingPillars = new List<(GameObject, Vector3, float)>();
        Vector3 reinfWorld; float reinfShowUntil = -1f;
        float nextZoneSend; string lastZoneSig = "";

        /// <summary>每幀（故事關卡）：區域模型、倒下的石柱、送狀態給隊友</summary>
        void UpdateArenaPropViews()
        {
            if (battle == null) return;
            var zs = battle.zones;
            if (zoneGo.Count != zs.Count)
            {
                foreach (var g in zoneGo) if (g) Destroy(g.gameObject);
                zoneGo.Clear();
                foreach (var z in zs) { var a = HazardArt.ArenaZone(z.kind, z.r); a.transform.position = WorldFrame.FromWorld(z.at); zoneGo.Add(a); }
            }
            for (int i = 0; i < zs.Count && i < zoneGo.Count; i++) if (zoneGo[i]) { zoneGo[i].transform.position = WorldFrame.FromWorld(zs[i].at); zoneGo[i].Tick(zs[i].state, Time.time); }
            for (int i = fallingPillars.Count - 1; i >= 0; i--)
            {
                var (go, axis, born) = fallingPillars[i];
                if (!go) { fallingPillars.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((Time.time - born) / Battle.PillarFallDelay);
                go.transform.rotation = Quaternion.AngleAxis(88f * t * t, axis);   // 越倒越快
                if (Time.time - born > Battle.PillarFallDelay + 3f) { MinionRig.SpawnFx(go.transform.position); Destroy(go); fallingPillars.RemoveAt(i); }
            }
            SendZones();
        }

        void SendZones()
        {
            if (!coop || !NetIsHost || battle == null || Time.time < nextZoneSend) return;
            var zs = battle.zones;
            var sb = new System.Text.StringBuilder();
            foreach (var z in zs) sb.Append(z.kind).Append(z.state);
            string sig = sb.ToString();
            if (zs.Count == 0 && lastZoneSig.Length == 0) return;
            nextZoneSend = Time.time + (sig == lastZoneSig ? 1.5f : 0.3f);
            lastZoneSig = sig;
            var pts = new Vector3[zs.Count]; var ks = new string[zs.Count];
            for (int i = 0; i < zs.Count; i++) { pts[i] = zs[i].at; ks[i] = zs[i].kind + "," + zs[i].state; }
            NetSend?.Invoke(new Msg { t = "azones", pts = pts, ks = ks });
        }

        void ClearArenaPropViews()
        {
            foreach (var g in zoneGo) if (g) Destroy(g.gameObject);
            zoneGo.Clear();
            foreach (var f in fallingPillars) if (f.go) Destroy(f.go);
            fallingPillars.Clear();
            reinfShowUntil = -1f; lastZoneSig = "";
        }

        /// <summary>OnRoomEventFx 叫：小隊、機關的一次性效果（回傳 true＝處理了）</summary>
        bool ArenaFx(string kind, Vector3 at, Color c, string text)
        {
            var w = WorldFrame.FromWorld(at);
            bool near = Me != null && Battle.FlatDistance(at, Me.Feet) < 3f;
            switch (kind)
            {
                case "evcaptain":
                    Sfx.Play("horn", 0.5f, 1.2f);
                    if (!string.IsNullOrEmpty(text)) Say(text, 3f);
                    return true;
                case "evrout":
                    Sfx.Play("charged", 0.8f, 0.8f); Haptics.Play(Haptics.Kind.Medium);
                    if (!string.IsNullOrEmpty(text)) { Say("⚑ " + text, 3f); ChatEvent("⚑ " + text); }
                    return true;
                case "evreinf":
                    Sfx.Play("horn", 0.9f, 0.85f); Sfx.Play("warn", 0.6f, 1.1f); Haptics.Play(Haptics.Kind.Warning);
                    reinfWorld = at; reinfShowUntil = Time.time + Battle.ReinforceWarn + 1.5f;
                    if (!string.IsNullOrEmpty(text)) Say(text, 3f);
                    return true;
                case "evprophit":
                    Sfx.Play("block", 0.5f, 1.2f);
                    return true;
                case "evboom":
                    SpellFx.Impact("mage", new Color(1f, 0.5f, 0.12f), w + Vector3.up * 0.5f, 2.6f);
                    SpellFx.Impact("mage", new Color(1f, 0.85f, 0.3f), w + Vector3.up * 0.9f, 1.6f);
                    Sfx.Play("boom", 1f, 0.85f); Haptics.Play(near ? Haptics.Kind.Heavy : Haptics.Kind.Medium); Kick(near ? 22f : 10f, 0.05f);
                    return true;
                case "evfall":
                {
                    // 石柱倒下：原地放一根新的柱子，繞著底部往倒的方向轉 90 度
                    var d = Vector3.forward;
                    var parts = (text ?? "").Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dx) && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dz)) d = new Vector3(dx, 0f, dz);
                    var ud = WorldFrame.DirFromWorld(d); ud.y = 0f; if (ud.sqrMagnitude < 1e-6f) ud = Vector3.forward;
                    var go = HazardArt.RoomProp("pr_pillar_fall", Color.white).gameObject;
                    go.transform.position = w;
                    fallingPillars.Add((go, Vector3.Cross(Vector3.up, ud.normalized), Time.time));
                    Sfx.Play("warn", 0.6f, 0.7f);
                    return true;
                }
                case "evcrash":
                    MinionRig.SpawnFx(w); SpellFx.Impact("swordsman", new Color(0.75f, 0.68f, 0.6f), w + Vector3.up * 0.2f, 1.8f);
                    Sfx.Play("boom", 0.8f, 0.6f); Haptics.Play(near ? Haptics.Kind.Heavy : Haptics.Kind.Medium); Kick(near ? 16f : 8f, 0.04f);
                    if (!string.IsNullOrEmpty(text)) Say("🪨 " + text, 2f);
                    return true;
                case "evgrass":
                    Sfx.Play("burn", 0.9f); if (!string.IsNullOrEmpty(text)) Say("🔥 " + text, 2f);
                    return true;
                case "evshock":
                    SpellFx.Impact("mage", new Color(1f, 0.92f, 0.35f), w + Vector3.up * 0.2f, 2f);
                    Sfx.Play("fizz", 1f, 1.2f); Haptics.Play(Haptics.Kind.Medium);
                    if (!string.IsNullOrEmpty(text)) Say(text, 2f);
                    return true;
                case "evfreeze":
                    SpellFx.Impact("mage", new Color(0.6f, 0.9f, 1f), w + Vector3.up * 0.2f, 1.8f);
                    Sfx.Play("shatter", 0.8f, 1.2f);
                    if (!string.IsNullOrEmpty(text)) Say(text, 2f);
                    return true;
                case "evzone":
                    return true;
            }
            return false;
        }

        /// <summary>HUD：隊長的旗子、增援方向的箭頭</summary>
        void DrawArenaHud(float W, float H)
        {
            if (battle == null || phase != Phase.Fighting || cam == null) return;
            int fs = Mathf.RoundToInt(H * 0.022f);
            foreach (var m in battle.minions)
            {
                if (!m.Alive || !m.troopCaptain) continue;
                var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(m.head + Vector3.up * 0.45f));
                if (sp.z <= 0) continue;
                UiKit.Text(new Rect(sp.x - W * 0.12f, H - sp.y - fs, W * 0.24f, fs * 1.6f), "⚑ 隊長", fs, new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter, true);
            }
            if (Time.time < reinfShowUntil)
            {
                var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(reinfWorld + Vector3.up * 0.8f));
                var c = new Vector2(W * 0.5f, H * 0.5f);
                var p = new Vector2(sp.x, H - sp.y);
                bool onScreen = sp.z > 0 && p.x > W * 0.08f && p.x < W * 0.92f && p.y > H * 0.12f && p.y < H * 0.88f;
                float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 10f);
                var col = new Color(1f, 0.35f, 0.25f, blink);
                if (onScreen) { UiKit.Text(new Rect(p.x - fs * 3f, p.y - fs * 1.5f, fs * 6f, fs * 3f), "⚠", fs * 3, col, TextAnchor.MiddleCenter, true); return; }
                if (sp.z <= 0) p = c - (p - c);   // 在背後：方向反過來
                var d = p - c; if (d.sqrMagnitude < 1f) d = Vector2.up; d.Normalize();
                float k = Mathf.Min(W * 0.42f / Mathf.Max(0.01f, Mathf.Abs(d.x)), H * 0.38f / Mathf.Max(0.01f, Mathf.Abs(d.y)));
                var e = c + d * k;
                var keep = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, e);
                UiKit.Text(new Rect(e.x - fs * 2f, e.y - fs * 2f, fs * 4f, fs * 4f), "▶", fs * 3, col, TextAnchor.MiddleCenter, true);
                GUI.matrix = keep;
            }
        }
    }
}

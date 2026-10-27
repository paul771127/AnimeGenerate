using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本特殊地點的畫面與連線（邏輯在 Combat.RoomEvents.cs，只在房主／單人跑）：
    ///   ・道具（守護物、圖騰、雕像、寶箱）＝ npcId 開頭 ev_ 的劇情人物，這裡換成道具模型；護送的人用一般角色模型
    ///   ・場上的點：符文、屬性球、門、出口、崩塌的格子
    ///   ・黑暗房：畫面變暗、遠處的怪只看得到發光的眼睛
    ///   ・上方的任務說明；咒語之門聽語音
    ///   ・連線：房主每 0.25 秒送 rev（狀態），一次性的事件送 reve；隊友唸的答案送 evsay 給房主
    /// </summary>
    public partial class SoloBattle
    {
        readonly Dictionary<int, GameObject> propGo = new Dictionary<int, GameObject>();
        readonly List<GameObject> evPointGo = new List<GameObject>();
        readonly Dictionary<Fighter, GameObject> darkEyes = new Dictionary<Fighter, GameObject>();
        string evViewKind; int evViewCount = -1;
        float nextRevSend;

        /// <summary>房主：開打這個地點時，特殊地點要開始</summary>
        void StartRoomEventIfAny()
        {
            var r = drun != null ? drun.node : null;
            if (r == null || !r.IsEvent || !DungeonDecider) return;
            battle.StartRoomEvent(r.kind, r.param);
        }

        // ---------------------------------------------------------------- 道具模型
        /// <summary>UpdateNpcRigs 叫：這個人物是道具 → 這裡畫，回傳 true（護送的人不算道具）</summary>
        bool PropVisual(Fighter n)
        {
            if (n.npcId == null || !n.npcId.StartsWith("ev_") || n.npcId == "ev_walker") return false;
            if (!propGo.TryGetValue(n.minionId, out var go) || !go)
            {
                go = MakeProp(n.npcId);
                propGo[n.minionId] = go;
                SpawnFade.Play(go);
            }
            go.transform.position = WorldFrame.FromWorld(n.Feet);
            // 雕像：點亮了就發光；寶箱：真的那個偶爾閃（看 reve 的 evglint）
            if (n.npcId.StartsWith("ev_statue"))
            {
                int idx = battle.evPts.FindIndex(p => Battle.FlatDistance(p, n.Feet) < 0.1f);
                bool lit = idx >= 0 && idx < battle.evState.Count && battle.evState[idx] == 1;
                var orb = go.transform.Find("Orb");
                if (orb) orb.localScale = Vector3.one * (lit ? 0.42f : 0.28f) * (1f + 0.08f * Mathf.Sin(Time.time * 4f));
            }
            if (n.npcId == "ev_core" || n.npcId.StartsWith("ev_totem"))
            {
                var top = go.transform.Find("Glow");
                if (top) top.localScale = Vector3.one * (0.35f + 0.05f * Mathf.Sin(Time.time * 3f));
            }
            return true;
        }

        GameObject MakeProp(string id)
        {
            var root = new GameObject("EventProp_" + id);
            GameObject Part(PrimitiveType t, Color c, Vector3 pos, Vector3 scale, string name = null)
            {
                var p = Prim(t, c); p.transform.SetParent(root.transform, false); p.transform.localPosition = pos; p.transform.localScale = scale; if (name != null) p.name = name; return p;
            }
            if (id == "ev_core")
            {
                Part(PrimitiveType.Cylinder, new Color(0.35f, 0.3f, 0.25f), new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.15f, 0.7f));
                Part(PrimitiveType.Cylinder, new Color(0.85f, 0.65f, 0.25f), new Vector3(0f, 0.65f, 0f), new Vector3(0.45f, 0.35f, 0.45f));   // 鐘身
                Part(PrimitiveType.Sphere, new Color(1f, 0.9f, 0.5f, 0.8f), new Vector3(0f, 1.15f, 0f), Vector3.one * 0.35f, "Glow");
            }
            else if (id.StartsWith("ev_totem"))
            {
                Part(PrimitiveType.Cylinder, new Color(0.2f, 0.12f, 0.25f), new Vector3(0f, 0.7f, 0f), new Vector3(0.35f, 0.7f, 0.35f));
                Part(PrimitiveType.Cube, new Color(0.3f, 0.18f, 0.35f), new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.12f, 0.2f));
                Part(PrimitiveType.Sphere, new Color(0.75f, 0.4f, 1f, 0.85f), new Vector3(0f, 1.55f, 0f), Vector3.one * 0.35f, "Glow");
            }
            else if (id.StartsWith("ev_statue"))
            {
                int idx = id[id.Length - 1] - '0';
                var me = battle.npcs.Find(x => x.npcId == id);
                var col = Battle.ElementColor(me != null ? me.evElement : 0);
                Part(PrimitiveType.Cube, new Color(0.45f, 0.45f, 0.5f), new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.9f, 0.5f));
                Part(PrimitiveType.Sphere, new Color(col.r, col.g, col.b, 0.9f), new Vector3(0f, 1.15f, 0f), Vector3.one * 0.28f, "Orb");
            }
            else if (id.StartsWith("ev_chest"))
            {
                Part(PrimitiveType.Cube, new Color(0.45f, 0.28f, 0.12f), new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.4f, 0.42f));
                Part(PrimitiveType.Cube, new Color(0.85f, 0.7f, 0.25f), new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.08f, 0.44f));
            }
            return root;
        }

        void CleanupProps()
        {
            foreach (var kv in new List<KeyValuePair<int, GameObject>>(propGo))
                if (battle == null || battle.npcs.Find(n => n.minionId == kv.Key) == null)
                {
                    if (kv.Value) { MinionRig.SpawnFx(kv.Value.transform.position); Destroy(kv.Value); }
                    propGo.Remove(kv.Key);
                }
        }

        // ---------------------------------------------------------------- 場上的點、黑暗房
        void UpdateRoomEventVisuals()
        {
            if (battle == null) return;
            CleanupProps();
            string kind = battle.roomEvent;
            if (kind != evViewKind || battle.evPts.Count != evViewCount) RebuildEventPoints();
            for (int i = 0; i < evPointGo.Count && i < battle.evPts.Count; i++)
            {
                var go = evPointGo[i]; if (!go) continue;
                int st = i < battle.evState.Count ? battle.evState[i] : 0;
                var r = go.GetComponent<Renderer>();
                switch (kind)
                {
                    case "runes":
                        r.material.color = st == 1 ? new Color(1f, 0.95f, 0.4f, 0.95f) : st == 2 ? new Color(0.4f, 1f, 0.6f, 0.9f) : st == 3 ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.55f, 0.4f, 1f, 0.45f);
                        go.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f) * (st == 1 ? 1.15f : 1f);
                        break;
                    case "elements":
                        if (i >= 3) go.transform.position = WorldFrame.FromWorld(battle.evPts[i] + Vector3.up * (0.9f + 0.08f * Mathf.Sin(Time.time * 2.5f + i)));
                        else go.SetActive(st == 1);   // 雕像點亮：腳下的光圈
                        break;
                    case "collapse":
                        go.SetActive(st > 0);
                        if (st == 1) r.material.color = new Color(1f, 0.15f, 0.05f, 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 8f)));
                        else if (st == 2) r.material.color = new Color(0.05f, 0.02f, 0.02f, 0.85f);
                        break;
                    case "chant":
                        r.material.color = new Color(0.6f, 0.5f, 1f, 0.55f + 0.2f * Mathf.Sin(Time.time * 2f));
                        break;
                }
            }
            UpdateDarkness(kind == "dark");
        }

        void RebuildEventPoints()
        {
            foreach (var g in evPointGo) if (g) Destroy(g);
            evPointGo.Clear();
            evViewKind = battle.roomEvent; evViewCount = battle.evPts.Count;
            if (evViewKind == null) return;
            for (int i = 0; i < battle.evPts.Count; i++)
            {
                var p = battle.evPts[i]; GameObject go;
                switch (evViewKind)
                {
                    case "runes":
                        go = Prim(PrimitiveType.Cylinder, new Color(0.55f, 0.4f, 1f, 0.45f), true);
                        go.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
                        go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 0.02f);
                        break;
                    case "elements":
                        if (i < 3) { go = Prim(PrimitiveType.Cylinder, new Color(1f, 1f, 0.7f, 0.5f), true); go.transform.localScale = new Vector3(1.2f, 0.01f, 1.2f); go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 0.02f); go.SetActive(false); }
                        else { var c = Battle.ElementColor(battle.evState[i]); go = Prim(PrimitiveType.Sphere, new Color(c.r, c.g, c.b, 0.85f)); go.transform.localScale = Vector3.one * 0.3f; go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 0.9f); }
                        break;
                    case "collapse":
                        go = Prim(PrimitiveType.Cube, new Color(1f, 0.15f, 0.05f, 0.3f), true);
                        go.transform.localScale = new Vector3(0.96f, 0.01f, 0.96f);
                        go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 0.015f);
                        go.SetActive(false);
                        break;
                    case "chant":
                        // 咒語之門：兩根門柱＋中間發光的符文面
                        go = Prim(PrimitiveType.Cube, new Color(0.6f, 0.5f, 1f, 0.6f), true);
                        go.transform.localScale = new Vector3(1.4f, 2f, 0.05f);
                        go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 1f);
                        var face = Fighter.Flat(Me.Feet - p); if (face != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(WorldFrame.DirFromWorld(face), Vector3.up);
                        break;
                    case "escort":
                        go = Prim(PrimitiveType.Cylinder, new Color(0.4f, 1f, 0.5f, 0.45f), true);
                        go.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f);
                        go.transform.position = WorldFrame.FromWorld(p + Vector3.up * 0.02f);
                        break;
                    default: go = null; break;
                }
                evPointGo.Add(go);
            }
        }

        /// <summary>黑暗房：2.5m 外的小怪看不到身體，只剩一對發光的眼睛</summary>
        void UpdateDarkness(bool on)
        {
            foreach (var kv in minionGo)
            {
                var m = kv.Key; var rig = kv.Value; if (!rig) continue;
                bool far = on && m.Alive && Battle.FlatDistance(m.Feet, Me.Feet) > DarkSight;
                foreach (var r in rig.GetComponentsInChildren<Renderer>()) r.enabled = !far;
                if (far)
                {
                    if (!darkEyes.TryGetValue(m, out var eye) || !eye)
                    {
                        eye = new GameObject("DarkEyes");
                        for (int s = -1; s <= 1; s += 2) { var e = Prim(PrimitiveType.Sphere, new Color(1f, 0.15f, 0.1f, 1f), true); e.transform.SetParent(eye.transform, false); e.transform.localPosition = new Vector3(0.07f * s, 0f, 0f); e.transform.localScale = Vector3.one * 0.06f; }
                        darkEyes[m] = eye;
                    }
                    eye.SetActive(true);
                    eye.transform.position = WorldFrame.FromWorld(m.head + Fighter.Flat(m.forward) * 0.12f);
                    var f = WorldFrame.DirFromWorld(m.forward); f.y = 0f; if (f.sqrMagnitude > 1e-6f) eye.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                }
                else if (darkEyes.TryGetValue(m, out var e2) && e2) e2.SetActive(false);
            }
            foreach (var kv in new List<KeyValuePair<Fighter, GameObject>>(darkEyes))
                if (!minionGo.ContainsKey(kv.Key) || !kv.Key.Alive || !on) { if (kv.Value) Destroy(kv.Value); darkEyes.Remove(kv.Key); }
        }
        public const float DarkSight = 2.5f;

        void ClearRoomEventViews()
        {
            foreach (var g in evPointGo) if (g) Destroy(g);
            evPointGo.Clear(); evViewKind = null; evViewCount = -1;
            foreach (var g in propGo.Values) if (g) Destroy(g);
            propGo.Clear();
            foreach (var g in darkEyes.Values) if (g) Destroy(g);
            darkEyes.Clear();
        }

        // ---------------------------------------------------------------- 上方說明、黑暗、頭上的血條
        void DrawRoomEventHud(float W, float H)
        {
            if (battle == null || phase != Phase.Fighting || string.IsNullOrEmpty(battle.roomEvent)) return;
            if (battle.roomEvent == "dark") { GUI.color = new Color(0f, 0f, 0.02f, 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            int fs = Mathf.RoundToInt(H * 0.022f);
            if (!string.IsNullOrEmpty(battle.evText)) UiKit.Pill(H * 0.2f, battle.evText, fs, new Color(1f, 0.93f, 0.7f), W * 0.92f, 0.6f);
            // 圖騰、守護物、護送的人：頭上的血條
            foreach (var n in battle.npcs)
            {
                if (!n.Alive || n.npcId == null || !n.npcId.StartsWith("ev_") || n.damageTakenMul <= 0f) continue;
                var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(n.Feet + Vector3.up * (n.npcId == "ev_walker" ? 2f : 1.8f)));
                if (sp.z <= 0) continue;
                var r = new Rect(sp.x - W * 0.08f, H - sp.y, W * 0.16f, fs * 0.6f);
                UiKit.Bar(r, n.hp / Mathf.Max(1, n.maxHp), n.npcSide == 1 ? new Color(0.75f, 0.4f, 1f) : new Color(0.4f, 1f, 0.6f), "", Mathf.RoundToInt(fs * 0.7f));
            }
        }

        // ---------------------------------------------------------------- 事件（Battle.Emit）
        /// <summary>OnBattleEvent 叫：ev 開頭的事件；房主順便送給隊友</summary>
        bool OnRoomEventFx(string kind, Vector3 at, Color c, string text)
        {
            if (!kind.StartsWith("ev")) return false;
            if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "reve", k = kind, a = text ?? "", p = at, d = new Vector3(c.r, c.g, c.b) });
            switch (kind)
            {
                case "evstart":
                    Sfx.Play("horn", 0.6f, 1.1f); break;
                case "evok":
                    Sfx.Play("charged", 0.7f, 1.3f); Haptics.Play(Haptics.Kind.Light);
                    if (!string.IsNullOrEmpty(text)) floaters.Add(new Floater { posMap = at + Vector3.up * 1f, text = text, color = c, born = Time.time });
                    break;
                case "evwrong":
                    Sfx.Play("warn", 0.9f, 0.8f); Haptics.Play(Haptics.Kind.Heavy); Kick(10f, 0f);
                    if (!string.IsNullOrEmpty(text)) Say("❌ " + text, 2.5f);
                    break;
                case "evdone":
                    Sfx.Play("charged", 0.9f, 1f); Haptics.Play(Haptics.Kind.Medium);
                    if (!string.IsNullOrEmpty(text)) { Say("✅ " + text, 3f); ChatEvent("✅ " + text); }
                    break;
                case "evcharge":
                    Sfx.Play("select", 0.8f, 1.3f);
                    if (!string.IsNullOrEmpty(text)) Say(text, 2f);
                    break;
                case "evglint":
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.7f, text = "✨", color = c, born = Time.time });
                    if (Battle.FlatDistance(at, Me.Feet) < 1.5f) Haptics.Play(Haptics.Kind.Light);   // 靠近真的寶箱：手機輕震
                    break;
                case "evinfo":
                    if (!string.IsNullOrEmpty(text)) Say(text, 2f);
                    break;
            }
            return true;
        }

        // ---------------------------------------------------------------- 連線
        /// <summary>房主：每 0.25 秒送狀態（沒有特殊地點時送一次空的，讓隊友清掉）</summary>
        void SendRoomEventState()
        {
            if (!coop || !NetIsHost || battle == null || drun == null || Time.time < nextRevSend) return;
            bool active = battle.roomEvent != null;
            if (!active && !revWasActive) return;
            revWasActive = active;
            nextRevSend = Time.time + 0.25f;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < battle.evState.Count; i++) { if (i > 0) sb.Append(','); sb.Append(battle.evState[i]); }
            NetSend?.Invoke(new Msg { t = "rev", k = battle.roomEvent ?? "", e = battle.evPhase, id = battle.evFails, a = battle.evText ?? "", g = sb.ToString(), pts = battle.evPts.ToArray(), ks = new[] { battle.evRiddle ?? "", battle.evHint ?? "" }, dmg = battle.evCharge });
        }
        bool revWasActive;

        bool OnRoomEventMsg(Msg m)
        {
            switch (m.t)
            {
                case "rev":
                    if (NetIsHost || battle == null) return true;
                    battle.roomEvent = string.IsNullOrEmpty(m.k) ? null : m.k;
                    battle.evPhase = m.e; battle.evFails = m.id; battle.evText = m.a ?? ""; battle.evCharge = m.dmg;
                    battle.evPts.Clear(); if (m.pts != null) battle.evPts.AddRange(m.pts);
                    battle.evState.Clear();
                    if (!string.IsNullOrEmpty(m.g)) foreach (var x in m.g.Split(',')) battle.evState.Add(int.TryParse(x, out var v) ? v : 0);
                    if (m.ks != null && m.ks.Length >= 2) { battle.evRiddle = m.ks[0]; battle.evHint = m.ks[1]; }
                    return true;
                case "reve":
                    if (NetIsHost) return true;
                    OnRoomEventFx(m.k ?? "", m.p, new Color(m.d.x, m.d.y, m.d.z), string.IsNullOrEmpty(m.a) ? null : m.a);
                    return true;
                case "evsay":
                    if (NetIsHost && battle != null) battle.EvChant(m.k);
                    return true;
            }
            return false;
        }

        /// <summary>語音：咒語之門作答（房主直接判定；隊友送給房主）。回傳 true＝這句話當作答案用掉了</summary>
        bool RoomEventSpeech(string text)
        {
            if (battle == null || battle.roomEvent != "chant" || battle.evPhase != 1 || string.IsNullOrEmpty(text)) return false;
            if (DungeonDecider) battle.EvChant(text);
            else NetSend?.Invoke(new Msg { t = "evsay", k = text });
            return true;
        }
    }
}

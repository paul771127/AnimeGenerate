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
        readonly Dictionary<string,int> roomElementLooks=new Dictionary<string,int>();

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
            int idx=battle.evPts.FindIndex(p=>Battle.FlatDistance(p,n.Feet)<.1f);
            bool lit=idx>=0 && idx<battle.evState.Count && battle.evState[idx]==1;
            var prop=go.GetComponent<RoomPropArt>();
            if(n.npcId.StartsWith("ev_statue"))prop.SetElement(RoomElementLook(n.npcId));
            prop.Tick(n.hp,n.maxHp,lit,Time.time);
            return true;
        }

        int RoomElementLook(string id)
        {if(roomElementLooks.TryGetValue(id,out var element))return element;return battle.npcs.Find(n=>n.npcId==id)?.evElement??0;}
        GameObject MakeProp(string id)
        {
            var n=battle.npcs.Find(x=>x.npcId==id);
            var prop=HazardArt.RoomProp(id,Color.white);if(id.StartsWith("ev_statue"))prop.SetElement(RoomElementLook(id));return prop.gameObject;
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
                var art=go.GetComponent<RoomPointArt>();
                if(kind=="runes" && st==1 && !battle.RuneVisibleFor(MyPid))st=0;
                if(art)art.Tick(st,Time.time);
                switch(kind)
                {
                    case "elements":
                        if(Battle.IsEventOrb(i))go.transform.position=WorldFrame.FromWorld(battle.evPts[i]+Vector3.up*(.9f+.08f*Mathf.Sin(Time.time*2.5f+i)));
                        else go.SetActive(st==1);
                        break;
                    case "collapse":go.SetActive(st>0);break;
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
                string type=evViewKind;
                if(type=="elements")type=Battle.IsEventOrb(i)?"orb":"seal";
                if(type=="runes"||type=="orb"||type=="seal"||type=="collapse"||type=="chant"||type=="escort")
                {
                    Color color=type=="orb"?Battle.ElementColor(battle.evState[i]):type=="escort"?new Color(.3f,1,.5f):new Color(.65f,.4f,1,.8f);
                    go=HazardArt.RoomPoint(type,type=="orb"?battle.evState[i]:i,color).gameObject;
                    go.transform.position=WorldFrame.FromWorld(p+Vector3.up*(type=="orb"?.9f:0));
                    if(type=="seal"||type=="collapse")go.SetActive(false);
                    if(type=="chant"){var face=Fighter.Flat(Me.Feet-p);if(face!=Vector3.zero)go.transform.rotation=Quaternion.LookRotation(WorldFrame.DirFromWorld(face),Vector3.up);}
                }
                else go=null;
                evPointGo.Add(go);
            }
        }

        /// <summary>黑暗房：2.5m 外的小怪看不到身體，只剩一對發光的眼睛</summary>
        void UpdateDarkness(bool on)
        {
            foreach (var kv in minionGo)
            {
                var m = kv.Key; var rig = kv.Value; if (!rig) continue;
                bool far = on && m.Alive && (battle.RoomCoop?!battle.EventLitByTeam(m.Feet,DarkSight):Battle.FlatDistance(m.Feet, Me.Feet)>DarkSight);
                foreach (var r in rig.GetComponentsInChildren<Renderer>()) r.enabled = !far;
                if (far)
                {
                    if (!darkEyes.TryGetValue(m, out var eye) || !eye)
                    {
                        eye=HazardArt.RoomPoint("eyes",0,Color.red).gameObject;
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
            propGo.Clear();roomElementLooks.Clear();
            foreach (var g in darkEyes.Values) if (g) Destroy(g);
            darkEyes.Clear();
        }

        // ---------------------------------------------------------------- 上方說明、黑暗、頭上的血條
        void DrawRoomEventHud(float W, float H)
        {
            if (battle == null || phase != Phase.Fighting || string.IsNullOrEmpty(battle.roomEvent)) return;
            if (battle.roomEvent == "dark") { GUI.color = new Color(0f, 0f, 0.02f, 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            int fs = Mathf.RoundToInt(H * 0.022f);
            if (!string.IsNullOrEmpty(battle.evText)) UiKit.Pill(H * 0.2f, battle.RoomTextFor(MyPid), fs, new Color(1f, 0.93f, 0.7f), W * 0.92f, 0.6f);
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
        bool OnRoomEventFx(string kind, Vector3 at, Color c, string text, int viewer=-1)
        {
            if (!kind.StartsWith("ev")) return false;
            if(kind=="evglint" && (!coop||NetIsHost))viewer=battle!=null?battle.evGlintOwner:-1;
            if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "reve", k = kind, a = text ?? "", p = at, d = new Vector3(c.r, c.g, c.b),id=viewer,ok=viewer>=0 });
            if(viewer>=0 && viewer!=MyPid)return true;
            if(kind=="evinfo" || kind=="evglint")
                foreach(var g in propGo.Values)if(g && Vector3.Distance(g.transform.position,WorldFrame.FromWorld(at))<.8f)g.GetComponent<RoomPropArt>().Pulse(Time.time);
            if(kind=="evwrong" && battle!=null && battle.roomEvent=="mimic")MinionRig.SpawnFx(WorldFrame.FromWorld(at));
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
                    foreach(var point in evPointGo)if(point){MinionRig.SpawnFx(point.transform.position);point.SetActive(false);}
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
            var looks=new List<string>();foreach(var n in battle.npcs)if(n.evElement>0)looks.Add(n.npcId+":"+n.evElement);
            NetSend?.Invoke(new Msg { t = "rev", k = battle.roomEvent ?? "", e = battle.evPhase, id = battle.evFails, a = battle.roomEvent=="chant" && battle.RoomCoop?"請分享各自的線索":battle.evText ?? "", g = sb.ToString(), pts = battle.evPts.ToArray(), ks = new[] { battle.evRiddle ?? "", battle.evHint ?? "", string.Join(",",battle.evPlayerIds),battle.evRuneOwner.ToString(),string.Join(",",looks) }, dmg = battle.evCharge });
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
                    battle.SetRoomViewPlayers(m.ks!=null && m.ks.Length>2?m.ks[2]:"",m.ks!=null && m.ks.Length>3 && int.TryParse(m.ks[3],out int owner)?owner:-1);
                    roomElementLooks.Clear();
                    if(m.ks!=null && m.ks.Length>4)foreach(var pair in m.ks[4].Split(',')){var bits=pair.Split(':');if(bits.Length==2 && int.TryParse(bits[1],out int element))roomElementLooks[bits[0]]=element;}
                    return true;
                case "reve":
                    if (NetIsHost) return true;
                    OnRoomEventFx(m.k ?? "", m.p, new Color(m.d.x, m.d.y, m.d.z), string.IsNullOrEmpty(m.a) ? null : m.a,m.ok?m.id:-1);
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

using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 合作連線：自己的友方人物（影分身、收服的魔物）送給隊友顯示、被隊友那邊的怪打到時把傷害送回來；
    /// 主教的普攻治療隊友、聖域障壁的同步與畫面；審判之地的畫面。
    /// </summary>
    public partial class SoloBattle
    {
        float nextFriendSend; int friendSentCount;

        void WireFriendNet()
        {
            battle.OnFriendHurt += (n, dmg) => SendTo(n.friendOwner, new Msg { t = "fhurt", id = n.friendLocalId, s = dmg });
            battle.OnFriendHeal += (n, amt) => SendTo(n.friendOwner, new Msg { t = "fheal", id = n.friendLocalId, dmg = amt });
            battle.OnAllyHeal += (a, amt) => SendTo(a.pid, new Msg { t = "aheal", dmg = amt });
            battle.OnCaptured += id => SendTo(0, new Msg { t = "capt", id = id });
            battle.OnBarrier += b => { b.key = $"{MyPid}:{b.id}"; NetSend?.Invoke(new Msg { t = "barrier", k = b.key, p = b.pos, rad = b.radius, s = b.until - battle.now, dmg = b.charges }); };
            battle.OnBarrierUse += b => { if (b.key != null) NetSend?.Invoke(new Msg { t = "baruse", k = b.key, dmg = b.charges }); };
        }

        /// <summary>每 0.1 秒：自己這台的友方人物（位置、血量、種類）送給隊友</summary>
        void SendFriends()
        {
            if (!coop || battle == null || Time.time < nextFriendSend) return;
            nextFriendSend = Time.time + 0.1f;
            var fs = battle.LocalFriends();
            if (fs.Count == 0 && friendSentCount == 0) return;
            friendSentCount = fs.Count;
            var pts = new Vector3[fs.Count]; var ks = new string[fs.Count];
            for (int i = 0; i < fs.Count; i++)
            {
                var n = fs[i];
                pts[i] = n.head;
                string name = (n.name ?? "").Replace("|", "");
                if (n.cloneOf != null) name = $"👤 {Me.name}的影分身".Replace("|", "");
                ks[i] = $"{n.minionId}|{n.tamedKind}|{Mathf.CeilToInt(n.hp)}|{n.maxHp}|{name}|{(n.cls != null ? n.cls.id : "assassin")}";
            }
            NetSend?.Invoke(new Msg { t = "fnpc", pts = pts, ks = ks });
        }

        /// <summary>合作的友方人物／治療／障壁訊息；處理了回傳 true</summary>
        bool OnFriendMsg(Msg m)
        {
            switch (m.t)
            {
                case "fnpc": battle.SyncFriends(m.pid, m.pts, m.ks); return true;
                case "fhurt": battle.HurtLocalFriend(m.id, m.s); return true;
                case "fheal": battle.HealLocalFriend(m.id, m.dmg); return true;
                case "aheal":
                {
                    var who = battle.AllyByPid(m.pid);
                    battle.HealFromAlly(m.dmg);
                    Say($"✨ {(who != null ? who.name : "隊友")}的神聖飛彈幫你回了 {m.dmg} HP", 1.5f);
                    return true;
                }
                case "capt": if (NetIsHost) battle.ReleaseMinion(m.id); return true;
                case "barrier": battle.AddRemoteBarrier(m.k, m.p, m.rad, m.s, m.dmg); return true;
                case "baruse": battle.SyncBarrierUse(m.k, m.dmg); return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 障壁的畫面：金色半透明圓頂＋地上的光圈，剩幾次就幾道光環
        readonly Dictionary<Battle.Barrier, GameObject> barrierGo = new Dictionary<Battle.Barrier, GameObject>();
        void UpdateBarrierViews()
        {
            if (battle == null) return;
            var live = new HashSet<Battle.Barrier>(battle.barriers);
            foreach (var b in battle.barriers)
            {
                if (!barrierGo.TryGetValue(b, out var go) || !go)
                {
                    go = new GameObject("Holy barrier");
                    var dome = Prim(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.4f, 0.1f), true);
                    dome.name = "Dome"; dome.transform.SetParent(go.transform, false);
                    dome.transform.localScale = new Vector3(b.radius * 2f, b.radius * 1.2f, b.radius * 2f);
                    for (int i = 0; i < Mathf.Max(1, b.maxCharges); i++)
                    {
                        var ring = SpellFx.Line(go.transform, "Charge ring " + i, SpellFx.Circle(48, b.radius * (1f - 0.08f * i)), true, 0.03f, new Color(1f, 0.9f, 0.5f, 0.9f), true, true);
                        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); ring.transform.localPosition = new Vector3(0f, 0.02f + 0.01f * i, 0f);
                    }
                    barrierGo[b] = go;
                }
                go.transform.position = WorldFrame.FromWorld(b.pos);
                float left = Mathf.Clamp01((b.until - battle.now) / 1.5f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                var dr = go.transform.Find("Dome").GetComponent<Renderer>();
                dr.material.color = new Color(1f, 0.85f, 0.4f, (0.08f + 0.05f * pulse) * left);
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var ch = go.transform.GetChild(i);
                    if (ch.name.StartsWith("Charge ring ")) ch.gameObject.SetActive(int.Parse(ch.name.Substring(12)) < b.charges);
                }
            }
            foreach (var kv in new List<KeyValuePair<Battle.Barrier, GameObject>>(barrierGo))
                if (!live.Contains(kv.Key)) { if (kv.Value) { SpellFx.Impact("priest", new Color(1f, 0.9f, 0.5f), WorldFrame.FromWorld(kv.Key.pos + Vector3.up * 0.8f), 1.5f); Destroy(kv.Value); } barrierGo.Remove(kv.Key); }
        }
        void ClearBarrierViews()
        {
            foreach (var go in barrierGo.Values) if (go) Destroy(go);
            barrierGo.Clear();
        }

        /// <summary>審判之地：金色的地面光圈，偶爾冒出光柱碎片</summary>
        GameObject HolyZoneView(BurnZone z)
        {
            var go = new GameObject("Judgement ground");
            var disc = Prim(PrimitiveType.Cylinder, new Color(1f, 0.85f, 0.35f, 0.35f), true);
            disc.name = "Disc"; disc.transform.SetParent(go.transform, false);
            disc.transform.localScale = new Vector3(z.radius * 2f, 0.004f, z.radius * 2f);
            var ring = SpellFx.Line(go.transform, "Edge", SpellFx.Circle(48, z.radius), true, 0.035f, new Color(1f, 0.9f, 0.5f, 0.95f), true, true);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            return go;
        }
        void TickHolyZone(BurnZone z, GameObject go)
        {
            float left = Mathf.Clamp01((z.until - battle.now) / 1.5f);
            float flick = 0.25f + 0.12f * Mathf.Sin(Time.time * 6f + z.pos.x * 5f);
            var d = go.transform.Find("Disc");
            if (d) d.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.35f, flick * left);
            if (UnityEngine.Random.value < Time.deltaTime * 3f)
            {
                var r2 = UnityEngine.Random.insideUnitCircle * z.radius * 0.85f;
                SpellFx.Impact("priest", new Color(1f, 0.9f, 0.5f), WorldFrame.FromWorld(z.pos + new Vector3(r2.x, 0.05f, r2.y)), 0.5f);
            }
        }
    }
}

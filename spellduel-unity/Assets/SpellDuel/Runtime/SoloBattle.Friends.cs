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

        // Presentation only: remaining charges and life always come from Battle.Barrier.
        readonly Dictionary<Battle.Barrier, GameObject> barrierGo = new Dictionary<Battle.Barrier, GameObject>();
        void UpdateBarrierViews()
        {
            if(battle==null)return;var live=new HashSet<Battle.Barrier>(battle.barriers);
            foreach(var b in battle.barriers)
            {
                if(!barrierGo.TryGetValue(b,out var go)||!go)
                {
                    go=SpellFx.BarrierShell(b.radius,Mathf.Max(1,b.maxCharges));barrierGo[b]=go;
                    go.GetComponent<BarrierArtView>().LastCharges=b.charges;
                }
                go.transform.position=WorldFrame.FromWorld(b.pos);var view=go.GetComponent<BarrierArtView>();
                if(b.charges<view.LastCharges&&b.charges>0)SpellFx.BarrierRipple(WorldFrame.FromWorld(b.pos+Vector3.up*b.radius*.55f));
                view.Tick(b.charges,Mathf.Clamp01((b.until-battle.now)/1.5f));
            }
            foreach(var kv in new List<KeyValuePair<Battle.Barrier,GameObject>>(barrierGo))
                if(!live.Contains(kv.Key))
                {
                    if(kv.Value)
                    {
                        var view=kv.Value.GetComponent<BarrierArtView>();
                        if(kv.Key.charges<=0&&!view.shattered){view.shattered=true;SpellFx.BarrierShatter(kv.Value.transform.position,kv.Key.radius);}
                        Destroy(kv.Value);
                    }
                    barrierGo.Remove(kv.Key);
                }
        }
        void BarrierHitArt(Vector3 at)
        {
            SpellFx.BarrierRipple(WorldFrame.FromWorld(at));
            foreach(var kv in barrierGo)if(kv.Value&&Battle.FlatDistance(at,kv.Key.pos)<=kv.Key.radius)
            {var view=kv.Value.GetComponent<BarrierArtView>();if(view.LastCharges>kv.Key.charges){view.LastCharges=kv.Key.charges;break;}}
        }
        void BarrierBreakArt(Vector3 at)
        {
            foreach(var kv in barrierGo)if(kv.Value&&Battle.FlatDistance(at,kv.Key.pos)<.1f)
            {var view=kv.Value.GetComponent<BarrierArtView>();if(!view.shattered){view.shattered=true;SpellFx.BarrierShatter(WorldFrame.FromWorld(at),kv.Key.radius);}return;}
            SpellFx.BarrierShatter(WorldFrame.FromWorld(at),2.5f);
        }
        void ClearBarrierViews()
        {
            foreach(var go in barrierGo.Values)if(go)Destroy(go);barrierGo.Clear();
        }
        GameObject HolyZoneView(BurnZone z)=>SpellFx.HolyGround(z.radius,z.skill!=null&&z.skill.id=="heavenpunish");
        void TickHolyZone(BurnZone z,GameObject go)
        {go.GetComponent<HolyGroundView>().Tick(Mathf.Clamp01((z.until-battle.now)/1.5f));}
    }
}

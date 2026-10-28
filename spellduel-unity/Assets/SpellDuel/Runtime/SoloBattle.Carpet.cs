using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 天穹飛毯副本的畫面與連線（邏輯在 Combat.Carpet.cs、資料在 Carpet.cs；美術是 Codex 的 CarpetScene 等）：
    ///   飛毯場景（取代原本的主題場景）、飛來的障礙與預警、彈幕砲彈、載具接近→敵人跳上飛毯（落地布面下沉＋手機震動）、
    ///   飛艇 Boss（招式開砲、俯衝、第二階段冒煙、墜落）、畫面中央的「跳／蹲／往哪邊閃」提示。
    ///   連線：房主送 skyq（障礙）、skyb（登毯）、skys（站別、前方、俯衝、階段）；時間用共同時鐘換算。
    /// </summary>
    public partial class SoloBattle
    {
        bool SkyDungeon => dungeon != null && dungeon.id == Carpet.DungeonId;
        /// <summary>現在這個地點的飛毯站別（−1＝不是飛毯）</summary>
        int SkyStationNow => SkyDungeon && drun != null && drun.node != null ? drun.node.CarpetStation : -1;
        double SkyNow => SharedClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;

        /// <summary>設定：飛毯的晃動（0 關掉、1 正常；只晃畫面，不動 AR 相機和判定）</summary>
        public const string CarpetSwayKey = "SD_CarpetSway";
        public static float CarpetSway { get => PlayerPrefs.GetFloat(CarpetSwayKey, 1f); set => PlayerPrefs.SetFloat(CarpetSwayKey, Mathf.Clamp01(value)); }

        CarpetScene skyScene; Vector3 skySceneFwd; int skyScenePts = -1;
        AirshipRig skyShip; float skyShipDeadAt = -1f; int skyShipPhase = 1;
        readonly Dictionary<int, CarpetObstacleView> skyObViews = new Dictionary<int, CarpetObstacleView>();
        readonly Dictionary<int, SkyShotView> skyShotViews = new Dictionary<int, SkyShotView>();
        readonly Dictionary<int, float> skyFireAt = new Dictionary<int, float>();   // 飛艇的招式：開砲的時間（之前是砲門打開的預備）
        sealed class BoardView { public SkyVehicleView veh; public SkyEnemyRig rider; public bool landed, dropped; }
        readonly Dictionary<int, BoardView> skyBoardViews = new Dictionary<int, BoardView>();
        readonly Dictionary<Fighter, SkyEnemyRig> skyBodies = new Dictionary<Fighter, SkyEnemyRig>();
        readonly HashSet<int> skyKeep = new HashSet<int>(), skyFired = new HashSet<int>();
        float nextSkySend; bool skyWired;

        // ---------------------------------------------------------------- 開始、連線
        void BeginSky()
        {
            if (!SkyDungeon || skyWired) return;
            skyWired = true;
            battle.OnSkyCue += q => { if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "skyq", k = JsonUtility.ToJson(q), t0 = SkyNow + q.hit - battle.now }); };
            battle.OnSkyBoard += b => { if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "skyb", k = JsonUtility.ToJson(b), t0 = SkyNow + b.start - battle.now }); };
        }

        /// <summary>進到地點（房主、隊友）：飛毯的站別；前方先用自己面對的方向（隊友之後收房主的）</summary>
        void SkyEnterNode()
        {
            if (battle == null) return;
            ItemGen.ExclusiveSet = SkyDungeon ? "skyweave" : null;   // 天穹航裝：只有這個副本掉
            int st = SkyStationNow;
            battle.SetCarpetStation(st, Fighter.Flat(Me.forward));
        }

        void SendSkyState()
        {
            if (!coop || !NetIsHost || battle == null || battle.carpetStation < 0 || Time.time < nextSkySend) return;
            nextSkySend = Time.time + 0.2f;
            NetSend?.Invoke(new Msg { t = "skys", e = battle.carpetStation, d = battle.carpetFwd, s = battle.carpetSwoop, hp = battle.carpetPhase });
        }

        bool SkyMessage(Msg m)
        {
            switch (m.t)
            {
                case "skyq":
                    if (NetIsHost || battle == null || SkyStationNow < 0) return true;
                    { var q = JsonUtility.FromJson<SkyCue>(m.k); if (q != null && q.station == SkyStationNow) battle.AddRemoteSkyCue(q, battle.now + (float)(m.t0 - SkyNow)); }
                    return true;
                case "skyb":
                    if (NetIsHost || battle == null || SkyStationNow < 0) return true;
                    { var b = JsonUtility.FromJson<SkyBoard>(m.k); if (b != null) battle.AddRemoteSkyBoard(b, battle.now + (float)(m.t0 - SkyNow)); }
                    return true;
                case "skys":
                    if (NetIsHost || battle == null) return true;
                    if (m.e == SkyStationNow) { battle.SetCarpetStation(m.e, m.d); battle.carpetSwoop = m.s; battle.carpetPhase = m.hp; }
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 每幀的畫面（UpdateDungeonMap 叫）
        void UpdateSkyArt(float dt)
        {
            int st = SkyStationNow;
            if (st < 0 || battle == null) { if (skyScene || skyShip) ClearSkyViews(); return; }
            SendSkyState();
            if (bossRig) bossRig.gameObject.SetActive(false);   // 原本的 Boss 模型：飛艇取代
            var poly = area.Polygon;
            var fwdU = WorldFrame.DirFromWorld(battle.carpetFwd); fwdU.y = 0f;
            if (fwdU.sqrMagnitude < 1e-4f) fwdU = Vector3.forward;
            fwdU.Normalize();
            // 飛毯：前方換了（新的一站房主面對別的方向）或場地重設 → 重建
            if (skyScene && (Vector3.Angle(skySceneFwd, fwdU) > 10f || skyScenePts != poly.Count)) { Destroy(skyScene.gameObject); skyScene = null; }
            if (!skyScene && poly.Count >= 3)
            {
                skyScene = CarpetScene.Create(new List<Vector3>(poly), poly[0].y, fwdU);
                skySceneFwd = fwdU; skyScenePts = poly.Count;
            }
            if (skyScene)
            {
                skyScene.SetVariant(st);
                bool swoop = battle.carpetSwoop > 0.01f;
                float want = battle.Over || battle.awaitingNext ? 18f : swoop ? 6f : 12f;   // 俯衝時慢下來；清完一站加速
                skyScene.Speed = Mathf.MoveTowards(skyScene.Speed, want, dt * 6f);
                skyScene.SwayScale = CarpetSway;
            }
            UpdateSkyCueViews();
            UpdateSkyBoardViews();
            UpdateAirship(dt);
        }

        Vector3 SkyU(Vector3 world) => WorldFrame.FromWorld(world);

        void UpdateSkyCueViews()
        {
            battle.CarpetFrame(out var c, out float halfW, out float front, out float back);
            var fwd = battle.carpetFwd; var right = Carpet.Right(fwd);
            var frontU = SkyU(c + fwd * front);
            var fwdU = WorldFrame.DirFromWorld(fwd); fwdU.y = 0f; fwdU.Normalize();
            float chest = (StandH > 0f ? StandH : 1.5f) * 0.85f;
            float now = battle.now;
            skyKeep.Clear();
            foreach (var q in battle.skyCues)
            {
                skyKeep.Add(q.id);
                if (q.kind >= Carpet.ShotLow)
                {
                    // 砲彈：從飛艇船首飛到前緣那一條線，再掠過飛毯
                    if (!skyShotViews.TryGetValue(q.id, out var sv) || !sv)
                    {
                        if (now < q.Spawn) continue;
                        sv = SkyShotView.Create(q.kind == Carpet.ShotLow); skyShotViews[q.id] = sv;
                        if (skyShip && q.lane == FirstLane(q.group)) skyShip.Fire(4);
                    }
                    float h = q.kind == Carpet.ShotLow ? 0.25f : chest;
                    var from = battle.ShipRoot - fwd * 4.5f + right * q.lane * 0.6f + Vector3.up * -0.6f;
                    var to = c + fwd * front + right * q.lane + Vector3.up * h;
                    Vector3 p = now < q.hit ? Vector3.Lerp(from, to, Mathf.Clamp01((now - q.Spawn) / Mathf.Max(0.1f, q.hit - q.Spawn))) : to - fwd * (now - q.hit) * q.speed;
                    sv.transform.SetPositionAndRotation(SkyU(p), Quaternion.LookRotation(-fwdU));
                    if (now > q.hit && (now - q.hit) * q.speed > front + back + 2f) sv.gameObject.SetActive(false);
                    continue;
                }
                if (!skyObViews.TryGetValue(q.id, out var v) || !v)
                {
                    if (now < q.Spawn) continue;
                    v = CarpetObstacleView.Create(q.kind, q.station); skyObViews[q.id] = v;
                    if (q.boss && skyShip)
                    {
                        // 飛艇的招式：砲門先打開 0.5 秒，再開火（障礙從船首飛出來）
                        if (q.kind == Carpet.Half) skyShip.SetSide(-q.side);   // 船首對著玩家：船的右邊＝玩家的左邊
                        if (q.kind == Carpet.Gap) skyShip.SetGap(-q.gap / Mathf.Max(0.3f, halfW) * 1.8f);
                        skyShip.Warn(q.kind, 0.5f);
                        skyFireAt[q.id] = q.Spawn + 0.5f;
                    }
                }
                if (q.boss && skyFireAt.TryGetValue(q.id, out var fire))
                {
                    if (!skyFired.Contains(q.id))
                    {
                        if (now < fire) { v.gameObject.SetActive(false); continue; }
                        skyFired.Add(q.id); v.gameObject.SetActive(true);
                        if (skyShip) skyShip.Fire(q.kind);
                    }
                    float span = Mathf.Max(0.2f, q.hit - fire), u = Mathf.Clamp01((now - fire) / span);
                    v.TravelSeconds = span * 30f / 9f; v.WarningLeadSeconds = q.lead;
                    if (now < q.hit) v.Tick(1f - 0.3f * (1f - u), frontU, fwdU, halfW * 2f, chest, q.side, q.gap, q.gapW);
                }
                else
                {
                    v.TravelSeconds = Mathf.Max(0.5f, q.travel); v.WarningLeadSeconds = q.lead;
                    if (now < q.hit) v.Tick(Mathf.Clamp01((now - q.Spawn) / Mathf.Max(0.1f, q.travel)), frontU, fwdU, halfW * 2f, chest, q.side, q.gap, q.gapW);
                }
                if (now >= q.hit) v.Passed();   // 過了前緣：掠過飛毯（撞到的話特效、扣血在 HazardDamage）
            }
            Prune(skyObViews); Prune(skyShotViews);
        }

        float FirstLane(int group) { foreach (var q in battle.skyCues) if (q.group == group) return q.lane; return float.NaN; }

        void Prune<T>(Dictionary<int, T> views) where T : Component
        {
            List<int> gone = null;
            foreach (var kv in views)
                if (!skyKeep.Contains(kv.Key) || !kv.Value) { (gone ??= new List<int>()).Add(kv.Key); if (kv.Value) Destroy(kv.Value.gameObject, 1.5f); }
            if (gone != null) foreach (var id in gone) { views.Remove(id); skyFireAt.Remove(id); skyFired.Remove(id); }
        }

        /// <summary>載具接近→並排→敵人起跳（落點圈 1 秒）→落地（布面下沉、手機震動）→載具飛走；空賊從飛艇甲板跳下來</summary>
        void UpdateSkyBoardViews()
        {
            float now = battle.now;
            skyKeep.Clear();
            foreach (var b in battle.skyBoards)
            {
                skyKeep.Add(b.id);
                if (now < b.start - 0.05f) continue;
                if (!skyBoardViews.TryGetValue(b.id, out var bv)) { bv = new BoardView(); skyBoardViews[b.id] = bv; }
                float t = (now - b.start) / Carpet.BoardTime;
                var landU = SkyU(b.land);
                if (b.ship)
                {
                    // 甲板替身跳下來（AirshipRig 的演出，最後 1 秒有落點圈）；真正的小怪由房主在落地時生出
                    if (!bv.dropped && skyShip && now >= b.LandAt - 1f) { bv.dropped = true; skyShip.SetPirateLanding(b.side, landU); skyShip.DropPirate(b.side); }
                }
                else
                {
                    if (!bv.veh) bv.veh = SkyVehicleView.Create(b.sky);
                    bv.veh.Tick(Mathf.Clamp01(t), SkyU(b.from), landU);
                    if (t < Carpet.BoardLand)
                    {
                        if (!bv.rider && b.sky != 3) bv.rider = SkyEnemyRig.Create(b.sky);
                        if (bv.rider)
                        {
                            var ride = bv.veh.transform.position + Vector3.down * (b.sky == 1 ? -0.3f : 0.6f);
                            var pos = t < Carpet.BoardRing ? ride : Vector3.Lerp(ride, landU, (t - Carpet.BoardRing) / (Carpet.BoardLand - Carpet.BoardRing)) + Vector3.up * Mathf.Sin((t - Carpet.BoardRing) / (Carpet.BoardLand - Carpet.BoardRing) * Mathf.PI) * 0.8f;
                            var look = landU - pos; look.y = 0f;
                            bv.rider.transform.SetPositionAndRotation(pos, look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look) : bv.rider.transform.rotation);
                        }
                    }
                    else if (bv.rider) { Destroy(bv.rider.gameObject); bv.rider = null; }
                }
                if (!bv.landed && now >= b.LandAt)
                {
                    bv.landed = true;
                    float k = b.sky == 4 ? 1f : b.ship ? 0.85f : 0.6f;   // 菁英、飛艇跳下來的空賊更強
                    if (skyScene) skyScene.Impact(landU, k);
                    float d = Me != null ? Battle.FlatDistance(Me.Feet, b.land) : 9f;
                    Haptics.Play(k >= 0.85f || d < 1.5f ? Haptics.Kind.Heavy : Haptics.Kind.Medium);
                    Kick(Mathf.Lerp(10f, 4f, Mathf.Clamp01(d / 3f)) * k, 0f);
                    Sfx.Play("boom", 0.5f, 1.4f);
                }
            }
            List<int> gone = null;
            foreach (var kv in skyBoardViews)
            {
                bool done = !skyKeep.Contains(kv.Key);
                if (!done) continue;
                (gone ??= new List<int>()).Add(kv.Key);
                if (kv.Value.veh) Destroy(kv.Value.veh.gameObject);
                if (kv.Value.rider) Destroy(kv.Value.rider.gameObject);
            }
            if (gone != null) foreach (var id in gone) skyBoardViews.Remove(id);
        }

        /// <summary>飛艇：Boss 站才出現；根固定在前緣前方，俯衝只動船身；打倒後墜落 5 秒</summary>
        void UpdateAirship(float dt)
        {
            bool bossStation = battle.CarpetBoss;
            var en = battle.enemy;
            bool show = bossStation && (en.Alive || skyShipDeadAt > 0f && Time.time - skyShipDeadAt < 5.5f);
            if (!show) { if (skyShip) { Destroy(skyShip.gameObject); skyShip = null; } if (!bossStation) skyShipDeadAt = -1f; return; }
            if (!skyShip) { skyShip = AirshipRig.Create(); skyShipPhase = 1; skyShip.SetPhase(1); SpawnFade.Play(skyShip.gameObject); }
            var fwdU = WorldFrame.DirFromWorld(battle.carpetFwd); fwdU.y = 0f;
            skyShip.transform.SetPositionAndRotation(SkyU(battle.ShipRoot), Quaternion.LookRotation(fwdU.sqrMagnitude > 1e-4f ? -fwdU.normalized : Vector3.back));
            skyShip.SetSwoop(battle.carpetSwoop);
            if (battle.carpetPhase != skyShipPhase) { skyShipPhase = battle.carpetPhase; skyShip.SetPhase(skyShipPhase); }
            if (en.Alive) skyShipDeadAt = -1f;
            else
            {
                if (skyShipDeadAt < 0f) { skyShipDeadAt = Time.time; Sfx.Play("roar", 0.8f, 0.7f); Haptics.Play(Haptics.Kind.Heavy); }
                skyShip.SetDying(Mathf.Clamp01((Time.time - skyShipDeadAt) / 5f));
            }
        }

        /// <summary>小怪的外觀：天穹飛毯的敵人換成 Codex 的全新骨架（原本的骨架藏起來，動作、受擊、倒下照常驅動）</summary>
        SkyEnemyRig DressSky(Fighter m, MinionRig rig)
        {
            var v = MonsterVariants.Get(m.variant);
            if (v == null || v.sky < 0) return null;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var sky = SkyEnemyRig.Create(v.sky);
            sky.transform.SetParent(rig.transform, false);
            skyBodies[m] = sky;
            return sky;
        }
        SkyEnemyRig SkyBody(Fighter m) => skyBodies.TryGetValue(m, out var s) && s ? s : null;

        void ClearSky()
        {
            ClearSkyViews();
            skyWired = false;
            if (battle != null) battle.SetCarpetStation(-1, battle.carpetFwd);
            ItemGen.ExclusiveSet = null;
        }

        void ClearSkyViews()
        {
            if (skyScene) Destroy(skyScene.gameObject); skyScene = null; skyScenePts = -1;
            if (skyShip) Destroy(skyShip.gameObject); skyShip = null; skyShipDeadAt = -1f;
            foreach (var v in skyObViews.Values) if (v) Destroy(v.gameObject); skyObViews.Clear();
            foreach (var v in skyShotViews.Values) if (v) Destroy(v.gameObject); skyShotViews.Clear();
            foreach (var b in skyBoardViews.Values) { if (b.veh) Destroy(b.veh.gameObject); if (b.rider) Destroy(b.rider.gameObject); }
            skyBoardViews.Clear(); skyFireAt.Clear(); skyFired.Clear(); skyBodies.Clear();
        }

        // ---------------------------------------------------------------- 提示（畫面中央）
        void DrawSkyHud()
        {
            if (battle == null || SkyStationNow < 0 || phase != Phase.Fighting || Me == null || !Me.Alive) return;
            SkyCue next = null;
            foreach (var q in battle.skyCues)
                if (!q.judged && battle.now >= q.hit - q.lead && (next == null || q.hit < next.hit)) next = q;
            if (next == null) return;
            string text; Color c;
            switch (next.kind)
            {
                case Carpet.Low: text = "⬆ 跳起來！（低空橫木）"; c = new Color(1f, 0.55f, 0.15f); break;
                case Carpet.High: text = "⬇ 蹲下！（高空橫樑）"; c = new Color(0.35f, 0.7f, 1f); break;
                case Carpet.Half: text = next.side > 0 ? "⬅ 往左閃！（右半邊會被撞）" : "往右閃 ➡！（左半邊會被撞）"; c = new Color(1f, 0.35f, 0.3f); break;
                case Carpet.Gap: text = next.gap < -0.05f ? "🟩 站到左邊的缺口！" : next.gap > 0.05f ? "🟩 站到右邊的缺口！" : "🟩 站到中間的缺口！"; c = new Color(0.4f, 1f, 0.55f); break;
                case Carpet.ShotLow: text = "⬆ 跳！（橘色砲彈）"; c = new Color(1f, 0.55f, 0.15f); break;
                default: text = "⬇ 蹲！（藍色砲彈）"; c = new Color(0.35f, 0.7f, 1f); break;
            }
            int depth = GUI.depth; GUI.depth = -800;
            UiKit.Pill(Screen.height * 0.6f, text, Mathf.RoundToInt(UiKit.BaseFont * 1.5f), c, Screen.width * 0.9f, 0.6f);
            GUI.depth = depth;
        }
    }
}

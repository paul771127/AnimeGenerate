using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每日挑戰（規則、獎牌、紀錄在 DailyChallenge.cs）：
    ///   開打 → 站好校正（2 秒）→ 倒數 3 秒 → 60 秒挑戰 → 結算（分數、獎牌、今天第一次拿到的獎勵）。
    ///   閃避：看不見的「出題者」每次瞬移到你前方 4.5～6m，先開一個光門（顏色＝攻擊種類，畫面提示怎麼躲），再放出攻擊。
    ///   輸出：一般的木頭人練習加上 60 秒計時。
    ///   跑步：場地內離你遠的地方出現金幣（金色光柱，畫面外有箭頭），吃到才出下一個。
    /// </summary>
    public partial class SoloBattle
    {
        DailyChallenge.Kind? daily;           // 正在玩的每日挑戰（null＝不是）
        int dailyPick;                        // 選單上選的挑戰
        int dailyScore, dailyHearts, dailyFired, dailyHits, dailyLevel;
        float dailyLastHit = -9f, dailyLastMeHp, dailyCountdownShown = -1f;
        DailyChallenge.DodgeDirector dailyDirector;
        System.Random dailyRng;
        (int gold, int xp, int medal, bool newBest) dailyResult;

        class DailyShot { public DailyChallenge.Attack kind; public Vector3 from; public float fireAt, speed; public GameObject portal; }
        readonly List<DailyShot> dailyShots = new List<DailyShot>();
        Vector3 coinPos; GameObject coinGo, coinPillar; bool coinActive; float coinBorn;

        float DailyStart => CalibTime + DailyChallenge.Countdown;   // 站好校正 2 秒＋倒數 3 秒
        float DailyElapsed => battle != null ? battle.now - DailyStart : 0f;
        /// <summary>閃避、跑步挑戰沒有看得見的敵人（不畫鎖定框、方向提示）</summary>
        bool NoEnemy => daily == DailyChallenge.Kind.Dodge || daily == DailyChallenge.Kind.Run;

        void StartDaily(DailyChallenge.Kind kind)
        {
            SaveChoices();
            EndBattle();
            storyStage = null; dungeon = null;
            daily = kind;
            prof = Progress.Char(myClass);
            dailyLevel = prof.level;
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            prof.ApplyTo(me, Skills.Classes[myClass], myLoadout);   // 用角色養成的能力值（輸出挑戰的門檻依等級）
            dummyMode = kind == DailyChallenge.Kind.Damage;
            dummyDamage = 0f; dummyHits = 0;
            var en = new Fighter(dummyMode ? "木頭人" : DailyChallenge.Names[(int)kind], DummyClass, new List<string>(), false);
            float reach = area.ReachInside(area.Origin.forward, 0.6f);
            en.head = dummyMode ? new Vector3(0f, 1.6f, Mathf.Clamp(reach, 0f, 3f)) : new Vector3(0f, 1.5f, 40f);   // 出題者：先放在很遠的地方
            if (!dummyMode) { me.maxHp = 9999; me.hp = me.maxHp; }   // 閃避／跑步：不會倒（用心數計算）
            SyncPlayer(me);
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            ai = null; bossAi = null; bossMode = false; endless = false; coop = false;
            me.damageTakenMul = 1f;
            enemyClassUsed = "dummy";
            if (dummyMode) enemyRig = CharacterRig.Create(CharacterRig.DummyId);
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            dailyScore = 0; dailyHearts = DailyChallenge.Hearts; dailyFired = 0; dailyHits = 0; dailyLastHit = -9f; dailyLastMeHp = me.hp; dailyCountdownShown = -1f;
            dailyDirector = new DailyChallenge.DodgeDirector(DailyChallenge.Seed(kind));
            dailyRng = new System.Random(DailyChallenge.Seed(kind));
            coinActive = false;
            phase = Phase.Fighting; resultShown = false;
            Say($"{DailyChallenge.Names[(int)kind]}：站好、手機對著前方，倒數後開始", 3f);
        }

        void ClearDaily()
        {
            foreach (var s in dailyShots) if (s.portal) Destroy(s.portal);
            dailyShots.Clear();
            if (coinGo) Destroy(coinGo);
            if (coinPillar) Destroy(coinPillar);
            coinActive = false;
            daily = null;
        }

        /// <summary>每幀（戰鬥中、Battle 更新之後）；挑戰結束回傳 true</summary>
        bool UpdateDaily()
        {
            if (daily == null || battle == null) return false;
            float now = battle.now;
            // 倒數：3、2、1、開始
            if (now < DailyStart)
            {
                if (dummyMode) { dummyDamage = 0f; dummyHits = 0; }
                int sec = Mathf.CeilToInt(DailyStart - now);
                if (now >= CalibTime && sec != (int)dailyCountdownShown) { dailyCountdownShown = sec; Sfx.Play("tick", 0.9f); }
                return false;
            }
            if (dailyCountdownShown > 0f) { dailyCountdownShown = 0f; Sfx.Play("go", 1f); Haptics.Play(Haptics.Kind.Medium); }
            float t = DailyElapsed;
            switch (daily.Value)
            {
                case DailyChallenge.Kind.Dodge: UpdateDodge(now, t); break;
                case DailyChallenge.Kind.Damage: dailyScore = Mathf.RoundToInt(dummyDamage); break;
                case DailyChallenge.Kind.Run: UpdateRun(); break;
            }
            if (t >= DailyChallenge.Duration || (daily == DailyChallenge.Kind.Dodge && dailyHearts <= 0)) { FinishDaily(); return true; }
            return false;
        }

        // ---------------------------------------------------------------- 閃避
        void UpdateDodge(float now, float t)
        {
            var me = Me; var en = En;
            // 被打中：扣一顆心（0.5 秒內的連續傷害算一次）；血量補滿（不會倒）
            if (me.hp < dailyLastMeHp - 0.01f && now - dailyLastHit > 0.5f)
            {
                dailyLastHit = now; dailyHits++; dailyHearts--;
                Sfx.Play("hurt", 1f); Haptics.Play(Haptics.Kind.Heavy);
                floaters.Add(new Floater { text = dailyHearts > 0 ? $"💔 還剩 {dailyHearts} 顆心" : "💔 沒有心了！", color = new Color(1f, 0.4f, 0.45f), born = Time.time, screen = true });
            }
            me.hp = me.maxHp; dailyLastMeHp = me.hp;
            // 新的一波：先開光門（預備），時間到放出攻擊
            foreach (var shot in dailyDirector.Poll(t))
            {
                var kind = shot.kind;
                var fwd = Fighter.Flat(me.forward); if (fwd == Vector3.zero) fwd = Vector3.forward;
                var from = me.Feet + Battle.RotateY(fwd, shot.angle) * shot.dist; from.y = 0f;
                var col = AttackColor(kind);
                var portal = Prim(PrimitiveType.Sphere, new Color(col.r, col.g, col.b, 0.55f), true);
                portal.transform.position = WorldFrame.FromWorld(from + Vector3.up * (kind == DailyChallenge.Attack.Quake ? 0.15f : 1.2f));
                portal.transform.localScale = Vector3.one * 0.05f;
                dailyShots.Add(new DailyShot { kind = kind, from = from, fireAt = now + DailyChallenge.DodgeDirector.Windup(t), speed = shot.speed, portal = portal });
                Sfx.Play(kind == DailyChallenge.Attack.Sweep ? "blade_warn" : "aim_warn", 0.7f);
            }
            for (int i = dailyShots.Count - 1; i >= 0; i--)
            {
                var s = dailyShots[i];
                float left = s.fireAt - now;
                if (s.portal)
                {
                    float grow = Mathf.Clamp01(1f - left / 1.1f);
                    s.portal.transform.localScale = Vector3.one * (0.15f + 0.45f * grow) * (1f + 0.1f * Mathf.Sin(Time.time * 20f));
                }
                if (left > 0f) continue;
                // 放出：出題者瞬移到光門的位置、面向你
                en.head = new Vector3(s.from.x, 1.5f, s.from.z);
                en.forward = Fighter.Flat(me.Feet - s.from);
                var sk = Skills.All[DailyChallenge.AttackSkill[(int)s.kind]];
                if (s.kind == DailyChallenge.Attack.Quake) battle.SpawnShockwave(en, sk);
                else
                {
                    en.charging = sk; en.chargeStart = now - 100f;
                    var hand = en.head + en.forward * 0.25f;
                    int before = battle.projectiles.Count;
                    battle.TryRelease(en, (me.Chest - hand).normalized, me.Feet, out _);
                    for (int j = before; j < battle.projectiles.Count; j++) battle.projectiles[j].speedMul = s.speed;   // 每一發快慢不同
                }
                dailyFired++;
                if (s.portal) Destroy(s.portal);
                dailyShots.RemoveAt(i);
            }
            dailyScore = Mathf.Max(0, dailyFired - dailyHits);
        }

        static Color AttackColor(DailyChallenge.Attack k) => k switch
        {
            DailyChallenge.Attack.Sweep => new Color(1f, 0.6f, 0.15f),
            DailyChallenge.Attack.Quake => new Color(1f, 0.85f, 0.2f),
            DailyChallenge.Attack.Rain => new Color(1f, 0.25f, 0.2f),
            _ => new Color(0.4f, 0.8f, 1f),
        };

        // ---------------------------------------------------------------- 跑步
        void UpdateRun()
        {
            var me = Me;
            me.hp = me.maxHp;
            if (!coinActive) SpawnCoin();
            if (!coinActive) return;
            if (Battle.FlatDistance(me.Feet, coinPos) < 0.6f)
            {
                dailyScore++;
                Sfx.Play("coin", 1f); Haptics.Play(Haptics.Kind.Success);
                float took = Time.time - coinBorn;
                floaters.Add(new Floater { text = took < 3f ? $"💰 ×{dailyScore}　快！" : $"💰 ×{dailyScore}", color = new Color(1f, 0.85f, 0.3f), born = Time.time, screen = true });
                SpellFx.Impact("priest", new Color(1f, 0.85f, 0.3f), WorldFrame.FromWorld(coinPos + Vector3.up * 0.4f), 0.6f);
                coinActive = false;
                SpawnCoin();
            }
            if (coinActive && coinGo)
            {
                coinGo.transform.position = WorldFrame.FromWorld(coinPos + Vector3.up * (0.45f + 0.06f * Mathf.Sin(Time.time * 3f)));
                coinGo.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
            }
        }

        /// <summary>金幣：場地內、離你夠遠（場地最遠距離的 55% 以上、至少 1.6m）、離邊界 0.5m 以上的隨機位置（用今天的種子）</summary>
        void SpawnCoin()
        {
            var poly = area.Polygon;
            if (poly == null || poly.Count < 3) return;
            var me = Me;
            var c = Vector3.zero; foreach (var q in poly) c += q; c /= poly.Count;
            float far = 0f; foreach (var q in poly) { var w = WorldFrame.ToWorld(q); far = Mathf.Max(far, Battle.FlatDistance(w, me.Feet)); }
            float need = Mathf.Max(1.6f, far * 0.55f);
            float bestD = -1f; Vector3 best = Vector3.zero;
            for (int tries = 0; tries < 60; tries++)
            {
                float a = (float)(dailyRng.NextDouble() * Mathf.PI * 2.0), r = (float)(dailyRng.NextDouble() * 6.0);
                var s = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!area.Inside(s) || area.DistanceToEdge(s) < 0.5f) continue;
                var w = WorldFrame.ToWorld(s); w.y = 0f;
                float d = Battle.FlatDistance(w, me.Feet);
                if (d >= need) { best = w; bestD = d; break; }
                if (d > bestD) { bestD = d; best = w; }   // 場地太小：至少選最遠的
            }
            if (bestD < 0f) return;
            coinPos = best; coinActive = true; coinBorn = Time.time;
            if (!coinGo)
            {
                coinGo = Prim(PrimitiveType.Cylinder, new Color(1f, 0.82f, 0.25f), false);
                coinGo.transform.localScale = new Vector3(0.32f, 0.025f, 0.32f);
                coinPillar = Prim(PrimitiveType.Cylinder, new Color(1f, 0.85f, 0.3f, 0.22f), true);
                coinPillar.transform.localScale = new Vector3(0.12f, 1.5f, 0.12f);
            }
            coinGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            coinPillar.transform.position = WorldFrame.FromWorld(coinPos + Vector3.up * 1.5f);
            SpawnFade.Play(coinGo, 0.3f);
        }

        // ---------------------------------------------------------------- 結算
        void FinishDaily()
        {
            phase = Phase.Over; resultShown = true;
            SaveExercise();
            foreach (var s in dailyShots) if (s.portal) Destroy(s.portal);
            dailyShots.Clear();
            if (coinGo) coinGo.SetActive(false);
            if (coinPillar) coinPillar.SetActive(false);
            var k = daily.Value;
            dailyResult = DailyChallenge.Record(k, dailyScore, dailyLevel);
            if (dailyResult.gold > 0) Economy.Gold += dailyResult.gold;
            int ups = 0;
            if (dailyResult.xp > 0 && prof != null) { ups = prof.AddXp(dailyResult.xp); Progress.SaveChar(prof); }
            bool good = dailyResult.medal > 0;
            Sfx.Play(good ? "win" : "lose"); Haptics.Play(good ? Haptics.Kind.Success : Haptics.Kind.Warning);
            if (ups > 0) Say($"⬆ 升到 Lv{prof.level}！", 3f);
        }

        string DailyScoreText(DailyChallenge.Kind k, int score) => k == DailyChallenge.Kind.Dodge ? $"閃過 {score} 次" : k == DailyChallenge.Kind.Damage ? $"傷害 {score}" : $"金幣 {score} 枚";

        /// <summary>挑戰中的 HUD：蓋在上方敵人卡片的位置（計時、分數、下一個獎牌、心數），加上倒數、躲法提示、金幣方向</summary>
        void DrawDailyHud(Rect topR, float W, float H, float lh, int fs, int fsS, int fsL, float pillW)
        {
            var k = daily.Value; var col = DailyChallenge.Colors[(int)k];
            UiKit.Card(topR, 0.92f, col, 0.8f);
            float gap = W * 0.015f, ix = topR.x + gap, iw = topR.width - gap * 2, y = topR.y + gap * 0.5f;
            float t = Mathf.Clamp(DailyElapsed, 0f, DailyChallenge.Duration);
            UiKit.Text(new Rect(ix, y, iw * 0.55f, lh), $"{DailyChallenge.Names[(int)k]}　{DailyScoreText(k, dailyScore)}", fs, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Bar(new Rect(ix + iw * 0.56f, y + lh * 0.15f, iw * 0.44f, lh * 0.7f), 1f - t / DailyChallenge.Duration, col, $"{DailyChallenge.Duration - t:0} 秒", fsS);
            y += lh;
            var th = DailyChallenge.Thresholds(k, dailyLevel);
            int medal = DailyChallenge.MedalOf(k, dailyScore, dailyLevel);
            string next = medal < 3 ? $"下一個：{DailyChallenge.MedalNames[medal + 1]}（{th[medal]}）" : "🥇 已經拿到金牌！";
            UiKit.Text(new Rect(ix, y, iw, lh * 0.9f), $"目前：{DailyChallenge.MedalNames[medal]}　{next}", fsS, new Color(1f, 0.92f, 0.6f));
            y += lh * 0.9f;
            if (k == DailyChallenge.Kind.Dodge) UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), new string('❤', Mathf.Max(0, dailyHearts)) + new string('♡', Mathf.Max(0, DailyChallenge.Hearts - dailyHearts)) + $"　被打中 {dailyHits} 次", fsS, new Color(1f, 0.5f, 0.55f));
            else UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), $"今天最佳 {DailyScoreText(k, DailyChallenge.BestToday(k))}・歷史最佳 {DailyScoreText(k, DailyChallenge.BestEver(k))}", fsS, new Color(0.8f, 0.88f, 1f));

            // 倒數
            if (battle.now < DailyStart)
            {
                string txt = battle.now < CalibTime ? "站好別動（校正身高）" : $"{Mathf.CeilToInt(DailyStart - battle.now)}";
                UiKit.Text(new Rect(0, H * 0.35f, W, H * 0.15f), txt, battle.now < CalibTime ? fsL : fsL * 3, col, TextAnchor.MiddleCenter, true);
            }
            else if (DailyElapsed < 1f) UiKit.Text(new Rect(0, H * 0.35f, W, H * 0.15f), "開始！", fsL * 2, col, TextAnchor.MiddleCenter, true);

            // 閃避：每一個要放出的攻擊都標方向（身後的也指得到）；最快的兩種攻擊提示怎麼躲
            if (k == DailyChallenge.Kind.Dodge && dailyShots.Count > 0)
            {
                var order = new List<DailyShot>(dailyShots); order.Sort((a, b) => a.fireAt.CompareTo(b.fireAt));
                var kinds = new List<DailyChallenge.Attack>();
                foreach (var s in order) if (!kinds.Contains(s.kind)) kinds.Add(s.kind);
                for (int i = 0; i < Mathf.Min(2, kinds.Count); i++)
                    UiKit.Pill(H * (0.62f + 0.07f * i), DailyChallenge.AttackHint[(int)kinds[i]], i == 0 ? fsL : fs, AttackColor(kinds[i]), pillW);
                foreach (var s in order) DrawEdgeArrow(s.from + Vector3.up * 1.2f, "⚠", AttackColor(s.kind), W, H, lh, fsS);
            }
            // 跑步：金幣的方向與距離
            if (k == DailyChallenge.Kind.Run && coinActive)
                DrawEdgeArrow(coinPos + Vector3.up * 0.5f, $"💰 {Battle.FlatDistance(Me.Feet, coinPos):F1}m", new Color(1f, 0.85f, 0.3f), W, H, lh, fsS);
        }

        /// <summary>場地上的一點：在畫面中 → 上方標籤；不在 → 畫面邊緣的箭頭</summary>
        void DrawEdgeArrow(Vector3 at, string label, Color col, float W, float H, float lh, int fsS)
        {
            var wp = WorldFrame.FromWorld(at);
            var sp = cam.WorldToScreenPoint(wp);
            if (sp.z > 0.2f && sp.x > 0 && sp.x < W && sp.y > 0 && sp.y < H)
            {
                UiKit.Text(new Rect(sp.x - W * 0.2f, H - sp.y - lh * 1.6f, W * 0.4f, lh), "▼ " + label, fsS, col, TextAnchor.LowerCenter, true);
                return;
            }
            var local = cam.transform.InverseTransformPoint(wp);
            var d2 = new Vector2(local.x, local.y);
            if (local.z < 0f && d2.sqrMagnitude < 0.01f) d2 = new Vector2(0f, -1f);
            if (d2.sqrMagnitude < 1e-6f) d2 = Vector2.right;
            d2.Normalize();
            float margin = W * 0.12f;
            float kx = (W / 2f - margin) / Mathf.Max(1e-4f, Mathf.Abs(d2.x)), ky = (H / 2f - margin * 1.6f) / Mathf.Max(1e-4f, Mathf.Abs(d2.y));
            var edge = new Vector2(W / 2f, H / 2f) + new Vector2(d2.x, -d2.y) * Mathf.Min(kx, ky);
            float ang = Mathf.Atan2(-d2.y, d2.x) * Mathf.Rad2Deg;
            string arrow = ang > -22.5f && ang <= 22.5f ? "➡" : ang > 22.5f && ang <= 67.5f ? "↘" : ang > 67.5f && ang <= 112.5f ? "⬇" : ang > 112.5f && ang <= 157.5f ? "↙"
                : ang > 157.5f || ang <= -157.5f ? "⬅" : ang > -157.5f && ang <= -112.5f ? "↖" : ang > -112.5f && ang <= -67.5f ? "⬆" : "↗";
            var r = new Rect(edge.x - W * 0.17f, edge.y - lh * 0.6f, W * 0.34f, lh * 1.2f);
            UiKit.Card(r, 0.6f * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f)), col, 0.9f);
            UiKit.Text(r, $"{arrow} {label}{(local.z < 0f ? "（身後）" : "")}", fsS, col, TextAnchor.MiddleCenter, true);
        }

        void DrawDailyResult(float W, float pad, float top, float botY, float gap, float lh, int fs, int fsS)
        {
            var k = daily.Value; var col = DailyChallenge.Colors[(int)k];
            var orr = new Rect(pad, top + (botY - top) * .2f, W - pad * 2, (botY - top) * .44f);
            UiKit.Result(orr, dailyResult.medal > 0);
            float y = orr.y + gap;
            UiKit.Text(new Rect(orr.x, y, orr.width, lh * 1.5f), $"{DailyChallenge.Names[(int)k]}　{DailyChallenge.MedalNames[dailyResult.medal]}", Mathf.RoundToInt(fs * 1.4f), col, TextAnchor.MiddleCenter, true);
            y += lh * 1.6f;
            UiKit.Text(new Rect(orr.x, y, orr.width, lh), DailyScoreText(k, dailyScore) + (dailyResult.newBest ? "　🎉 新紀錄！" : "") + (k == DailyChallenge.Kind.Dodge ? $"（被打中 {dailyHits} 次）" : ""), fs, Color.white, TextAnchor.MiddleCenter, true);
            y += lh * 1.1f;
            var th = DailyChallenge.Thresholds(k, dailyLevel);
            UiKit.Text(new Rect(orr.x, y, orr.width, lh * 0.9f), $"🥉 {th[0]}　🥈 {th[1]}　🥇 {th[2]}", fsS, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter);
            y += lh;
            string reward = dailyResult.gold > 0 || dailyResult.xp > 0 ? $"今日獎勵：💰 {dailyResult.gold}　經驗 {dailyResult.xp}"
                : DailyChallenge.MedalToday(k) > 0 ? "今天的獎勵已經領過了（拿到更高的獎牌會補發）" : "拿到銅牌以上就有獎勵";
            UiKit.Text(new Rect(orr.x, y, orr.width, lh * 0.9f), reward, fsS, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleCenter);
            float bw = (orr.width - gap * 3) / 2f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
            if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), "再挑戰", fs, true, col)) StartDaily(k);
            if (UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "回選單", fs)) ShowSetup();
        }

        /// <summary>選單：三個挑戰的按鈕＋說明＋今天的紀錄</summary>
        void DrawDailyPicker(ref float y, float pad, float gap, float u, int fs, int fsS, float W)
        {
            UiKit.Text(new Rect(pad, y, W - pad * 2, u * 0.9f), $"📅 今天的每日挑戰（{System.DateTime.Now:M/d}）：每天題目不同，每個挑戰每天領一次獎勵", fsS, new Color(1f, 0.9f, 0.6f));
            y += u;
            float bw = (W - pad * 2 - gap * 2) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var kk = (DailyChallenge.Kind)i;
                int m = DailyChallenge.MedalToday(kk);
                if (UiKit.Button(new Rect(pad + i * (bw + gap), y, bw, u * 1.6f), $"{DailyChallenge.Names[i]}\n{(m > 0 ? DailyChallenge.MedalNames[m] : "今天還沒拿獎牌")}", fsS, dailyPick == i, DailyChallenge.Colors[i]))
                { dailyPick = i; PlayerPrefs.SetInt("sd_daily_pick", i); }
            }
            y += u * 1.6f + gap;
            var pk = (DailyChallenge.Kind)dailyPick;
            string info = DailyChallenge.Descs[dailyPick] + $"\n今天最佳：{DailyScoreText(pk, DailyChallenge.BestToday(pk))}・歷史最佳：{DailyScoreText(pk, DailyChallenge.BestEver(pk))}";
            float h = UiKit.TextHeight(info, W - pad * 2 - gap * 2, fsS) + gap;
            UiKit.Card(new Rect(pad, y, W - pad * 2, h + gap), 0.5f, DailyChallenge.Colors[dailyPick], 0.4f);
            UiKit.Text(new Rect(pad + gap, y + gap * 0.5f, W - pad * 2 - gap * 2, h), info, fsS, new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft);
            y += h + gap * 2;
        }
    }
}

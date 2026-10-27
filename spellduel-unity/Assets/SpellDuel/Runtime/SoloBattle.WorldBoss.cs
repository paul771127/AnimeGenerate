using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 世界 Boss 討伐（大地圖 →「🐲 世界 Boss」→ 三隻選一隻；單人）：
    ///   外觀用 Codex 做的 WorldBossArt（BossRig.CreateWorld），AI 用 WorldBossAI（三個固定階段、固定出招順序）。
    ///   血量＝一般討伐 Boss 的 8 倍（照難度、等級加成）；打贏保底一件史詩以上的裝備＋試煉幣 10 枚（一般討伐的獎勵也有）。
    /// </summary>
    public partial class SoloBattle
    {
        public const float WorldBossHpMul = 8f, WorldBossRadius = 2.2f;
        public const int WorldBossCoins = 10;
        string worldBossId;
        WorldBossAI worldAi;
        WorldBossArt worldArt;
        GameObject worldWarn; string worldChargingId;

        public static string WorldBossName(string id) { int i = System.Array.IndexOf(WorldBossArt.Ids, id); return i >= 0 ? WorldBossArt.Names[i] : id; }
        static string WorldMoveOf(string skillId) => skillId switch { "w_sweep" => "sweep", "w_quake" => "quake", "w_lance" => "lance", "w_scatter" => "scatter", "w_transition" => "transition", _ => null };

        void StartWorldBoss(string id)
        {
            if (System.Array.IndexOf(WorldBossArt.Ids, id) < 0) return;
            if (GateArea(() => StartWorldBoss(id))) return;   // 先重新放置場地
            SaveChoices();
            EndBattle();
            bossMode = true; coop = false; endless = false; dummyMode = false; enemyClassUsed = "boss"; worldBossId = id;
            storyStage = null; dungeon = null; pendingStage = null; pendingDungeon = null; ClearTicker();
            dummyDamage = 0f; dummyHits = 0; bossHideAt = -1f; bossAliveShown = false;
            ResetLoot(); potionReadyAt = 0f;
            EnsureLoadout();
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            prof = null; charOpen = false;
            ApplyProfile(me);
            var bc = Skills.Classes["boss_" + id];
            var boss = new Fighter(bc.name, bc, new List<string>(bc.defaultLoadout), false) { bodyRadius = WorldBossRadius };
            int diff = difficulty;
            boss.maxHp = Mathf.RoundToInt(BossHpSolo * BossHpFor(diff) * Battle.BossHpCut * WorldBossHpMul); boss.hp = boss.maxHp;
            me.damageTakenMul = DamageTakenFor(diff);
            SyncPlayer(me);
            battle = new Battle(me, boss);
            boss.accuracy = BossAccuracyBonus + 0.05f * 4 + (difficulty >= 2 ? 0.60f : difficulty == 1 ? 0.35f : 0f);   // 世界 Boss：照第五章的敵人命中率＋Boss 的 30%
            battle.levelBoost = Mathf.Max(0, Progress.Char(myClass).level - 1);   // 依角色等級變強（和一般討伐一樣）
            boss.maxHp = Mathf.RoundToInt(boss.maxHp * battle.BoostHpMul); boss.hp = boss.maxHp;
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            battle.OnLane += ShowLane;
            battle_MeForSpawn = me;
            // 第 1 階段站樁：直接站在場地中間（你站在中間就往你對面退一點）
            var at = Vector3.zero;
            if (Battle.FlatDistance(at, me.Feet) < 3f) { var away = Fighter.Flat(-me.Feet); if (away == Vector3.zero) away = Fighter.Flat(me.forward); at = me.Feet + away * 3f; }
            worldArt = BossRig.CreateWorld(id);
            boss.head = new Vector3(at.x, worldArt.Height * 0.75f, at.z);
            boss.forward = Fighter.Flat(me.head - boss.head);
            bossSeen = true;
            worldAi = new WorldBossAI(battle, boss, p => area.Inside(WorldFrame.FromWorld(p)), p => area.DistanceToEdge(WorldFrame.FromWorld(p)));
            worldAi.OnPhase += OnWorldPhase;
            ai = null; bossAi = null; bossRig = null;
            worldArt.transform.SetPositionAndRotation(WorldFrame.FromWorld(boss.Feet), Quaternion.LookRotation(WorldFrame.DirFromWorld(boss.forward), Vector3.up));
            SpawnFade.Play(worldArt.gameObject);
            worldChargingId = null;
            lastEnemyFeet = boss.Feet; lastEnemyHp = boss.hp; lastBossHp = boss.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            enemyShield.SetActive(false);
            phase = Phase.Fighting;
            Sfx.Play("roar"); Haptics.Play(Haptics.Kind.Heavy);
            Say($"🐲 世界 Boss：{bc.name}（HP {boss.maxHp}）\n第 1 階段站樁：衝刺（側步）→ 橫掃（蹲）→ 衝刺 → 震地環（跳）固定輪流，看預警再動！", 6f);
        }

        void OnWorldPhase(int p)
        {
            if (worldArt) worldArt.SetPhase(p);
            Sfx.Play("roar"); Haptics.Play(Haptics.Kind.Heavy); Kick(16f, 0.1f);
            Say(p == 2 ? "⚠ 第 2 階段・落地：先來一記全場衝擊環（跳）！之後牠會走過來，攻擊變強：橫掃（蹲）→ 震地環（跳）→ 衝刺（側步）→ 雷印（走出落點）"
                : "🔥 第 3 階段・狂暴：全場衝擊環（跳）！之後牠會不停繞著你出招：衝刺 → 橫掃 → 雷印 → 震地環 → 衝刺 → 橫掃", 7f);
        }

        void ClearWorldWarn() { if (worldWarn) Destroy(worldWarn); worldWarn = null; }

        void ClearWorldBoss()
        {
            ClearWorldWarn();
            if (worldArt) Destroy(worldArt.gameObject);
            worldArt = null; worldAi = null; worldBossId = null; worldChargingId = null;
        }

        /// <summary>每幀（UpdateVisuals）：世界 Boss 的位置、動作、預警圖、被打、倒下</summary>
        void UpdateWorldArt(Fighter en, Vector3 feetS, Vector3 fwdS, float dt)
        {
            if (!worldArt) return;
            var rot = fwdS.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(new Vector3(fwdS.x, 0f, fwdS.z).normalized, Vector3.up) : worldArt.transform.rotation;
            worldArt.transform.SetPositionAndRotation(feetS, rot);
            worldArt.MoveSpeed = Mathf.Lerp(worldArt.MoveSpeed, Fighter.Flat(en.Feet - lastEnemyFeet).magnitude / Mathf.Max(1e-3f, dt), 0.2f);
            string cid = en.charging != null ? en.charging.id : null;
            if (cid != null && cid != worldChargingId)
            {
                // 開始蓄力＝預備動作＋預警圖（時間＝預警時間）
                string move = WorldMoveOf(cid);
                if (move != null)
                {
                    worldArt.Windup(move, Mathf.Max(0.3f, en.charging.charge * en.chargeScale * (1f - battle.ChargeProgress(en))));
                    ClearWorldWarn();
                    worldWarn = worldArt.Warning(move);
                    if (cid == "w_scatter") worldWarn.transform.position = WorldFrame.FromWorld(en.aimPoint);   // 雷印：預警畫在落點
                }
                Sfx.Play(cid == "w_sweep" ? "blade_warn" : "aim_warn", 0.8f);
            }
            else if (cid == null && worldChargingId != null) { worldArt.Attack(); ClearWorldWarn(); }
            worldChargingId = cid;
            if (en.hp < lastBossHp - 0.01f) worldArt.PlayHit();
            lastBossHp = en.hp;
            if (!en.Alive && !enemyDeadShown) { worldArt.SetDead(true); ClearWorldWarn(); }
        }

        /// <summary>HUD：階段與下一招</summary>
        string WorldBossState()
        {
            if (worldAi == null) return "";
            string nx = Skills.All.TryGetValue(worldAi.Next, out var s) ? s.name : worldAi.Next;
            string how = worldAi.Next switch { "w_sweep" => "蹲", "w_quake" => "跳", "w_lance" => "側步", "w_scatter" => "走開", _ => "" };
            return $"🐲 第 {worldAi.Phase} 階段・{WorldBossAI.PhaseName(worldAi.Phase)}　下一招：{nx}（{how}）　";
        }

        /// <summary>打贏世界 Boss：保底一件史詩以上＋試煉幣</summary>
        void GrantWorldBossReward()
        {
            if (worldBossId == null) return;
            if (prof == null) prof = Progress.Char(myClass);
            var r = new System.Random();
            var it = ItemGen.Make(r, prof.level, ItemGen.RollRarity(r, 30f, 3), myClass);
            bool inBag = Progress.AddToBagOrMail(it, "世界 Boss・" + WorldBossName(worldBossId));
            TrialCoins.Coins += WorldBossCoins;
            Say($"🐲 世界 Boss 討伐成功！額外獲得 [{Item.RarityNames[it.rarity]}] {it.name}{(inBag ? "" : "（背包滿了，寄到信箱）")}＋試煉幣 {WorldBossCoins}", 8f);
        }
    }
}

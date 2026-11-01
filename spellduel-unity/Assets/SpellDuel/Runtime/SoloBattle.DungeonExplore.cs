using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本大地圖（使用者要求：副本全部改成像翠蔭迷林那樣，在一張大地圖上探索、打怪，強度照原本的副本等級）。
    /// 地圖由 DungeonMaze（ExploreDungeon.cs）照副本劇本產生；這裡把地圖上的區域接到原本副本的戰鬥流程：
    ///   ・戰鬥地點：區域裡守著的怪（探索的怪），全部打倒＝清除。強度照那個地點的波次（ApplyDungeonStrength：同等級故事關卡 ×2）。
    ///   ・特殊事件、中途首領、最後的首領：走進區域 → 用原本副本的地點流程（EnterNode＋StartRoomEvent），場地＝這塊區域；打完＝清除。
    ///   ・寶箱地點：走進去就打開（一件稀有以上＋金幣）。
    ///   ・浮動難度（最少以 2 人算）、等級差修正、通關獎勵都照原本的副本。
    /// </summary>
    public partial class SoloBattle
    {
        ExArea exNodeArea; float exNodeAt;
        List<Vector3> exSavedPoly;
        readonly Queue<ExArea> exNodeQueue = new Queue<ExArea>();

        /// <summary>這個副本要不要用大地圖：飛毯副本不改；千機迴廊之後改成機關房間；連線多人之後接</summary>
        bool DungeonExploreOk(DungeonDef d, bool coopMode) => !coopMode && !NetMode && DungeonMaze.Supports(d) && d.id != TrapGallery.Id;

        /// <summary>StartBossBattle（副本那一段）叫：把副本大地圖接上戰鬥</summary>
        void SetupDungeonExplore()
        {
            var run = exploreRun;
            exNodeArea = null; exNodeQueue.Clear(); exSavedPoly = null;
            battle.StartExplore(run);
            battle.OnExWave = ApplyDungeonMinionStrength;
            battle.themeOverride = dungeon.ThemeOf(run.map.areas[0].node);
            ExHookBattle();
            Ticker(ExLines($"📜 {run.Objective}"));
        }

        /// <summary>地圖上的怪放上戰場時：小怪倍率照那個地點的波次（不動首領的設定）</summary>
        void ApplyDungeonMinionStrength(int w)
        {
            float bh = battle.storyBossHp, bd = battle.storyBossDmg, bm = battle.bossHpMul; int bo = battle.bossBoostOverride;
            ApplyDungeonStrength(w);
            battle.storyBossHp = bh; battle.storyBossDmg = bd; battle.bossHpMul = bm; battle.bossBoostOverride = bo;
        }

        /// <summary>每幀（UpdateExplore 叫）：場景主題、走進區域、地點打完、劇情台詞</summary>
        void UpdateDungeonExplore(ExploreRun run)
        {
            if (dungeon == null || battle == null) return;
            // 場景主題：跟著最近的區域（正在打的地點優先）
            if (exNodeArea == null)
            {
                ExArea near = null; float best = float.MaxValue;
                foreach (var a in run.map.areas) { float d = Battle.FlatDistance(a.center, Me.Feet); if (d < best) { best = d; near = a; } }
                if (near != null) { int th = dungeon.ThemeOf(near.node); if (battle.themeOverride != th) battle.themeOverride = th; }
            }
            foreach (var a in run.areaStarts) exNodeQueue.Enqueue(a);
            run.areaStarts.Clear();
            while (exNodeArea == null && exNodeQueue.Count > 0) StartAreaNode(run, exNodeQueue.Dequeue());
            // 地點的戰鬥（事件、首領）打完
            if (exNodeArea != null && battle.awaitingNext && !battle.Over && Time.time >= exNodeAt + 1f)
            {
                var a = exNodeArea; exNodeArea = null;
                battle.CleanupRoomEvent(); battle.arenaPoly = exSavedPoly; battle.bossPoint = null;
                run.ClearArea(a);
            }
            foreach (var a in run.areaCleared) Ticker(a.node.clear);
            run.areaCleared.Clear();
            // 首領剩一半血
            if (exNodeArea != null && exNodeArea.IsBoss && dungeonHalfSaid != exNodeArea.index && battle.IsBossWave && En.Alive && En.maxHp > 0 && En.hp <= En.maxHp * 0.5f)
            { dungeonHalfSaid = exNodeArea.index; Ticker(exNodeArea.node.bossHalf); }
            UpdateRoomEventVisuals();
        }

        /// <summary>走進一個區域：寶箱直接打開；事件、首領照原本副本的地點流程開打（場地＝這塊區域）</summary>
        void StartAreaNode(ExploreRun run, ExArea a)
        {
            Ticker(a.node.enter);
            if (a.IsChest)
            {
                ItemGen.SetTheme = dungeon.ThemeOf(a.node);
                pendingItems.Add(ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, Mathf.Max(2, ItemGen.RollRarity(dropRng, 0f, 2)), myClass));
                pendingGold += 40 * dungeon.level;
                floaters.Add(new Floater { text = $"🧰 {a.Title}：裝備＋金幣（通關後獲得）", color = new Color(1f, 0.85f, 0.35f), born = Time.time, screen = true });
                Sfx.Play("coin", 0.9f); Haptics.Play(Haptics.Kind.Success);
                run.ClearArea(a);
                return;
            }
            bool final = run.map.arenas.Exists(x => x.n == 4 && Vector3.Distance(x.center, a.center) < 0.1f);
            exSavedPoly = battle.arenaPoly;
            battle.arenaPoly = AreaPoly(run.map, a);
            battle.themeOverride = dungeon.ThemeOf(a.node);
            var center = a.center;
            battle.bossPoint = () => center;
            battle.EnterNode(Mathf.Max(1, a.wave), final, 1.5f);
            if (a.IsEvent) battle.StartRoomEvent(a.node.kind, a.node.param);
            exNodeArea = a; exNodeAt = Time.time;
            Say(final ? $"👑 {a.Title}" : a.IsBoss ? $"💀 {a.Title}" : $"⚠ {a.Title}", 3f);
        }

        /// <summary>區域的場地（四個角，往內縮一點；事件的道具、出怪位置照這個）</summary>
        static List<Vector3> AreaPoly(ExploreMap m, ExArea a)
        {
            float half = ExploreMap.CellSize * 0.5f, inset = 0.6f;
            var lo = m.World(a.rect.xMin, a.rect.yMin); var hi = m.World(a.rect.xMax, a.rect.yMax);
            float x0 = lo.x - half + inset, z0 = lo.z - half + inset, x1 = hi.x + half - inset, z1 = hi.z + half - inset;
            return new List<Vector3> { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) };
        }
    }
}

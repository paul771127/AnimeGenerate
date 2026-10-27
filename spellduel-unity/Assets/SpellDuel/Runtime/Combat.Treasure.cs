using System;
using UnityEngine;
namespace SpellDuel
{
    // 寶箱怪（使用者要求）：偶爾在小怪波裡冒出來，在場上到處逃跑、不攻擊；
    // 生命 10，玩家每次打中只扣 1（不管多痛；毒、灼燒沒用）；TreasureLife 秒內沒打倒就逃走（沒有獎勵）。
    // 打倒：大量金幣＋一定掉一件裝備（SoloBattle.DropOne）。不算在「這一波還剩幾隻」裡，不會卡住清波。
    public partial class Battle
    {
        public const int TreasureKind = 53, TreasureHp = 10;
        public const float TreasureLife = 25f;          // 出現後幾秒逃走
        public const float TreasureWalk = 0.7f, TreasureRun = 1.3f;   // 閒晃／被追的速度（m/s）
        public const float TreasureScare = 4.5f;        // 玩家多近牠會開始逃
        /// <summary>每一波出現寶箱怪的機率（SoloBattle 依模式設定；0＝不出）</summary>
        public float treasureChance;
        int treasureRolledWave = -1; float treasureAt = -1f;

        static T[] TreasureArray<T>(T[] old, T value) { var a = new T[TreasureKind + 1]; Array.Copy(old, a, old.Length); a[TreasureKind] = value; return a; }
        public static bool IsTreasure(int kind) => kind == TreasureKind;
        /// <summary>會卡住清波的小怪數（寶箱怪不算）</summary>
        public int BlockingMinions { get { int n = 0; foreach (var m in minions) if (!m.treasure) n++; return n; } }

        void SetupTreasure(Fighter m)
        {
            m.treasure = true; m.treasureLeaveAt = now + TreasureLife;
            m.maxHp = TreasureHp; m.hp = TreasureHp; m.dmgMul = 0f; m.accuracy = 0f;
            m.attackReadyAt = float.MaxValue; m.bodyRadius = MinionRadius[TreasureKind];
            m.strafeSign = rng.NextDouble() < 0.5 ? 1 : -1;
            Emit("treasure", m.Feet, new Color(1f, 0.85f, 0.3f), "💰 寶箱怪出現了！快追！（每下只扣 1）");   // 主機、隊友端（代理生出來時）都提示
        }

        /// <summary>每一波擲一次：中了就在波次開始後 3～10 秒從場邊冒出來</summary>
        void UpdateTreasureSpawn()
        {
            if (treasureChance <= 0f || edgePoint == null || manualWaves && awaitingNext) return;
            if (treasureRolledWave != wave)
            {
                treasureRolledWave = wave;
                treasureAt = rng.NextDouble() < treasureChance ? now + 3f + 7f * (float)rng.NextDouble() : -1f;
            }
            if (treasureAt < 0f || now < treasureAt) return;
            treasureAt = -1f;
            if (minions.Exists(x => x.treasure)) return;
            SpawnMinion(edgePoint(), 0, TreasureKind);
        }

        /// <summary>逃跑：附近有玩家 → 往遠離所有玩家的方向跑（碰到場地邊就沿著邊繞）；沒人靠近 → 慢慢閒晃</summary>
        /// <summary>隊友端：主機那邊這隻寶箱怪不見了——是時間到逃走（不算擊倒）嗎？</summary>
        public bool TreasureFled(Fighter m) => m.treasure && now >= m.treasureLeaveAt - 1.5f;
        void UpdateTreasure(Fighter m, float dt)
        {
            if (now >= m.treasureLeaveAt || stageCleared || manualWaves && awaitingNext)
            {
                m.hp = 0f;   // 逃走：沒有擊倒紀錄、沒有獎勵
                Emit("treasurefled", m.Chest, new Color(1f, 0.85f, 0.3f), "寶箱怪逃走了……");
                return;
            }
            bool outside = minionInside != null && !minionInside(m.Feet);
            Vector3 away = Vector3.zero; Fighter nearest = null; float nd = float.MaxValue;
            foreach (var pl in Players())
            {
                if (!pl.Alive) continue;
                var v = m.Feet - pl.Feet; v.y = 0f; float d = v.magnitude;
                if (d < nd) { nd = d; nearest = pl; }
                if (d < TreasureScare) away += (d > 0.01f ? v / d : new Vector3(m.strafeSign, 0f, 0f)) * (TreasureScare - d);
            }
            Vector3 dir; float speed;
            if (outside && nearest != null) { dir = Fighter.Flat(nearest.Feet - m.Feet); speed = TreasureRun; }   // 剛出現：先跑進場地
            else if (away != Vector3.zero) { dir = away.normalized; speed = TreasureRun; }
            else
            {
                // 閒晃：繞著最近的玩家慢慢兜圈（保持在看得到的地方）
                dir = nearest != null ? RotateY(Fighter.Flat(nearest.Feet - m.Feet), 90f * m.strafeSign) : m.forward;
                speed = TreasureWalk;
                if (rng.NextDouble() < dt * 0.3) m.strafeSign = -m.strafeSign;
            }
            if (dir == Vector3.zero) return;
            dir = dir.normalized;
            float step = speed * dt;
            // 被逼到邊：試著往兩邊斜著跑（先試上次轉的那一邊）
            foreach (float turn in new[] { 0f, 50f * m.strafeSign, -50f * m.strafeSign, 100f * m.strafeSign, -100f * m.strafeSign, 150f * m.strafeSign, -150f * m.strafeSign })
            {
                var d2 = turn == 0f ? dir : RotateY(dir, turn);
                var next = m.Feet + d2 * step;
                if (!outside && minionInside != null && !minionInside(next)) continue;
                if (turn != 0f && Mathf.Abs(turn) <= 50f) m.strafeSign = turn > 0f ? 1 : -1;
                m.head = new Vector3(next.x, m.head.y, next.z);
                m.forward = d2;
                return;
            }
        }
    }
}

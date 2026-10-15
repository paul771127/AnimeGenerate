using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 戰鬥核心（純邏輯，不碰畫面）：玩家與敵人用完全相同的規則。
    /// 所有座標都是「場地座標」（公尺）：原點＝場地中心，地板 y = 0，Y 朝上。
    /// 因為不依賴 Unity 的原生功能，可以在編輯器外直接模擬測試。
    /// </summary>
    public class Fighter
    {
        public const float BodyRadius = 0.28f;

        public string name;
        public ClassDef cls;
        public List<SkillDef> loadout = new List<SkillDef>();
        public float hp, mp, regen;
        public int maxHp, maxMp;
        public Vector3 head;                  // 頭（玩家＝手機）的位置
        public Vector3 forward = Vector3.forward;   // 面向（水平）
        public bool isPlayer;

        public readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        public SkillDef charging; public float chargeStart, chargeDelay;
        public float blockUntil, blockReduce, shieldUntil, counterUntil, snaredUntil, blindUntil;
        public float sliceUntil;
        public float focusUntil;        // 獵人精神集中：這段時間內新的冷卻減半
        public float prepUntil, prepBonus;   // 刺殺準備：這段時間內傷害 +prepBonus（受到傷害就取消）
        public Vector3 chargeFrom;      // 開始詠唱時腳的位置（刺殺準備要站著不動）
        public float OutMul(float now) => (prepUntil > now ? 1f + prepBonus : 1f) * (1f + chargeBonus);
        public bool overcharge;         // 獵人「蓄力強化」：蓄滿後可以繼續蓄到 200%
        public float chargeBonus;       // 這一發的蓄力加成（200% 時 +150%）；放招當下設定        // 劍士斬擊：這段時間內會斬開正面 1m 內飛來的法球
        public int shieldLeft;
        // 刺客的近身技能：放招後進入「伏擊」，時限內靠近到射程內就自動出手
        public SkillDef armed; public float armedUntil;
        public bool palmVisible = true;        // 玩家：手掌有沒有出現在鏡頭前（刺客伏擊技能要看到手掌才會蓄力；電腦／對手一律 true）
        // 身體閃避：蹲下（躲橫向攻擊）、快速側跨（短暫無敵）
        public bool crouching; public float dodgeUntil;
        // 體型（Boss 比較大）、蓄力倍率（Boss 狂暴時變快）、目前攻擊的目標（合作模式 Boss 會換目標）
        public float bodyRadius = BodyRadius, chargeScale = 1f;
        public Fighter target;
        public float damageTakenMul = 1f;   // 受到的傷害倍率（單人模式的電腦難度：簡單／普通時玩家受傷較少）
        // Boss 召喚的小怪：編號、攻擊狀態（預備到 windupUntil 出手，attackReadyAt 之後才能再攻擊）
        public bool isAlly; public int pid = -1;   // 合作：其他玩家的代理、那支手機的編號（主機 0、加入的人 1～3）
        public bool isMinion; public int minionId; public float windupUntil, attackReadyAt, teleportAt;
        public Vector3 aimPoint;        // Boss 熔岩躍擊／樹根爆發的落點（蓄力時畫紅圈）
        public float dashUntil; public Vector3 dashDir; public bool dashHit;   // 荊棘衝鋒獸／跳躍魚／俯衝海鳥：衝刺中
        public float dashStart, dashFromAlt;   // 衝刺開始時間、俯衝開始的高度
        public float alt;               // 離地高度（海鳥飛在空中、水母和河豚浮著、跳躍魚跳起來）；0＝在地上
        public int minionKind;                 // 0 近戰小鬼、1 火焰射手、2 岩石衝鋒獸、3 自爆小鬼、4 岩石投擲者、5 熔岩狙擊手
        public float recoverUntil;             // 小怪出手後原地不動到這個時間（1 秒）
        public float dmgMul = 1f, speedMul = 1f;   // 無盡模式：越後面的波次越強
        // 無盡模式的玩家升級：攻擊力、冷卻、爆擊率、攻擊距離、命中時附加灼燒／中毒／冰凍的機率
        public float atkMul = 1f, cdMul = 1f, critChance, rangeMul = 1f, burnChance, poisonChance, freezeChance, evadeChance;
        // 牧師的守護：時限內受到的直接傷害按比例減少
        public float armorUntil, armorReduce;
        // 角色養成（無盡模式）：裝備減傷、側跨無敵時間加長、吸血、命中回 MP、每秒回血、低血量減傷
        public float gearReduce, dodgeBonus, lifesteal, mpOnHit, hpRegen, lastStand;
        public readonly List<Dot> dots = new List<Dot>();

        public class Dot { public float dps, until, acc; }

        public Fighter(string name, ClassDef cls, IEnumerable<string> skillIds, bool isPlayer)
        {
            this.name = name; this.cls = cls; this.isPlayer = isPlayer;
            maxHp = cls.maxHp; maxMp = cls.maxMp; regen = cls.mpRegen;
            hp = maxHp; mp = maxMp;
            foreach (var id in skillIds) if (Skills.All.TryGetValue(id, out var s)) loadout.Add(s);
        }

        public bool Alive => hp > 0;
        public Vector3 Feet => new Vector3(head.x, 0f, head.z);
        public Vector3 Chest => new Vector3(head.x, Mathf.Max(0.3f, bodyRadius > 0.5f ? head.y * 0.6f : head.y - 0.45f), head.z);   // Boss 的胸口在身高 60% 處

        /// <summary>身體判定：從頭到地板的膠囊；玩家的身體在手機後方一點</summary>
        public void BodySegment(out Vector3 top, out Vector3 bottom)
        {
            var back = isPlayer ? -Flat(forward) * 0.12f : Vector3.zero;
            top = head + back + Vector3.up * 0.1f;
            bottom = new Vector3(head.x, 0.1f, head.z) + back;
            if (alt > 0.05f) bottom = new Vector3(head.x, Mathf.Max(0.05f, alt - 0.35f), head.z);   // 飛在空中：身體只有一小段
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }
    }

    public class Projectile
    {
        public bool evaded;             // 已經被閃過（只提示一次，法術繼續飛）
        public Fighter target;          // 這一發要打誰（合作模式 Boss 會打隊友）；null＝對手
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos, dir;
        public float traveled;
        public int damage;
        public EffectKind effect; public float effectDps, effectDur;
        public bool reflected, alive = true;
        public bool crit;               // 爆擊（傷害 ×2）
        public bool lob; public Vector3 vel, landAt;   // 拋物線投擲（毒液彈）：速度向量、預定落點
        public float rangeMul = 1f;     // 飛行距離倍率（無盡模式升級）
        public int id;
        public bool remote;          // 雙人：對手的法術（只顯示，命中由對手判定）
        // 弧線飛行（風刃）：沿直線前進的「基準點」＋往側邊的偏移 sin(π×進度)
        public Vector3 basePos, curveSide;   // curveSide：側向單位向量 × 振幅（公尺，可為負）
        public float curveAmp, pathLen;
        public bool Curved => pathLen > 0.01f && Mathf.Abs(curveAmp) > 0.01f;
    }

    /// <summary>隕石落地後的火海：範圍內的敵人每秒受到灼燒傷害</summary>
    public class BurnZone
    {
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos;            // 地板上的圓心
        public float radius, until, dps, acc;
        public bool remote;            // 雙人：對手的火海（只顯示，傷害由對手判定）
        public bool alive = true;
    }

    public class Trap
    {
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos;          // 地板上的點
        public float armAt, until;
        public bool alive = true;
        public int id;
        public bool remote;          // 雙人：對手的陷阱（只顯示，觸發由對手判定）
        public bool Armed(float now) => now >= armAt;
    }

    public class Battle
    {
        public float now;
        public readonly Fighter player, enemy;
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public readonly List<Trap> traps = new List<Trap>();
        public readonly List<BurnZone> burns = new List<BurnZone>();
        public const float MeteorDropHeight = 6f, BurnRadius = 1.5f, BurnDuration = 10f, BurnDps = 6.5f;
        readonly System.Random rng = new System.Random();
        public float chargeTimeout = 7f;
        public const float AmbushWindow = 5f;   // 伏擊狀態持續秒數
        int nextId = 1;

        /// <summary>事件（給畫面做特效與文字）：種類、位置、顏色、文字</summary>
        public event Action<string, Vector3, Color, string> OnEvent;
        void Emit(string kind, Vector3 at, Color c, string text) => OnEvent?.Invoke(kind, at, c, text);

        public Battle(Fighter player, Fighter enemy) { this.player = player; this.enemy = enemy; }

        // ------------------------------------------------------------ 合作模式（兩人打 Boss）
        // allies＝其他玩家的代理（2～4 人合作；位置、血量、蹲下／閃避由網路更新；pid＝那支手機的編號）。
        // 主機（coopHost）跑 Boss：Boss 打到隊友 → OnAllyHit 送給隊友，由隊友自己扣血；隊友打 Boss → ApplyHitOnEnemy。
        // 隊友端：Boss 是 remoteEnemy（位置、血量由主機送來），Boss 與主機的法術都只顯示。
        public readonly List<Fighter> allies = new List<Fighter>();
        /// <summary>第一位隊友（舊程式相容用；多人時請用 allies）</summary>
        public Fighter ally => allies.Count > 0 ? allies[0] : null;
        public bool AnyAllyAlive { get { foreach (var a in allies) if (a.Alive) return true; return false; } }
        public Fighter AllyByPid(int pid) => allies.Find(a => a.pid == pid);
        public bool coopHost;
        public event Action<Fighter, SkillDef, int, Vector3, int> OnAllyHit;      // Boss 打到隊友：哪位隊友、技能、傷害、位置、法術 id
        public event Action<Vector3, float> OnDispel;                    // 我放了抗拒法環（中心、半徑）：要告訴另一支手機
        IEnumerable<Fighter> Players() { yield return player; foreach (var a in allies) yield return a; }
        public IEnumerable<Fighter> AllPlayers => Players();

        /// <summary>隊友（或主機）判定打到 Boss：套用到 Boss 身上（主機端）；by＝打的人（算仇恨）</summary>
        public void ApplyHitOnEnemy(SkillDef s, int damage, Vector3 at, bool isTrap, Fighter by = null)
        {
            if (isTrap) Emit("trap", at, s.color, $"{s.name}！");
            AddThreat(by, damage);
            ApplyDamageAndEffect(enemy, damage, s.effect, s.effectDps, s.effectDur, at);
        }
        public void ApplyBurnOnEnemy(float dmg, Vector3 at, Fighter by = null) { AddThreat(by, dmg); Damage(enemy, dmg, "burn", at); }

        // ------------------------------------------------------------ 仇恨值（多人合作：Boss 傾向打仇恨最高的人）
        // 對 Boss 造成的傷害 1:1、治療量（自己＋隊友，每補 1 HP）1:1 累積；每秒衰減 3%，換一隻 Boss 時清空。
        public readonly Dictionary<Fighter, float> threat = new Dictionary<Fighter, float>();
        public const float ThreatDecay = 0.03f;
        public void AddThreat(Fighter f, float amount)
        {
            if (f == null || amount <= 0f || f == enemy || f.isMinion) return;
            threat[f] = Threat(f) + amount;
        }
        public float Threat(Fighter f) => f != null && threat.TryGetValue(f, out var v) ? v : 0f;
        public void ClearThreat() => threat.Clear();
        /// <summary>嘲諷：仇恨變成「目前最高的人＋amount」，Boss 沒在蓄力就立刻轉向他</summary>
        public void Taunt(Fighter f, float amount)
        {
            if (f == null) return;
            float top = 0f; foreach (var kv in threat) if (kv.Key != f) top = Mathf.Max(top, kv.Value);
            threat[f] = Mathf.Max(Threat(f), top) + amount;
            if (enemy.Alive && enemy.charging == null) enemy.target = f;
            Emit("taunt", enemy.Chest, new Color(0.98f, 0.45f, 0.09f), $"Boss 被{f.name}嘲諷了！");
        }

        // ------------------------------------------------------------ Boss 的小怪（熔岩小鬼）
        // 單人／合作主機：小怪由這裡的 UpdateMinions 驅動（走向最近的玩家、預備 0.7 秒後爪擊）。
        // 合作隊友端：minions 是主機送來的代理（只顯示、只判定我的法術有沒有打到），打到 → OnMinionHit 送主機。
        public readonly List<Fighter> minions = new List<Fighter>();
        public Func<Vector3, bool> minionInside;           // 場地內（場地座標）
        public float minionSpeed = 1.0f;
        public const int MaxMinions = 10;
        public event Action<int, int, SkillDef> OnMinionHit;   // 隊友端：我的法術打到小怪（編號、傷害、技能）

        public static readonly string[] MinionNames = { "熔岩小鬼", "火焰射手", "岩石衝鋒獸", "自爆小鬼", "岩石投擲者", "熔岩狙擊手", "虛空術士", "毒液噴花", "荊棘衝鋒獸", "藤蔓術士", "食人花", "孢子菇", "種子槍手", "跳躍魚", "俯衝海鳥", "騎魚泡泡怪", "寄居蟹", "雷電水母", "河豚" };
        // 各種小怪的基本數值：血量、移動速度、身體半徑、頭的高度
        static readonly int[] MinionHp = { 20, 15, 60, 10, 23, 15, 18, 16, 30, 18, 22, 26, 14, 14, 12, 20, 45, 16, 18 };   // 血量（已下修 50%）
        public const float EnemySpeedScale = 0.3f;   // 小怪與 Boss 的移動速度倍率（調慢到 30%）
        public const float MinionRecover = 1f;       // 小怪出手後原地不動的秒數
        static readonly float[] MinionSpeed = { 1.0f, 0.9f, 0.6f, 1.7f, 0.7f, 0.8f, 0.6f, 0.7f, 0.9f, 0.6f, 1.0f, 0.8f, 0.8f, 1.2f, 1.5f, 0.7f, 0.5f, 0.6f, 0.7f }, MinionRadius = { 0.25f, 0.23f, 0.4f, 0.22f, 0.28f, 0.24f, 0.24f, 0.25f, 0.35f, 0.25f, 0.3f, 0.28f, 0.24f, 0.25f, 0.3f, 0.3f, 0.38f, 0.3f, 0.28f }, MinionHead = { 0.85f, 0.8f, 1.3f, 0.65f, 0.95f, 0.85f, 0.85f, 0.9f, 1.0f, 1.1f, 1.0f, 0.8f, 0.9f, 0.35f, 0.3f, 1.2f, 0.8f, 0.35f, 0.3f };
        // 遠程小怪想站的距離（最近、最遠）；0＝近戰（走到身邊）
        static readonly float[] KeepMin = { 0f, 3.5f, 0f, 0f, 4.5f, 6f, 6f, 4f, 0f, 3.5f, 0f, 0f, 4f, 0f, 0f, 6f, 0f, 0f, 3f }, KeepMax = { 0f, 6f, 0f, 0f, 7.5f, 9f, 12f, 7.5f, 0f, 5.5f, 0f, 0f, 8f, 0f, 0f, 9f, 0f, 0f, 4.5f };
        // 出手距離、預備時間（-1＝用 m_claw 的蓄力）
        /// <summary>遠距離小怪（攻擊距離 5m 以上：射手、狙擊手、藤蔓、毒液…）</summary>
        public static bool IsRangedMinion(int kind) => kind >= 0 && kind < MinionReach.Length && MinionReach[kind] >= 5f;
        static readonly float[] MinionReach = { 0.95f, 9f, 1.2f, 1.3f, 11f, 14f, 13f, 8f, 5f, 6f, 1.0f, 1.3f, 9f, 7f, 9f, 11f, 1.3f, 1.4f, 5f };
        static readonly float[] MinionWindup = { -1f, 0.8f, 1.0f, 0.5f, 1.2f, 1.6f, 1.0f, 1.0f, 0.8f, 1.0f, 0.7f, 0.8f, 0.9f, 0.6f, 0.6f, 1.2f, 0.8f, 0.6f, 1.0f };
        public const int MinionKinds = 19;
        public static bool IsSea(int kind) => kind >= 13;
        /// <summary>海裡／空中的小怪：不用走進場地就能出手、可以在海上移動（寄居蟹例外，要爬上礁石）</summary>
        static bool FreeRoam(int kind) => kind == 13 || kind == 14 || kind == 15 || kind == 17 || kind == 18;
        static float BaseAlt(int kind) => kind == 14 ? 3.5f : kind == 17 ? 0.9f : kind == 18 ? 0.5f : 0f;
        public const float BirdCruise = 3.5f;
        public const float VineStay = 3f;   // 藤蔓術士放出藤蔓後不能移動的秒數（＝藤蔓持續時間）
        public static bool IsPlant(int kind) => kind >= 7 && kind <= 12;
        public static bool IsRanged(int kind) => kind == 1 || kind == 4 || kind == 5 || kind == 6 || kind == 7 || kind == 9 || kind == 12 || kind == 15 || kind == 18;
        public const float VoidStay = 3f;   // 虛空術士出手後停留幾秒再瞬移

        public Fighter SpawnMinion(Vector3 feet, int id = 0, int kind = 0, float hpMul = 1f, float dmgMul = 1f, float speedMul = 1f)
        {
            kind = Mathf.Clamp(kind, 0, MinionKinds - 1);
            var c = Skills.Classes["minion"];
            var m = new Fighter(MinionNames[kind], c, new string[0], false)
            {
                isMinion = true, minionKind = kind, bodyRadius = MinionRadius[kind], minionId = id > 0 ? id : nextId++,
                dmgMul = dmgMul, speedMul = speedMul,
            };
            m.maxHp = Mathf.RoundToInt(MinionHp[kind] * hpMul); m.hp = m.maxHp;
            m.alt = kind == 14 ? 5f : BaseAlt(kind);   // 海鳥從高空飛來
            m.head = new Vector3(feet.x, MinionHead[kind] + m.alt, feet.z);
            if (kind == 16) m.damageTakenMul = 0.7f;   // 寄居蟹：殼很硬
            m.attackReadyAt = now + 1.2f;
            minions.Add(m);
            Emit("minionspawn", m.Feet, c.color, null);
            return m;
        }

        // ------------------------------------------------------------ 無盡模式
        // 一波＝一批小怪（從離玩家 15m 以上的地方陸續出現），全部打倒才進下一波；
        // 每第 10、20、30… 波是 Boss 波（只有 Boss，沒有小怪）。波次越後面敵人越強，也會解鎖新的小怪種類。
        public bool endless;
        public int wave, kills, bossKills, waveToSpawn, waveTotal;
        public float nextMinionAt, waveStartAt, bossBaseHp = 150f;
        public const int BossEvery = 10;
        public Func<Vector3> edgePoint;          // 小怪出生點：離所有玩家 15m 以上（可以在場地外，再走進來）
        public Func<Vector3> bossPoint;          // Boss 出生點：場地邊緣附近（沒設定就用 edgePoint）
        public const float MinionSpawnDistance = 15f;
        public event Action<int> OnBossSpawn;    // 無盡模式 Boss 出現（第幾隻 Boss，0 起算）

        public bool IsBossWave => wave > 0 && wave % BossEvery == 0;
        /// <summary>下一個 Boss 波</summary>
        public int NextBossWave => (wave / BossEvery + (IsBossWave ? 0 : 1)) * BossEvery;
        /// <summary>這一波還剩幾隻（還沒出現的＋場上活著的）</summary>
        public int WaveRemaining => IsBossWave ? (enemy.Alive ? 1 : 0) : waveToSpawn + minions.Count;

        // 故事模式的關卡：打第 firstWave～lastWave 波（同一套無盡模式的波次、主題、Boss），打完就通關
        public int stageEnd = -1, stageStart = 1;
        string WaveLabel => stageEnd > 0 ? $"第 {wave - stageStart + 1}/{stageEnd - stageStart + 1} 波" : $"第 {wave} 波";
        public bool stageCleared;
        /// <summary>故事關卡的敵人數量倍率（初級地區＝熔岩地帶 0.5；其他 1）</summary>
        public float stageEnemyMul = 1f;

        public void StartStage(int firstWave, int lastWave)
        {
            endless = true; kills = 0; bossKills = 0; stageCleared = false;
            wave = Mathf.Max(0, firstWave - 1); stageStart = firstWave; stageEnd = Mathf.Max(firstWave, lastWave);
            enemy.hp = 0f;
            NextWave(3f);
        }
        bool StageDone()
        {
            if (stageEnd <= 0 || wave < stageEnd) return false;
            stageCleared = true;
            Emit("stageclear", player.Feet, Color.white, "關卡完成！");
            return true;
        }

        public void StartEndless()
        {
            endless = true; wave = 0; kills = 0; bossKills = 0;
            enemy.hp = 0f;                        // 一開始沒有 Boss
            NextWave(3f);
        }

        /// <summary>進入下一波（delay 秒後開始出怪／Boss 出現）</summary>
        void NextWave(float delay)
        {
            wave++;
            waveStartAt = now + delay;
            if (IsBossWave) { waveToSpawn = 0; waveTotal = 1; }
            else
            {
                // 每波的小怪數：6＋波次（最多 28），合作 ×1.5
                waveTotal = Mathf.Min(28, 6 + wave);
                if (allies.Count > 0) waveTotal = Mathf.CeilToInt(waveTotal * (1f + 0.5f * allies.Count));
                waveTotal = Mathf.Max(1, Mathf.CeilToInt(waveTotal * stageEnemyMul));
                waveToSpawn = waveTotal;
            }
            nextMinionAt = waveStartAt;
            Emit("wavestart", player.Feet, Color.white, IsBossWave ? $"{WaveLabel}：Boss 來了！" : $"{WaveLabel}：{waveTotal} 隻小怪");
        }

        // 經驗值：小怪依種類與波次、Boss 依第幾隻；合作時兩人共用同一份經驗（各自選升級）
        public int xp;
        static readonly int[] MinionXp = { 10, 12, 25, 12, 15, 18, 20, 18, 20, 20, 12, 16, 16, 16, 18, 20, 22, 16, 18 };
        /// <summary>升到下一級需要的累積經驗：55、140、255、400…</summary>
        public static int XpForLevel(int level) => 40 * (level - 1) + 15 * (level - 1) * (level - 1) + 0;
        public static int LevelOf(int xp) { int l = 1; while (xp >= XpForLevel(l + 1)) l++; return l; }
        /// <summary>參與人數倍率：每多一位玩家，敵人的血量與攻擊力 +30%</summary>
        public float PartyMul => 1f + 0.3f * allies.Count;
        int Tier => Mathf.Max(0, wave - 1);
        public float HpMul => (1f + 0.08f * Tier) * PartyMul;
        public float DmgMul => 0.75f + 0.05f * Tier;
        public const float EndlessRegen = 0.5f;   // 無盡模式：玩家每秒自然回血
        public float SpeedMul => Mathf.Min(1.5f, 1f + 0.02f * Tier);

        float Time_dt;
        void UpdateEndless()
        {
            if (endless && player.Alive) player.hp = Mathf.Min(player.maxHp, player.hp + EndlessRegen * Time_dt);
            if (!endless || remoteEnemy || edgePoint == null) return;
            if (stageCleared) return;   // 故事關卡已經通關
            if (!player.Alive && !AnyAllyAlive) return;
            if (now < waveStartAt) return;
            if (IsBossWave)
            {
                if (!enemy.Alive && !bossSpawnedThisWave) SpawnBoss();
                return;
            }
            // 小怪波：照間隔陸續出現（每次兩隻）；場上同時最多 6＋波次/3×2 隻（合作 +4）
            int maxAlive = Mathf.Max(2, Mathf.CeilToInt((Mathf.Min(16, 6 + wave / 3 * 2) + 4 * allies.Count) * stageEnemyMul));
            if (waveToSpawn > 0 && now >= nextMinionAt && minions.Count < maxAlive)
            {
                for (int k = 0; k < 2 && waveToSpawn > 0 && minions.Count < maxAlive; k++)
                {
                    SpawnMinion(edgePoint(), 0, PickMinionKind(), HpMul, DmgMul, SpeedMul);
                    waveToSpawn--;
                }
                nextMinionAt = now + Mathf.Max(2.5f, 5f - 0.1f * wave) * (allies.Count > 0 ? Mathf.Max(0.5f, 1f - 0.25f * allies.Count) : 1f);
            }
            // 這一波全部打倒：回 10% 血，休息 4 秒進下一波
            if (waveToSpawn == 0 && minions.Count == 0)
            {
                if (player.Alive) player.hp = Mathf.Min(player.maxHp, player.hp + player.maxHp * 0.1f);
                Emit("wavecleared", player.Feet, Color.white, $"{WaveLabel}清空！");
                if (!StageDone()) NextWave(4f);
            }
        }
        bool bossSpawnedThisWave;

        // 小怪解鎖的波次：熔岩小鬼、火焰射手（遠程）第 1 波起；岩石投擲者（遠程）第 4 波；岩石衝鋒獸第 6 波；
        // 熔岩狙擊手（遠程）第 8 波；自爆小鬼第 11 波（打倒第一隻 Boss 後）
        static readonly int[] UnlockWave = { 1, 1, 6, 11, 4, 8, 5 };
        public const float RangedShare = 0.2f;   // 遠程小怪約佔兩成
        /// <summary>場景主題：第 1～10 波熔岩火山（0）、11～20 波森林（1）、21～30 波海上礁石（2），之後輪流</summary>
        public static int ThemeOf(int wave) => ((Mathf.Max(1, wave) - 1) / 10) % 3;
        public const int ThemeCount = 3;
        public static string BossClassOf(int theme) => theme == 1 ? "boss_forest" : theme == 2 ? "boss_sea" : "boss";
        public int Theme => endless ? ThemeOf(wave) : 0;
        // 森林小怪（7～12）在第 11～20 波的解鎖：這個主題的第幾波起（第二輪起全部解鎖）
        static readonly int[] ThemeUnlock = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 3, 1, 5, 7, 1, 2, 3, 1, 5, 7 };   // 森林（7～12）、海上（13～18）

        int PickMinionKind()
        {
            // 先決定近戰或遠程（遠程約兩成），再在這個主題已解鎖的種類裡依權重挑
            bool ranged = rng.NextDouble() < RangedShare;
            int theme = Theme;
            int local = wave - (wave - 1) / 10 * 10;       // 這個主題的第幾波（1～10）
            bool firstCycle = wave <= 10 * ThemeCount;
            float[] w = { 3f, 2f, 1.2f + 0.08f * wave, 1.2f + 0.08f * wave, 1.5f, 1.0f + 0.05f * wave, 1.2f, 1.5f, 1.3f, 1.2f, 3f, 1.5f, 1.2f, 2.5f, 1.5f, 1.2f, 2f, 1.3f, 1.2f };
            bool Ok(int k) => IsRanged(k) == ranged && (theme == 0 ? k < 7 && wave >= UnlockWave[k]
                : (theme == 1 ? IsPlant(k) : IsSea(k)) && (!firstCycle || local >= ThemeUnlock[k]));
            float sum = 0f;
            for (int k = 0; k < w.Length; k++) if (Ok(k)) sum += w[k];
            float r = (float)rng.NextDouble() * sum;
            for (int k = 0; k < w.Length; k++)
            {
                if (!Ok(k)) continue;
                r -= w[k]; if (r <= 0f) return k;
            }
            return theme == 1 ? (ranged ? 7 : 10) : theme == 2 ? (ranged ? 15 : 13) : (ranged ? 1 : 0);
        }

        void SpawnBoss()
        {
            bossSpawnedThisWave = true;
            int bossIndex = wave / BossEvery - 1;
            var at = (bossPoint ?? edgePoint)();
            enemy.maxHp = Mathf.RoundToInt(bossBaseHp * (1f + 0.5f * bossIndex) * PartyMul);
            enemy.hp = enemy.maxHp;
            enemy.head = new Vector3(at.x, enemy.head.y > 0.5f ? enemy.head.y : 3.2f, at.z);
            enemy.charging = null; enemy.cooldownUntil.Clear(); enemy.dots.Clear(); enemy.snaredUntil = 0f; enemy.blindUntil = 0f;
            // 依主題換 Boss：熔岩巨像／腐化樹王（招式不同）
            var bc = Skills.Classes[BossClassOf(Theme)];
            if (enemy.cls != bc)
            {
                enemy.cls = bc; enemy.name = bc.name;
                enemy.loadout.Clear();
                foreach (var id in bc.defaultLoadout) enemy.loadout.Add(Skills.All[id]);
            }
            Emit("bossarrive", enemy.Feet, enemy.cls.color, $"第 {wave} 波 Boss 出現！");
            OnBossSpawn?.Invoke(bossIndex);
        }

        void OnBossDown()
        {
            bossKills++;
            xp += 150 * bossKills;
            bossSpawnedThisWave = false;
            // 獎勵：打倒 Boss 回復 30% 血量（隊友端看到 Boss 數增加時自己回）
            if (player.Alive) player.hp = Mathf.Min(player.maxHp, player.hp + player.maxHp * 0.3f);
            Emit("bossdown", enemy.Feet, enemy.cls.color, $"打倒第 {bossKills} 隻 Boss！之後的敵人更強、種類更多");
            if (!StageDone()) NextWave(5f);
        }
        public Fighter MinionById(int id) => minions.Find(m => m.minionId == id);
        /// <summary>主機：隊友打到小怪</summary>
        public void ApplyHitOnMinion(int id, int damage, SkillDef s)
        {
            var m = MinionById(id);
            if (m != null && m.Alive) ApplyDamageAndEffect(m, damage, s != null ? s.effect : EffectKind.None, s != null ? s.effectDps : 0f, s != null ? s.effectDur : 0f, m.Chest);
        }

        void UpdateMinions(float dt)
        {
            if (!enemy.Alive && !endless) { foreach (var m in minions) if (m.Alive) { m.hp = 0; Emit("miniondie", m.Feet, m.cls.color, null); } }
            minions.RemoveAll(m => !m.Alive);
            if (remoteEnemy) return;   // 隊友端：代理，不跑 AI
            var claw = Skills.All["m_claw"];
            foreach (var m in minions)
            {
                if (!m.Alive) continue;
                // 目標：最近的活著的玩家
                Fighter tgt = null; float best = float.MaxValue;
                foreach (var pl in Players()) { if (!pl.Alive) continue; float d0 = FlatDistance(m.Feet, pl.Feet); if (d0 < best) { best = d0; tgt = pl; } }
                if (tgt == null) continue;
                m.target = tgt;
                m.forward = Fighter.Flat(tgt.head - m.head);
                float d = best;
                int kind = m.minionKind;
                // 衝刺中（荊棘衝鋒獸、跳躍魚的飛撲、海鳥的俯衝）：直線往前衝，撞到玩家就停
                if (m.dashUntil > 0f)
                {
                    if (now >= m.dashUntil) EndDash(m);
                    else DashStep(m, dt);
                    continue;
                }
                // 海鳥：俯衝完飛回空中；平常在玩家上空 4～6m 繞圈
                if (kind == 14 && m.windupUntil <= 0f)
                {
                    if (m.alt < BirdCruise) { m.alt = Mathf.Min(BirdCruise, m.alt + 2f * dt); m.head.y = MinionHead[kind] + m.alt; }
                    if (m.alt > BirdCruise) { m.alt = Mathf.Max(BirdCruise, m.alt - 1f * dt); m.head.y = MinionHead[kind] + m.alt; }
                    var rad = Fighter.Flat(m.Feet - tgt.Feet); if (rad == Vector3.zero) rad = Vector3.forward;
                    var tan = new Vector3(-rad.z, 0f, rad.x);
                    float corr = Mathf.Clamp(5f - d, -1f, 1f);
                    var fly = (tan + rad * corr).normalized * 1.5f * m.speedMul * dt;
                    m.head += fly;
                    m.forward = Fighter.Flat(fly);
                    if (now >= m.recoverUntil && m.alt >= BirdCruise - 0.3f && d <= MinionReach[kind] && now >= m.attackReadyAt)
                    { m.windupUntil = now + MinionWindup[kind]; Emit("minionwindup", m.Feet, Skills.All["m_dive"].color, null); }
                    continue;
                }
                if (m.windupUntil > 0f)
                {
                    if (now < m.windupUntil) continue;   // 預備中站著不動
                    m.windupUntil = 0f;
                    MinionStrike(m, tgt, d);
                    // 出手後原地不動：一般 1 秒、虛空術士 3 秒、藤蔓術士在藤蔓消失前（3 秒）；衝鋒獸改由衝刺決定
                    m.recoverUntil = kind == 8 || kind == 13 || kind == 14 ? 0f : now + (kind == 6 ? VoidStay : kind == 9 ? VineStay : MinionRecover);
                    if (kind == 6) m.teleportAt = now + VoidStay;                   // 停留完就瞬移
                    continue;
                }
                // 虛空術士：停留結束 → 瞬移到場內隨機位置
                if (m.teleportAt > 0f && now >= m.teleportAt)
                {
                    m.teleportAt = 0f;
                    var to = RandomInsidePoint(m.Feet);
                    Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    m.head = new Vector3(to.x, m.head.y, to.z);
                    Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    continue;
                }
                if (m.snaredUntil > now || now < m.recoverUntil) continue;
                // 出手條件：近戰／衝鋒獸／自爆要貼近；射手在 9m 內
                float reach = MinionReach[kind];
                float windup = MinionWindup[kind] < 0f ? claw.charge : MinionWindup[kind];
                bool outside = minionInside != null && !minionInside(m.Feet) && !FreeRoam(kind);   // 還在場地外：先走進來（不在場外放招）；海裡／空中的不用
                if (!outside && d <= reach + tgt.bodyRadius && now >= m.attackReadyAt) { m.windupUntil = now + windup; Emit("minionwindup", m.Feet, claw.color, null); continue; }
                // 走位：射手保持 3.5～6m，其他走向目標（停在 0.8m）；小怪之間互相推開
                float speed = minionSpeed * MinionSpeed[kind] * m.speedMul * EnemySpeedScale;
                if (kind == 13 && minionInside != null && !minionInside(m.Feet)) speed *= 3f;   // 跳躍魚在海裡游得很快
                if (outside && endless) speed *= 2f;   // 無盡／故事：還在場地外的小怪移動速度 2 倍（快點進場）
                Vector3 step;
                if (IsRanged(kind) && !outside)
                {
                    // 遠程：保持在 KeepMin～KeepMax 之間（太近就後退）
                    float want = d > KeepMax[kind] ? d - KeepMax[kind] : d < KeepMin[kind] ? -(KeepMin[kind] - d) : 0f;
                    step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Clamp(want, -speed * dt, speed * dt);
                }
                else step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Min(speed * dt, Mathf.Max(0f, d - 0.8f));
                foreach (var o in minions)
                {
                    if (o == m) continue;
                    var away = m.Feet - o.Feet; away.y = 0f;
                    float od = away.magnitude;
                    if (od < 0.6f) step += (od > 1e-3f ? away / od : new Vector3(0.1f, 0f, 0f)) * (0.6f - od) * 2f * dt;
                }
                var next = m.Feet + step;
                // 場地外出生的小怪：還在場地外時也可以走（朝玩家走進場地）；進到場地後就不會再走出去
                if (minionInside == null || FreeRoam(kind) || minionInside(next) || !minionInside(m.Feet)) m.head = new Vector3(next.x, m.head.y, next.z);
            }
        }

        /// <summary>場內隨機一點（虛空術士瞬移用）：離玩家 3～10m、在場地內；找不到就留在原地</summary>
        Vector3 RandomInsidePoint(Vector3 fallback)
        {
            var pls = new List<Fighter>(); foreach (var pl in Players()) if (pl.Alive) pls.Add(pl);
            if (pls.Count == 0) return fallback;
            for (int tries = 0; tries < 30; tries++)
            {
                var c = pls[rng.Next(pls.Count)].Feet;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rng.NextDouble() * 7f;
                var p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); p.y = 0f;
                if (minionInside != null && !minionInside(p)) continue;
                bool near = false; foreach (var pl in pls) if (FlatDistance(pl.Feet, p) < 3f) near = true;
                if (!near) return p;
            }
            return fallback;
        }

        /// <summary>小怪預備完成、出手：近戰爪擊、射手丟火球、衝鋒獸重擊、自爆</summary>
        void MinionStrike(Fighter m, Fighter tgt, float d)
        {
            var claw = Skills.All["m_claw"];
            switch (m.minionKind)
            {
                case 1:
                {
                    var fb = Skills.All["m_fireball"];
                    m.attackReadyAt = now + fb.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    Spawn(m, fb, from, (tgt.Chest - from).normalized, Mathf.RoundToInt(fb.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, fb.color, null);
                    return;
                }
                case 4:
                {
                    // 岩石投擲者：石頭從目標正上方砸下（地上有紅圈，走開就閃得掉）
                    var rk = Skills.All["m_rock"];
                    m.attackReadyAt = now + rk.cooldown;
                    var at = tgt.Feet + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.6f;
                    Spawn(m, rk, at + Vector3.up * rk.dropHeight, Vector3.down, Mathf.RoundToInt(rk.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, rk.color, null);
                    return;
                }
                case 7:
                {
                    // 毒液噴花：往上拋物線把毒液彈射向玩家（落點＝玩家現在的位置）
                    var vs = Skills.All["m_venom"];
                    m.attackReadyAt = now + vs.cooldown;
                    SpawnLob(m, vs, m.head + Vector3.up * 0.2f, tgt.Feet, Mathf.RoundToInt(vs.damage * m.dmgMul), 1.1f);
                    Emit("minionattack", m.Feet, vs.color, null);
                    return;
                }
                case 8:
                {
                    // 荊棘衝鋒獸：朝玩家現在的位置直線衝刺（衝過頭一點），地上畫出衝刺路線
                    var ds = Skills.All["m_dash"];
                    m.attackReadyAt = now + ds.cooldown;
                    StartDash(m, tgt, Mathf.Min(6f, d + 1.5f), ds);
                    Emit("minionattack", m.Feet, ds.color, null);
                    return;
                }
                case 9:
                {
                    // 藤蔓術士：從自己長到玩家當下位置的藤蔓（0.6 秒長滿，趁這時候閃開），持續 3 秒，碰到就受傷
                    var vn = Skills.All["m_vine"];
                    m.attackReadyAt = now + vn.cooldown;
                    AddLane("vine", m.Feet, tgt.Feet, 0.6f, VineStay, vn.radius, vn, Mathf.RoundToInt(vn.damage * m.dmgMul), false);
                    Emit("minionattack", m.Feet, vn.color, null);
                    return;
                }
                case 13:
                {
                    // 跳躍魚：從海裡（或礁石上）朝玩家現在的位置飛撲，拋物線跳起
                    var lp = Skills.All["m_leap"];
                    m.attackReadyAt = now + lp.cooldown;
                    StartDash(m, tgt, Mathf.Min(8f, d + 1f), lp);
                    Emit("splash", m.Feet, lp.color, null);
                    return;
                }
                case 14:
                {
                    // 俯衝海鳥：從空中往玩家現在的位置俯衝（衝過頭一點），之後飛回空中
                    var dv = Skills.All["m_dive"];
                    m.attackReadyAt = now + dv.cooldown;
                    m.dashFromAlt = m.alt;
                    StartDash(m, tgt, d + 2f, dv);
                    return;
                }
                case 15:
                {
                    // 騎魚泡泡怪：一次吹出五顆大泡泡（慢、範圍大）
                    var bb = Skills.All["m_bubble"];
                    m.attackReadyAt = now + bb.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    var aim = Fighter.Flat(tgt.Chest - from); aim.y = (tgt.Chest.y - from.y) / Mathf.Max(1f, d); aim.Normalize();
                    for (int i = 0; i < bb.multi; i++)
                        Spawn(m, bb, from, RotateY(aim, (i - (bb.multi - 1) / 2f) * bb.spreadDeg), Mathf.RoundToInt(bb.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, bb.color, null);
                    return;
                }
                case 16:
                {
                    // 寄居蟹：大螯夾擊
                    var pc = Skills.All["m_pincer"];
                    m.attackReadyAt = now + pc.cooldown;
                    Emit("minionattack", m.Feet, pc.color, null);
                    if (d <= pc.rangeMax + tgt.bodyRadius)
                    {
                        int dmg = Mathf.RoundToInt(pc.damage * m.dmgMul);
                        if (tgt.dodgeUntil > now) Emit("evade", tgt.Chest, pc.color, "閃避！");
                        else if (tgt.isAlly) { Emit("allyhit", tgt.Chest, pc.color, null); OnAllyHit?.Invoke(tgt, pc, dmg, tgt.Chest, 0); }
                        else ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest);
                    }
                    return;
                }
                case 17:
                {
                    // 雷電水母：周圍放電（受傷＋麻痺）
                    var sh = Skills.All["m_shock"];
                    m.attackReadyAt = now + sh.cooldown;
                    Emit("shock", m.Feet, sh.color, null);   // 地面點（特效自己往上長成電光球）
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (!v.Alive || FlatDistance(v.Feet, m.Feet) > sh.areaRadius + v.bodyRadius) continue;
                        int dmg = Mathf.RoundToInt(sh.damage * m.dmgMul);
                        if (v.dodgeUntil > now) Emit("evade", v.Chest, sh.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, sh.color, null); OnAllyHit?.Invoke(v, sh, dmg, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, dmg, sh.effect, sh.effectDps, sh.effectDur, v.Chest);
                    }
                    return;
                }
                case 18:
                {
                    // 河豚：膨脹後往四面八方射出 8 根刺
                    var sp = Skills.All["m_spine"];
                    m.attackReadyAt = now + sp.cooldown;
                    var from = m.head;
                    var toT = Fighter.Flat(tgt.Chest - from); if (toT == Vector3.zero) toT = Vector3.forward;   // 其中一根一定朝玩家
                    for (int i = 0; i < sp.multi; i++)
                        Spawn(m, sp, from, RotateY(toT, i * 360f / sp.multi), Mathf.RoundToInt(sp.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, sp.color, null);
                    return;
                }
                case 11:
                {
                    // 孢子菇：周圍一團孢子雲（受傷＋中毒），自己不會死
                    var sp = Skills.All["m_spore"];
                    m.attackReadyAt = now + sp.cooldown;
                    Emit("sporeburst", m.Feet, sp.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (!v.Alive || FlatDistance(v.Feet, m.Feet) > sp.areaRadius + v.bodyRadius) continue;
                        int dmg = Mathf.RoundToInt(sp.damage * m.dmgMul);
                        if (v.dodgeUntil > now) Emit("evade", v.Chest, sp.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, sp.color, null); OnAllyHit?.Invoke(v, sp, dmg, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, dmg, sp.effect, sp.effectDps, sp.effectDur, v.Chest);
                    }
                    return;
                }
                case 12:
                {
                    // 種子槍手：三顆種子扇形射出
                    var sd = Skills.All["m_seed"];
                    m.attackReadyAt = now + sd.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    var aim = (tgt.Chest - from).normalized;
                    for (int i = 0; i < sd.multi; i++)
                        Spawn(m, sd, from, RotateY(aim, (i - (sd.multi - 1) / 2f) * sd.spreadDeg), Mathf.RoundToInt(sd.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, sd.color, null);
                    return;
                }
                case 6:
                {
                    // 虛空術士：超遠距離的虛空彈（射程 14m）；之後停留 3 秒再瞬移
                    var vb = Skills.All["m_void"];
                    m.attackReadyAt = now + vb.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    Spawn(m, vb, from, (tgt.Chest - from).normalized, Mathf.RoundToInt(vb.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, vb.color, null);
                    return;
                }
                case 5:
                {
                    // 熔岩狙擊手：瞄準完成後高速射擊
                    var sn = Skills.All["m_snipe"];
                    m.attackReadyAt = now + sn.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    Spawn(m, sn, from, (tgt.Chest - from).normalized, Mathf.RoundToInt(sn.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, sn.color, null);
                    return;
                }
                case 3:
                {
                    // 自爆：周圍 1.8m 內的玩家受傷（側跨閃得掉），自己消失
                    var bomb = Skills.All["m_bomb"];
                    Emit("minionexplode", m.Feet, bomb.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (!v.Alive || FlatDistance(v.Feet, m.Feet) > bomb.areaRadius + v.bodyRadius) continue;
                        int dmg = Mathf.RoundToInt(bomb.damage * m.dmgMul);
                        if (v.dodgeUntil > now) Emit("evade", v.Chest, bomb.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, bomb.color, null); OnAllyHit?.Invoke(v, bomb, dmg, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest);
                    }
                    m.hp = 0f;
                    return;
                }
                default:
                {
                    // 近戰小鬼 7、岩石衝鋒獸 16、食人花 9（再乘上波次倍率）
                    var bite = Skills.All["m_bite"];
                    bool chomp = m.minionKind == 10;
                    int dmg = Mathf.RoundToInt((m.minionKind == 2 ? 16 : chomp ? bite.damage : claw.damage) * m.dmgMul);
                    m.attackReadyAt = now + (m.minionKind == 2 ? 2.2f : chomp ? bite.cooldown : claw.cooldown);
                    Emit("minionattack", m.Feet, chomp ? bite.color : claw.color, null);
                    float reach = m.minionKind == 2 ? 1.6f : chomp ? bite.rangeMax + 0.2f : claw.rangeMax;
                    if (d <= reach + tgt.bodyRadius)
                    {
                        if (tgt.dodgeUntil > now) Emit("evade", tgt.Chest, claw.color, "閃避！");
                        else if (tgt.isAlly) { Emit("allyhit", tgt.Chest, claw.color, null); OnAllyHit?.Invoke(tgt, claw, dmg, tgt.Chest, 0); }
                        else ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest);
                    }
                    return;
                }
            }
        }

        /// <summary>法術有沒有打到小怪（玩家方的法術才會）；打到回傳 true</summary>
        bool HitMinions(Projectile p)
        {
            if (minions.Count == 0 || p.owner == enemy || p.owner.isMinion) return false;
            foreach (var m in minions)
            {
                if (!m.Alive) continue;
                m.BodySegment(out var a, out var b);
                if (DistancePointSegment(p.pos, a, b) >= p.skill.radius + m.bodyRadius) continue;
                p.alive = false;
                if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                LastProcs = RollProcs(p.owner);
                if (remoteEnemy) { Emit("hit", p.pos, p.skill.color, null); OnMinionHit?.Invoke(m.minionId, p.damage, p.skill); }
                else { ApplyDamageAndEffect(m, p.damage, p.effect, p.effectDps, p.effectDur, p.pos); ApplyProcs(m, LastProcs, p.pos); }
                OnDealt(p.owner, p.damage);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 雙人連線模式
        // enemy 是「對手的代理」：位置、血量由網路更新；對手的法術與陷阱只顯示。
        // 判定規則（攻擊方判定）：我的法術／陷阱打到對手 → OnRemoteHit 送給對手，由對手自己扣血與套用格擋、護盾、反擊；
        // 對手判定打到我 → ApplyRemoteHit。
        public bool remoteEnemy;
        public event Action<Projectile> OnSpawn;                 // 我方新增的法術（要送給對手）
        public event Action<Trap> OnTrapPlaced;                  // 我方新設的陷阱
        public event Action<SkillDef, int, Vector3, int> OnRemoteHit;   // 打到對手：技能、傷害、位置、法術或陷阱 id
        public event Action<int> OnTrapGone;                     // 我方陷阱消失（觸發或過期）
        public event Action<SkillDef, float, Vector3> OnRemoteBurn;     // 我方火海燒到對手：技能、傷害、位置

        /// <summary>顯示對手的法術（從對手送來的起點、方向；elapsed＝已經飛了幾秒）</summary>
        public Projectile AddRemoteProjectile(int id, SkillDef s, Vector3 from, Vector3 dir, float elapsed, bool reflected, float curveAmp = 0f, float pathLen = 0f, Fighter owner = null)
        {
            var p = new Projectile { owner = owner ?? enemy, skill = s, pos = from, basePos = from, dir = dir.normalized, damage = s.damage, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur, reflected = reflected, id = id, remote = true };
            if (s.lob)
            {
                // 拋物線：pathLen 帶的是初速大小
                p.lob = true; p.vel = p.dir * pathLen; p.landAt = LobLanding(from, p.vel);
                for (float t = 0f; t < elapsed; t += 0.05f) { p.vel += Vector3.down * LobGravity * 0.05f; p.pos += p.vel * 0.05f; }
                projectiles.Add(p);
                return p;
            }
            SetCurve(p, curveAmp, pathLen);
            Advance(p, s.speed * Mathf.Max(0f, elapsed));
            projectiles.Add(p);
            return p;
        }

        /// <summary>對手的火海燒到我（對手判定）</summary>
        public void ApplyRemoteBurn(float dmg, Vector3 at) { Damage(player, dmg, "burn", at); }

        static void SetCurve(Projectile p, float amp, float len)
        {
            p.curveAmp = amp; p.pathLen = len;
            var side = Vector3.Cross(Vector3.up, p.dir); side.y = 0;
            p.curveSide = side.sqrMagnitude > 1e-6f ? side.normalized * amp : Vector3.zero;
        }

        /// <summary>沿路徑前進 d 公尺（弧線：基準點走直線，再加側向偏移）</summary>
        static void Advance(Projectile p, float d)
        {
            p.basePos += p.dir * d;
            p.traveled += d;
            p.pos = p.Curved ? p.basePos + p.curveSide * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p.traveled / p.pathLen)) : p.basePos;
        }

        public void RemoveRemoteProjectile(int id)
        {
            foreach (var p in projectiles)
                if (p.remote && p.id == id && p.alive)
                {
                    p.alive = false;
                    if (p.skill.id == "meteor" && !p.reflected && p.owner == enemy) AddBurn(p, player.Feet);   // 對手的隕石砸中我：顯示火海
                }
        }

        public void AddRemoteTrap(int id, SkillDef s, Vector3 at, float armIn)
        {
            at.y = 0;
            traps.Add(new Trap { owner = enemy, skill = s, pos = at, armAt = now + armIn, until = now + s.trapLife, id = id, remote = true });
        }

        public void RemoveRemoteTrap(int id)
        {
            foreach (var t in traps) if (t.remote && t.id == id) t.alive = false;
        }

        /// <summary>對手判定打到我：反擊中就把攻擊打回去（回傳 true），否則套用格擋、護盾、傷害與效果</summary>
        public bool ApplyRemoteHit(SkillDef s, int damage, Vector3 at, bool isTrap)
        {
            if (!isTrap && player.counterUntil > now)
            {
                player.counterUntil = 0;
                var from = player.Chest + Fighter.Flat(player.forward) * 0.4f;
                var dir = (enemy.Chest - from).normalized;
                var p = Spawn(player, s, from, dir, damage, s.effect, s.effectDps, s.effectDur, true);
                OnSpawn?.Invoke(p);
                Emit("counter", at, new Color(0.96f, 0.45f, 0.71f), "反擊！打回去");
                return true;
            }
            if (isTrap) Emit("trap", at, s.color, $"{s.name}！");
            ApplyDamageAndEffect(player, damage, s.effect, s.effectDps, s.effectDur, at);
            return false;
        }

        /// <summary>對手：玩家（和隊友）打 enemy；enemy 打它目前的目標（沒有就打玩家）</summary>
        public Fighter Opponent(Fighter f) => f == enemy || f.isMinion ? (f.target != null && f.target.Alive ? f.target : player) : enemy;
        public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
        public float Distance => FlatDistance(player.head, enemy.head);
        public bool Over => (!endless && !enemy.Alive) || stageCleared || (!player.Alive && !AnyAllyAlive);

        // ------------------------------------------------------------ 詠唱
        public bool TryChant(Fighter f, SkillDef s, out string why)
        {
            why = null;
            if (Over) { why = "戰鬥結束"; return false; }
            if (!f.Alive) { why = "你已倒下，等隊友"; return false; }
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) { why = $"{s.name} 冷卻中"; return false; }
            if (f.mp < s.cost) { why = "MP 不足"; return false; }
            if (f.snaredUntil > now && s.self != SelfKind.Cleanse) { why = "被困住，暫時不能施法（牧師可以淨化）"; return false; }
            if (s.releaseNear && NearestFoe(f, out var nd) != null && nd < AmbushClear) { why = $"身邊直徑 {AmbushClear * 2f:0.#}m 內有敵人，先退開才能蓄力{s.name}"; return false; }
            f.charging = s; f.chargeStart = now; f.chargeFrom = f.Feet;
            return true;
        }

        // ------------------------------------------------------------ 普通攻擊
        /// <summary>
        /// 普通攻擊：近身（劍士、刺客）＝砍身邊 rangeMax 公尺內的所有敵人；遠程＝朝 aimDir 射出一發（走一般的投射物流程，連線也會同步）。
        /// 正在詠唱技能時不能普攻（呼叫者負責）。回傳有沒有出手。
        /// </summary>
        public bool BasicAttack(Fighter f, SkillDef s, Vector3 aimDir, Vector3 floor, bool locked)
        {
            if (s == null || f == null || !f.Alive || Over || f.charging != null) return false;
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) return false;
            if (!Skills.IsMeleeBasic(s))
            {
                f.charging = s; f.chargeStart = now - 1f;
                return TryRelease(f, aimDir, floor, out _, locked);
            }
            f.cooldownUntil[s.id] = now + s.cooldown * f.cdMul;
            Emit("basicmelee", f.Feet + Fighter.Flat(f.forward) * 0.5f, s.color, s.id);
            int dmg = Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now));
            var foes = new List<Fighter>();
            if (f == enemy || f.isMinion) { foreach (var pl in Players()) foes.Add(pl); }
            else { if (enemy != null) foes.Add(enemy); foes.AddRange(minions); }
            foreach (var v in foes)
            {
                if (v == null || !v.Alive || FlatDistance(f.Feet, v.Feet) > s.rangeMax + v.bodyRadius) continue;
                if (remoteEnemy && v == enemy) { Emit("hit", v.Chest, s.color, null); OnRemoteHit?.Invoke(s, dmg, v.Chest, 0); OnDealt(f, dmg); continue; }
                if (remoteEnemy && v.isMinion) { Emit("hit", v.Chest, s.color, null); OnMinionHit?.Invoke(v.minionId, dmg, s); OnDealt(f, dmg); continue; }
                Hit(v, new Projectile { owner = f, skill = s, pos = v.Chest, dir = Fighter.Flat(v.head - f.head), damage = dmg });
            }
            return true;
        }

        /// <summary>刺客伏擊技能（刺殺、影襲、毒刃）：身邊這個半徑內不能有敵人才能蓄力；蓄力完成後靠近到這個距離就自動出手</summary>
        public const float AmbushClear = 2f;     // 半徑 2m（直徑 4m）內有敵人：不能開始／蓄力暫停
        public const float AmbushReach = 1.5f;   // 伏擊中靠近到半徑 1.5m（直徑 3m）內：自動出手

        /// <summary>f 最近的敵人（玩家方：Boss／對手＋小怪；敵方：玩家）</summary>
        public Fighter NearestFoe(Fighter f, out float dist)
        {
            dist = float.MaxValue; Fighter best = null;
            if (f == enemy || f.isMinion)
            {
                foreach (var pl in Players()) if (pl.Alive) { float d = FlatDistance(f.head, pl.head); if (d < dist) { dist = d; best = pl; } }
                return best;
            }
            if (enemy != null && enemy.Alive) { dist = FlatDistance(f.head, enemy.head); best = enemy; }
            foreach (var m in minions) if (m.Alive) { float d = FlatDistance(f.head, m.head); if (d < dist) { dist = d; best = m; } }
            return best;
        }

        /// <summary>獵人蓄力強化：超過 100% 的部分（0～1，1＝蓄到 200%）</summary>
        public float OverchargeFrac(Fighter f)
        {
            if (f == null || !f.overcharge || f.charging == null || f.charging.charge <= 0f) return 0f;
            float t = f.charging.charge * f.chargeScale;
            return Mathf.Clamp01((now - f.chargeStart - t) / Mathf.Max(0.01f, t));
        }
        public const float OverchargeDamage = 1.5f;   // 蓄到 200% 時傷害 +150%

        public float ChargeProgress(Fighter f) => f.charging == null ? 0f : f.charging.charge <= 0f ? 1f : Mathf.Clamp01((now - f.chargeStart) / Mathf.Max(0.01f, f.charging.charge * f.chargeScale));

        /// <summary>射程狀態：0＝可以、1＝太遠、-1＝太近</summary>
        public int RangeState(Fighter f, SkillDef s)
        {
            if (s.type != SkillType.Projectile) return 0;
            float d = FlatDistance(f.head, Opponent(f).head);
            if (d > s.rangeMax * f.rangeMul) return 1;
            if (d < s.rangeMin) return -1;
            return 0;
        }

        // ------------------------------------------------------------ 出手
        /// <param name="aimDir">投射物的方向（場地座標）</param>
        /// <param name="floorPoint">陷阱的位置（地板上）；沒鎖定時隕石也落在這裡</param>
        /// <param name="locked">有沒有鎖定敵人。沒鎖定也能放：朝準星方向飛，隕石落在準星指的地面</param>
        public bool TryRelease(Fighter f, Vector3 aimDir, Vector3 floorPoint, out string why, bool locked = true)
        {
            why = null;
            var s = f.charging;
            if (s == null || Over) { why = "沒有在詠唱"; return false; }
            if (f.mp < s.cost) { f.charging = null; why = "MP 不足"; return false; }
            if (now - f.chargeStart < s.charge * f.chargeScale) { why = $"蓄力中 {Mathf.FloorToInt(ChargeProgress(f) * 100)}%"; return false; }
            // 蓄力完成就一定放得出去（不管有沒有瞄到敵人、距離多遠），打不打得中看瞄準與距離。
            // 陷阱：準星指得太遠時，放在同方向的最遠設置距離上
            if (s.type == SkillType.Trap)
            {
                var off = floorPoint - f.Feet; off.y = 0f;
                if (off.sqrMagnitude < 1e-6f) off = Fighter.Flat(f.forward) * 1.5f;
                if (off.magnitude > s.trapRange) off = off.normalized * s.trapRange;
                floorPoint = f.Feet + off; floorPoint.y = 0f;
            }

            f.chargeBonus = OverchargeDamage * OverchargeFrac(f);   // 獵人蓄力強化（下一幀清掉）
            f.mp -= s.cost;
            f.cooldownUntil[s.id] = now + s.cooldown * f.cdMul * (f.focusUntil > now ? 0.5f : 1f);
            f.charging = null;
            if (s.id == "slash") { f.sliceUntil = now + SliceWindow; SliceNow(f); }

            if (s.releaseNear)
            {
                f.armed = s; f.armedUntil = now + AmbushWindow;
                Emit("armed", f.Chest, s.color, $"{s.name} 伏擊中");
                return true;
            }

            switch (s.type)
            {
                case SkillType.Self: CastSelf(f, s); break;
                case SkillType.Trap: PlaceTrap(f, s, floorPoint); break;
                case SkillType.Projectile when s.areaRadius > 0f:
                {
                    // 範圍重擊（Boss 地裂／踐踏；樹根爆發打在目標位置）：放招當下站在半徑內的玩家都受傷；側跨的無敵時間內閃得掉
                    var ac = s.atTarget ? new Vector3(f.aimPoint.x, 0f, f.aimPoint.z) : f.Feet;
                    Emit(s.atTarget ? "roots" : "slam", ac, s.color, s.id);
                    Emit("cast", f.head, s.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (v == f || !v.Alive || FlatDistance(ac, v.Feet) > s.areaRadius + v.bodyRadius) continue;
                        if (v.dodgeUntil > now) { Emit("evade", v.Chest, s.color, "閃避！"); continue; }
                        if (v.isAlly) { Emit("allyhit", v.Chest, s.color, null); OnAllyHit?.Invoke(v, s, s.damage, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, s.damage, s.effect, s.effectDps, s.effectDur, v.Chest);
                    }
                    break;
                }
                case SkillType.Projectile when s.lob:
                {
                    // 拋物線投擲（毒孢彈）：第一顆落在目標腳下，其餘落在周圍 0.8～1.5m
                    var tgtFeet = locked ? Opponent(f).Feet : floorPoint;
                    for (int i = 0; i < Mathf.Max(1, s.multi); i++)
                    {
                        var at = tgtFeet;
                        if (i > 0) { float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)rng.NextDouble() * 0.7f; at += new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); }
                        SpawnLob(f, s, f.head + Vector3.up * 0.3f, at, Mathf.RoundToInt(s.damage * f.dmgMul), 1.1f + 0.15f * i);
                    }
                    Emit("cast", f.head, s.color, null);
                    break;
                }
                case SkillType.Projectile when s.fromAbove:
                {
                    // 隕石、審判：從敵人（施放當下的位置）正上方砸下，敵人可以趁下落時移動閃開；
                    // 沒鎖定時落在準星指的地面（最遠到射程）
                    var target = Opponent(f).Feet;
                    if (!locked)
                    {
                        var off = floorPoint - f.Feet; off.y = 0f;
                        if (off.magnitude > s.rangeMax) off = off.normalized * s.rangeMax;
                        target = f.Feet + off; target.y = 0f;
                    }
                    Spawn(f, s, target + Vector3.up * s.dropHeight, Vector3.down, s.damage, s.effect, s.effectDps, s.effectDur, false);
                    // 一次落下好幾顆（Boss 落石雨）：其餘落在目標周圍 2.5m 內，錯開一點高度（陸續落下）
                    for (int i = 1; i < s.multi; i++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)rng.NextDouble() * 1.7f;
                        var at = target + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        Spawn(f, s, at + Vector3.up * (s.dropHeight + i * 1.2f), Vector3.down, s.damage, s.effect, s.effectDps, s.effectDur, false);
                    }
                    Emit("cast", f.head, s.color, null);
                    break;
                }
                default:
                    var hand = f.head + Fighter.Flat(f.forward) * 0.25f + Vector3.down * 0.25f;
                    aimDir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Fighter.Flat(f.forward);
                    if (s.originHeight > 0f)
                    {
                        // 水平射出（Boss 橫掃）：從身體前方、固定高度出發
                        hand = new Vector3(f.head.x, s.originHeight, f.head.z) + Fighter.Flat(f.forward) * (f.bodyRadius + 0.3f);
                        aimDir = Fighter.Flat(aimDir); if (aimDir == Vector3.zero) aimDir = Fighter.Flat(f.forward);
                    }
                    if (s.id == "wind")
                    {
                        // 風刃：隨機從左或右側繞弧線飛向目標（終點仍是準星方向、敵人的距離）
                        float dist = Mathf.Max(1f, locked ? FlatDistance(f.head, Opponent(f).head) : s.rangeMax * 0.6f);
                        float amp = Mathf.Max(0.5f, dist * 0.45f) * (rng.Next(2) == 0 ? -1f : 1f);
                        var wp = Spawn(f, s, hand, aimDir, s.damage, s.effect, s.effectDps, s.effectDur, false, amp, dist);
                        Emit("cast", hand, s.color, null);
                        break;
                    }
                    for (int i = 0; i < s.multi; i++)
                    {
                        float ang = s.multi > 1 ? (i - (s.multi - 1) / 2f) * s.spreadDeg : 0f;
                        Spawn(f, s, hand, RotateY(aimDir, ang), s.damage, s.effect, s.effectDps, s.effectDur, false);
                    }
                    Emit("cast", hand, s.color, null);
                    break;
            }
            return true;
        }

        public event Action<SkillDef> OnTeamBuff;
        public event Action<SkillDef> OnTaunt;      // 合作的隊友端：我嘲諷了 → 告訴主機（主機的 Boss 才有仇恨）   // 合作：牧師的輔助技能也要作用在隊友（隊友那支手機自己套用）

        void CastSelf(Fighter f, SkillDef s)
        {
            // 牧師的輔助技能：作用在所有己方單位（自己＋合作隊友）
            if (s.cls == "priest")
            {
                // 治療的仇恨：補到的人數（自己＋活著的隊友）× 治療量
                if (s.heal > 0) { int n = 1; foreach (var a in allies) if (a.Alive) n++; AddThreat(f, s.heal * n); }
                ApplySupport(f, s, null);
                if (f == player && AnyAllyAlive) OnTeamBuff?.Invoke(s);
                return;
            }
            switch (s.self)
            {
                case SelfKind.Heal: AddThreat(f, s.heal); f.hp = Mathf.Min(f.maxHp, f.hp + s.heal); Emit("heal", f.Chest, s.color, $"+{s.heal}"); return;
                case SelfKind.Block: f.blockUntil = now + s.buffDur; f.blockReduce = s.buffReduce; break;
                case SelfKind.Shield: f.shieldUntil = now + s.buffDur; f.shieldLeft = s.buffAmount; break;
                case SelfKind.Counter: f.counterUntil = now + s.buffDur; break;
                case SelfKind.Armor: f.armorUntil = now + s.buffDur; f.armorReduce = s.buffReduce; break;
                case SelfKind.Taunt:
                    // 嘲諷：防禦提升＋把 Boss 的仇恨拉過來
                    f.armorUntil = now + s.buffDur; f.armorReduce = s.buffReduce;
                    if (remoteEnemy) OnTaunt?.Invoke(s); else Taunt(f, s.buffAmount);
                    Emit("buff", f.Chest, s.color, $"{s.name}！防禦 +{s.buffReduce * 100f:0}%");
                    return;
                case SelfKind.Mana: f.mp = Mathf.Min(f.maxMp, f.mp + s.buffAmount); Emit("buff", f.Chest, s.color, $"{s.name}：MP +{s.buffAmount}"); return;
                case SelfKind.Focus:
                {
                    // 精神集中：之後的冷卻減半，現在正在冷卻的也減半
                    f.focusUntil = now + s.buffDur;
                    var keys = new List<string>(f.cooldownUntil.Keys);
                    foreach (var k in keys) if (k != s.id && f.cooldownUntil[k] > now) f.cooldownUntil[k] = now + (f.cooldownUntil[k] - now) * 0.5f;
                    Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內冷卻減半");
                    return;
                }
                case SelfKind.Prep: f.prepUntil = now + s.buffDur; f.prepBonus = s.buffReduce; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內傷害 +{s.buffReduce * 100:0}%"); return;
                case SelfKind.Cleanse:
                    // 淨化：解除中毒、定身、致盲，回一點血
                    f.dots.Clear(); f.snaredUntil = 0f; f.blindUntil = 0f;
                    if (s.heal > 0) f.hp = Mathf.Min(f.maxHp, f.hp + s.heal);
                    Emit("heal", f.Chest, s.color, s.heal > 0 ? $"淨化 +{s.heal}" : "淨化");
                    return;
                case SelfKind.Dispel:
                {
                    // 抗拒法環：以自己為中心，打斷範圍內所有敵人的蓄力（含刺客的伏擊），被打斷的技能重新冷卻
                    Emit("dispel", f.Feet, s.color, s.name);
                    foreach (var v in new[] { Opponent(f) })
                        if (!(remoteEnemy && v == enemy)) DispelAt(v, f.Feet, s.areaRadius);
                    if (f == player) OnDispel?.Invoke(f.Feet, s.areaRadius);
                    return;
                }
                case SelfKind.Summon:
                {
                    // Boss 召喚小怪：在身邊 1.6～2.4m 冒出來（合作時多一隻），最多同時 MaxMinions 隻
                    int n = Mathf.Min(s.multi + allies.Count, MaxMinions - minions.Count);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 at = f.Feet;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.6f + (float)rng.NextDouble() * 0.8f;
                            at = f.Feet + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                            if (minionInside == null || minionInside(at)) break;
                        }
                        int sk = Theme == 1 ? (rng.Next(3) == 0 ? 7 : 10) : Theme == 2 ? (rng.Next(2) == 0 ? 13 : 16) : 0;   // 森林：食人花為主；海上：跳躍魚與寄居蟹
                        SpawnMinion(at, 0, sk, PartyMul);
                    }
                    Emit("summon", f.Feet, s.color, n > 0 ? $"召喚了 {n} 隻小怪！" : null);
                    return;
                }
            }
            Emit("buff", f.Chest, s.color, s.name);
        }

        /// <summary>牧師的輔助效果套用在 t 身上（from＝施放的隊友名字，null＝自己放的）</summary>
        void ApplySupport(Fighter t, SkillDef s, string from)
        {
            string by = from != null ? $"{from}的" : "";
            switch (s.self)
            {
                case SelfKind.Heal: t.hp = Mathf.Min(t.maxHp, t.hp + s.heal); Emit("heal", t.Chest, s.color, $"{by}{s.name} +{s.heal}"); return;
                case SelfKind.Armor: t.armorUntil = now + s.buffDur; t.armorReduce = s.buffReduce; Emit("buff", t.Chest, s.color, by + s.name); return;
                case SelfKind.Cleanse:
                    t.dots.Clear(); t.snaredUntil = 0f; t.blindUntil = 0f;
                    if (s.heal > 0) t.hp = Mathf.Min(t.maxHp, t.hp + s.heal);
                    Emit("heal", t.Chest, s.color, by + (s.heal > 0 ? $"淨化 +{s.heal}" : "淨化"));
                    return;
                case SelfKind.Prayer:
                {
                    // 祈禱：所有技能目前的冷卻時間減半
                    var keys = new List<string>(t.cooldownUntil.Keys);
                    foreach (var k in keys) if (k != s.id && t.cooldownUntil[k] > now) t.cooldownUntil[k] = now + (t.cooldownUntil[k] - now) * 0.5f;
                    t.mp = Mathf.Min(t.maxMp, t.mp + s.buffAmount);
                    Emit("buff", t.Chest, s.color, $"{by}祈禱：冷卻減半・MP +{s.buffAmount}");
                    return;
                }
                default: Emit("buff", t.Chest, s.color, by + s.name); return;
            }
        }

        /// <summary>合作：隊友（牧師）的輔助技能作用在我身上</summary>
        public void ApplyTeamBuff(SkillDef s, string from = "隊友")
        {
            if (!player.Alive || Over) return;
            ApplySupport(player, s, from);
        }

        void PlaceTrap(Fighter f, SkillDef s, Vector3 at)
        {
            at.y = 0;
            // 同一種陷阱最多 trapMax 個，超過就移除最舊的
            int count = 0;
            for (int i = traps.Count - 1; i >= 0; i--)
                if (traps[i].alive && traps[i].owner == f && traps[i].skill == s && ++count >= s.trapMax) traps[i].alive = false;
            var trap = new Trap { owner = f, skill = s, pos = at, armAt = now + s.trapArm, until = now + s.trapLife, id = nextId++ };
            traps.Add(trap);
            Emit("trapSet", at, s.color, $"{s.name} 設置");
            if (f == player) OnTrapPlaced?.Invoke(trap);
        }

        Projectile Spawn(Fighter owner, SkillDef s, Vector3 from, Vector3 dir, int dmg, EffectKind eff, float dps, float dur, bool reflected, float curveAmp = 0f, float pathLen = 0f)
        {
            var p = new Projectile { owner = owner, skill = s, pos = from, basePos = from, dir = dir.normalized, damage = dmg, effect = eff, effectDps = dps, effectDur = dur, reflected = reflected, id = nextId++ };
            float outMul = reflected ? 1f : owner.OutMul(now);
            if (owner.atkMul != 1f || owner.critChance > 0f || owner.rangeMul != 1f || outMul != 1f)
            {
                p.damage = Mathf.RoundToInt(dmg * owner.atkMul * outMul);
                if (owner.critChance > 0f && rng.NextDouble() < owner.critChance) { p.crit = true; p.damage *= 2; }
                p.rangeMul = owner.rangeMul;
            }
            if ((owner == enemy || owner.isMinion) && owner.target != null) p.target = owner.target;
            SetCurve(p, curveAmp, pathLen);
            projectiles.Add(p);
            // 要給另一支手機顯示的法術：雙人對戰的我方法術；合作模式的我方法術、主機上 Boss 的法術
            if ((owner == player && (remoteEnemy || allies.Count > 0)) || ((owner == enemy || owner.isMinion) && coopHost)) OnSpawn?.Invoke(p);
            return p;
        }

        // ------------------------------------------------------------ 每幀
        public void Update(float dt)
        {
            now += dt;
            if (threat.Count > 0) { var ks = new List<Fighter>(threat.Keys); foreach (var k in ks) threat[k] *= 1f - ThreatDecay * dt; }
            foreach (var f in new[] { player, enemy })
            {
                if (remoteEnemy && f == enemy) continue;   // 對手的 MP、詠唱、持續傷害由對手自己算
                f.chargeBonus = 0f;
                UpdateAmbush(f);
                f.mp = Mathf.Min(f.maxMp, f.mp + f.regen * dt);
                if (f.hpRegen > 0f && f.Alive) f.hp = Mathf.Min(f.maxHp, f.hp + f.hpRegen * dt);
                if (f.charging != null && now - f.chargeStart > f.charging.charge + chargeTimeout + (f.charging.releaseNear ? 8f : 0f))
                { f.charging = null; Emit("timeout", f.Chest, Color.gray, "詠唱逾時"); }
                // 刺客伏擊技能：手掌不在鏡頭前、或身邊 2m 內有敵人 → 蓄力暫停；蓄滿自動進入伏擊（不用放招動作）
                if (f.charging != null && f.charging.releaseNear)
                {
                    if (ChargeProgress(f) < 1f)
                    {
                        if (!f.palmVisible || (NearestFoe(f, out var nd2) != null && nd2 < AmbushClear)) f.chargeStart += dt;
                    }
                    else TryRelease(f, Fighter.Flat(f.forward), f.Feet, out _);
                }
                // 刺殺準備：蓄力完成前離開原地太遠 → 失敗
                // 獵人：整個蓄力期間（包含蓄滿後拉著弓）都不能離開原地直徑 1m
                if (f.charging != null && f.charging.stillRadius > 0f && (ChargeProgress(f) < 1f || f.charging.cls == "archer") && FlatDistance(f.Feet, f.chargeFrom) > f.charging.stillRadius)
                { var cs = f.charging; f.charging = null; Emit("fail", f.Chest, Color.gray, $"移動太遠，{cs.name}失敗"); }
                // 持續傷害：每滿 1 秒扣一次
                for (int i = f.dots.Count - 1; i >= 0; i--)
                {
                    var d = f.dots[i];
                    d.acc += dt;
                    while (d.acc >= 1f && f.Alive) { d.acc -= 1f; Damage(f, d.dps, "dot"); }
                    if (d.until <= now) f.dots.RemoveAt(i);
                }
            }
            UpdateProjectiles(dt);
            // 小怪身上的持續傷害（灼燒、中毒）
            if (!remoteEnemy)
                foreach (var m in minions)
                    for (int i = m.dots.Count - 1; i >= 0; i--)
                    {
                        var d = m.dots[i];
                        d.acc += dt;
                        while (d.acc >= 1f && m.Alive) { d.acc -= 1f; Damage(m, d.dps, "dot", m.Chest); }
                        if (d.until <= now) m.dots.RemoveAt(i);
                    }
            UpdateMinions(dt);
            Time_dt = dt;
            UpdateEndless();
            UpdateTraps();
            UpdateBurns(dt);
            UpdatePools();
            UpdateLanes();
        }

        // ------------------------------------------------------------ 森林：拋物線毒液彈、毒液區、藤蔓／衝刺路線
        public const float LobGravity = 7f, PoolLife = 5f, PoolRadius = 0.5f;

        /// <summary>拋物線投擲：T 秒後落在 to（地面）</summary>
        void SpawnLob(Fighter owner, SkillDef s, Vector3 from, Vector3 to, int dmg, float T)
        {
            to.y = 0f;
            var flat = Fighter.Flat(to - from);
            var v = new Vector3((to.x - from.x) / T, (to.y - from.y + 0.5f * LobGravity * T * T) / T, (to.z - from.z) / T);
            var p = new Projectile { owner = owner, skill = s, pos = from, basePos = from, dir = v.normalized, damage = dmg, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur, id = nextId++, lob = true, vel = v, landAt = to, pathLen = v.magnitude };
            if ((owner == enemy || owner.isMinion) && owner.target != null) p.target = owner.target;
            projectiles.Add(p);
            if ((owner == player && (remoteEnemy || allies.Count > 0)) || ((owner == enemy || owner.isMinion) && coopHost)) OnSpawn?.Invoke(p);
        }

        /// <summary>從 from 以速度 v 拋出，落地（y=0）的位置</summary>
        public static Vector3 LobLanding(Vector3 from, Vector3 v)
        {
            float g = LobGravity, t = (v.y + Mathf.Sqrt(Mathf.Max(0f, v.y * v.y + 2f * g * from.y))) / g;
            return new Vector3(from.x + v.x * t, 0f, from.z + v.z * t);
        }

        void StepLob(Projectile p, float dt)
        {
            p.vel += Vector3.down * LobGravity * dt;
            p.pos += p.vel * dt;
            p.traveled += p.vel.magnitude * dt;
            if (p.pos.y > 0.05f || p.vel.y > 0f) { if (p.traveled > 60f) p.alive = false; return; }
            // 落地：落點周圍受傷＋中毒，留下一攤毒液區
            p.alive = false;
            var at = new Vector3(p.pos.x, 0f, p.pos.z);
            Emit(p.skill.lobPool ? "poisonsplash" : "inksplat", at, p.skill.color, null);
            if (!p.remote)
                foreach (var v in new List<Fighter>(Players()))
                {
                    if (!v.Alive || FlatDistance(at, v.Feet) > p.skill.radius + v.bodyRadius) continue;
                    if (v.dodgeUntil > now) { Emit("evade", v.Chest, p.skill.color, "閃避！"); continue; }
                    if (v.isAlly) { Emit("allyhit", v.Chest, p.skill.color, null); OnAllyHit?.Invoke(v, p.skill, p.damage, v.Chest, 0); }
                    else ApplyDamageAndEffect(v, p.damage, p.effect, p.effectDps, p.effectDur, v.Chest);
                }
            if (p.skill.lobPool) AddPool(at, p.remote);
        }

        public class PoisonPool
        {
            public Vector3 pos; public float radius, until; public bool remote, alive = true;
            public readonly Dictionary<Fighter, float> next = new Dictionary<Fighter, float>();
        }
        public readonly List<PoisonPool> pools = new List<PoisonPool>();
        public event Action<PoisonPool> OnPool;   // 新的毒液區（畫面用）

        void AddPool(Vector3 at, bool remote)
        {
            var pl = new PoisonPool { pos = at, radius = PoolRadius, until = now + PoolLife, remote = remote };
            pools.Add(pl);
            OnPool?.Invoke(pl);
        }

        /// <summary>毒液區：站在裡面的玩家每秒受傷一次並中毒（隊友端的毒液區只顯示，由主機判定）</summary>
        void UpdatePools()
        {
            if (pools.Count == 0) return;
            var ps = Skills.All["m_pool"];
            foreach (var pl in pools)
            {
                if (now > pl.until) { pl.alive = false; continue; }
                if (pl.remote) continue;
                foreach (var v in new List<Fighter>(Players()))
                {
                    if (!v.Alive || FlatDistance(v.Feet, pl.pos) > pl.radius + 0.1f) continue;
                    if (pl.next.TryGetValue(v, out var t) && now < t) continue;
                    pl.next[v] = now + 1f;
                    if (v.isAlly) { Emit("allyhit", v.Chest, ps.color, null); OnAllyHit?.Invoke(v, ps, ps.damage, v.Chest, 0); }
                    else { ApplyDamageAndEffect(v, ps.damage, ps.effect, ps.effectDps, ps.effectDur, v.Chest); Emit("poolstep", v.Feet, ps.color, "踩到毒液！"); }
                }
            }
            pools.RemoveAll(x => !x.alive);
        }

        /// <summary>地上的長條：藤蔓（碰到就受傷）、衝刺路線（只是提示）</summary>
        public class Lane
        {
            public string kind; public Vector3 a, b; public float start, grow, until, width; public int damage;
            public SkillDef skill; public bool remote, alive = true;
            public readonly Dictionary<Fighter, float> next = new Dictionary<Fighter, float>();
            public Vector3 Tip(float now) => grow <= 0f ? b : Vector3.Lerp(a, b, Mathf.Clamp01((now - start) / grow));
        }
        public readonly List<Lane> lanes = new List<Lane>();
        public event Action<Lane> OnLane;   // 新的長條（畫面用；主機也拿來送給隊友）

        Lane AddLane(string kind, Vector3 a, Vector3 b, float grow, float life, float width, SkillDef s, int dmg, bool remote)
        {
            a.y = 0f; b.y = 0f;
            var ln = new Lane { kind = kind, a = a, b = b, start = now, grow = grow, until = now + life, width = width, skill = s, damage = dmg, remote = remote };
            lanes.Add(ln);
            OnLane?.Invoke(ln);
            return ln;
        }

        /// <summary>隊友端：主機送來的藤蔓／衝刺路線（只顯示）</summary>
        public void AddRemoteLane(string kind, Vector3 a, Vector3 b, float grow, float life)
        {
            Skills.All.TryGetValue(kind == "vine" ? "m_vine" : "m_dash", out var s);
            AddLane(kind, a, b, grow, life, s != null ? s.radius : 0.2f, s, 0, true);
        }

        void UpdateLanes()
        {
            if (lanes.Count == 0) return;
            foreach (var ln in lanes)
            {
                if (now > ln.until) { ln.alive = false; continue; }
                if (ln.remote || ln.kind != "vine") continue;
                var tip = ln.Tip(now);
                foreach (var v in new List<Fighter>(Players()))
                {
                    if (!v.Alive || DistancePointSegment(v.Feet, ln.a, tip) > ln.width + v.bodyRadius) continue;
                    if (ln.next.TryGetValue(v, out var t) && now < t) continue;
                    ln.next[v] = now + 1f;
                    if (v.dodgeUntil > now) { Emit("evade", v.Chest, ln.skill.color, "閃避！"); continue; }
                    if (v.isAlly) { Emit("allyhit", v.Chest, ln.skill.color, null); OnAllyHit?.Invoke(v, ln.skill, ln.damage, v.Chest, 0); }
                    else { ApplyDamageAndEffect(v, ln.damage, EffectKind.None, 0f, 0f, v.Chest); Emit("vinehit", v.Chest, ln.skill.color, "被藤蔓刺到！"); }
                }
            }
            lanes.RemoveAll(x => !x.alive);
        }

        /// <summary>荊棘衝鋒獸衝刺一步：撞到玩家就受傷並停下；衝出場地也停</summary>
        static SkillDef DashSkill(int kind) => Skills.All[kind == 13 ? "m_leap" : kind == 14 ? "m_dive" : "m_dash"];

        /// <summary>開始衝刺：朝目標現在的位置衝 len 公尺；地上畫出路線（衝刺開始才畫）</summary>
        void StartDash(Fighter m, Fighter tgt, float len, SkillDef ds)
        {
            var dir = Fighter.Flat(tgt.Feet - m.Feet); if (dir == Vector3.zero) dir = Fighter.Flat(m.forward);
            m.dashDir = dir; m.dashHit = false; m.dashStart = now; m.dashUntil = now + len / ds.speed;
            AddLane("dash", m.Feet, m.Feet + dir * len, 0f, len / ds.speed + 0.15f, m.bodyRadius, ds, 0, false);
        }

        void EndDash(Fighter m)
        {
            m.dashUntil = 0f;
            int kind = m.minionKind;
            if (kind == 13)
            {
                // 跳躍魚落地（掉回海裡或摔在礁石上），停 1.5 秒
                m.alt = 0f; m.head.y = MinionHead[kind];
                Emit("splash", m.Feet, Skills.All["m_leap"].color, null);
                m.recoverUntil = now + 1.5f;
            }
            else m.recoverUntil = now + (kind == 14 ? 0.2f : MinionRecover);   // 海鳥：馬上飛回空中
        }

        void DashStep(Fighter m, float dt)
        {
            int kind = m.minionKind;
            var ds = DashSkill(kind);
            var next = m.Feet + m.dashDir * ds.speed * dt;
            bool roam = FreeRoam(kind);
            if (!roam && minionInside != null && !minionInside(next)) { EndDash(m); return; }
            m.head = new Vector3(next.x, m.head.y, next.z);
            m.forward = m.dashDir;
            float dur = Mathf.Max(0.05f, m.dashUntil - m.dashStart), prog = Mathf.Clamp01((now - m.dashStart) / dur);
            if (kind == 13) m.alt = Mathf.Sin(prog * Mathf.PI) * 1.1f;                                   // 跳躍魚：拋物線躍起
            else if (kind == 14) m.alt = Mathf.Lerp(m.dashFromAlt, 1.0f, Mathf.Clamp01(prog * 1.4f));   // 海鳥：俯衝到胸口高度
            if (kind == 13 || kind == 14) m.head.y = MinionHead[kind] + m.alt;
            if (m.dashHit) return;
            if (kind == 14 && m.alt > 1.8f) return;   // 還在高空：碰不到
            foreach (var v in new List<Fighter>(Players()))
            {
                if (!v.Alive || FlatDistance(m.Feet, v.Feet) > m.bodyRadius + v.bodyRadius + 0.1f) continue;
                m.dashHit = true;
                int dmg = Mathf.RoundToInt(ds.damage * m.dmgMul);
                if (v.dodgeUntil > now) Emit("evade", v.Chest, ds.color, "閃避！");
                else if (v.isAlly) { Emit("allyhit", v.Chest, ds.color, null); OnAllyHit?.Invoke(v, ds, dmg, v.Chest, 0); }
                else ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest);
                if (kind == 8) { m.dashUntil = 0f; m.recoverUntil = now + MinionRecover; }   // 衝鋒獸撞到就停；魚和鳥衝完全程
                return;
            }
        }

        // ------------------------------------------------------------ 劍士斬擊：斬開飛向自己正面的法球
        public const float SliceRange = 1f, SliceWindow = 0.3f;
        const float SliceCos = 0.34f;   // 正面 ±70°
        public event Action<int, bool> OnSlice;   // 斬開了有連線的法球：id、是不是對方那支手機的法術（true＝請對方移除它自己的）
        readonly HashSet<int> slicedRemote = new HashSet<int>();
        /// <summary>這發對手的法術已經被我斬開（之後才到的命中結果要忽略）</summary>
        public bool WasSliced(int id) => slicedRemote.Contains(id);

        bool Hostile(Fighter a, Fighter b) => a != null && b != null && (a == enemy || a.isMinion) != (b == enemy || b.isMinion);

        /// <summary>可以被斬開的：直線飛行的法術（從天而降、地裂、橫掃這類範圍攻擊不行）</summary>
        public static bool Sliceable(SkillDef s) => s.type == SkillType.Projectile && !s.fromAbove && !s.lob && s.areaRadius <= 0f && s.originHeight <= 0f;

        bool InSliceArc(Fighter f, Vector3 pos)
        {
            var d = pos - f.Chest;
            if (Mathf.Abs(d.y) > SliceRange) return false;
            d.y = 0f;
            float m = d.magnitude;
            if (m > SliceRange) return false;
            return m < 0.15f || Vector3.Dot(d / m, Fighter.Flat(f.forward)) >= SliceCos;
        }

        bool TrySlice(Projectile p)
        {
            if (!p.alive || !Sliceable(p.skill)) return false;
            foreach (var f in new[] { player, enemy })
            {
                if (f == null || !f.Alive || f.sliceUntil <= now || !Hostile(f, p.owner)) continue;
                if (!InSliceArc(f, p.pos)) continue;
                p.alive = false;
                Emit("slice", p.pos, p.skill.color, $"斬開 {p.skill.name}！");
                if (p.remote) { slicedRemote.Add(p.id); OnSlice?.Invoke(p.id, true); }
                else if (remoteEnemy || allies.Count > 0) OnSlice?.Invoke(p.id, false);
                return true;
            }
            return false;
        }

        void SliceNow(Fighter f) { foreach (var p in projectiles) TrySlice(p); }   // 放招當下已經在範圍內的

        /// <summary>另一支手機斬開了法術：mine＝是我發出的那發</summary>
        public void ApplyRemoteSlice(int id, bool mine)
        {
            foreach (var p in projectiles)
                if (p.alive && p.id == id && p.remote != mine)
                {
                    p.alive = false;
                    Emit("slice", p.pos, p.skill.color, "被斬開了！");
                }
        }

        void UpdateProjectiles(float dt)
        {
            // 用索引走訪：反擊會在迴圈中新增「打回去的法術」，不能用 foreach（會丟例外）；新增的下一幀才開始飛
            int count = projectiles.Count;
            for (int i = 0; i < count; i++)
            {
                var p = projectiles[i];
                if (!p.alive) continue;
                if (p.lob) { StepLob(p, dt); continue; }
                var target = p.target != null && p.target.Alive ? p.target : Opponent(p.owner);
                float step = p.skill.speed * dt;
                // 分小段前進，快的箭也不會穿過身體
                int sub = Mathf.Max(1, Mathf.CeilToInt(step / 0.08f));
                for (int k = 0; k < sub && p.alive; k++)
                {
                    Advance(p, step / sub);
                    if (TrySlice(p)) break;
                    // 隕石落地：留下火海（我方的才判定傷害；對手的只顯示）
                    if (p.skill.id == "meteor" && p.pos.y <= 0.05f)
                    {
                        p.alive = false;
                        AddBurn(p, p.pos);
                        break;
                    }
                    target.BodySegment(out var a, out var b);
                    if (!p.remote && HitMinions(p)) break;   // 先擋在前面的小怪會吃到這一發
                    if (!target.Alive)
                    {
                        // 目標不在了（無盡模式 Boss 還沒出現）：只飛，飛完消失
                        if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel * p.rangeMul) p.alive = false;
                    }
                    else if (p.remote)
                    {
                        // 對手的法術：命中由對手判定（收到結果時移除）；這裡只讓它飛，飛完消失
                        if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel) p.alive = false;
                    }
                    else if (DistancePointSegment(p.pos, a, b) < p.skill.radius + target.bodyRadius)
                    {
                        // 閃避：蹲下躲過橫向攻擊、側跨的無敵時間內 → 法術穿過去
                        if (Evades(target, p))
                        {
                            if (!p.evaded) { p.evaded = true; Emit("evade", target.Chest, p.skill.color, target.dodgeUntil > now ? "閃避！" : "蹲下躲過！"); }
                            continue;
                        }
                        p.alive = false;
                        if (p.skill.id == "meteor" && !p.reflected) AddBurn(p, target.Feet);   // 直接砸中也留下火海
                        if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                        LastProcs = RollProcs(p.owner);
                        if (remoteEnemy && target == enemy) { Emit("hit", p.pos, p.skill.color, null); OnRemoteHit?.Invoke(p.skill, p.damage, p.pos, p.id); OnDealt(p.owner, p.damage); }
                        else if (target.isAlly) { Emit("allyhit", p.pos, p.skill.color, null); OnAllyHit?.Invoke(target, p.skill, p.damage, p.pos, p.id); }   // 主機：Boss 打到隊友
                        else Hit(target, p);
                    }
                    else if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel * p.rangeMul) { p.alive = false; Emit("miss", p.pos, p.skill.color, null); }
                }
            }
            projectiles.RemoveAll(x => !x.alive);
        }

        /// <summary>抗拒法環打到 v（v 的腳在 center 半徑 r 內）：取消蓄力／伏擊，那個技能重新冷卻。回傳有沒有打斷</summary>
        public bool DispelAt(Fighter v, Vector3 center, float r)
        {
            if (v == null || !v.Alive || FlatDistance(v.Feet, center) > r + v.bodyRadius) return false;
            bool any = false;
            if (v.charging != null) { v.cooldownUntil[v.charging.id] = now + v.charging.cooldown; v.charging = null; any = true; }
            if (v.armed != null) { v.cooldownUntil[v.armed.id] = now + v.armed.cooldown; v.armed = null; any = true; }
            if (any) Emit("dispelled", v.Chest, new Color(0.94f, 0.67f, 0.99f), "被打斷！");
            return any;
        }

        /// <summary>這一發會不會被閃過：側跨的無敵時間內全部閃過；蹲著時橫向攻擊（duckable）打不到</summary>
        public bool Evades(Fighter target, Projectile p) => target.dodgeUntil > now || (target.crouching && p.skill.duckable);

        /// <summary>伏擊：時限內和對手的水平距離 ≤ 技能射程 → 自動出手一次</summary>
        void UpdateAmbush(Fighter f)
        {
            var s = f.armed;
            if (s == null) return;
            if (now > f.armedUntil || !f.Alive) { f.armed = null; Emit("armedExpire", f.Chest, Color.gray, $"{s.name} 落空"); return; }
            var target = NearestFoe(f, out var td);
            if (target == null || !target.Alive || td > AmbushReach + target.bodyRadius) return;
            f.armed = null;
            Emit("ambush", target.Chest, s.color, s.name + "！");
            int dmg = Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now));
            if (target == enemy) AddThreat(f, dmg);
            if (remoteEnemy && target.isMinion) { Emit("hit", target.Chest, s.color, null); OnMinionHit?.Invoke(target.minionId, dmg, s); OnDealt(f, dmg); }
            else if (remoteEnemy && target == enemy) { Emit("hit", target.Chest, s.color, null); OnRemoteHit?.Invoke(s, dmg, target.Chest, 0); OnDealt(f, dmg); }
            else Hit(target, new Projectile { owner = f, skill = s, pos = target.Chest, dir = Fighter.Flat(target.head - f.head), damage = dmg, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur });
        }

        void AddBurn(Projectile p, Vector3 at)
        {
            var c = new Vector3(at.x, 0f, at.z);
            burns.Add(new BurnZone { owner = p.owner, skill = p.skill, pos = c, radius = BurnRadius, until = now + BurnDuration, dps = BurnDps, remote = p.remote });
            Emit("burnzone", c, p.skill.color, null);
        }

        void UpdateBurns(float dt)
        {
            foreach (var z in burns)
            {
                if (!z.alive) continue;
                if (now > z.until) { z.alive = false; continue; }
                if (z.remote) continue;   // 對手的火海由對手判定
                var victim = Opponent(z.owner);
                if (!victim.Alive || FlatDistance(victim.Feet, z.pos) > z.radius) { z.acc = 0f; continue; }
                z.acc += dt;
                while (z.acc >= 1f)
                {
                    z.acc -= 1f;
                    if (remoteEnemy && victim == enemy) { Emit("hit", victim.Chest, z.skill.color, null); OnRemoteBurn?.Invoke(z.skill, z.dps, victim.Feet); }
                    else { if (victim == enemy) AddThreat(z.owner, z.dps); Damage(victim, z.dps, "burn"); }
                }
            }
            burns.RemoveAll(x => !x.alive);
        }

        void UpdateTraps()
        {
            foreach (var t in traps)
            {
                if (!t.alive) continue;
                if (now > t.until) { t.alive = false; if (!t.remote && (remoteEnemy || allies.Count > 0)) OnTrapGone?.Invoke(t.id); continue; }
                if (t.remote || !t.Armed(now)) continue;   // 對手的陷阱由對手判定
                var victim = Opponent(t.owner);
                if (FlatDistance(victim.Feet, t.pos) < t.skill.radius + 0.15f)
                {
                    t.alive = false;
                    Emit("trap", t.pos, t.skill.color, $"{t.skill.name}！");
                    if (remoteEnemy && victim == enemy) { OnRemoteHit?.Invoke(t.skill, t.skill.damage, t.pos, -t.id); OnTrapGone?.Invoke(t.id); }
                    else
                    {
                        if (victim == enemy) AddThreat(t.owner, t.skill.damage);
                        ApplyDamageAndEffect(victim, t.skill.damage, t.skill.effect, t.skill.effectDps, t.skill.effectDur, t.pos);
                        if (allies.Count > 0) OnTrapGone?.Invoke(t.id);   // 合作模式：告訴隊友陷阱沒了
                    }
                }
            }
            traps.RemoveAll(x => !x.alive);
        }

        // ------------------------------------------------------------ 命中：反擊 → 格擋 → 護盾 → 傷害 → 附加效果
        void Hit(Fighter target, Projectile p)
        {
            if (target.counterUntil > now)
            {
                target.counterUntil = 0;
                // 把攻擊原樣打回去，朝施法者目前的胸口飛（施法者移動就能閃開）
                var from = target.Chest + Fighter.Flat(target.forward) * 0.4f;
                var dir = (p.owner.Chest - from).normalized;
                Spawn(target, p.skill, from, dir, p.damage, p.effect, p.effectDps, p.effectDur, true);
                Emit("counter", p.pos, new Color(0.96f, 0.45f, 0.71f), "反擊！打回去");
                return;
            }
            if (target == enemy) AddThreat(p.owner, p.damage);
            ApplyDamageAndEffect(target, p.damage, p.effect, p.effectDps, p.effectDur, p.pos);
            ApplyProcs(target, LastProcs, p.pos);
            OnDealt(p.owner, p.damage);
        }

        /// <summary>打中別人之後：吸血、命中回 MP（角色養成的特殊能力）</summary>
        void OnDealt(Fighter a, float dmg)
        {
            if (a == null || !a.Alive || dmg <= 0f) return;
            if (a.lifesteal > 0f) a.hp = Mathf.Min(a.maxHp, a.hp + dmg * a.lifesteal);
            if (a.mpOnHit > 0f) a.mp = Mathf.Min(a.maxMp, a.mp + a.mpOnHit);
        }

        // ------------------------------------------------------------ 命中附加效果（無盡模式升級）
        /// <summary>最近一次命中擲出的附加效果（1 灼燒、2 中毒、4 冰凍）；合作隊友端送給主機用</summary>
        public int LastProcs;
        int RollProcs(Fighter attacker)
        {
            if (attacker == null) return 0;
            int r = 0;
            if (attacker.burnChance > 0f && rng.NextDouble() < attacker.burnChance) r |= 1;
            if (attacker.poisonChance > 0f && rng.NextDouble() < attacker.poisonChance) r |= 2;
            if (attacker.freezeChance > 0f && rng.NextDouble() < attacker.freezeChance) r |= 4;
            return r;
        }
        /// <summary>套用附加效果：灼燒（每秒 5、4 秒）、中毒（每秒 3、6 秒）、冰凍（定身 1.5 秒，打斷蓄力）</summary>
        public void ApplyProcs(Fighter target, int procs, Vector3 at)
        {
            if (procs == 0 || target == null || !target.Alive) return;
            if ((procs & 1) != 0) { target.dots.Add(new Fighter.Dot { dps = 5f, until = now + 4f }); Emit("effect", at, new Color(1f, 0.5f, 0.15f), "灼燒"); }
            if ((procs & 2) != 0) { target.dots.Add(new Fighter.Dot { dps = 3f, until = now + 6f }); Emit("effect", at, Color.green, "中毒"); }
            if ((procs & 4) != 0) { target.snaredUntil = now + 1.5f; target.charging = null; target.windupUntil = 0f; Emit("effect", at, new Color(0.6f, 0.9f, 1f), "冰凍"); }
        }

        void ApplyDamageAndEffect(Fighter target, int damage, EffectKind eff, float dps, float dur, Vector3 at)
        {
            // 閃避率（刺客技能樹）：整次攻擊（傷害＋效果）完全閃開
            if (target.evadeChance > 0f && damage > 0 && rng.NextDouble() < target.evadeChance) { Emit("evade", target.Chest, new Color(0.7f, 0.6f, 1f), "閃避！"); return; }
            float dmg = damage;
            string note = null;
            if (target.blockUntil > now && dmg > 0) { dmg = Mathf.Round(dmg * (1f - target.blockReduce)); target.blockUntil = 0; note = "格擋"; }
            if (target.armorUntil > now && dmg > 0) { dmg = Mathf.Round(dmg * (1f - target.armorReduce)); note = note == null ? "守護" : note + "＋守護"; }
            if (target.shieldUntil > now && target.shieldLeft > 0 && dmg > 0)
            {
                int absorb = Mathf.Min(target.shieldLeft, Mathf.RoundToInt(dmg));
                target.shieldLeft -= absorb; dmg -= absorb;
                if (target.shieldLeft <= 0) target.shieldUntil = 0;
                note = note == null ? "護盾吸收" : note + "＋護盾";
            }
            if (note != null) Emit("mitigate", at, new Color(0.58f, 0.77f, 0.99f), note);
            switch (eff)
            {
                case EffectKind.Dot: target.dots.Add(new Fighter.Dot { dps = dps, until = now + dur }); Emit("effect", at, Color.green, "中毒"); break;
                case EffectKind.Blind: target.blindUntil = now + dur; Emit("effect", at, Color.gray, "致盲"); break;
                case EffectKind.Snare: target.snaredUntil = now + dur; target.charging = null; Emit("effect", at, new Color(1f, 0.6f, 0.2f), "定身"); break;
            }
            if (dmg > 0 || note == null) Damage(target, dmg, "hit", at);
        }

        void Damage(Fighter f, float dmg, string kind, Vector3? at = null)
        {
            if (!f.Alive) return;
            dmg *= f.damageTakenMul * (1f - f.gearReduce);
            if (f.lastStand > 0f && f.hp < f.maxHp * 0.3f) dmg *= 1f - f.lastStand;   // 不屈：低血量減傷
            dmg = Mathf.Round(dmg);
            f.hp = Mathf.Max(0, f.hp - dmg);
            if (dmg > 0 && f.prepUntil > now) { f.prepUntil = 0f; Emit("fail", f.Chest, Color.gray, "刺殺準備被打斷！"); }
            if (dmg > 0 && kind != "dot" && kind != "burn" && f.armed != null && f.armed.releaseNear) { f.cooldownUntil[f.armed.id] = now; Emit("fail", f.Chest, Color.gray, $"{f.armed.name}被打斷！要重新蓄力"); f.armed = null; }
            Emit(kind == "dot" || kind == "burn" ? kind : (f.isPlayer ? "hurt" : "hit"), at ?? f.Chest, Color.red, dmg > 0 ? $"-{dmg:0}" : "");
            if (!f.Alive)
            {
                if (f.isMinion) { kills++; if (endless) xp += Mathf.RoundToInt(MinionXp[f.minionKind] * (1f + 0.03f * wave)); Emit("miniondie", f.Chest, Color.white, null); }
                else if (f.isPlayer) Emit(AnyAllyAlive ? "down" : "lose", f.Chest, Color.white, null);
                else if (endless && f == enemy) OnBossDown();
                else Emit("win", f.Chest, Color.white, null);
            }
        }

        // ------------------------------------------------------------ 小工具
        public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>繞 Y 軸旋轉（不用 Quaternion，讓純邏輯可在 Unity 外測試）</summary>
        public static Vector3 RotateY(Vector3 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
        }
    }
}

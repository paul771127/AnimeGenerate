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
    public partial class Fighter
    {
        public const float BodyRadius = 0.28f;

        public string name;
        public ClassDef cls;
        public List<SkillDef> loadout = new List<SkillDef>();
        public float hp, mp, regen;
        /// <summary>MP 自然回復：職業基礎速度的 30%；精神加到 99 點才回到原本的 100%（中間依點數線性增加）</summary>
        public const float MpRegenBase = 0.3f;
        public const int MpRegenFullSpi = 99;
        public int maxHp, maxMp;
        public Vector3 head;                  // 頭（玩家＝手機）的位置
        public Vector3 forward = Vector3.forward;   // 面向（水平）
        public Vector3 lockFwd;                     // 小怪近身攻擊預備時鎖住的面向（扇形範圍跟著它，不會追著玩家轉）
        public bool isPlayer;

        public readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        public SkillDef charging; public float chargeStart, chargeDelay;
        public float blockUntil, blockReduce, shieldUntil, counterUntil, snaredUntil, blindUntil;
        public float sliceUntil;
        public float focusUntil;        // 獵人精神集中：這段時間內新的冷卻減半
        public float prepUntil, prepBonus;   // 刺殺準備：這段時間內傷害 +prepBonus（受到傷害就取消）
        public Vector3 chargeFrom;      // 開始詠唱時腳的位置（刺殺準備要站著不動）
        // 牧師的全隊輔助：攻擊 +、加速（集氣／蓄力）、持續回復、爆擊率 +（到期就失效）
        public float teamAtkUntil, teamAtk, teamHasteUntil, teamHaste, teamRegenUntil, teamRegen, teamCritUntil, teamCrit;
        public float HasteMul(float now) => Mathf.Max(0.4f, 1f - (teamHasteUntil > now ? teamHaste : 0f) - (killRushUntil > now ? Battle.KillRushHaste : 0f));   // 隊伍加速＋追獵（擊倒後加快）
        public float TeamCritAt(float now) => teamCritUntil > now ? teamCrit : 0f;
        public float OutMul(float now) => (evadeBoostUntil > now ? 1f + evadeBoost : 1f) * (prepUntil > now ? 1f + prepBonus : 1f) * (1f + chargeBonus) * RageMul * (teamAtkUntil > now ? 1f + teamAtk : 1f)
            * (lowMpBonus > 0f && maxMp > 0f && mp < maxMp * 0.3f ? 1f + lowMpBonus : 1f)       // 法師「孤注一擲」：MP 低於 30%
            * (zeal > 0f && maxHp > 0f ? 1f + zeal * Mathf.Clamp01(1f - hp / maxHp) : 1f);        // 牧師「狂信」：HP 越低傷害越高
        // ---- 技能樹的流派效果（CharacterProfile.ApplyTo 設定；0＝沒點）
        public SkillDef basic;          // 普攻（技能樹強化後的版本；null＝用 Skills.BasicOf）
        public float spellEcho, basicMp, lowMpBonus, melt, iceFreeze;            // 法師：奧術回響、奧術迴路、孤注一擲、融化、寒冰
        public float arcaneBurst, arcaneTwin;
        public float fireBurst, icePierce, windOrbit, windOrbitTime;   // 法師：火球爆裂、冰槍穿透（幾隻）、風刃迴旋（有沒有學）、迴旋延長（秒）   // 法師：奧術爆裂（命中爆炸的範圍傷害比例）、奧術連射（連射兩發的機率）
        /// <summary>法師的魔力盾（開關）：開著時受到的傷害改扣 MP（MP 不夠才扣 HP），只能用奧術飛彈</summary>
        public bool manaCoat;
        /// <summary>這一場累計打出的傷害、治療（合作時的每秒傷害／每秒治療）</summary>
        public float dealtDone, healDone;
        public float longShot, steadyAim, trapCrit, basicHoming;                  // 獵人：遠射、穩定瞄準、獵殺時刻；牧師：普攻追蹤
        public float arrowBleed, arrowPoison, arrowSilence;                       // 獵人：箭命中時出血／中毒／沉默的機率（技能樹）
        public float silencedUntil;                                               // 被沉默：這段時間不能唸技能（普攻照常）
        public float comboStrike, basicLeech, waveFlow, riposte;                  // 劍士：連斬、飲血、劍意、反擊之刃
        public float orbSlice;                                                    // 劍士技能樹「斬開法球」：斬擊能斬開飛來的法球（0＝沒點）
        public float venom, assassinate, vanish, knifeCdr;                        // 刺客：劇毒連刺、刺殺、遁影（秒數：伏擊後這段時間內下一次物理攻擊必閃）、飛刀節奏
        public float holySeal, martyr, zeal;                                      // 牧師：聖印、殉道、狂信
        public float bashGuard, sanctionCombo, critDmg, falconMark, petMp, falconAssault, falconPatrol, falconDiveReady, falconPatrolAt;
        // 流派被動（Combat.Styles 區段）
        public float styleThorns, executeDmg, firstStrike, statusAmp, hitStack, chainHit, killRush, killSoul, castShield, overhealShield;
        public int stackCount; public float stackUntil, killRushUntil;
        /// <summary>畫面用：自己身上的增益（施放／持續特效；SoloBattle.NewSkillFx.cs 讀）</summary>
        public class BuffFx { public string id; public float start, until; public bool shown; }
        public readonly List<BuffFx> buffFx = new List<BuffFx>();
        /// <summary>流派爆發技能的暫時加成（PassiveKind → 數值、到期時間）</summary>
        public readonly Dictionary<PassiveKind, KeyValuePair<float, float>> styleBuffs = new Dictionary<PassiveKind, KeyValuePair<float, float>>();
        public float closeInstinct;     // 刺客「近身本能」等級（0＝沒點）        // 進階職業：盾擊格擋、制裁連斬、爆擊傷害、鷹眼標記、契約魔力
        public int basicHits, venomStacks, seals;
        // 刺殺流派：有沒有被敵人看見（SoloBattle 每幀設定：對戰＝對手的鏡頭有沒有拍到我；電腦／Boss／小怪＝我在不在它的視野內）、這次蓄力累計躲開視線幾秒
        /// <summary>玩家：這一發法術從哪裡射出（畫面上手的位置；SoloBattle 放招前設定，用一次就清掉）</summary>
        public Vector3 castFrom; public bool hasCastFrom;
        public bool seenByFoe = true; public float hiddenTime;                                 // 連斬計數、身上的毒層、聖印層數
        public float waveFreeUntil, riposteUntil, vanishUntil, venomUntil;
        // ---- 進階職業
        public string adv;              // 進階職業 id（Advanced.All）；null＝沒轉職
        public float healMul = 1f;      // 治療量倍率（主教 ×1.2、審判者 ×0.6）
        /// <summary>狂戰士怒氣：HP 越低傷害越高（HP 30% 以下 +60%）</summary>
        public float RageMul => 1f;   // 原本狂戰士的怒氣（劍仙取代後不再使用）
        /// <summary>劍仙：劍來（普攻強化到這個時間）、御劍術（飛劍連射到這個時間、用哪個技能的數值）</summary>
        public float swordUntil, yujianUntil; public SkillDef yujianSkill;
        /// <summary>衍生技能（連環陷阱、回響火球、翻滾箭、屬性苦無、獵鷹俯衝）跟著原本那招強化後的版本（CharacterProfile.ApplyTo 填）</summary>
        public readonly Dictionary<string, SkillDef> derived = new Dictionary<string, SkillDef>();
        public SkillDef Derived(string id) => derived.TryGetValue(id, out var s) ? s : Skills.All.TryGetValue(id, out var b) ? b : null;
        /// <summary>劍來期間普攻的集氣多幾秒（0.5～2 秒，依「劍來延長」的等級）</summary>
        public float jianlaiSlow;
        public float oathUntil, oathShare; public Fighter oathBy;   // 聖騎士的守護誓言：我受到的傷害有 oathShare 轉給 oathBy
        public float selfOathUntil;     // 聖騎士自己：誓言期間受到的傷害 -20%
        public float frenzyUntil, undyingUntil, invulnUntil;        // 狂戰士：血之狂暴、不死之軀、無敵
        public float markNextUntil;     // 神射手：致命標記（下一箭必定爆擊）
        public float rollUntil, lastDodgeSeen;                      // 遊俠：翻滾射擊
        public bool ampNext;            // 大魔導士：唸了增幅咒（下一招傷害 +50%）
        public float manaShieldUntil;   // 大魔導士：魔力護盾
        public float subUntil, cloneUntil;                          // 忍者：替身術、分身術
        public float comboUntil, danceUntil;                        // 影舞者：連殺、影舞
        public float blessUntil;        // 主教：復活祝福（倒下時自動站起來）
        public float markedUntil;       // 被懲戒：受到的傷害 +20%
        public int faith;               // 審判者：信仰（0～5）
        public Vector3 stillAnchor; public float stillSince;        // 站著不動從什麼時候開始（神射手鷹眼、主教祈禱領域）
        public bool overcharge;         // 獵人「蓄力強化」：蓄滿後可以繼續蓄到 200%
        public float chargeBonus;       // 這一發的蓄力加成（200% 時 +150%）；放招當下設定        // 劍士斬擊：這段時間內會斬開正面 1m 內飛來的法球
        public int shieldLeft;
        /// <summary>精魄（Souls.cs）：格擋率、受到的傷害倍率（副作用）、反彈、觸發型效果</summary>
        public float blockChance, soulTaken = 1f, thorns;
        public float trapReduce,evadeBoost,evadeBoostUntil;
        public float dodgeCdr, airReduce, duckShield, dodgeCdrAt, duckShieldAt;   // 天穹飛毯：閃過障礙減冷卻、跳躍中減傷、蹲避護盾（Combat.Carpet.cs）
        /// <summary>無盡塔每 5 波出現的精銳敵人（血量 ×6、攻擊 ×1.6、體型 ×1.5）</summary>
        public bool isElite;
        /// <summary>召喚師收服的魔物（小怪種類；−1＝不是）</summary>
        public int tamedKind = -1;
        /// <summary>忍者：苦無普攻同時射出火／冰／雷苦無的機率；審判者：造成傷害轉護盾的比例</summary>
        public float kunaiFire, kunaiIce, kunaiBolt, dmgShield;
        /// <summary>影舞者：借給影分身的 HP 上限（分身倒下時還回來）；影分身本體（null＝沒有）</summary>
        public int cloneLent; public Fighter shadowClone;
        /// <summary>影分身：主人（攻擊算主人的）、出手次數（每 3 次影殺、每 4 次劇毒）</summary>
        public Fighter cloneOf; public int cloneStrikes;
        /// <summary>友方人物：localFriend＝這台手機在跑 AI（影分身、收服的魔物）；friendOwner＝別台手機的代理（−1＝不是）、在那台的編號；影分身的時限</summary>
        public bool localFriend; public int friendOwner = -1, friendLocalId; public float friendUntil;
        public float dmgShieldAcc;
        public float basicShield, hitShield;   // 劍士系列（取代吸血）：普攻傷害轉護盾、所有傷害轉護盾
        public readonly List<SoulFx> soulProcs = new List<SoulFx>();
        // 刺客的近身技能：放招後進入「伏擊」，時限內靠近到射程內就自動出手
        public SkillDef armed; public float armedUntil;
        /// <summary>電腦敵人的近戰多伸出去的距離（公尺）：它們最近只走到離玩家「一個身高」，近戰仍要打得到（EnemyAI 設定；玩家＝0）</summary>
        public float reachBonus;
        public bool palmVisible = true;        // 玩家：手掌有沒有出現在鏡頭前（刺客伏擊技能要看到手掌才會蓄力；電腦／對手一律 true）
        // 身體閃避：蹲下（躲橫向攻擊）、快速側跨（短暫無敵）
        public bool crouching; public float dodgeUntil;
        public float airborneUntil;   // 跳起來（手機快速往上）：震地波打不到
        // 體型（Boss 比較大）、蓄力倍率（Boss 狂暴時變快）、目前攻擊的目標（合作模式 Boss 會換目標）
        public float bodyRadius = BodyRadius, chargeScale = 1f;
        public Fighter target;
        public float damageTakenMul = 1f;   // 受到的傷害倍率（單人模式的電腦難度：簡單／普通時玩家受傷較少）
        // Boss 召喚的小怪：編號、攻擊狀態（預備到 windupUntil 出手，attackReadyAt 之後才能再攻擊）
        public bool isAlly; public int pid = -1;   // 合作：其他玩家的代理、那支手機的編號（主機 0、加入的人 1～3）
        public bool isMinion; public int minionId; public float windupUntil, attackReadyAt, teleportAt;
        /// <summary>副本的劇情人物：0 我方（保護目標、友軍）、1 敵方、2 第三勢力（誰都打）；-1＝不是劇情人物</summary>
        public int npcSide = -1; public string npcId; public bool npcEscort; public float npcNextAtk;
        public Vector3 aimPoint;        // Boss 熔岩躍擊／樹根爆發的落點（蓄力時畫紅圈）
        /// <summary>受擊硬直（使用者要求）：小怪、菁英、稀有怪每掉 1/3 血（剩 2/3、1/3）硬直一次，正在準備的攻擊取消</summary>
        public int staggerTier; public float staggerUntil;
        public float dashUntil; public Vector3 dashDir; public bool dashHit;   // 荊棘衝鋒獸／跳躍魚／俯衝海鳥：衝刺中
        public float dashStart, dashFromAlt;   // 衝刺開始時間、俯衝開始的高度
        public float alt;               // 離地高度（海鳥飛在空中、水母和河豚浮著、跳躍魚跳起來）；0＝在地上
        public float leapStart, leapEnd; public Vector3 leapFrom, leapTo;   // 黃土山道：從崖頂跳進場內（leapEnd＞0＝還在崖上／跳躍中）
        public int minionKind;                 // 0 近戰小鬼、1 火焰射手、2 岩石衝鋒獸、3 自爆小鬼、4 岩石投擲者、5 熔岩狙擊手
        public float recoverUntil;             // 小怪出手後原地不動到這個時間（1 秒）
        public int shotCount;                  // 火焰射手：連射了幾發（兩發就繞到側面）
        public float fuseAt;                   // 自爆小鬼：引信到時就爆（不管離玩家多遠）
        public int strafeSign = 1;             // 熔岩小鬼：等別隻出手時往哪邊繞（±1）
        public bool ambusher;                  // 簡單模式：從別的方向偷襲來的那隻（同時最多一隻）
        public Battle.MarchGroup march; public Vector3 marchSpawn; public float marchHp;
        public float outsideSince, buriedAt;   // 在場外待了多久（保險：太久就拉回場內，免得打不到、關卡卡住）   // 軍陣行進中（Combat.cs LayoutFormation）：照隊伍一起往前推，到定點才散開打
        public bool hidden;                    // 食人花：埋在地下（玩家走近 1.5m 才冒出來；埋著時打不到）
        public bool miniBoss;                  // 第 3 波單獨登場的特殊敵人（有名字和血條）
        public bool treasure, treasureRunning; public float treasureLeaveAt;
        public int variant;                    // 變種怪（Combat.Variants.cs）：刺客怪、天候怪的 id（0＝不是）
        public float lureUntil; public bool lureRolled, capturedPet; public Vector3 lureAt;   // 寵物誘餌（Combat.Pets.cs）
        public long petUid; public int petAtk, petLevel; public float petGap, petActLock; public List<string> petSkills; public readonly Dictionary<string, float> petCd = new Dictionary<string, float>();   // 出戰的寵物
        // 敵人招式（Combat.EnemySkills.cs）：讀條中的招、目標、冷卻；被排程的人：忙到什麼時候、上一次要跳／蹲的時間
        public EnemySkillDef esCast; public Fighter esTarget; public float esCastAt, esGuardUntil, esGuardBuffUntil, esBornAt; public bool esCountered, esMoved; public Vector3 esCrossTo;
        public readonly List<EnemyCue> esCues = new List<EnemyCue>(); public readonly Dictionary<string, float> esCd = new Dictionary<string, float>();
        public float esBusyUntil, esLastJump = -99f, esLastDuck = -99f;
        public int human, humState, humSide = 1; public float humUntil, humCdAt, humSwitchAt, humHealed, humWaitFrom;   // 人形敵人（Combat.Variants.cs）：職業、0 交戰／1 跑開回血
        public int support; public float hasteUntil, rallyUntil;   // 輔助型敵人（Combat.Variants.cs）：1 薩滿、2 仙女、3 戰鼓手、4 旗手；被加速、被舉旗增益到什麼時候
        public bool swift;   // 疾風稀有怪（Combat.Variants.cs）
        public bool assassin, healer, summoner, summoned; public int asnState; public float asnUntil; public Fighter asnTarget, healTarget, summonedBy;   // summoned＝召喚物：沒有經驗、不掉寶   // 刺客怪：0 潛伏、1 沿邊繞、2 突襲、4 退回   // 寶箱怪（Combat.Treasure.cs）：到處逃跑、每下只扣 1、時間到就逃走
        // 副本特殊地點（Combat.RoomEvents.cs）
        public bool rooted;                    // 不會動、不會出手（守護物、圖騰、雕像、寶箱）
        public float lure;                     // 小怪優先打它（多算幾公尺的優勢）
        public List<Vector3> walkPath; public int walkIdx;   // 護送：沿著這條路走
        public int evElement;                  // 對應元素的雕像：要的屬性（1 火、2 冰、3 雷）
        public bool evReal;                    // 找出真身：真的寶箱
        public bool huntTarget;                // 斬首的目標
        public Vector3 homePos;                // 跳躍魚：海裡的家（咬完跳回去）
        public Vector3 dashFrom;               // 衝刺起點（荊棘衝鋒獸衝完沿路留下荊棘帶）
        public float restUntil;                // 浮空／飛行的小怪出手後：降到場內地上休息到這個時間（近戰打得到），之後才飛回空中
        public bool repositionNext;            // 遠程小怪：出手後（站定結束）要先隨機換個位置才能再出手
        public Vector3 moveTo; public float moveUntil;   // 換位置的目的地、最晚走到什麼時候
        public float dmgMul = 1f, speedMul = 1f;   // 無盡模式：越後面的波次越強
        // 無盡模式的玩家升級：攻擊力、冷卻、爆擊率、攻擊距離、命中時附加灼燒／中毒／冰凍的機率
        public float basicCdMul = 1f;   // 普攻冷卻倍率（敏捷越高越快；角色養成設定）
        public float atkMul = 1f, cdMul = 1f, critChance, rangeMul = 1f, burnChance, poisonChance, freezeChance, evadeChance;
        public float accuracy;          // 命中率（幸運每點 +0.1%）：打人時抵掉對方的閃避率
        // 牧師的守護：時限內受到的直接傷害按比例減少
        public float armorUntil, armorReduce;
        // 角色養成（無盡模式）：裝備減傷、側跨無敵時間加長、吸血、命中回 MP、每秒回血、低血量減傷
        public float gearReduce, dodgeBonus, lifesteal, mpOnHit, hpRegen, lastStand;
        public float leechSwingAt = -1f, leechSwingBest;
        public float leechBudget = -1f, leechBudgetAt;   // 吸血每秒上限（LeechPerSec）用的額度   // 普攻吸血（飲血）：同一揮只算一次（取打中的最高傷害）
        public float shieldSwingAt = -1f, shieldSwingBest; // 劍氣護體（普攻轉護盾）：同一揮只算一次
        public float regenTick, nextRegenAt;   // 自然回復（劍士／主教的被動）：每 10 秒一次回 regenTick（不是每秒）
        /// <summary>魔法減傷（智力）；gearReduce＝物理減傷（體質、裝備）</summary>
        public float magicReduce;
        /// <summary>強化藥劑（藥劑 id、到期時間 battle.now）：右上角狀態圖示用（SoloBattle.Potions.cs）</summary>
        public readonly List<KeyValuePair<string, float>> potionBuffs = new List<KeyValuePair<string, float>>();
        /// <summary>減傷（使用者要求）：體質加到 99 點＝30%；裝備、精魄、技能樹、職業另外加；全部加起來最多 70%（超過沒有作用）</summary>
        public const float MaxReduce = 0.7f, VitMaxReduce = 0.3f;
        /// <summary>精神從初始值加到 99 點：魔法減傷 +15%（使用者要求，和智力的 30% 疊加）</summary>
        public const float SpiMaxMagicReduce = 0.15f;
        public const int VitFullReduce = 99;
        public readonly List<Dot> dots = new List<Dot>();

        public class Dot { public float dps, until, acc; }

        public Fighter(string name, ClassDef cls, IEnumerable<string> skillIds, bool isPlayer)
        {
            this.name = name; this.cls = cls; this.isPlayer = isPlayer;
            maxHp = cls.maxHp; maxMp = cls.maxMp; regen = cls.mpRegen * MpRegenBase;   // MP 自然回復只有職業原本的 30%（精神再往上加）
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
        public float homing;            // 追蹤：每秒最多轉幾度（牧師普攻「聖光導引」）
        public Fighter target;          // 這一發要打誰（合作模式 Boss 會打隊友）；null＝對手
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos, dir;
        public float traveled;
        public float homingUntil;       // 追蹤到這個時間為止（泡泡彈：3 秒）；0＝一直追
        public int damage;
        public int pierceLeft;          // 還能穿透幾個敵人（法師「冰槍穿透」；skill.pierce＝無限）
        public EffectKind effect; public float effectDps, effectDur;
        public bool reflected, alive = true;
        public bool crit;               // 爆擊（傷害 ×2）
        public bool lob; public Vector3 vel, landAt;   // 拋物線投擲（毒液彈）：速度向量、預定落點
        public float rangeMul = 1f;     // 飛行距離倍率（無盡模式升級）
        public float speedMul = 1f;     // 飛行速度倍率（每日閃避挑戰：每一發快慢不同）
        public Projectile clashedWith;  // 已經和這一發對撞過（不重複判定）
        public float slowBase = -1f;    // Boss 法球第一次被減速前的速度倍率（最慢＝這個的 30%）
        public bool sliceClashed;       // 已經和劍士的斬擊對撞過（撐住了就不再判定）
        public bool locked;             // 法術角力中：卡在半空，不前進、不打人
        public float hitMul = 1f;       // 命中判定範圍倍率（電腦敵人的投射物 0.5：不用跑很遠才躲得掉）
        public int id;
        public bool remote;          // 雙人：對手的法術（只顯示，命中由對手判定）
        public HashSet<Fighter> pierced;   // 穿透箭已經打過的小怪
        public bool pierceCut;             // 穿透箭已經穿過第一個敵人（之後傷害減半）
        // 弧線飛行（風刃）：沿直線前進的「基準點」＋往側邊的偏移 sin(π×進度)
        public Vector3 basePos, curveSide;   // curveSide：側向單位向量 × 振幅（公尺，可為負）
        public float curveAmp, pathLen;
        public bool Curved => pathLen > 0.01f && Mathf.Abs(curveAmp) > 0.01f;
        /// <summary>目前的飛行方向（弧線＝路徑的切線）：新月形法球的朝向</summary>
        public Vector3 Heading
        {
            get
            {
                if (!Curved || traveled >= pathLen) return dir;
                var h = dir + curveSide * (Mathf.PI / pathLen * Mathf.Cos(Mathf.PI * traveled / pathLen));
                return h.sqrMagnitude > 1e-6f ? h.normalized : dir;
            }
        }
        /// <summary>飛超過技能射程：開始淡出（不再打到人），fadeEnd＝飛到這個距離就消失；-1＝還沒</summary>
        public float fadeEnd = -1f;
        public bool Fading => fadeEnd >= 0f;
        /// <summary>畫面用的大小倍率：1＝正常，淡出時縮到 0</summary>
        public float FadeK => Fading ? Mathf.Clamp01((fadeEnd - traveled) / Battle.FadeDistance) : 1f;
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
        public readonly Dictionary<Fighter, float> accs = new Dictionary<Fighter, float>();   // 每個受害者各自累積（玩家的毒霧可以同時燒好幾隻小怪）
    }

    /// <summary>召喚物（遊俠的獵鷹、召喚師的火靈／石像、忍者的分身）：跟著主人或待在原地，自動攻擊附近的敵人</summary>
    public class Pet
    {
        public int id;
        public Fighter owner;
        public string kind;            // falcon / fire / golem / clone
        public Vector3 pos;            // 場地座標（身體中心）
        public float until, next, hp, maxHp, range, interval;
        public int dmg, blast;         // 每次攻擊的傷害、被引爆時的爆炸傷害
        public bool remote;            // 別人的召喚物（只顯示）
        public Fighter body;           // 收服的魔物：在戰場上的身體（友方人物，怪物會打牠）
        public float power = 1f;       // 餵養累積的倍率（每次 +1%；重新召喚歸零）
        public int feeds;              // 被主人餵養幾次
        public bool alive = true;
        public Vector3 home;           // 石像：站的位置；分身：相對主人的偏移
        public SkillDef Attack => Skills.All.TryGetValue("pet_" + kind, out var s) ? s : null;
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

    public partial class Battle
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
        /// <summary>刺殺流派（點了「暗殺術」）：離開敵人視線這麼久就完成蓄力；伏擊狀態維持更久</summary>
        public const float StealthTime = 1f, StealthAmbushWindow = 10f;
        public static bool StealthAmbush(Fighter f) => f != null && f.assassinate > 0f;
        int nextId = 1;

        /// <summary>事件（給畫面做特效與文字）：種類、位置、顏色、文字</summary>
        public event Action<string, Vector3, Color, string> OnEvent;
        void Emit(string kind, Vector3 at, Color c, string text) => OnEvent?.Invoke(kind, at, c, text);
        /// <summary>正在發生的命中是哪個技能打的、是否爆擊（"hit" 事件期間有效，給畫面挑命中特效用）</summary>
        public SkillDef HitSkill { get; private set; }
        /// <summary>正在打到人的技能（判斷物理／魔法減傷用）；沒有＝物理</summary>
        SkillDef incoming;
        public bool HitCrit { get; private set; }
        void EmitHit(SkillDef s, Vector3 at, bool crit = false)
        {
            HitSkill = s; HitCrit = crit;
            Emit("hit", at, s.color, null);
            HitSkill = null; HitCrit = false;
        }

        public Battle(Fighter player, Fighter enemy) { this.player = player; this.enemy = enemy; }

        // ------------------------------------------------------------ 進階職業
        public readonly List<Pet> pets = new List<Pet>();
        public event Action<Pet> OnPetSpawn, OnPetGone;         // 我的召喚物出現／消失（要告訴其他手機顯示）
        public event Action<Fighter, float> OnOathShare;         // 守護誓言：我受到的傷害轉一部分給聖騎士（哪位、多少）
        readonly List<KeyValuePair<float, Action>> timers = new List<KeyValuePair<float, Action>>();
        void After(float sec, Action a) => timers.Add(new KeyValuePair<float, Action>(now + sec, a));
        public const int MaxPets = 2, FaithMax = 5;
        /// <summary>石像替主人承受的比例；餵養（主人打中自己的召喚物）每次 +1% 攻擊與血量</summary>
        public const float GolemShare = 0.8f, FeedGrow = 0.01f, TamedHpNeed = 0.5f, CaptureRange = 6f;
        public const float StillMove = 0.35f, PrayerField = 3f, PrayerRegen = 0.015f, AuraRange = 3f, AuraReduce = 0.15f;
        /// <summary>敵方（Boss／對手代理／小怪）</summary>
        bool FoeSide(Fighter f) => f == enemy || f.isMinion;
        /// <summary>站著不動超過 1 秒</summary>
        public bool Still(Fighter f) => now - f.stillSince >= 1f;
        /// <summary>神射手鷹眼層數（站著不動每 2 秒 1 層，最多 3 層）</summary>
        public int EagleStacks(Fighter f) => f.adv == "marksman" ? Mathf.Clamp(Mathf.FloorToInt((now - f.stillSince) / 2f), 0, 3) : 0;
        /// <summary>冷卻倍率（狂戰士血之狂暴：攻速 ×1.5）</summary>
        float CdFactor(Fighter f) => f.frenzyUntil > now ? 1f / 1.5f : 1f;
        /// <summary>這招要蓄力多久（影舞者連殺：伏擊技能 −70%）</summary>
        public float ChargeTime(Fighter f, SkillDef s) => s.charge * f.chargeScale * f.HasteMul(now) * (f.comboUntil > now && s.releaseNear ? 0.3f : 1f) * (f.danceUntil > now && s.releaseNear ? 0.5f : 1f);   // 連殺 −70%、影舞 −50%
        public bool HasPet(Fighter f) { foreach (var p in pets) if (p.alive && p.owner == f && p.kind != "clone") return true; return false; }
        /// <summary>聖騎士的守護光環：自己是聖騎士，或 3m 內有活著的聖騎士隊友</summary>
        bool InAura(Fighter f)
        {
            if (f.adv == "paladin") return true;
            foreach (var a in allies) if (a != f && a.Alive && a.adv == "paladin" && FlatDistance(a.Feet, f.Feet) <= AuraRange) return true;
            return false;
        }

        /// <summary>直接打到某個敵人（範圍攻擊、召喚物、毒霧）：連線時交給對方判定</summary>
        void HitFoe(Fighter f, Fighter v, SkillDef s, int dmg, EffectKind eff = EffectKind.None, float dps = 0f, float dur = 0f)
        {
            if (v == null || !v.Alive || dmg <= 0 && eff == EffectKind.None) return;
            LastProcs = RollProcs(f, s);
            dmg = PreHit(f, v, s, dmg, null);
            if (remoteEnemy && v == enemy) { EmitHit(s, v.Chest); OnRemoteHit?.Invoke(s, dmg, v.Chest, 0); OnDealt(f, dmg, v, s); return; }
            if (remoteEnemy && (v.isMinion || IsHostileRoomProp(v))) { EmitHit(s, v.Chest); OnMinionHit?.Invoke(v.minionId, dmg, s); OnDealt(f, dmg, v, s); return; }
            Hit(v, new Projectile { owner = f, skill = s, pos = v.Chest, dir = Fighter.Flat(v.head - f.head), damage = dmg, effect = eff, effectDps = dps, effectDur = dur });
        }

        /// <summary>f 的敵人（活著的）</summary>
        /// <summary>準星方向（35° 內、射程內）最近的活著的敵人；沒有＝null</summary>
        Fighter AimedFoe(Fighter f, Vector3 aimDir, float range)
        {
            var d = Fighter.Flat(aimDir); if (d == Vector3.zero) return null; d.Normalize();
            Fighter best = null; float bestAng = 35f;
            foreach (var v in FoesOf(f))
            {
                var to = Fighter.Flat(v.Feet - f.Feet); float dist = to.magnitude;
                if (dist > range + 1f || dist < 0.01f) continue;
                float ang = Vector3.Angle(d, to);
                if (ang < bestAng) { bestAng = ang; best = v; }
            }
            return best;
        }

        /// <summary>射程內離 f 最近的敵人（沒有＝null）</summary>
        Fighter NearestFoeInRange(Fighter f, float range)
        {
            Fighter best = null; float bestD = range + 1f;
            foreach (var v in FoesOf(f)) { float d = FlatDistance(v.Feet, f.Feet); if (d < bestD) { bestD = d; best = v; } }
            return best;
        }

        List<Fighter> FoesOf(Fighter f)
        {
            var list = new List<Fighter>();
            if (FoeSide(f)) { foreach (var pl in Players()) if (pl.Alive) list.Add(pl); foreach (var n in npcs) if (n.Alive && n.npcSide != 1) list.Add(n); }
            else if (f.npcSide >= 0) list.AddRange(NpcFoes(f));
            else { if (enemy != null && enemy.Alive) list.Add(enemy); foreach (var m in minions) if (m.Alive) list.Add(m); foreach (var n in npcs) if (n.Alive && n.npcSide >= 1) list.Add(n); }
            return list;
        }

        /// <summary>玩家方的範圍攻擊：center 半徑 r 內的敵人全部受傷（hits 段）</summary>
        void AreaFoes(Fighter f, SkillDef s, Vector3 center, float r, int dmg)
        {
            for (int k = 0; k < Mathf.Max(1, s.hits); k++)
            {
                Action strike = () =>
                {
                    if (!f.Alive) return;
                    var c = s.atTarget ? center : f.Feet;   // 旋風斬跟著自己轉
                    foreach (var v in FoesOf(f))
                        if (InAreaShape(s, v, c, r)) HitFoe(f, v, s, dmg, s.effect, s.effectDps, s.effectDur);
                };
                if (k == 0) strike(); else After(k * s.hitGap, () => { if (f.Alive) Emit("aoetick", s.atTarget ? center : f.Feet, s.color, s.id); strike(); });   // 第二段起：範圍特效再播一次
            }
        }

        // ---- 召喚物
        Pet SpawnPet(Fighter f, SkillDef s, string kind, float life)
        {
            if (kind != "clone")
            {
                // 最多同時 MaxPets 隻：超過就讓最舊的消失
                var mine = pets.FindAll(x => x.alive && !x.remote && x.owner == f && x.kind != "clone");
                for (int i = 0; i <= mine.Count - MaxPets; i++) KillPet(mine[i]);
            }
            float power = f.atkMul / Mathf.Max(0.1f, Advanced.Get(f.adv)?.atkMul ?? 1f);   // 召喚物不吃「本人傷害 −30%」
            var side = Vector3.Cross(Vector3.up, Fighter.Flat(f.forward)).normalized;
            var p = new Pet
            {
                id = nextId++, owner = f, kind = kind, until = now + life, next = now + 0.8f,
                dmg = Mathf.RoundToInt(s.damage * power), blast = Mathf.RoundToInt(Skills.All["pet_blast"].damage * power),
                range = s.petRange, interval = s.petInterval, hp = s.petHp, maxHp = s.petHp,
            };
            if (p.maxHp <= 0f && kind != "clone") { p.maxHp = 40f; p.hp = 40f; }   // 沒寫血量的召喚物：40（餵養時回的血才有意義）
            if (kind == "fairy") p.dmg = Mathf.RoundToInt(s.heal * power);         // 妖精：每次回的血
            switch (kind)
            {
                case "golem": p.home = f.Feet + Fighter.Flat(f.forward) * 1f; p.pos = p.home + Vector3.up * 0.6f; break;
                case "clone": p.home = -side * 1.1f; p.pos = f.Feet + p.home + Vector3.up * 0.9f; break;
                case "falcon": p.pos = f.head + Vector3.up * 1f; break;
                case "ice": p.pos = f.head - side * 0.5f; break;
                case "fairy": p.pos = f.head + Vector3.up * 0.5f; break;
                default: p.pos = f.head + side * 0.5f; break;
            }
            pets.Add(p);
            Emit("petspawn", p.pos, s.color, kind);
            if (f == player) OnPetSpawn?.Invoke(p);
            return p;
        }

        void KillPet(Pet p)
        {
            if (!p.alive) return;
            p.alive = false;
            if (p.body != null && p.body.Alive) { p.body.hp = 0f; Emit("buff", p.body.Chest, new Color(0.5f, 0.9f, 0.3f), $"{p.body.name}回到野外了"); }   // 收服的魔物：時間到就離開
            Emit("petgone", p.pos, Color.white, p.kind);
            if (!p.remote && p.owner == player) OnPetGone?.Invoke(p);
        }

        /// <summary>別人的召喚物（只顯示；跟著它的主人動）</summary>
        public void AddRemotePet(int id, string kind, Fighter owner, Vector3 pos, float life)
        {
            if (owner == null) return;
            pets.Add(new Pet { id = id, owner = owner, kind = kind, pos = pos, home = kind == "golem" ? new Vector3(pos.x, 0f, pos.z) : pos - owner.Feet - Vector3.up * 0.9f, until = now + life, remote = true });
            Emit("petspawn", pos, Color.white, kind);
        }
        public void RemoveRemotePet(int id) { foreach (var p in pets) if (p.remote && p.id == id && p.alive) { p.alive = false; Emit("petgone", p.pos, Color.white, p.kind); } }

        void UpdatePets(float dt)
        {
            foreach (var p in pets)
            {
                if (!p.alive) continue;
                var o = p.owner;
                if (now > p.until || o == null || (!o.Alive && p.kind != "golem") || (p.kind == "clone" && !p.remote && o.cloneUntil <= now)) { KillPet(p); continue; }
                if (p.body != null)
                {
                    // 收服的魔物：走路、出手交給友方人物的 AI（Combat.Npc.cs）；倒下＝召喚物消失
                    if (!p.body.Alive) { KillPet(p); continue; }
                    p.pos = p.body.Chest; p.hp = p.body.hp; p.maxHp = p.body.maxHp;
                    continue;
                }
                var side = Vector3.Cross(Vector3.up, Fighter.Flat(o.forward)).normalized;
                // 位置：火靈浮在主人肩膀旁、獵鷹在頭上盤旋、石像站著不動、分身站在主人旁邊
                Vector3 want = p.kind switch
                {
                    "fire" => o.head + side * 0.55f + Vector3.up * 0.1f,
                    "falcon" => o.head + Vector3.up * 1.2f + new Vector3(Mathf.Cos(now * 1.7f), 0f, Mathf.Sin(now * 1.7f)) * 1.2f,
                    "golem" => p.home + Vector3.up * 0.6f,
                    "ice" => o.head - side * 0.55f + Vector3.up * 0.1f,
                    "fairy" => o.head + Vector3.up * 0.45f + new Vector3(Mathf.Cos(now * 2.2f), 0f, Mathf.Sin(now * 2.2f)) * 0.35f,
                    _ => o.Feet + (p.remote ? p.home : -side * 1.1f) + Vector3.up * 0.9f,
                };
                p.pos = Vector3.Lerp(p.pos, want, 1f - Mathf.Exp(-dt * 6f));
                if (p.remote || p.kind == "clone" || now < p.next) continue;
                if (p.kind == "fairy")
                {
                    // 治癒妖精：每 interval 秒幫主人（和 3m 內的隊友）回血
                    p.next = now + p.interval;
                    int h = Mathf.Max(1, Mathf.RoundToInt(p.dmg * p.power));
                    foreach (var pl in Players())
                        if (pl.Alive && (pl == o || FlatDistance(pl.Feet, o.Feet) <= 3f) && pl.hp < pl.maxHp)
                        { pl.hp = Mathf.Min(pl.maxHp, pl.hp + h); Emit("heal", pl.Chest, new Color(1f, 0.65f, 0.85f), $"+{h}"); }
                    continue;
                }
                // 攻擊：範圍內最近的敵人
                Fighter tgt = null; float best = p.range;
                foreach (var v in FoesOf(o)) { float d = FlatDistance(p.pos, v.Feet); if (d <= best + v.bodyRadius) { best = d; tgt = v; } }
                if (tgt == null) continue;
                p.next = now + p.interval;
                var atk = p.Attack;
                int pd = Mathf.Max(1, Mathf.RoundToInt(p.dmg * p.power));   // 餵養過的召喚物更痛
                if (p.kind == "golem")
                {
                    Emit("basicmelee", tgt.Feet, atk.color, "pet_golem");
                    HitFoe(o, tgt, atk, pd);
                }
                else
                {
                    var dir = (tgt.Chest - p.pos).normalized;
                    var pr = Spawn(o, atk, p.pos, dir, pd, atk.effect, atk.effectDps, atk.effectDur, false);
                    pr.damage = pd; pr.crit = false;
                    Emit("cast", p.pos, atk.color, null);
                }
            }
            pets.RemoveAll(x => !x.alive);
        }

        /// <summary>聖騎士的守護誓言：隊友的傷害轉到我身上（隊友那支手機送來）</summary>
        public void TakeShared(float dmg, Vector3 at)
        {
            if (!player.Alive || dmg <= 0f) return;
            Emit("mitigate", player.Chest, new Color(1f, 0.9f, 0.4f), "守護誓言");
            Damage(player, dmg, "oath", at);
        }

        // ------------------------------------------------------------ 合作模式（兩人打 Boss）
        // allies＝其他玩家的代理（2～4 人合作；位置、血量、蹲下／閃避由網路更新；pid＝那支手機的編號）。
        // 主機（coopHost）跑 Boss：Boss 打到隊友 → OnAllyHit 送給隊友，由隊友自己扣血；隊友打 Boss → ApplyHitOnEnemy。
        // 隊友端：Boss 是 remoteEnemy（位置、血量由主機送來），Boss 與主機的法術都只顯示。
        public readonly List<Fighter> allies = new List<Fighter>();
        /// <summary>第一位隊友（舊程式相容用；多人時請用 allies）</summary>
        public Fighter ally => allies.Count > 0 ? allies[0] : null;
        /// <summary>人數倍率用的「隊友人數」：一般＝實際人數；副本固定（以 3 人隊伍設計，2 人吃力、4 人輕鬆）</summary>
        public int scaleAllies = -1;
        public int ScaleAllies => scaleAllies >= 0 ? scaleAllies : allies.Count;
        /// <summary>副本的浮動難度：隊伍人數（最少算 2 人；0＝不是副本）。敵人數量、Boss 血量 ×人數，小怪個別血量不另外加</summary>
        public int dungeonParty;
        public bool AnyAllyAlive { get { foreach (var a in allies) if (a.Alive) return true; return false; } }
        public Fighter AllyByPid(int pid) => allies.Find(a => a.pid == pid);
        public bool coopHost;
        public event Action<Fighter, SkillDef, int, Vector3, int> OnAllyHit;
        /// <summary>我這台判定的法術命中（不含已經用 OnRemoteHit 廣播的）：給觀眾看命中、移除那一發（合作模式用）</summary>
        public event Action<Projectile> OnProjectileHit;      // Boss 打到隊友：哪位隊友、技能、傷害、位置、法術 id
        public event Action<Vector3, float> OnDispel;                    // 我放了抗拒法環（中心、半徑）：要告訴另一支手機
        IEnumerable<Fighter> Players() { yield return player; foreach (var a in allies) yield return a; }
        public IEnumerable<Fighter> AllPlayers => Players();

        /// <summary>隊友（或主機）判定打到 Boss：套用到 Boss 身上（主機端）；by＝打的人（算仇恨）</summary>
        public void ApplyHitOnEnemy(SkillDef s, int damage, Vector3 at, bool isTrap, Fighter by = null)
        {
            if (isTrap) Emit("trap", at, s.color, $"{s.name}！");
            AddThreat(by, damage);
            incoming = s; ApplyDamageAndEffect(enemy, damage, s.effect, s.effectDps, s.effectDur, at, by); incoming = null;
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

        public static readonly string[] MinionNames = { "熔岩小鬼", "火焰射手", "岩石衝鋒獸", "自爆小鬼", "岩石投擲者", "熔岩狙擊手", "虛空術士", "毒液噴花", "荊棘衝鋒獸", "藤蔓術士", "食人花", "孢子菇", "種子槍手", "跳躍魚", "俯衝海鳥", "騎魚泡泡怪", "寄居蟹", "雷電水母", "河豚", "古根撼地獸", "潮鞭鰻", "鏈錘鬥奴", "裂岩甲蜥" };
        // 各種小怪的基本數值：血量、移動速度、身體半徑、頭的高度
        static readonly int[] MinionHp = { 20, 15, 60, 10, 23, 15, 18, 16, 30, 18, 22, 26, 14, 14, 12, 20, 45, 16, 18, 90, 80, 100, 110 };   // 血量（已下修 50%）
        public const float EnemySpeedScale = 0.3f;   // 小怪與 Boss 的移動速度倍率（調慢到 30%）
        public const float MinionRecover = 1f;       // 小怪出手後原地不動的秒數
        static readonly float[] MinionSpeed = { 1.0f, 0.9f, 0.6f, 1.7f, 0.7f, 0.8f, 0.6f, 0.7f, 0.9f, 0.6f, 1.0f, 0.8f, 0.8f, 1.2f, 1.5f, 0.7f, 0.5f, 0.6f, 0.7f, 0.5f, 0.55f, 0.55f, 0.45f }, MinionRadius = { 0.25f, 0.23f, 0.4f, 0.22f, 0.28f, 0.24f, 0.24f, 0.25f, 0.35f, 0.25f, 0.3f, 0.28f, 0.24f, 0.25f, 0.3f, 0.3f, 0.38f, 0.3f, 0.28f, 0.6f, 0.5f, 0.45f, 0.6f }, MinionHead = { 0.85f, 0.8f, 1.3f, 0.65f, 0.95f, 0.85f, 0.85f, 0.9f, 1.0f, 1.1f, 1.0f, 0.8f, 0.9f, 0.35f, 0.3f, 1.2f, 0.8f, 0.35f, 0.3f, 1.7f, 1.4f, 1.9f, 1.5f };
        // 遠程小怪想站的距離（最近、最遠）；0＝近戰（走到身邊）
        static readonly float[] KeepMin = { 0f, 3.5f, 0f, 0f, 4.5f, 6f, 6f, 4f, 0f, 3.5f, 0f, 0f, 4f, 0f, 0f, 6f, 0f, 0f, 3f, 0f, 0f, 0f, 0f }, KeepMax = { 0f, 6f, 0f, 0f, 7.5f, 9f, 12f, 7.5f, 0f, 5.5f, 0f, 0f, 8f, 0f, 0f, 9f, 0f, 0f, 4.5f, 0f, 0f, 0f, 0f };
        // 出手距離、預備時間（-1＝用 m_claw 的蓄力）
        /// <summary>遠距離小怪（攻擊距離 5m 以上：射手、狙擊手、藤蔓、毒液…）</summary>
        public static bool IsRangedMinion(int kind) => kind >= 0 && kind < MinionReach.Length && MinionReach[kind] >= 5f;
        static readonly float[] MinionReach = { 0.95f, 9f, 1.2f, 1.3f, 11f, 14f, 13f, 8f, 5f, 6f, 1.0f, 1.3f, 9f, 7f, 9f, 11f, 1.3f, 1.4f, 5f, 1.8f, 1.8f, 1.8f, 1.8f };
        static readonly float[] MinionWindup = { 1.5f, 0.8f, 1.5f, 0.5f, 1.2f, 1.6f, 1.0f, 1.0f, 0.8f, 1.0f, 0.7f, 0.8f, 0.9f, 0.6f, 0.6f, 1.2f, 0.8f, 0.6f, 1.0f, SpecialWindup, SpecialWindup, SpecialWindup, SpecialWindup };
        public const int MinionKinds = 54;   // 53＝寶箱怪（Combat.Treasure.cs）
        // ---- 第二～五章 x-4、x-5 的特殊敵人（19 古根撼地獸、20 潮鞭鰻、21 鏈錘鬥奴、22 裂岩甲蜥）：
        //      場上同時最多 1 隻、出現頻率低；大範圍招式（比 Boss 小），要跳起來（地面震環）或蹲下（胸口高度橫掃）才躲得掉，走出範圍也打不到
        public const float SpecialWindup = 1.5f;
        public static bool IsSpecial(int kind) => kind >= 19 && kind <= 22;
        /// <summary>這一章的特殊敵人（第二章＝19…第五章＝22；其他章沒有＝−1）</summary>
        public static int SpecialKindOfChapter(int chapter) => chapter >= 2 && chapter <= 5 ? 17 + chapter : -1;
        /// <summary>要跳（地面震環）還是蹲（胸口橫掃）</summary>
        public static bool SpecialNeedsJump(int kind) => kind == 19 || kind == 22;
        /// <summary>特殊敵人招式的範圍（從腳下中心往外算）：撼地 3.5m、潮鞭 3m、鏈錘 3m、裂岩 3m</summary>
        public static float SpecialRadius(int kind) => kind == 19 ? 3.5f : 3f;
        /// <summary>範圍的半角：潮鞭鰻是前方半圓（180°），其他整圈</summary>
        public static float SpecialArcHalf(int kind) => kind == 20 ? 90f : 180f;
        public static string SpecialSkillId(int kind) => kind == 19 ? "m_rootquake" : kind == 20 ? "m_tidewhip" : kind == 21 ? "m_chainsweep" : "m_twinquake";
        /// <summary>預備時地上警示的半角（一般近戰＝前方 60°；特殊敵人照招式）</summary>
        public static float WarnArcHalf(int kind) => IsSpecial(kind) ? SpecialArcHalf(kind) : MeleeFanHalf;
        /// <summary>這一關會出特殊敵人（−1＝不出）；SoloBattle 在第二～五章的 x-4、x-5 設定</summary>
        public int specialKind = -1;
        /// <summary>特殊敵人：每一波開始後多久第一次出現、上一隻還在或倒下後隔多久才會再出現</summary>
        public const float SpecialFirstDelay = 12f, SpecialGap = 35f, MiniBossHp = 2.5f;
        /// <summary>第四、五章 x-4／x-5 的特殊敵人（使用者要求）：改成這一關的第 3 波單獨登場、當小 Boss（血量 ×2.5，有名字和血條）</summary>
        public bool MiniBossWave => RhythmSpawns && specialKind >= 0 && stageEnd > 0 && wave == stageStart + 2 && !IsBossWave;
        bool miniBossSpawned;
        float nextSpecialAt;
        /// <summary>這種小怪的預備時間（秒）：預備動作要剛好在出手時擺到位</summary>
        /// <summary>近身重擊的小怪（熔岩小鬼、岩石衝鋒獸、食人花）：預備時地上畫紅色受擊範圍（中心＝小怪腳下；玩家站在圈外就打不到）</summary>
        public static bool HasMeleeWarnDisc(int kind) => kind == 0 || kind == 2 || kind == 8 || kind == 10 || kind == 16 || IsSpecial(kind);
        /// <summary>近戰小怪的身體半徑多加 0.5m：不會貼到玩家臉上（看不清楚），玩家走過去也會把牠推開；攻擊距離照舊（從中心算）</summary>
        public const float MeleeBodyExtra = 0.5f;
        /// <summary>近戰小怪的攻擊範圍：前方扇形（左右各 60°，共 120°）</summary>
        public const float MeleeFanHalf = 60f;
        /// <summary>玩家在不在這隻小怪攻擊的扇形裡（貼在身上也算）</summary>
        public static bool InMeleeFan(Fighter m, Fighter tgt)
        {
            var to = tgt.Feet - m.Feet; to.y = 0f;
            if (to.magnitude <= m.bodyRadius + tgt.bodyRadius * 0.5f) return true;
            var fw = Fighter.Flat(m.forward); if (fw == Vector3.zero) return true;
            return Vector3.Angle(fw, to) <= MeleeFanHalf;
        }
        /// <summary>小怪停下來的距離（中心到中心）：身體不重疊、也不比原本更近</summary>
        static float KeepDist(Fighter m, Fighter tgt) => Mathf.Max(MinionKeep(m.minionKind), m.bodyRadius + tgt.bodyRadius + 0.05f);
        /// <summary>近身重擊打得到多遠（小怪腳下到玩家身體中心；已含玩家身體半徑）</summary>
        public static float MeleeStrikeRadius(int kind) => IsSpecial(kind) ? SpecialRadius(kind) : MeleeReach(kind, kind == 2 ? 1.6f : kind == 10 ? Skills.All["m_bite"].rangeMax + 0.2f : kind == 16 ? Skills.All["m_pincer"].rangeMax : Skills.All["m_claw"].rangeMax) + 0.1f + Fighter.BodyRadius;
        /// <summary>
        /// 教學用的控制：0＝照常、1＝定住（不動、不出手；預備中的取消）、2＝只走位不出手。null＝全部照常
        /// </summary>
        public Func<Fighter, int> minionHold;
        /// <summary>教學用：暫停出怪</summary>
        public bool holdSpawns;
        /// <summary>教學用：指定下一隻小怪的種類（回傳 −1＝照常隨機）</summary>
        public Func<int> kindOverride;
        /// <summary>教學／新手關：指定某種小怪的血量（回傳 0＝照常）</summary>
        public Func<int, float> minionHpOverride;
        /// <summary>小怪站在中間攻擊區裡了沒（教學：走進來才定住）</summary>
        public bool MinionInZone(Fighter m) => CanAttackFrom(m.Feet);
        public static float MinionWindupOf(int kind) => kind >= 0 && kind < MinionWindup.Length && MinionWindup[kind] >= 0f ? MinionWindup[kind] : Skills.All["m_claw"].charge;
        public static bool IsSea(int kind) => kind >= 13 && kind <= 18;
        /// <summary>海裡／空中的小怪：不用走進場地就能出手、可以在海上移動（寄居蟹例外，要爬上礁石）</summary>
        /// <summary>
        /// 遠程小怪想站的距離（夾在 RangedKeepCap 以內）：原本 6～9m（騎魚泡泡怪、狙擊手）在 6m×6m 場地會一直退到最邊邊，
        /// 近戰職業根本追不到；現在最遠只想站 2.5～4m，射程不變。
        /// </summary>
        public const float RangedKeepLoCap = 2.5f, RangedKeepHiCap = 4f;
        static float KeepLo(int kind) => Mathf.Min(KeepMin[kind], RangedKeepLoCap);
        static float KeepHi(int kind) => Mathf.Min(KeepMax[kind], RangedKeepHiCap);
        static bool FreeRoam(int kind) => kind == 13 || kind == 14 || kind == 15 || kind == 17 || kind == 18;
        static float BaseAlt(int kind) => kind == 14 ? 3.5f : kind == 17 ? 0.9f : kind == 18 ? 0.5f : 0f;
        public const float BirdCruise = 3.5f;
        /// <summary>海鳥在玩家上空繞圈的半徑（以前 5m，在 6m 場地會繞到場外）</summary>
        public const float BirdCircle = 2.2f;
        public const float VineStay = 3f;   // 藤蔓術士放出藤蔓後不能移動的秒數（＝藤蔓持續時間）
        public static bool IsPlant(int kind) => kind >= 7 && kind <= 12;
        /// <summary>小怪最近只走到離玩家這麼遠（約一個身高，至少 1m），免得貼在鏡頭上看不清楚；近身攻擊的距離會補到打得到這裡</summary>
        public static float MinionKeep(int kind) => Mathf.Max(1f, MinionHead[kind] + 0.1f);
        /// <summary>近身小怪的出手距離：至少要打得到停下來的位置</summary>
        static float MeleeReach(int kind, float reach) => Mathf.Max(reach, MinionKeep(kind) + 0.15f);
        public static bool IsRanged(int kind) => kind == 1 || kind == 4 || kind == 5 || kind == 6 || kind == 7 || kind == 9 || kind == 12 || kind == 15 || kind == 18;
        public const float VoidStay = 1f;   // 虛空術士召喚後停留幾秒再瞬移
        public const float CrabShellMul = 0.5f, CrabTurn = 70f;   // 寄居蟹正面減傷、每秒最多轉幾度（轉身慢，繞到背後打）
        public const float ChomperTrigger = 1.5f, SporeHealEvery = 6f, SporeHealFrac = 0.1f, SporeHealRadius = 3f, FishCycle = 4f, JellyEvery = 5f, PufferFuse = 2f, PufferRadius = 2f;
        public const float BubbleHoming = 50f, BubbleHomeTime = 3f, ThornLife = 3f, VineWarn = 2f, VineSnare = 1.5f;
        public const float VoidSummonEvery = 8f, BombFuse = 4.5f, BombSpeedMul = 2.5f, SniperAim = 2f, SniperRearm = 5f, ChargerStun = 2f, ChargerDash = 6f, ImpKnock = 0.35f;

        public Fighter SpawnMinion(Vector3 feet, int id = 0, int kind = 0, float hpMul = 1f, float dmgMul = 1f, float speedMul = 1f)
        {
            kind = Mathf.Clamp(kind, 0, MinionKinds - 1);
            var c = Skills.Classes["minion"];
            var m = new Fighter(MinionNames[kind], c, new string[0], false)
            {
                isMinion = true, minionKind = kind, bodyRadius = MinionRadius[kind] + (IsRanged(kind) ? 0f : MeleeBodyExtra), minionId = id > 0 ? id : nextId++,
                dmgMul = dmgMul * (EliteCatalog.IsElite(kind) ? EliteCatalog.Get(kind).damage : 1f), speedMul = speedMul,
            };
            m.maxHp = Mathf.RoundToInt(MinionHp[kind] * hpMul); m.hp = m.maxHp;
            m.alt = kind == 14 ? 5f : BaseAlt(kind);   // 海鳥從高空飛來
            m.head = new Vector3(feet.x, MinionHead[kind] + m.alt, feet.z);
            // 寄居蟹：正面有殼，從正面打減傷 50%（Damage 裡依打擊方向判定；繞到背後打才是全額）——使用者要求不要正面無敵
            if (kind == 14) m.speedMul *= birdSpeedMul;   // 俯衝海鳥：飛行速度倍率（故事第三章 ×0.5）
            m.attackReadyAt = now + 1.2f; m.esBornAt = now;   // 第二招：進場 2 秒後才可能出
            m.homePos = feet;
            m.accuracy = enemyAccuracy;   // 敵人的命中率（故事各章；抵掉玩家的閃避）
            if (kind == 3) { m.fuseAt = now + BombFuse; m.speedMul *= BombSpeedMul; }   // 自爆小鬼：直直衝過來，引信到就爆
            if (kind == TreasureKind) SetupTreasure(m);
            ApplyWeatherTraits(m);   // 天候讓怪物表現不同（Combat.Variants.cs）
            minions.Add(m);
            Emit("minionspawn", m.Feet, c.color, null);
            return m;
        }

        // ------------------------------------------------------------ 無盡模式
        // 一波＝一批小怪（從離玩家 15m 以上的地方陸續出現），全部打倒才進下一波；
        // 每第 10、20、30… 波是 Boss 波（只有 Boss，沒有小怪）。波次越後面敵人越強，也會解鎖新的小怪種類。
        public bool endless;
        public int wave, kills, bossKills, waveToSpawn, waveTotal;
        /// <summary>故事關卡每一波額外多出的小怪數（使用者要求：每波 +3；SoloBattle 設定）</summary>
        public int extraPerWave;
        public float nextMinionAt, waveStartAt, bossBaseHp = 150f;
        public const int BossEvery = 10;
        public Func<Vector3> edgePoint;          // 小怪出生點：場地邊緣外 1～2m（淡入出現，再走進來）
        /// <summary>依方位出生：角度＝相對玩家面向（0 正前、90 右、180 後、-90 左）的邊緣外一點（SoloBattle 提供）</summary>
        public Func<float, Vector3> edgePointAt;
        /// <summary>出怪節奏（使用者要求）：每波三拍——前排正面 → 3 秒後後排側面 → 8 秒後特殊怪背後；場上同時最多 rhythmMaxAlive 隻（簡單 3、其他 4），
        /// 角色不重複，倒了才補同一角色。第一章 1-1～1-3 只用前兩拍。副本地圖、陷阱迴廊、教學（kindOverride）照原本的出怪。</summary>
        public int rhythmMaxAlive = 4;
        /// <summary>敵人（小怪、Boss）的命中率：抵掉玩家的閃避（SoloBattle 依故事章節設定）</summary>
        public float enemyAccuracy;
        /// <summary>單面進場（使用者要求：所有難度）：每一波挑一面（七成正面、三成左或右），這一波的敵人都從那一面進場（三拍 ±25°、輪次陣形 ±30°）；
        /// 偶爾一隻從別的方向偷襲（同時最多一隻）</summary>
        public bool easySides;
        int easyBearingWave = -1, ambushWave = -1; float easyBearing;   // ambushWave：這一波已經有一隻從別面偷襲過（一波最多一隻）
        const float EasyAmbushChance = 0.12f;
        public const float Beat2At = 3f, Beat3At = 8f, RoleRefillGap = 2.5f;
        bool RhythmSpawns => endless && !FixedDungeonSpawns && !Gallery && kindOverride == null && waveSeq == null;
        public enum MinionRole { Front, Back, Special }
        /// <summary>小怪在一波裡的角色：前排（小鬼、食人花、孢子菇、寄居蟹）、後排（所有遠程、水母）、特殊（衝鋒獸、自爆、術士、魚、鳥、特殊敵人、菁英）</summary>
        public static MinionRole RoleOf(int kind)
        {
            if (IsSpecial(kind) || EliteCatalog.IsElite(kind) || kind >= 48) return MinionRole.Special;
            if (kind == 2 || kind == 3 || kind == 6 || kind == 8 || kind == 13 || kind == 14) return MinionRole.Special;
            if (IsRanged(kind) || kind == 17) return MinionRole.Back;
            return MinionRole.Front;
        }
        readonly float[] roleNextAt = new float[3];
        readonly List<Vector3> pendingSummons = new List<Vector3>();   // 虛空術士這一幀要召喚的小鬼位置
        public Func<Vector3> bossPoint;          // Boss 出生點：場地邊緣外 1～2m（沒設定就用 edgePoint）
        public const float MinionSpawnDistance = 15f;
        public event Action<int> OnBossSpawn;    // 無盡模式 Boss 出現（第幾隻 Boss，0 起算）

        public bool IsBossWave => wave > 0 && wave % BossEvery == 0;
        /// <summary>下一個 Boss 波</summary>
        public int NextBossWave => (wave / BossEvery + (IsBossWave ? 0 : 1)) * BossEvery;
        /// <summary>這一波還剩幾隻（還沒出現的＋場上活著的）</summary>
        public int WaveRemaining => IsBossWave && !bossPending ? (enemy.Alive ? 1 : 0) : waveToSpawn + BlockingMinions;

        // 故事模式的關卡：打第 firstWave～lastWave 波（同一套無盡模式的波次、主題、Boss），打完就通關
        public int stageEnd = -1, stageStart = 1;
        // 副本：照指定的波次順序打（每個房間一波，可以跨主題），打完最後一個房間就通關
        public int[] waveSeq; public int seqIdx;
        string WaveLabel => waveSeq != null ? $"房間 {seqIdx}/{waveSeq.Length}" : stageEnd > 0 ? $"第 {wave - stageStart + 1}/{stageEnd - stageStart + 1} 波" : $"第 {wave} 波";
        public bool stageCleared;
        /// <summary>故事關卡的敵人數量倍率（初級地區＝熔岩地帶 0.5；其他 1）</summary>
        public float stageEnemyMul = 1f;
        /// <summary>遠程／近戰小怪的數量倍率（故事第三章：遠程減半、近戰與飛鳥加倍）：總數跟著變，遠程的比例也跟著變</summary>
        public float rangedCountMul = 1f, meleeCountMul = 1f;
        /// <summary>遠程小怪：出手前的準備時間倍率、射出去的飛行速度倍率（故事第二章：準備 ×1.5、飛行 ×0.5）</summary>
        public float rangedWindupMul = 1f, rangedShotSpeedMul = 1f;
        /// <summary>所有小怪出手前的預備時間倍率（紅圈閃爍的時間；故事第三章 ×2）</summary>
        public float minionWindupMul = 1f;
        /// <summary>俯衝海鳥的移動速度倍率（飛行、俯衝；故事第三章 ×0.5）</summary>
        public float birdSpeedMul = 1f;
        /// <summary>
        /// 補怪間隔（秒；0＝照常）：同時在場的一批出齊之後，每打倒一隻要等這麼久才補下一隻（一次補一隻），直到這一波全部打倒（故事第三章 20 秒）
        /// </summary>
        public float refillDelay;
        /// <summary>故事關卡：場上同時最多幾隻小怪（Boss 不算；5 × 人數）；0＝照公式</summary>
        public int storyMaxAlive;
        int pendingRefill;   // 到時間但場上滿了（還沒補）的隻數
        bool batchOut; int lastMinionCount;
        readonly Queue<float> refillAt = new Queue<float>();   // 每打倒一隻：排定 refillDelay 秒後補一隻
        /// <summary>這種小怪這一場的預備時間（含倍率）</summary>
        public float WindupOf(int kind) => MinionWindupOf(kind) * minionWindupMul;
        /// <summary>近戰小怪血量倍率；遠程小怪每次出手後多休息幾秒（故事第二章：近戰 ×0.7、遠程 +3 秒）</summary>
        public float meleeHpMul = 1f, rangedRecoverAdd;
        /// <summary>同一波裡每批小怪之間多等幾秒（故事第三章 +5 秒）</summary>
        public float spawnGapAdd;
        float CountMixMul => RangedShare * rangedCountMul + (1f - RangedShare) * meleeCountMul;
        float EffectiveRangedShare => RangedShare * rangedCountMul / Mathf.Max(0.01f, CountMixMul);

        public void StartStage(int firstWave, int lastWave)
        {
            treasureStarts++;   // 寶箱怪：新的一關重新擲
            endless = true; kills = 0; bossKills = 0; stageCleared = false;
            bossAfterMinions = true;   // 故事關卡：Boss 波先來一整波小怪，全部打倒後 Boss 才現身
            waveSeq = null;
            wave = Mathf.Max(0, firstWave - 1); stageStart = firstWave; stageEnd = Mathf.Max(firstWave, lastWave);
            enemy.hp = 0f;
            NextWave(3f);
        }
        /// <summary>
        /// 副本地圖（房主選路線）：開打後先等 EnterNode 指定第一個地點；每個地點打完 → awaitingNext，
        /// 等房主選下一站（或寶箱開完）再 EnterNode；final＝首領地點，打完就通關。
        /// </summary>
        public void StartDungeonMap()
        {
            treasureStarts++;   // 寶箱怪：新的一關重新擲
            endless = true; kills = 0; bossKills = 0; stageCleared = false; stageFailed = false;
            bossAfterMinions = true;
            waveSeq = null; stageEnd = -1; manualWaves = true; awaitingNext = true;
            enemy.hp = 0f;
        }
        public bool manualWaves, awaitingNext, finalRoom, stageFailed;
        int queuedWave;
        /// <summary>前往下一個戰鬥地點：wave 決定敵人強度，final＝最後的首領</summary>
        public void EnterNode(int nodeWave, bool final, float delay = 3f)
        {
            CleanupRoomEvent();
            treasureRooms++;   // 寶箱怪：副本每個地點算一關
            queuedWave = Mathf.Max(1, nodeWave); finalRoom = final; awaitingNext = false; bossSpawnedThisWave = false;
            enemy.hp = 0f;
            NextWave(delay);
        }
        /// <summary>保護目標倒下：副本失敗</summary>
        public void FailStage(string why) { if (stageFailed || stageCleared) return; stageFailed = true; Emit("stagefail", player.Feet, Color.red, why); }
        public void StartDungeon(int[] waves)
        {
            endless = true; kills = 0; bossKills = 0; stageCleared = false;
            bossAfterMinions = true;   // 首領房：先清一整波小怪，首領才現身
            waveSeq = waves; seqIdx = 0; stageEnd = -1;
            enemy.hp = 0f;
            NextWave(3f);
        }

        bool StageDone()
        {
            if (manualWaves)
            {
                // 副本地圖：一般地點打完 → 等房主選下一站；首領地點打完 → 通關
                if (!finalRoom) { awaitingNext = true; return true; }
                stageCleared = true;
                Emit("stageclear", player.Feet, Color.white, "副本完成！");
                return true;
            }
            if (waveSeq != null)
            {
                if (seqIdx < waveSeq.Length) return false;
                stageCleared = true;
                Emit("stageclear", player.Feet, Color.white, "副本完成！");
                return true;
            }
            if (stageEnd <= 0 || wave < stageEnd) return false;
            stageCleared = true;
            Emit("stageclear", player.Feet, Color.white, "關卡完成！");
            return true;
        }

        public void StartEndless()
        {
            treasureStarts++;   // 寶箱怪：新的一關重新擲
            endless = true; wave = 0; kills = 0; bossKills = 0; waveSeq = null;
            enemy.hp = 0f;                        // 一開始沒有 Boss
            NextWave(3f);
        }

        /// <summary>進入下一波（delay 秒後開始出怪／Boss 出現）</summary>
        void NextWave(float delay)
        {
            if (manualWaves) wave = queuedWave;
            else if (waveSeq != null && waveSeq.Length > 0) wave = waveSeq[Mathf.Min(seqIdx++, waveSeq.Length - 1)];
            else wave++;
            dungeonSpawnOrdinal = 0;
            eliteLeft = eliteWaves && wave % 5 == 0 && !IsBossWave ? 1 : 0;   // 無盡塔：第 5、15、25…波有一隻精銳
            waveStartAt = now + delay;
            bossPending = false;
            if (IsBossWave && !bossAfterMinions) { waveToSpawn = 0; waveTotal = 1; }
            else
            {
                bossPending = IsBossWave;   // 故事關卡的 Boss 波：先清小怪
                // 每波的小怪數：6＋波次（最多 28），合作 ×1.5
                waveTotal = Mathf.Min(28, 6 + wave);
                if (dungeonParty > 0) waveTotal = Mathf.CeilToInt(waveTotal * dungeonParty);   // 副本：敵人數量 ×人數（最少 2）
                else if (ScaleAllies > 0) waveTotal = Mathf.CeilToInt(waveTotal * (1f + 0.5f * ScaleAllies));
                waveTotal = Mathf.Max(1, Mathf.CeilToInt(waveTotal * stageEnemyMul * CountMixMul));
                waveToSpawn = waveTotal;
                if (GroupMode)
                {
                    // 輪次出怪：一波 2～3 輪，每輪 groupSize 隻（一次出齊、全部打完才出下一輪）
                    roundsInWave = 2 + rng.Next(2); roundNo = 0;
                    waveTotal = roundsInWave * groupSize; waveToSpawn = waveTotal;
                }
                if (extraPerWave > 0) { waveTotal += extraPerWave; waveToSpawn = waveTotal; if (GroupMode && groupSize > 0) roundsInWave = Mathf.CeilToInt(waveTotal / (float)groupSize); }   // 故事：每波多幾隻（輪次出怪：多出來的再排一輪）
            }
            groupQueue.Clear(); roundWaiting = false; nextRoundAt = waveStartAt;
            nextMinionAt = waveStartAt;
            BonusOnWaveStart();
            nextSpecialAt = waveStartAt + SpecialFirstDelay;
            miniBossSpawned = false;
            if (MiniBossWave) { waveTotal = 1; waveToSpawn = 1; nextSpecialAt = waveStartAt; }
            batchOut = false; lastMinionCount = 0; refillAt.Clear(); pendingRefill = 0;
            SetupHazards();   // 角鬥場：這一波的陷阱配置；山道：落石計時（Combat.Hazards.cs）
            Emit("wavestart", player.Feet, Color.white, IsBossWave ? (bossPending ? $"{WaveLabel}：{waveTotal} 隻小怪…（最深處有什麼在等著）" : $"{WaveLabel}：Boss 來了！") : MiniBossWave ? $"{WaveLabel}：小 Boss「{MinionNames[specialKind]}」單獨登場！" : $"{WaveLabel}：{waveTotal} 隻小怪");
        }

        // 經驗值：小怪依種類與波次、Boss 依第幾隻；合作時兩人共用同一份經驗（各自選升級）
        public int xp;
        static readonly int[] MinionXp = { 10, 12, 25, 12, 15, 18, 20, 18, 20, 20, 12, 16, 16, 16, 18, 20, 22, 16, 18, 80, 80, 90, 90 };
        /// <summary>升到下一級需要的累積經驗：55、140、255、400…</summary>
        public static int XpForLevel(int level) => 40 * (level - 1) + 15 * (level - 1) * (level - 1) + 0;
        public static int LevelOf(int xp) { int l = 1; while (xp >= XpForLevel(l + 1)) l++; return l; }
        /// <summary>參與人數倍率：每多一位玩家，敵人的血量與攻擊力 +30%</summary>
        public float PartyMul => dungeonParty > 0 ? 1f : 1f + 0.3f * ScaleAllies;
        /// <summary>Boss 血量的人數倍率：每多一位玩家 +50%</summary>
        public float BossPartyMul => dungeonParty > 0 ? dungeonParty : 1f + 0.5f * ScaleAllies;
        int Tier => Mathf.Max(0, wave - 1 + levelBoost);
        /// <summary>難度加的等級（故事關卡、副本：簡單 0、普通 +40、困難 +80）：敵人的血量、攻擊、速度照「波次＋這個數」成長</summary>
        public int levelBoost;
        /// <summary>難度加的等級對 Boss 的倍率（Boss 的血量不跟波次走，另外乘）</summary>
        public float BoostHpMul => 1f + HpPerWave * levelBoost;
        public float BoostDmgMul => (0.75f + DmgPerWave * levelBoost) / 0.75f * enemyDmgMul;
        /// <summary>故事關卡的 Boss：指定的血量（簡單難度、1 人；普通／困難再照等級加成）與攻擊倍率（不吃小怪的 enemyDmgMul）；0＝照舊</summary>
        public float storyBossHp, storyBossDmg;
        /// <summary>所有 Boss 的血量再 ×70%（使用者要求；故事、無盡、無盡塔、副本、討伐 Boss、合作都算）</summary>
        public const float BossHpCut = 0.7f;
        /// <summary>
        /// 攻擊區：場地中間 2×attackHalf 公尺的方形（世界座標＝場地座標，場地中心是原點）。小怪要先走進這一區才會出手，
        /// 進來之後就待在這一區裡（場地本身還是 10m×10m，玩家可以走動閃避）。0＝不限制（整個場地都能出手）
        /// </summary>
        public float attackHalf;
        public const float AttackZoneHalf = 3f;
        bool InAttackZone(Vector3 p) => attackHalf <= 0f || (Mathf.Abs(p.x) <= attackHalf && Mathf.Abs(p.z) <= attackHalf);
        /// <summary>把一點拉回場內（攻擊區）：往 toward 一步步靠過去，直到在場內</summary>
        Vector3 PullInside(Vector3 p, Vector3 toward)
        {
            p = ClampToZone(p);
            for (int i = 0; i < 20 && minionInside != null && !minionInside(p); i++) p = Vector3.Lerp(p, toward, 0.2f);
            return p;
        }
        bool CanAttackFrom(Vector3 p) => (minionInside == null || minionInside(p)) && InAttackZone(p);
        /// <summary>攻擊區裡離 p 最近的一點（邊界往內 0.3m）</summary>
        Vector3 ClampToZone(Vector3 p) { if (attackHalf <= 0f) return p; float h = attackHalf - 0.3f; return new Vector3(Mathf.Clamp(p.x, -h, h), 0f, Mathf.Clamp(p.z, -h, h)); }
        public float BossDmgMul => (storyBossDmg > 0f ? (0.75f + DmgPerWave * levelBoost) / 0.75f * storyBossDmg : BoostDmgMul) * TowerMul;
        public float HpMul => (1f + HpPerWave * Tier) * PartyMul * hpBoost * enemyHpMul * TowerMul;
        /// <summary>無盡塔第 11 波（層）以上：敵人血量、傷害 ×1.5（Boss 也是）</summary>
        public const int TowerHardWave = 11; public const float TowerHardMul = 1.5f;
        public float TowerMul => eliteWaves && wave >= TowerHardWave ? TowerHardMul : 1f;
        /// <summary>這一關敵人的額外倍率（故事第二章森林：血量、攻擊 ×2，Boss 血量 ×5）</summary>
        public float enemyHpMul = 1f, enemyDmgMul = 1f, bossHpMul = 1f;
        // 每過一波敵人變強多少（2026-10 重調：原本 HP 8%、攻擊 5%，第二章起比玩家成長快太多，Lv18 撐不過一波；現在 HP 3.5%、攻擊 2%）
        public const float HpPerWave = 0.035f, DmgPerWave = 0.02f;
        /// <summary>故事關卡的敵人數量倍率：每一章都減半（使用者要求，和第一章一樣）</summary>
        /// <summary>故事關卡每波的小怪數量倍率：第一、二章 0.5；第三章以後 0.3（使用者要求：少一點、但每隻更痛，靠技術躲）</summary>
        public static float StoryEnemyMul(int theme) => theme >= 2 ? 0.3f : 0.5f;
        public float hpBoost = 1f;   // 轉職試煉：敵人更硬
        public float DmgMul => (EnemyDmgBase + MinionDmgPerTier * Tier) * enemyDmgMul * TowerMul;
        /// <summary>小怪每一級（波次＋等級加成）多多少攻擊（使用者要求調降：0.02 → 0.012；Boss 照舊用 DmgPerWave）</summary>
        public const float MinionDmgPerTier = 0.012f;
        /// <summary>小怪出手的間隔倍率（使用者要求：攻擊頻率調慢一些）：普攻、第二招的冷卻都 ×這個</summary>
        public const float MinionAttackGapMul = 1.2f;
        /// <summary>小怪傷害的基底（0.75 → 0.9 → 0.8：使用者回報第 4 章被打兩下就死）</summary>
        public const float EnemyDmgBase = 0.8f;
        public const float EndlessRegen = 0.5f, EndlessRegenPct = 0.005f;
        /// <summary>副本、故事（使用者要求）：沒有每秒自然回血；技能、被動（劍士、主教的自然回復）、精魄、藥劑、藥水照常</summary>
        public bool noNaturalRegen;   // 無盡／故事：玩家每秒自然回血（0.5 或最大 HP 的 0.5%，取大的）
        public float SpeedMul => Mathf.Min(1.5f, 1f + 0.02f * Tier);

        float Time_dt;
        void UpdateEndless()
        {
            if (endless && player.Alive && !noNaturalRegen) player.hp = Mathf.Min(player.maxHp, player.hp + Mathf.Max(EndlessRegen, player.maxHp * EndlessRegenPct) * Time_dt);
            if (!endless || remoteEnemy || edgePoint == null) return;
            if (stageCleared) return;   // 故事關卡已經通關
            if (manualWaves && awaitingNext) return;   // 副本地圖：等房主選下一站（不能把剛打倒的首領又叫出來）
            if (!player.Alive && !AnyAllyAlive) return;
            if (now < waveStartAt) return;
            if (roomEvent != null && UpdateRoomEvent()) return;   // 副本特殊地點（Combat.RoomEvents.cs）
            if (IsBossWave && !bossPending)
            {
                if (!enemy.Alive && !bossSpawnedThisWave && now >= bossAt) SpawnBoss();
                return;
            }
            // 小怪波：照間隔陸續出現（每次兩隻）；場上同時最多 6＋波次/3×2 隻（合作 +4）
            int maxAlive = dungeonParty > 0 ? Mathf.Min(24, Mathf.Min(12, 5 + wave / 4) * dungeonParty / 2 + 2)   // 副本：同時在場也跟著人數（最多 24）
                : Mathf.Max(2, Mathf.CeilToInt((Mathf.Min(12, 5 + wave / 4) + 4 * ScaleAllies) * stageEnemyMul * CountMixMul));   // 同時在場的上限（第 1 波 5 隻、每 4 波 +1，最多 12）
            if (storyMaxAlive > 0) maxAlive = Mathf.Min(maxAlive, storyMaxAlive);   // 故事：最多 5 隻 × 人數
            int due = 0;
            if (refillDelay > 0f)
            {
                // 補怪：一批出齊之後，每打倒一隻 → refillDelay 秒後補一隻（場上最多 maxAlive）
                if (minions.Count >= maxAlive || waveToSpawn == 0) batchOut = true;
                if (batchOut) for (int i = minions.Count; i < lastMinionCount; i++) refillAt.Enqueue(now + refillDelay);
                lastMinionCount = minions.Count;
                while (refillAt.Count > 0 && refillAt.Peek() <= now) { refillAt.Dequeue(); due++; }
                pendingRefill += due; due = pendingRefill;
            }
            bool refilling = refillDelay > 0f && batchOut;
            if (holdSpawns) nextMinionAt = Mathf.Max(nextMinionAt, now);
            else if (GroupMode) GroupSpawn();
            else if (RhythmSpawns) RhythmSpawn(Mathf.Min(maxAlive, rhythmMaxAlive));
            else if (waveToSpawn > 0 && (refilling ? due > 0 : now >= nextMinionAt) && minions.Count < maxAlive)
            {
                for (int k = 0; k < (refilling ? due : 2) && waveToSpawn > 0 && minions.Count < maxAlive; k++, pendingRefill = refilling ? pendingRefill - 1 : pendingRefill)   // 補怪：到時間的那幾隻
                {
                    int ordinal = dungeonSpawnOrdinal++;
                    var at = FixedDungeonSpawns ? DungeonSpawnPoint(ordinal) : edgePoint();
                    int forced = kindOverride != null ? kindOverride() : -1;
                    int kind = forced >= 0 ? forced : FixedDungeonSpawns ? DungeonMinionKind(ordinal) : PickMinionKind();
                    kind = EliteSpawnKind(kind, ordinal, forced >= 0);
                    var nm = SpawnMinion(at, 0, kind, HpMul * (IsRanged(kind) ? 1f : meleeHpMul), DmgMul, SpeedMul);
                    float fixHp = minionHpOverride != null ? minionHpOverride(kind) : 0f;
                    if (fixHp > 0f) { nm.maxHp = Mathf.Max(1, Mathf.RoundToInt(fixHp)); nm.hp = nm.maxHp; }
                    if (eliteLeft > 0 && !EliteCatalog.IsElite(kind)) { eliteLeft--; MakeElite(nm); }
                    if (Theme == CanyonTheme) StartCliffLeap(nm);   // 黃土山道：從崖頂跳進場內
                    waveToSpawn--;
                }
                nextMinionAt = now + Mathf.Max(2.5f, 5f - 0.1f * wave) * (ScaleAllies > 0 ? Mathf.Max(0.5f, 1f - 0.25f * ScaleAllies) : 1f) + spawnGapAdd;
                lastMinionCount = minions.Count;
            }
            UpdateSpecialSpawn();
            UpdateTreasureSpawn();   // 寶箱怪（Combat.Treasure.cs）
            UpdateVariantSpawns();   // 夜晚的刺客怪、天候怪（Combat.Variants.cs）
            // 故事關卡的 Boss 波：小怪全部打倒 → 地面震動，Boss 現身
            if (bossPending && waveToSpawn == 0 && BlockingMinions == 0 && !HostileNpcAlive)
            {
                bossPending = false; bossAt = now + BossEntranceDelay;
                Emit("bossincoming", player.Feet, new Color(1f, 0.5f, 0.3f), "大地在震動……");
                return;
            }
            // 這一波全部打倒：休息 4 秒進下一波（血量不會自己回：用藥水、治療技能，劍士可以點自然回復）
            if (waveToSpawn == 0 && BlockingMinions == 0 && !HostileNpcAlive)
            {
                if (roomEvent != null) { FinishRoomEvent(roomEvent == "defend" ? "守住了！" : "清空了！"); return; }   // 守住目標、崩塌、黑暗房：清完這一波就過關
                BonusOnWaveCleared();   // 小目標：幾秒清完第一波
                Emit("wavecleared", player.Feet, Color.white, $"{WaveLabel}清空！");
                if (!StageDone()) NextWave(RhythmSpawns ? 3f : 4f);
            }
        }
        /// <summary>
        /// 三拍出怪：每個角色有目標數（前排 2、後排 1～2、特殊 1）和開始時間（0／3／8 秒）；少於目標、到了時間、場上沒滿就補一隻，
        /// 同一角色補完要隔 RoleRefillGap 秒。前排從正面進、後排從側面（左右隨機）、特殊怪從背後。
        /// </summary>
        // ------------------------------------------------------------ 輪次出怪（故事第二章以後；使用者要求）
        // 每輪一次出齊 groupSize 隻（簡單 4、普通 5、困難 6），排成一種陣形；全部打完停 3 秒才出下一輪；一波 2～3 輪。
        public int groupSize;
        public bool GroupMode => RhythmSpawns && groupSize > 0 && !MiniBossWave;
        public const float RoundGap = 3f, RoundStagger = 0.3f;
        int roundsInWave, roundNo, lastFormation = -1; bool roundWaiting; float nextRoundAt;
        readonly List<(float at, MinionRole role, float ang, bool ambush, bool healer, Vector3 pos, MarchGroup march)> groupQueue = new List<(float, MinionRole, float, bool, bool, Vector3, MarchGroup)>();
        /// <summary>陣形排好的出生點（pos.x 是 NaN＝沒有，照方位找場邊）</summary>
        static readonly Vector3 NoPos = new Vector3(float.NaN, 0f, 0f);
        Vector3 formationPos = NoPos;
        /// <summary>場邊的出生點：陣形排好的位置優先（用一次就清掉），否則照方位找</summary>
        Vector3 EdgeAt(float ang)
        {
            if (!float.IsNaN(formationPos.x)) { var p = formationPos; formationPos = NoPos; return p; }
            return edgePointAt != null ? edgePointAt(ang) : edgePoint();
        }
        /// <summary>陣形的間距：同一排左右隔 FormationGap 公尺；後排比前排再往外 FormationDepth、特殊怪 FormationDepth×0.5</summary>
        public const float FormationGap = 1.2f, FormationDepth = 1.4f;
        public const float OutsideRescue = 12f;
        /// <summary>軍陣：一面的一隊。整隊等全部出齊才一起往玩家推進（同一速度，隊形不變），前排推到離玩家 MarchStop 公尺停住、再散開開打</summary>
        public sealed class MarchGroup { public Vector3 inward; public float startAt = float.MaxValue, maxAdvance, released, createdAt; public int members, spawned; public bool broken; }
        public const float MarchSpeed = 0.8f, MarchStop = 2.6f, MarchHold = 0.6f, MarchMaxTime = 14f;
        readonly List<MarchGroup> marchGroups = new List<MarchGroup>();
        MarchGroup pendingMarch;

        /// <summary>軍陣行進：回傳 true＝這一幀由行進處理（不出手）</summary>
        bool UpdateMarch(Fighter m, float dt)
        {
            var g = m.march;
            if (g == null) return false;
            if (m.hp < m.marchHp - 0.01f) g.broken = true;   // 隊伍裡有人被打 → 整隊散開迎戰
            float adv = now < g.startAt ? 0f : Mathf.Min(g.maxAdvance, (now - g.startAt) * MarchSpeed);
            bool arrived = now >= g.startAt && adv >= g.maxAdvance;
            if (arrived && g.released <= 0f) g.released = now + MarchHold;
            if (g.broken || (g.released > 0f && now >= g.released) || now - g.createdAt > MarchMaxTime + 4f) { m.march = null; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.3f); return false; }
            var want = m.marchSpawn + g.inward * adv;
            var to = want - m.Feet; to.y = 0f;
            float step = Mathf.Min(to.magnitude, (MarchSpeed * 1.6f) * dt);
            if (to.sqrMagnitude > 1e-6f) m.head += to.normalized * step;
            var face = Fighter.Flat(player.Feet - m.Feet); if (face != Vector3.zero) m.forward = face;
            m.windupUntil = 0f;
            return true;
        }

        /// <summary>
        /// 把這一輪排成真的陣形（使用者回報「敵方陣形看起來很差」：以前每一隻各自在場邊隨機找點，又被壓到同一面，看起來像一團）：
        ///   依方位分成幾隊（正面、左、右、背後；簡單難度全部在同一面），每隊在場邊找一個中心點，
        ///   前排一字排開、後排在前排後面錯開半格、特殊怪在中間偏後；出場順序也是前排先、後排再、特殊怪最後。
        /// </summary>
        void LayoutFormation(List<(MinionRole role, float ang, bool amb)> slots, out List<Vector3> pos, out List<int> order, out List<MarchGroup> grp)
        {
            pos = new List<Vector3>(); order = new List<int>(); grp = new List<MarchGroup>();
            for (int i = 0; i < slots.Count; i++) grp.Add(null);
            for (int i = 0; i < slots.Count; i++) pos.Add(NoPos);
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].amb) continue;   // 偷襲的那一隻照舊從別面
                int key = Mathf.RoundToInt(Mathf.DeltaAngle(easySides ? easyBearing : 0f, slots[i].ang) / 70f);
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<int>();
                g.Add(i);
            }
            var origin = player.Feet; origin.y = 0f;
            float depthK = Theme == ArenaTheme ? 0.6f : 1f;   // 角鬥場：場邊外面是看台牆，不要排太遠
            foreach (var kv in groups)
            {
                float center = 0f; foreach (var i in kv.Value) center += Mathf.DeltaAngle(0f, slots[i].ang); center /= kv.Value.Count;
                var anchor = edgePointAt != null ? edgePointAt(center) : edgePoint(); anchor.y = 0f;
                var outward = anchor - origin; outward.y = 0f; if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward; outward.Normalize();
                var right = new Vector3(outward.z, 0f, -outward.x);
                var mg = new MarchGroup { inward = -outward, members = kv.Value.Count, createdAt = now };
                mg.maxAdvance = Mathf.Max(0f, FlatDistance(anchor, origin) - MarchStop);
                marchGroups.Add(mg);
                int[] rowN = new int[3];
                foreach (var i in kv.Value)
                {
                    int row = slots[i].role == MinionRole.Front ? 0 : slots[i].role == MinionRole.Back ? 1 : 2;
                    int k = rowN[row]++;
                    float lat = row == 2 ? (k % 2 == 0 ? -1f : 1f) * (1.5f + k / 2) * FormationGap   // 特殊怪：在兩翼（不擋在前排正後面）
                        : (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * FormationGap + (row == 1 ? FormationGap * 0.5f : 0f);   // 0、+1、−1、+2…；後排錯開半格
                    float depth = row == 0 ? 0f : row == 1 ? FormationDepth : FormationDepth * 0.5f;
                    pos[i] = anchor + right * lat + outward * depth * depthK;
                    grp[i] = mg;
                }
            }
            // 出場順序：前排 → 後排 → 特殊怪（偷襲的最後）
            for (int pass = 0; pass < 4; pass++)
                for (int i = 0; i < slots.Count; i++)
                {
                    int r = slots[i].amb ? 3 : slots[i].role == MinionRole.Front ? 0 : slots[i].role == MinionRole.Back ? 1 : 2;
                    if (r == pass) order.Add(i);
                }
        }

        /// <summary>陣形：名字、每一隻的（角色、方位角＝相對玩家面向）。輪到幾隻就取前幾個</summary>
        public static readonly (string name, (MinionRole role, float ang)[] slots)[] Formations =
        {
            ("前壓後射", new[] { (MinionRole.Front, -20f), (MinionRole.Front, 20f), (MinionRole.Back, -55f), (MinionRole.Back, 55f), (MinionRole.Front, 0f), (MinionRole.Special, 0f) }),
            ("左右包夾", new[] { (MinionRole.Front, -85f), (MinionRole.Front, 85f), (MinionRole.Back, -100f), (MinionRole.Back, 100f), (MinionRole.Special, 0f), (MinionRole.Front, -70f) }),
            ("衝鋒掩護", new[] { (MinionRole.Special, 0f), (MinionRole.Back, -30f), (MinionRole.Back, 30f), (MinionRole.Front, -15f), (MinionRole.Front, 15f), (MinionRole.Special, 40f) }),
            ("砲台陣地", new[] { (MinionRole.Back, -40f), (MinionRole.Back, 0f), (MinionRole.Back, 40f), (MinionRole.Front, 0f), (MinionRole.Front, -25f), (MinionRole.Back, 70f) }),
            ("前後夾擊", new[] { (MinionRole.Front, 0f), (MinionRole.Front, 180f), (MinionRole.Back, 20f), (MinionRole.Special, 160f), (MinionRole.Front, 20f), (MinionRole.Back, 200f) }),
            ("鐵壁護衛", new[] { (MinionRole.Front, -15f), (MinionRole.Front, 0f), (MinionRole.Front, 15f), (MinionRole.Back, -35f), (MinionRole.Back, 35f), (MinionRole.Special, 0f) }),
            ("獵殺小隊", new[] { (MinionRole.Special, -20f), (MinionRole.Special, 20f), (MinionRole.Back, -40f), (MinionRole.Back, 40f), (MinionRole.Front, 0f), (MinionRole.Front, 25f) }),
            ("蜂擁近戰", new[] { (MinionRole.Front, -30f), (MinionRole.Front, 0f), (MinionRole.Front, 30f), (MinionRole.Special, -60f), (MinionRole.Front, 60f), (MinionRole.Back, 0f) }),
        };

        /// <summary>第四、五章才有的陣形（前排三面盾牆／兩隻特殊怪帶隊）</summary>
        static readonly string[] LateFormations = { "鐵壁護衛", "獵殺小隊" };
        /// <summary>第四、五章每一輪的補師數：6 隻以上的一輪（困難）2 隻，其他 1 隻</summary>
        static int HealersPerRound(int n) => n >= 6 ? 2 : n >= 3 ? 1 : 0;
        void GroupSpawn()
        {
            // 這一輪排隊中的：照時間一隻一隻出（錯開 0.3 秒，看得出是一隊）
            if (groupQueue.Count > 0)
            {
                if (now < groupQueue[0].at) return;
                var q = groupQueue[0]; groupQueue.RemoveAt(0);
                formationPos = q.pos;
                Fighter sp;
                if (q.healer && q.role == MinionRole.Special) sp = SpawnSummonerAt(q.ang); else if (q.healer) sp = SpawnHealerAt(q.ang); else sp = SpawnRoleAt(q.role, q.ang, q.ambush);
                formationPos = NoPos;
                if (q.march != null)
                {
                    q.march.spawned++;
                    if (sp != null && sp.Alive && sp.leapEnd <= 0f && !sp.hidden) { sp.march = q.march; sp.marchSpawn = sp.Feet; sp.marchHp = sp.hp; }
                    if (q.march.spawned >= q.march.members) q.march.startAt = now + 0.4f;   // 全部出齊 → 一起往前推
                }
                return;
            }
            if (waveToSpawn <= 0) return;
            bool anyAlive = false; foreach (var m in minions) if (m.Alive && !m.treasure) { anyAlive = true; break; }
            if (anyAlive) { roundWaiting = false; return; }
            // 這一輪全部打完：停 3 秒（第一輪照波次開始時間）
            if (!roundWaiting) { roundWaiting = true; if (roundNo > 0) nextRoundAt = now + RoundGap; }
            if (now < nextRoundAt) return;
            roundWaiting = false;
            bool late = Theme >= ArenaTheme;   // 第四、五章：多兩種比較硬的陣形，每一輪帶補師
            int f; do f = rng.Next(Formations.Length); while ((f == lastFormation && Formations.Length > 1) || (!late && Array.IndexOf(LateFormations, Formations[f].name) >= 0));
            if (easySides && Formations[f].name == "前後夾擊") f = 0;   // 簡單：不從背後夾擊
            lastFormation = f; roundNo++;
            int n = Mathf.Min(groupSize, waveToSpawn);
            if (easySides && (easyBearingWave != wave || roundNo == 1)) { easyBearingWave = wave; easyBearing = rng.NextDouble() < 0.7 ? 0f : (rng.NextDouble() < 0.5 ? -60f : 60f); }
            int ambushAt = easySides && ambushWave != wave && rng.NextDouble() < EasyAmbushChance * 2f ? n - 1 : -1;   // 偶爾最後一隻從背後偷襲（一波最多一隻）
            if (ambushAt >= 0) ambushWave = wave;
            float jitter = ((float)rng.NextDouble() - 0.5f) * 30f;   // 整隊稍微轉一下，不會每次都一樣的位置
            var slots = new List<(MinionRole role, float ang, bool amb)>();
            for (int i = 0; i < n; i++)
            {
                var (role, ang) = Formations[f].slots[i % Formations[f].slots.Length];
                ang += jitter;
                if (easySides) ang = easyBearing + Mathf.Clamp(Mathf.DeltaAngle(0f, ang), -100f, 100f) * 0.3f;   // 全部從同一面來
                bool amb = i == ambushAt;
                if (amb) ang = easyBearing + 180f + ((float)rng.NextDouble() - 0.5f) * 60f;
                slots.Add((role, ang, amb));
            }
            bool layout = Theme != CanyonTheme;   // 黃土山道：小怪從崖頂跳下來，不排陣形
            List<Vector3> fpos = null; List<int> forder = null; List<MarchGroup> fgrp = null;
            marchGroups.RemoveAll(g => g.createdAt < now - MarchMaxTime - 4f);
            if (layout) LayoutFormation(slots, out fpos, out forder, out fgrp);
            for (int j = 0; j < n; j++)
            {
                int i = layout ? forder[j] : j;
                groupQueue.Add((now + j * RoundStagger, slots[i].role, slots[i].ang, slots[i].amb, false, layout ? fpos[i] : NoPos, layout ? fgrp[i] : null));
            }
            // 第四、五章：每一輪額外帶一～兩隻補師（最後出來，躲在隊伍後面；不佔這一波的數量）；第五章再加一隻召喚師（role＝Special 當記號）
            if (late) for (int h = 0; h < HealersPerRound(n); h++) groupQueue.Add((now + (n + h) * RoundStagger, MinionRole.Back, easySides ? easyBearing + (h - 0.5f) * 20f : (h - 0.5f) * 30f, false, true, NoPos, null));
            if (late && MonsterVariants.SummonerFor(Theme) != null) groupQueue.Add((now + (n + HealersPerRound(n)) * RoundStagger, MinionRole.Special, easySides ? easyBearing + 15f : 15f, false, true, NoPos, null));
            Emit("round", player.Feet, Color.white, $"第 {roundNo}/{roundsInWave} 輪：{Formations[f].name}（{n} 隻" + (late && HealersPerRound(n) > 0 ? $"＋補師 {HealersPerRound(n)}" : "") + (late && MonsterVariants.SummonerFor(Theme) != null ? "＋召喚師 1" : "") + "）");
        }

        /// <summary>在某個方位出一隻某角色的小怪（三拍與輪次出怪共用）</summary>
        Fighter SpawnRoleAt(MinionRole role, float ang, bool ambush)
        {
            var p = EdgeAt(ang);
            int kind = PickMinionKind(role);
            kind = EliteSpawnKind(kind, dungeonSpawnOrdinal++, false);
            var nm = SpawnMinion(p, 0, kind, HpMul * (IsRanged(kind) ? 1f : meleeHpMul), DmgMul, SpeedMul);
            nm.ambusher = ambush;
            if (kind == 10) BuryChomper(nm);   // 食人花：不從邊緣走進來，直接埋在場地裡
            float fixHp = minionHpOverride != null ? minionHpOverride(kind) : 0f;
            if (fixHp > 0f) { nm.maxHp = Mathf.Max(1, Mathf.RoundToInt(fixHp)); nm.hp = nm.maxHp; }
            if (eliteLeft > 0 && !EliteCatalog.IsElite(kind)) { eliteLeft--; MakeElite(nm); }
            if (Theme == CanyonTheme) StartCliffLeap(nm);
            waveToSpawn--;
            lastMinionCount = minions.Count;
            return nm;
        }

        void RhythmSpawn(int maxAlive)
        {
            if (MiniBossWave) return;   // 小 Boss 波：只有牠（UpdateSpecialSpawn）
            if (waveToSpawn <= 0 || minions.Count >= maxAlive) return;
            int front = 0, back = 0, special = 0;
            foreach (var m in minions) if (m.Alive && !m.treasure) { var r = RoleOf(m.minionKind); if (r == MinionRole.Front) front++; else if (r == MinionRole.Back) back++; else special++; }
            bool threeBeats = Theme >= 1 || wave >= 4;   // 第一章 1-1～1-3：只有前兩拍
            int wantFront = rhythmMaxAlive <= 3 ? 1 : 2, wantBack = 1, wantSpecial = threeBeats && rhythmMaxAlive >= 3 ? 1 : 0;
            float t = now - waveStartAt;
            var order = new[] { (MinionRole.Front, front, wantFront, 0f, 0f), (MinionRole.Back, back, wantBack, Beat2At, 70f), (MinionRole.Special, special, wantSpecial, Beat3At, 180f) };
            foreach (var (role, alive, want, at, bearing) in order)
            {
                if (alive >= want || t < at || now < roleNextAt[(int)role]) continue;
                float ang = role == MinionRole.Back ? (rng.NextDouble() < 0.5 ? -1f : 1f) * (60f + (float)rng.NextDouble() * 30f) : role == MinionRole.Special ? 180f + ((float)rng.NextDouble() - 0.5f) * 90f : ((float)rng.NextDouble() - 0.5f) * 80f;
                if (easySides)
                {
                    if (easyBearingWave != wave) { easyBearingWave = wave; easyBearing = rng.NextDouble() < 0.7 ? 0f : (rng.NextDouble() < 0.5 ? -75f : 75f); }
                    bool ambushAlive = false; foreach (var m in minions) if (m.Alive && m.ambusher) { ambushAlive = true; break; }
                    bool ambush = !ambushAlive && ambushWave != wave && rng.NextDouble() < EasyAmbushChance && (front + back + special) >= 1;   // 場上已經有人才偷襲；一波最多一隻
                    if (ambush) ambushWave = wave;
                    ang = ambush ? easyBearing + 180f + ((float)rng.NextDouble() - 0.5f) * 90f : easyBearing + ((float)rng.NextDouble() - 0.5f) * 50f;
                }
                SpawnRoleAt(role, ang, easySides && Mathf.Abs(Mathf.DeltaAngle(ang, easyBearing)) > 90f);
                roleNextAt[(int)role] = now + (alive == 0 && t < at + 0.5f ? 0.8f : RoleRefillGap);   // 開場的第一拍可以連出兩隻（隔 0.8 秒）
                return;   // 一幀最多一隻
            }
        }

        /// <summary>特殊敵人：場上最多 1 隻；還有一般小怪要出的時候才出（不會在一波快結束時才冒出來）；上一隻在場上時計時重算</summary>
        void UpdateSpecialSpawn()
        {
            if (Gallery || specialKind < 0 || holdSpawns || edgePoint == null) return;
            if (RhythmSpawns)
            {
                // 三拍出怪的關卡：特殊敵人只在第 3 波當小 Boss 單獨登場（不再隨機插進來）
                if (!MiniBossWave || miniBossSpawned || now < waveStartAt) return;
                miniBossSpawned = true; waveToSpawn = 0;
                var mb = SpawnMinion(edgePointAt != null ? edgePointAt(0f) : edgePoint(), 0, specialKind, HpMul * meleeHpMul * MiniBossHp, DmgMul, SpeedMul);
                mb.miniBoss = true;
                Emit("bossarrive", mb.Feet, new Color(1f, 0.6f, 0.25f), $"⚠ 小 Boss「{mb.name}」登場！" + (SpecialNeedsJump(specialKind) ? "地面震環要跳起來躲（或離遠一點）" : "橫掃要蹲下躲（或離遠一點）"));
                if (Theme == CanyonTheme) StartCliffLeap(mb);
                return;
            }
            bool alive = false; foreach (var m in minions) if (m.Alive && (IsSpecial(m.minionKind) || EliteCatalog.IsElite(m.minionKind))) { alive = true; break; }
            if (alive) { nextSpecialAt = Mathf.Max(nextSpecialAt, now + SpecialGap); return; }
            if (waveToSpawn <= 0 || now < nextSpecialAt) return;
            var sp = SpawnMinion(edgePoint(), 0, specialKind, HpMul * meleeHpMul, DmgMul, SpeedMul);
            nextSpecialAt = now + SpecialGap;
            Emit("bossarrive", sp.Feet, new Color(1f, 0.6f, 0.25f), $"⚠ {sp.name} 出現了！" + (SpecialNeedsJump(specialKind) ? "地面震環要跳起來躲（或離遠一點）" : "橫掃要蹲下躲（或離遠一點）"));
            if (Theme == CanyonTheme) StartCliffLeap(sp);
        }

        /// <summary>無盡塔：每 5 波（不是 Boss 波）出現一隻精銳敵人</summary>
        public bool eliteWaves;
        int eliteLeft;
        public const float EliteHp = 6f, EliteDmg = 1.6f;
        void MakeElite(Fighter m)
        {
            m.isElite = true;
            m.maxHp = Mathf.RoundToInt(m.maxHp * EliteHp); m.hp = m.maxHp;
            m.dmgMul *= EliteDmg; m.bodyRadius *= 1.3f;
            m.name = "精銳・" + m.name;
            Emit("bossarrive", m.Feet, new Color(1f, 0.75f, 0.2f), $"⚠ 精銳敵人：{m.name}！");
        }
        bool bossSpawnedThisWave;
        public bool bossAfterMinions;   // 故事關卡：Boss 波先清小怪
        bool bossPending; float bossAt;
        public const float BossEntranceDelay = 2.5f;

        // 小怪解鎖的波次：熔岩小鬼、火焰射手（遠程）第 1 波起；岩石投擲者（遠程）第 4 波；岩石衝鋒獸第 6 波；
        // 熔岩狙擊手（遠程）第 8 波；自爆小鬼第 11 波（打倒第一隻 Boss 後）
        static readonly int[] UnlockWave = { 1, 1, 6, 11, 4, 8, 5 };
        public const float RangedShare = 0.2f;   // 遠程小怪約佔兩成
        /// <summary>場景主題：第 1～10 波熔岩火山（0）、11～20 波森林（1）、21～30 波海上礁石（2）、31～40 波角鬥場（3）、41～50 波黃土山道（4），之後輪流</summary>
        public static int ThemeOf(int wave) => ((Mathf.Max(1, wave) - 1) / 10) % ThemeCount;
        public const int ThemeCount = 5, ArenaTheme = 3, CanyonTheme = 4;
        public static string BossClassOf(int theme) => theme == 1 ? "boss_forest" : theme == 2 ? "boss_sea" : theme == 3 ? "boss_arena" : theme == 4 ? "boss_canyon" : "boss";
        /// <summary>副本：指定某些波次的主題（房間用高波次的強度，但保留原本的場景）；沒指定就照波次算</summary>
        public Dictionary<int, int> waveTheme;
        /// <summary>副本地圖：現在這個地點的場景（−1＝照波次）</summary>
        public int themeOverride = -1;
        public int Theme => themeOverride >= 0 && endless ? themeOverride : endless ? (waveTheme != null && waveTheme.TryGetValue(wave, out var wt) ? wt : ThemeOf(wave)) : 0;
        // 森林小怪（7～12）在第 11～20 波的解鎖：這個主題的第幾波起（第二輪起全部解鎖）
        static readonly int[] ThemeUnlock = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 3, 1, 5, 7, 1, 2, 3, 1, 5, 7 };   // 森林（7～12）、海上（13～18）

        int PickMinionKind() => PickMinionKind(null);

        /// <summary>role 指定：只在這個角色裡挑（三拍出怪）；null＝照遠程比例隨機</summary>
        int PickMinionKind(MinionRole? role)
        {
            // 先決定近戰或遠程（遠程約兩成），再在這個主題已解鎖的種類裡依權重挑
            bool ranged = role.HasValue ? role == MinionRole.Back : rng.NextDouble() < EffectiveRangedShare;
            int theme = Theme;
            int local = wave - (wave - 1) / 10 * 10;       // 這個主題的第幾波（1～10）
            bool firstCycle = wave <= 10 * ThemeCount;
            float[] w = { 3f, 2f, 1.2f + 0.08f * wave, 1.2f + 0.08f * wave, 1.5f, 1.0f + 0.05f * wave, 1.2f, 1.5f, 1.3f, 1.2f, 3f, 1.5f, 1.2f, 2.5f, 1.5f, 1.2f, 2f, 1.3f, 1.2f };
            bool Ok(int k) => (role.HasValue ? RoleOf(k) == role.Value : IsRanged(k) == ranged) && (theme == 0 ? k < 7 && wave >= UnlockWave[k]
                : theme >= ArenaTheme ? Array.IndexOf(theme == ArenaTheme ? ArenaKinds : CanyonKinds, k) >= 0 && (!firstCycle || local >= LateUnlock(k))
                : (theme == 1 ? IsPlant(k) : IsSea(k)) && (!firstCycle || local >= ThemeUnlock[k]));
            float sum = 0f;
            for (int k = 0; k < w.Length; k++) if (Ok(k)) sum += w[k];
            float r = (float)rng.NextDouble() * sum;
            for (int k = 0; k < w.Length; k++)
            {
                if (!Ok(k)) continue;
                r -= w[k]; if (r <= 0f) return k;
            }
            if (role == MinionRole.Special) return theme == 1 ? 8 : theme == 2 ? 13 : 2;   // 這個主題還沒解鎖特殊怪：衝鋒獸／荊棘衝鋒獸／跳躍魚
            return theme == 1 ? (ranged ? 7 : 10) : theme == 2 ? (ranged ? 15 : 13) : (ranged ? 1 : 0);
        }
        // 角鬥場：各地抓來的怪物（熔岩、森林、海上的都有，不含要在海裡／空中的）；黃土山道：岩石系為主，加上荊棘衝鋒獸與孢子菇
        // 第四、五章多加魔法小怪（使用者要求）：角鬥場加毒液噴花、雷電水母（籠子裡放出來的魔物）；山道加毒液噴花、藤蔓術士
        static readonly int[] ArenaKinds = { 0, 1, 2, 3, 5, 7, 8, 9, 11, 12, 16, 17 };
        static readonly int[] CanyonKinds = { 0, 1, 2, 3, 4, 5, 7, 8, 9, 11 };   // 虛空術士（6）不在山道出現：牠只會叫物理小鬼
        /// <summary>角鬥場／山道第一輪：自爆小鬼、虛空術士第 4 波起，其他一開始就有</summary>
        static int LateUnlock(int k) => k == 3 || k == 6 ? 4 : 1;

        void SpawnBoss()
        {
            bossSpawnedThisWave = true;
            int bossIndex = wave / BossEvery - 1;
            var at = FixedDungeonSpawns ? DungeonSpawnPoint(0) : (bossPoint ?? edgePoint)();
            if(Gallery){GalleryFrame(out var center,out float x,out float z);at=center+Vector3.forward*Mathf.Min(.6f,z*.3f);}
            enemy.maxHp = Mathf.RoundToInt((storyBossHp > 0f ? storyBossHp : bossBaseHp * (1f + 0.5f * bossIndex) * bossHpMul) * BossPartyMul * hpBoost * BoostHpMul * BossHpCut * TowerMul);
            enemy.hp = enemy.maxHp;
            enemy.head = new Vector3(at.x, enemy.head.y > 0.5f ? enemy.head.y : 3.2f, at.z);
            enemy.charging = null; enemy.cooldownUntil.Clear(); enemy.dots.Clear(); enemy.snaredUntil = 0f; enemy.blindUntil = 0f; enemy.silencedUntil = 0f;
            // 依主題換 Boss：熔岩巨像／腐化樹王（招式不同）
            var bc = Skills.Classes[Gallery ? "boss_clockwork" : CarpetBoss ? Carpet.BossClass : BossClassOf(Theme)];   // 天穹飛毯：空賊飛艇（Combat.Carpet.cs）
            if (enemy.cls != bc)
            {
                enemy.cls = bc; enemy.name = bc.name;
                enemy.loadout.Clear();
                foreach (var id in bc.defaultLoadout) enemy.loadout.Add(Skills.All[id]);
            }
            // 故事關卡的 Boss 多一招震地波（跳起來躲）
            enemy.loadout.RemoveAll(IsQuake);
            if (bossAfterMinions && !enemy.loadout.Contains(Skills.All[QuakeOf(Theme)])) enemy.loadout.Add(Skills.All[QuakeOf(Theme)]);   // 深淵海妖本來就有潮汐環（被靠近時用）
            Emit("bossarrive", enemy.Feet, enemy.cls.color, bossAfterMinions ? $"{enemy.name}現身！" : $"第 {wave} 波 Boss 出現！");
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
        public Fighter MinionById(int id) => minions.Find(m => m.minionId == id) ?? npcs.Find(n => n.minionId == id);
        /// <summary>主機：隊友打到小怪</summary>
        public void ApplyHitOnMinion(int id, int damage, SkillDef s)
        {
            var m = MinionById(id);
            if (m != null && m.Alive) { incoming = s; ApplyDamageAndEffect(m, damage, s != null ? s.effect : EffectKind.None, s != null ? s.effectDps : 0f, s != null ? s.effectDur : 0f, m.Chest); incoming = null; }
            if (m != null && s != null && s.id == "plague") Plague(m);   // 隊友的毒爆：主機引爆
        }

        /// <summary>
        /// 碰撞：近戰小怪的身體不會和玩家重疊（玩家走過去會把牠推開；在地上的才算，飛在空中的不算）。
        /// 推出去的位置在場地外就不推（不會把小怪推出場地）。
        /// </summary>
        /// <summary>Boss 模型的半徑；判定（受擊、碰撞）再往外加 BossBodyExtra，玩家碰到就靠不過去（太近會看不清楚）</summary>
        public const float BossModelRadius = 0.75f, BossBodyExtra = 1f;

        /// <summary>碰撞：Boss 的身體（判定半徑）不會和玩家重疊，玩家走過去會把 Boss 推開；推出去在場地外就不推</summary>
        void PushBossFromPlayers()
        {
            if (remoteEnemy || enemy == null || !enemy.Alive || enemy.bodyRadius <= BossModelRadius) return;
            foreach (var pl in Players())
            {
                if (!pl.Alive || pl == enemy) continue;
                var away = enemy.Feet - pl.Feet; away.y = 0f;
                float d = away.magnitude, min = enemy.bodyRadius + pl.bodyRadius;
                if (d >= min) continue;
                var dir = d > 1e-3f ? away / d : Fighter.Flat(-pl.forward);
                if (dir == Vector3.zero) dir = Vector3.forward;
                var to = pl.Feet + dir * min; to.y = enemy.Feet.y;
                if (minionInside != null && !minionInside(to)) continue;
                enemy.head = new Vector3(to.x, enemy.head.y, to.z);
            }
        }

        void PushMinionsFromPlayers()
        {
            foreach (var m in minions)
            {
                if (!m.Alive || IsRanged(m.minionKind) || m.alt > 0.5f || m.leapEnd > 0f) continue;
                foreach (var pl in Players())
                {
                    if (!pl.Alive) continue;
                    var away = m.Feet - pl.Feet; away.y = 0f;
                    float d = away.magnitude, min = m.bodyRadius + pl.bodyRadius;
                    if (d >= min) continue;
                    var dir = d > 1e-3f ? away / d : Fighter.Flat(-pl.forward);
                    if (dir == Vector3.zero) dir = Vector3.forward;
                    var to = pl.Feet + dir * min; to.y = m.Feet.y;
                    if (minionInside != null && !minionInside(to)) continue;
                    m.head = new Vector3(to.x, m.head.y, to.z);
                }
            }
        }

        void UpdateMinions(float dt)
        {
            CleanupElites();
            if (!enemy.Alive && !endless) { foreach (var m in minions) if (m.Alive) { m.hp = 0; Emit("miniondie", m.Feet, m.cls.color, null); } }
            minions.RemoveAll(m => !m.Alive);
            if (remoteEnemy) return;   // 隊友端：代理，不跑 AI
            PushMinionsFromPlayers();
            PushBossFromPlayers();
            foreach (var at in pendingSummons) { var imp = SpawnMinion(at, 0, 0, HpMul * meleeHpMul, DmgMul, SpeedMul); imp.attackReadyAt = now + 1.5f; }
            pendingSummons.Clear();
            FlushVariantSpawns();   // 召喚師上一幀叫的砂靈（Combat.Variants.cs）
            var claw = Skills.All["m_claw"];
            foreach (var m in minions)
            {
                if (!m.Alive) continue;
                if (m.hasteUntil > 0f || m.rallyUntil > 0f) TickSupportBuffs(m, dt);   // 戰鼓加速、戰旗增益：到期拿掉（Combat.Variants.cs）
                if (m.leapEnd > 0f) { UpdateCliffLeap(m); continue; }   // 黃土山道：還在崖上／跳下來中
                // 目標：最近的活著的玩家
                Fighter tgt = null; float best = float.MaxValue;
                foreach (var pl in Players()) { if (!pl.Alive) continue; float d0 = FlatDistance(m.Feet, pl.Feet); if (d0 < best) { best = d0; tgt = pl; } }
                // 副本：保護目標比玩家更容易被盯上（多算 3m 的優勢）、友軍和第三勢力也會被打
                foreach (var n in npcs) { if (!n.Alive || n.npcSide == 1) continue; float d0 = FlatDistance(m.Feet, n.Feet) - (n.npcEscort ? 3f : 0f) - n.lure; if (d0 < best) { best = d0; tgt = n; } }
                if (tgt == null) continue;
                if (tgt.npcSide >= 0) best = FlatDistance(m.Feet, tgt.Feet);
                m.target = tgt;
                if (m.treasure) { UpdateTreasure(m, dt); continue; }   // 寶箱怪：只會逃
                if (m.march != null && UpdateMarch(m, dt)) continue;   // 軍陣行進中：整隊一起推進，到定點才散開
                // 保險（使用者回報「有的敵人有顯示但打不到，關卡無法進行」）：在場外待超過 OutsideRescue 秒（不是正在跳崖、出場中），就拉回場內
                if (minionInside != null && !minionInside(m.Feet) && m.leapEnd <= 0f && carpetStation < 0)   // 埋著的食人花也算（埋在場外會永遠打不到）
                {
                    if (m.outsideSince <= 0f) m.outsideSince = now;
                    else if (now - m.outsideSince > OutsideRescue)
                    {
                        var back = PullInside(m.Feet, tgt.Feet);
                        Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                        m.head = new Vector3(back.x, m.head.y, back.z); m.outsideSince = 0f;
                        Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    }
                }
                else m.outsideSince = 0f;
                if (UpdateLure(m, dt)) continue;   // 吃誘餌中：不攻擊（Combat.Pets.cs）
                if (m.staggerUntil > now) { m.windupUntil = 0f; continue; }   // 受擊硬直中：不動、不出手
                if (m.exMon != null && !m.exAware) { ExPatrol(m, dt); continue; }   // 故事探索：還沒發現玩家，照路線巡邏（Combat.Explore.cs）
                if (m.exMon != null && ExChase(m, player, dt)) continue;              // 故事探索：發現了就沿著走道一直追
                bool esUser = m.variant > 0 && EnemySkills.Has(m.variant);   // 補師、輔助型、人形、砂靈：新招式（Combat.EnemySkills.cs）
                if (esUser) TickEnemySkillState(m);
                if (esUser && m.esCast != null) { TickEnemyCast(m, tgt); continue; }
                if (m.human > 0 && UpdateHuman(m, tgt, best, dt)) continue;   // 人形敵人：繞側、拉開距離、躲進怪群、沒血跑開回血（Combat.Variants.cs）
                if (esUser && m.human == 0 && UpdateEnemySkill(m, tgt, best, dt)) continue;
                if (m.assassin) { UpdateAssassin(m, dt); continue; }
                if (m.healer) { UpdateHealer(m, dt); continue; }   // 補師：躲在後面治療同伴（Combat.Variants.cs）
                if (m.support > 0) { UpdateSupport(m, dt); continue; }   // 輔助型：治療、護盾、加速、戰旗（Combat.Variants.cs）
                if (m.summoner) { UpdateSummoner(m, dt); continue; }
                if (esUser) { EnemySkillMove(m, tgt, best, dt); continue; }
                // 一般小怪、特殊敵人、菁英的第二招（Docs/ENEMY_MOVES_PROPOSAL.md）：讀條中就只處理讀條
                bool esKind = !esUser && !secondMovesOff && EnemySkills.For(m) != null;
                if (esKind && m.esCast != null) { TickEnemyCast(m, tgt); continue; }   // 人形、砂靈：不用原本小怪的攻擊   // 召喚師：躲在後面叫砂靈（Combat.Variants.cs）   // 刺客怪：繞到背後突襲（Combat.Variants.cs）
                var wantFwd = m.windupUntil > 0f && m.lockFwd != Vector3.zero ? m.lockFwd : Fighter.Flat(tgt.head - m.head);   // 預備中：面向鎖住（扇形不追著玩家轉）
                if (m.minionKind == 16 && m.forward != Vector3.zero && wantFwd != Vector3.zero)
                {
                    // 寄居蟹轉身很慢（每秒 70°）：繞到背後打
                    float delta = Mathf.DeltaAngle(Mathf.Atan2(m.forward.x, m.forward.z) * Mathf.Rad2Deg, Mathf.Atan2(wantFwd.x, wantFwd.z) * Mathf.Rad2Deg);
                    m.forward = RotateY(m.forward, Mathf.Clamp(delta, -CrabTurn * dt, CrabTurn * dt));
                }
                else m.forward = wantFwd;
                float d = best;
                int kind = m.minionKind;
                // 食人花埋在地下：有人走近 1.5m 才冒出來（埋著時不動、打不到）
                if (m.hidden)
                {
                    if (d <= ChomperTrigger + tgt.bodyRadius || now - m.buriedAt >= ChomperAutoEmerge || OnlyBuriedLeft()) { m.hidden = false; m.attackReadyAt = now + 0.25f; Emit("emerge", m.Feet, Skills.All["m_bite"].color, "食人花冒出來了！"); }   // 有人靠近、埋了 15 秒、或其他敵人都死光了（使用者要求）
                    continue;
                }
                // 河豚鼓起來中：不動、不出手，時間到就爆
                if (kind == 18 && m.fuseAt > 0f) { if (now >= m.fuseAt) PufferExplode(m); continue; }
                int hold = minionHold != null ? minionHold(m) : 0;
                if (Gallery && kind >= 48) { TickGalleryMinion(m,tgt,d,dt,hold); continue; }
                if (EliteCatalog.IsElite(kind))
                {
                    // 菁英的第三招：原本兩招都沒在預備、出手空檔到了才放
                    if (esKind && hold == 0 && m.windupUntil <= 0f && now >= m.attackReadyAt && m.snaredUntil <= now && CanAttackFrom(m.Feet) && UpdateEnemySkill(m, tgt, d, dt)) continue;
                    TickElite(m, tgt, d, dt, hold); continue;
                }
                if (hold == 1) { m.windupUntil = 0f; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.6f); continue; }   // 教學：定住
                if (hold == 2) { m.windupUntil = 0f; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.6f); }          // 教學：只走位
                // 衝刺中（荊棘衝鋒獸、跳躍魚的飛撲、海鳥的俯衝）：直線往前衝，撞到玩家就停
                if (m.dashUntil > 0f)
                {
                    if (now >= m.dashUntil) EndDash(m);
                    else DashStep(m, dt);
                    continue;
                }
                // 浮空／飛行的小怪（海鳥、雷電水母、河豚）出手後：降到場內地上休息 AirRest 秒（這時候近戰打得到），休息完才回到空中
                if (m.restUntil > now)
                {
                    if (m.alt > 0f) { m.alt = Mathf.Max(0f, m.alt - AirLandSpeed * dt); m.head.y = MinionHead[kind] + m.alt; }
                    continue;
                }
                if ((kind == 17 || kind == 18) && m.windupUntil <= 0f && m.alt < BaseAlt(kind))
                { m.alt = Mathf.Min(BaseAlt(kind), m.alt + 0.8f * dt); m.head.y = MinionHead[kind] + m.alt; }   // 休息完慢慢浮回原本的高度
                // 海鳥：俯衝完飛回空中；平常在玩家上空 4～6m 繞圈
                if (kind == 14 && m.windupUntil <= 0f)
                {
                    if (m.alt < BirdCruise) { m.alt = Mathf.Min(BirdCruise, m.alt + 2f * dt); m.head.y = MinionHead[kind] + m.alt; }
                    if (m.alt > BirdCruise) { m.alt = Mathf.Max(BirdCruise, m.alt - 1f * dt); m.head.y = MinionHead[kind] + m.alt; }
                    var rad = Fighter.Flat(m.Feet - tgt.Feet); if (rad == Vector3.zero) rad = Vector3.forward;
                    var tan = new Vector3(-rad.z, 0f, rad.x);
                    float corr = Mathf.Clamp(BirdCircle - d, -1f, 1f);   // 繞圈的半徑要在場地裡（6m 場地）
                    var fly = (tan + rad * corr).normalized * 1.5f * m.speedMul * dt;
                    m.head += fly;
                    m.forward = Fighter.Flat(fly);
                    if (esKind && hold == 0 && now >= m.recoverUntil && m.alt >= BirdCruise - 0.3f && now >= m.attackReadyAt && CanAttackFrom(m.Feet) && UpdateEnemySkill(m, tgt, d, dt)) continue;   // 第二招：平翼掠肩
                    if (now >= m.recoverUntil && m.alt >= BirdCruise - 0.3f && d <= MinionReach[kind] && now >= m.attackReadyAt && CanAttackFrom(m.Feet))   // 飛到攻擊區上空才俯衝
                    { m.windupUntil = now + MinionWindup[kind] * minionWindupMul; Emit("minionwindup", m.Feet, Skills.All["m_dive"].color, null); Emit("birdcry", tgt.Feet, Skills.All["m_dive"].color, (MinionWindup[kind] * minionWindupMul).ToString("0.00")); }   // 俯衝前叫一聲＋地上的影子（蹲）
                    continue;
                }
                if (m.windupUntil > 0f)
                {
                    if (now < m.windupUntil) continue;   // 預備中站著不動
                    m.windupUntil = 0f;
                    MinionStrike(m, tgt, d);
                    if (kind == 17 || kind == 18) { LandToRest(m); continue; }   // 浮空的：出手後降到地上休息
                    // 出手後原地不動：一般 1 秒、虛空術士 3 秒、藤蔓術士在藤蔓消失前（3 秒）；衝鋒獸改由衝刺決定
                    m.recoverUntil = kind == 8 || kind == 13 || kind == 14 ? 0f : IsSpecial(kind) ? now + SpecialRecover : now + (kind == 6 ? VoidStay : kind == 9 ? VineStay : MinionRecover) + (IsRanged(kind) ? rangedRecoverAdd : 0f);
                    if (kind == 6) m.teleportAt = now + VoidStay;                   // 停留完就瞬移
                    else if (IsRanged(kind) && kind != 4 && kind != 7 && kind != 17) m.repositionNext = true;
                    else if (MeleeShuffles(kind)) m.repositionNext = true;   // 近戰（使用者要求）：休息完先繞著你走一段，才會再打   // 其他遠程：站定完先隨機走位一下，才會再出手（投擲者、噴花、水母定住不動）
                    continue;
                }
                // 虛空術士：停留結束 → 瞬移到場內隨機位置
                if (m.teleportAt > 0f && now >= m.teleportAt)
                {
                    m.teleportAt = 0f;
                    if (kind == 13)
                    {
                        // 跳躍魚：咬完跳回海裡（出生的地方），4 秒後再來
                        Emit("splash", m.Feet, Skills.All["m_leap"].color, null);
                        m.head = new Vector3(m.homePos.x, m.head.y, m.homePos.z);
                        Emit("splash", m.Feet, Skills.All["m_leap"].color, null);
                        continue;
                    }
                    var to = RandomInsidePoint(m.Feet);
                    Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    m.head = new Vector3(to.x, m.head.y, to.z);
                    Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    continue;
                }
                if (m.snaredUntil > now || now < m.recoverUntil) continue;
                // 遠程小怪出手後的隨機走位：挑附近一點（和目標保持射擊距離、在場地內）走過去，走到（或逾時）才能再出手
                if (m.repositionNext)
                {
                    m.repositionNext = false;
                    if (PickRepositionPoint(m, tgt, out var dest)) { m.moveTo = dest; m.moveUntil = now + (IsRanged(kind) ? RepositionMaxTime : MeleeShuffleTime); }
                }
                if (m.moveUntil > 0f)
                {
                    var to = m.moveTo - m.Feet; to.y = 0f;
                    if (now < m.moveUntil && to.magnitude > 0.05f)
                    {
                        float rs = Mathf.Max(RepositionMinSpeed, minionSpeed * MinionSpeed[kind] * m.speedMul * EnemySpeedScale * 2f);
                        var mv = to.normalized * Mathf.Min(to.magnitude, rs * dt);
                        m.head += mv;
                        continue;   // 走位中不出手（一直面向目標，像側移找角度）
                    }
                    m.moveUntil = 0f;
                }
                // 第二招：原本那一招沒在預備、出手空檔到了；還在場外、還沒進攻擊區不放
                if (esKind && hold == 0 && now >= m.attackReadyAt && !(kind == 3 && m.fuseAt > 0f && m.fuseAt - now < 2.6f) && CanAttackFrom(m.Feet) && UpdateEnemySkill(m, tgt, d, dt)) continue;
                // 自爆小鬼：引信到了就在原地爆（不管離多遠）
                if (kind == 3 && m.fuseAt > 0f && now >= m.fuseAt) { MinionStrikeCore(m, tgt, d); continue; }
                // 出手條件：近戰／衝鋒獸／自爆要貼近；射手在 9m 內；岩石衝鋒獸在 5.5m 內就刨地準備衝
                float reach = IsRanged(kind) ? MinionReach[kind] : kind == 2 || kind == 8 ? ChargerDash - 0.5f : MeleeReach(kind, MinionReach[kind]);
                float windup = MinionWindup[kind] < 0f ? claw.charge : MinionWindup[kind];
                if (IsRanged(kind)) windup *= rangedWindupMul;
                if (kind == 5) windup = SniperAim;   // 狙擊手：瞄準 2 秒（紅線）
                if (kind == 9) windup = VineWarn;    // 藤蔓術士：預警 2 秒（地上先畫出藤蔓的路線，蹲下躲）
                if (kind == 11) { SporeTick(m); }    // 孢子菇：不攻擊，只治療
                windup *= minionWindupMul;
                bool outside = minionInside != null && !minionInside(m.Feet);   // 還在場地外：先走進來（不在場外放招）；海裡、空中的也一樣要進到場地才出手
                bool outZone = !CanAttackFrom(m.Feet);   // 還沒進到中間的攻擊區（6m×6m）：先走進去才出手
                // 熔岩小鬼：一次只有一隻出手，其他的在旁邊繞（等那隻打完）
                bool impWait = kind == 0 && AnotherImpWinding(m);
                bool inReach = d <= reach + tgt.bodyRadius || (kind == 17 && !outside);   // 雷電水母：不追你，時間到就放電（在場內就算）
                if (!outZone && !impWait && kind != 11 && inReach && now >= m.attackReadyAt)
                {
                    m.windupUntil = now + windup; m.lockFwd = Fighter.Flat(tgt.Feet - m.Feet); Emit("minionwindup", m.Feet, claw.color, null);
                    if (kind == 2 || kind == 8) AddLane("dash", m.Feet, m.Feet + m.lockFwd * Mathf.Min(ChargerDash, d + 1.5f), 0f, windup + 0.1f, m.bodyRadius, Skills.All["m_dash"], 0, false, m);   // 刨地：先畫出衝刺路線
                    if (kind == 5) AddLane("snipe", m.Feet, tgt.Feet, 0f, windup + 0.05f, 0.08f, Skills.All["m_snipe"], 0, false, m);   // 瞄準：紅線指著你
                    if (kind == 9) AddLane("vinewarn", m.Feet, tgt.Feet, 0f, windup + 0.05f, Skills.All["m_vine"].radius, Skills.All["m_vine"], 0, false, m);   // 藤蔓路線預警
                    if (kind == 13) Emit("landwarn", tgt.Feet, Skills.All["m_leap"].color, windup.ToString("0.00"));   // 跳躍魚落點的水花預警
                    if (kind == 17) Emit("landwarn", m.Feet, Skills.All["m_shock"].color, (windup + 0.05f).ToString("0.00") + "|" + Skills.All["m_shock"].areaRadius.ToString("0.00"));   // 水母放電的紅圈
                    if (IsSpecial(kind)) Emit("specialwarn", m.Feet, Skills.All[SpecialSkillId(kind)].color, SpecialNeedsJump(kind) ? "⬆ 跳起來！（或離遠一點）" : "⬇ 蹲下！（或離遠一點）");
                    continue;
                }
                // 走位：射手保持 3.5～6m，其他走向目標（停在 0.8m）；小怪之間互相推開
                float speed = minionSpeed * MinionSpeed[kind] * m.speedMul * EnemySpeedScale;
                if (m.swift && d > SwiftNear) speed = Mathf.Max(speed, SwiftRun * Mathf.Min(1f, m.speedMul / 1.5f + 0.34f));   // 疾風稀有怪：遠了就用跑的（被減速時跟著慢）
                if (kind == 13 && minionInside != null && !minionInside(m.Feet)) speed *= 3f;   // 跳躍魚在海裡游得很快
                if (outside && endless) speed *= 2f;   // 無盡／故事：還在場地外的小怪移動速度 2 倍（快點進場）
                Vector3 step;
                if (outZone && attackHalf > 0f)
                {
                    // 還沒進攻擊區（場地外或場地邊緣）：走向攻擊區裡離目標最近的那一點
                    var goal = ClampToZone(tgt.Feet); var to = goal - m.Feet; to.y = 0f;
                    step = to.magnitude > 0.05f ? to.normalized * Mathf.Min(speed * dt, to.magnitude) : Vector3.zero;
                }
                else if ((kind == 4 || kind == 7 || kind == 17) && !outside) step = Vector3.zero;   // 岩石投擲者、毒液噴花、雷電水母：進場就定住
                else if (IsRanged(kind) && !outside)
                {
                    // 遠程：保持在 KeepMin～KeepMax 之間（太近就後退）
                    float want = d > KeepHi(kind) ? d - KeepHi(kind) : d < KeepLo(kind) ? -(KeepLo(kind) - d) : 0f;
                    step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Clamp(want, -speed * dt, speed * dt);
                }
                else if (kind == 11) step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Min(speed * dt, Mathf.Max(0f, d - KeepDist(m, tgt) - 0.8f));   // 孢子菇：站在你前面一點擋著
                else if (impWait && d < KeepDist(m, tgt) + 1.2f)
                {
                    // 等別隻出手的小鬼：繞著你走（不靠近）
                    var tan = Vector3.Cross(Vector3.up, Fighter.Flat(tgt.Feet - m.Feet)) * m.strafeSign;
                    step = tan * speed * 0.8f * dt;
                }
                else step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Min(speed * dt, Mathf.Max(0f, d - KeepDist(m, tgt)));   // 停在約一個身高的距離（身體不重疊）
                foreach (var o in minions)
                {
                    if (o == m) continue;
                    var away = m.Feet - o.Feet; away.y = 0f;
                    float od = away.magnitude;
                    if (od < 0.6f) step += (od > 1e-3f ? away / od : new Vector3(0.1f, 0f, 0f)) * (0.6f - od) * 2f * dt;
                }
                var next = m.Feet + step;
                // 場地外出生的小怪：還在場地外時也可以走（朝玩家走進場地）；進到場地後就不會再走出去
                bool stayZone = InAttackZone(next) || !InAttackZone(m.Feet);   // 進了攻擊區就不再走出去
                if ((minionInside == null || minionInside(next) || !minionInside(m.Feet)) && stayZone) m.head = new Vector3(next.x, m.head.y, next.z);   // 進場後就不再走出去（海裡的也一樣）
            }
        }

        /// <summary>
        /// 食人花：埋在場地中間直徑 ChomperZone 公尺內（使用者要求：等人靠近才出來的怪不要埋在邊邊，找不到、走不到）；
        /// 玩家走近 1.5m 就冒出來；ChomperAutoEmerge 秒都沒人靠近，自己冒出來
        /// </summary>
        public const float ChomperZone = 3f, ChomperAutoEmerge = 15f;
        /// <summary>場上活著的敵人只剩埋著的食人花（Boss 也不在）</summary>
        bool OnlyBuriedLeft()
        {
            if (enemy != null && enemy.Alive) return false;
            foreach (var o in minions) if (o.Alive && !o.hidden && !o.treasure) return false;
            return true;
        }
        void BuryChomper(Fighter m)
        {
            ArenaFrame(out var c, out _); c.y = 0f;
            var at = c;
            for (int i = 0; i < 20; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * ChomperZone * 0.5f;
                var q = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                bool clear = true; foreach (var pl in Players()) if (pl.Alive && FlatDistance(q, pl.Feet) < ChomperTrigger + 0.3f) { clear = false; break; }   // 不要一埋下去就踩到
                if (minionInside != null && !minionInside(q)) clear = false;
                if (clear) { at = q; break; }
            }
            m.buriedAt = now;
            m.head = new Vector3(at.x, m.head.y, at.z); m.homePos = m.Feet;
            m.hidden = true;
            Emit("leafring", m.Feet, Skills.All["m_bite"].color, null);
        }

        /// <summary>河豚被打中：鼓起來 2 秒後爆（範圍 2m），自己消失</summary>
        void PufferExplode(Fighter m)
        {
            var sp = Skills.All["m_spine"];
            Emit("minionexplode", m.Feet, new Color(1f, 0.6f, 0.3f), "puffer");
            foreach (var v in new List<Fighter>(Players()))
            {
                if (!v.Alive || FlatDistance(v.Feet, m.Feet) > PufferRadius + v.bodyRadius) continue;
                int dmg = Mathf.RoundToInt(sp.damage * 2f * m.dmgMul);
                if (v.isAlly) { Emit("allyhit", v.Chest, sp.color, null); OnAllyHit?.Invoke(v, sp, dmg, v.Chest, 0); }
                else { ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest, m); SoulVictim(m, v, dmg); }
            }
            m.fuseAt = 0f; m.hp = 0f;
        }

        /// <summary>孢子菇：每 6 秒替周圍 3m 的小怪回 10% 血（自己不打人、擋在前面吸攻擊）</summary>
        void SporeTick(Fighter m)
        {
            if (now < m.attackReadyAt) return;
            m.attackReadyAt = now + SporeHealEvery;
            int healed = 0;
            foreach (var o in minions)
            {
                if (o == m || !o.Alive || o.hp >= o.maxHp || FlatDistance(o.Feet, m.Feet) > SporeHealRadius) continue;
                o.hp = Mathf.Min(o.maxHp, o.hp + o.maxHp * SporeHealFrac); healed++;
            }
            Emit("sporeheal", m.Feet, Skills.All["m_spore"].color, healed > 0 ? $"孢子治療 ×{healed}" : null);
        }

        /// <summary>熔岩小鬼：場上有別隻小鬼正在預備出手（或剛出手還沒恢復）嗎</summary>
        bool AnotherImpWinding(Fighter me)
        {
            foreach (var o in minions) if (o != me && o.Alive && o.minionKind == 0 && (o.windupUntil > 0f || now < o.recoverUntil)) return true;
            return false;
        }

        /// <summary>小怪被打中（Damage 裡）：小鬼後退半步、火焰射手跳開重新站位</summary>
        void OnMinionHurt(Fighter m)
        {
            int kind = m.minionKind;
            if (kind == 0 && m.windupUntil <= 0f)
            {
                var away = Fighter.Flat(m.Feet - player.Feet); if (away == Vector3.zero) away = -Fighter.Flat(m.forward);
                var next = m.Feet + away * ImpKnock;
                if (minionInside == null || minionInside(next)) m.head = new Vector3(next.x, m.head.y, next.z);
                m.strafeSign = rng.NextDouble() < 0.5 ? -1 : 1;
            }
            else if (kind == 1 && m.windupUntil <= 0f && m.moveUntil <= 0f) { m.repositionNext = true; m.recoverUntil = now; m.shotCount = 2; }
            else if (kind == 18 && m.fuseAt <= 0f && m.Alive)
            {
                // 河豚：被打中就鼓起來，2 秒後爆（打完馬上退開）
                m.fuseAt = now + PufferFuse; m.windupUntil = 0f; m.dashUntil = 0f;
                Emit("puffwarn", m.Feet, new Color(1f, 0.6f, 0.3f), "河豚鼓起來了！退開！");
            }
        }

        /// <summary>遠程小怪出手後的隨機走位：距離、最低速度（公尺／秒）、最多走幾秒</summary>
        public const float RepositionMin = 0.8f, RepositionMax = 1.8f, RepositionMinSpeed = 0.5f, RepositionMaxTime = 3.5f;

        /// <summary>
        /// 遠程小怪換位置的目的地：從現在的位置往隨機方向走 0.8～1.8m。一定會換位置——現在的位置可能是玩家實際走不過去的地方
        /// （家具、牆邊），一直站著射就打不到它。先找「和目標保持射擊距離、在場地內」的點；找不到就放寬距離；
        /// 再找不到就往玩家那一側（場地裡面）走 1m。
        /// </summary>
        /// <summary>
        /// 近戰小怪出手後會先走一段再打（使用者要求）：熔岩小鬼、寄居蟹、大型特殊敵人。
        /// 衝鋒獸（靠衝刺）、自爆、孢子菇（不打人）、食人花（埋著）、跳躍魚／海鳥／水母（自己的飛跳節奏）照舊。
        /// </summary>
        public static bool MeleeShuffles(int kind) => kind == 0 || kind == 16 || IsSpecial(kind);
        /// <summary>近戰繞行：繞著目標轉 MeleeShuffleDeg 度左右（隨機左右）、退到 MeleeShuffleDist 的距離；最多走這麼久</summary>
        public const float MeleeShuffleTime = 1.6f, MeleeShuffleDist = 1.7f, MeleeShuffleDegLo = 40f, MeleeShuffleDegHi = 80f;

        bool PickRepositionPoint(Fighter m, Fighter tgt, out Vector3 dest)
        {
            dest = m.Feet;
            int kind = m.minionKind;
            if (!IsRanged(kind))
            {
                // 近戰：繞到你側邊一點、稍微拉開距離（像在找破綻），走到了再衝上來打
                var rad = Fighter.Flat(m.Feet - tgt.Feet); if (rad == Vector3.zero) rad = Fighter.Flat(m.forward); if (rad == Vector3.zero) rad = Vector3.forward;
                rad.Normalize();
                float ang = MeleeShuffleDegLo + (float)rng.NextDouble() * (MeleeShuffleDegHi - MeleeShuffleDegLo);
                float r = Mathf.Max(MeleeShuffleDist, tgt.bodyRadius + m.bodyRadius + 0.6f);
                for (int sgn = rng.NextDouble() < 0.5 ? -1 : 1, i = 0; i < 2; i++, sgn = -sgn)
                {
                    var p = tgt.Feet + RotateY(rad, ang * sgn) * r; p.y = 0f;
                    if (CanAttackFrom(p) && (minionInside == null || minionInside(p))) { dest = p; return true; }
                }
                return false;
            }
            if (kind == 12)
            {
                // 種子槍手：射完後退保持距離
                var rad = Fighter.Flat(m.Feet - tgt.Feet); if (rad == Vector3.zero) rad = Vector3.forward;
                float want = Mathf.Min(KeepHi(kind), FlatDistance(m.Feet, tgt.Feet) + 2.5f);
                for (float k = want; k >= 2f; k -= 0.5f) { var p = tgt.Feet + rad * k; p.y = 0f; if (CanAttackFrom(p)) { dest = p; return true; } }
            }
            if (kind == 1 && m.shotCount >= 2)
            {
                // 火焰射手：射兩發後繞到你側面 90°（逼你轉身）
                m.shotCount = 0;
                var rad = Fighter.Flat(m.Feet - tgt.Feet); if (rad == Vector3.zero) rad = Vector3.forward;
                float keep = Mathf.Clamp(FlatDistance(m.Feet, tgt.Feet), KeepLo(kind), KeepHi(kind));
                for (int sgn = rng.NextDouble() < 0.5 ? -1 : 1, i = 0; i < 2; i++, sgn = -sgn)
                {
                    var flank = RotateY(rad, 90f * sgn);
                    for (float k = keep; k >= 2f; k -= 0.5f) { var p = tgt.Feet + flank * k; p.y = 0f; if (CanAttackFrom(p)) { dest = p; return true; } }
                }
            }
            bool Inside(Vector3 p) => CanAttackFrom(p);   // 走位一律在攻擊區內（海裡、空中的也是）
            float lo = Mathf.Max(1.5f, KeepLo(kind) - 0.5f), hi = Mathf.Max(lo + 0.5f, Mathf.Min(KeepHi(kind) + 0.5f, MinionReach[kind]));
            for (int pass = 0; pass < 2; pass++)
                for (int tries = 0; tries < 24; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = RepositionMin + (float)rng.NextDouble() * (RepositionMax - RepositionMin);
                    var p = m.Feet + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float d = FlatDistance(p, tgt.Feet);
                    if (pass == 0 && (d < lo || d > hi)) continue;   // 第二輪：只要不貼到玩家身上就好
                    if (d < 1.2f || !Inside(p)) continue;
                    dest = new Vector3(p.x, m.Feet.y, p.z);
                    return true;
                }
            // 都找不到（卡在角落）：往玩家那一側側移 1m（玩家在場地裡，往那邊走一定比較進來）
            var toward = Fighter.Flat(tgt.Feet - m.Feet); if (toward == Vector3.zero) toward = Vector3.forward;
            var side = new Vector3(-toward.z, 0f, toward.x) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            var q = m.Feet + (toward * 0.7f + side * 0.7f).normalized * 1f;
            dest = new Vector3(q.x, m.Feet.y, q.z);
            return true;
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
            int before = projectiles.Count;
            MinionStrikeCore(m, tgt, d);
            if (m.attackReadyAt > now) m.attackReadyAt = now + (m.attackReadyAt - now) * MinionAttackGapMul;   // 攻擊頻率調慢
            // 遠程小怪射出去的東西：飛行速度倍率
            if (rangedShotSpeedMul != 1f && IsRanged(m.minionKind))
                for (int j = before; j < projectiles.Count; j++) if (projectiles[j].owner == m) projectiles[j].speedMul *= rangedShotSpeedMul;
        }

        /// <summary>特殊敵人出手後站著喘氣的秒數（趁這時候打牠）</summary>
        public const float SpecialRecover = 1.8f;
        /// <summary>裂岩甲蜥的第二道震環在第一道之後幾秒</summary>
        public const float TwinQuakeGap = 2f;   // 使用者確認：全場躲招排程（兩次跳至少隔 2 秒），原本 0.6 秒改 2 秒

        void SpawnMinionQuake(Fighter m, SkillDef s)
        {
            if (!m.Alive) return;
            var c = m.Feet; c.y = 0f;
            int dmg = Mathf.RoundToInt(s.damage * m.dmgMul);
            shockwaves.Add(new Shockwave { owner = m, skill = s, center = c, born = now, speed = s.speed, maxR = s.rangeMax, dmg = dmg });
            Emit("quake", c, s.color, null);
            if (coopHost) OnSpecialStrike?.Invoke(m, s, c, Vector3.zero, dmg);   // 合作：隊友的手機自己重現震環、自己判定（跳）
        }

        /// <summary>合作（主機）：特殊敵人出招了（小怪、招式、中心、橫掃方向、傷害）→ 送給隊友，隊友自己重現、自己判定</summary>
        public event Action<Fighter, SkillDef, Vector3, Vector3, int> OnSpecialStrike;

        /// <summary>合作（隊友）：主機的特殊敵人出招 → 在我這台重現（震環／橫掃），只判定我自己（跳、蹲是我這台才知道）</summary>
        public void RemoteSpecialStrike(int minionId, string skillId, Vector3 center, Vector3 fwd, int dmg)
        {
            if (!Skills.All.TryGetValue(skillId ?? "", out var s)) return;
            if (EliteCatalog.TrySkill(skillId, out var ed, out int es)) { var em = minions.Find(x => x.minionId == minionId); if (em == null) em = SpawnMinion(center, minionId, ed.kind); EliteStrike(em, s, center, fwd, dmg, true); return; }
            var m = minions.Find(x => x.minionId == minionId);
            if (m == null) return;
            int kind = m.minionKind;
            if (s.id == "m_rootquake" || s.id == "m_twinquake")
            {
                center.y = 0f;
                shockwaves.Add(new Shockwave { owner = m, skill = s, center = center, born = now, speed = s.speed, maxR = s.rangeMax, dmg = dmg });
                Emit("quake", center, s.color, null);
                return;
            }
            float R = SpecialRadius(kind), half = SpecialArcHalf(kind);
            var v = player;
            if (v == null || !v.Alive) return;
            var to = v.Feet - center; to.y = 0f;
            if (to.magnitude > R) return;
            if (half < 180f && to.sqrMagnitude > 1e-4f && fwd != Vector3.zero && Vector3.Angle(fwd, to) > half) return;
            if (v.crouching) { BodyDodge(v, true); Emit("evade", v.Chest, new Color(0.6f, 0.9f, 1f), "蹲下閃過！"); return; }
            HitFoe(m, v, s, dmg);
        }

        /// <summary>特殊敵人的招式：震環（跳）、胸口高度的橫掃（蹲）</summary>
        void SpecialStrike(Fighter m)
        {
            int kind = m.minionKind;
            var s = Skills.All[SpecialSkillId(kind)];
            m.attackReadyAt = now + s.cooldown;
            if (SpecialNeedsJump(kind))
            {
                SpawnMinionQuake(m, s);
                if (kind == 22) timers.Add(new KeyValuePair<float, Action>(now + TwinQuakeGap, () => SpawnMinionQuake(m, s)));
                return;
            }
            // 橫掃：當下在範圍裡（半徑從腳下中心算、潮鞭鰻只打前方半圓）而且沒蹲下的才會被打到
            float R = SpecialRadius(kind), half = SpecialArcHalf(kind);
            var fw = m.lockFwd != Vector3.zero ? m.lockFwd : Fighter.Flat(m.forward);
            int dmg = Mathf.RoundToInt(s.damage * m.dmgMul);
            if (coopHost) OnSpecialStrike?.Invoke(m, s, m.Feet, fw, dmg);   // 合作：隊友自己判定（蹲）
            foreach (var v in FoesOf(m))
            {
                if (!v.Alive || (coopHost && v.isAlly)) continue;   // 隊友由他自己的手機判定
                var to = v.Feet - m.Feet; to.y = 0f;
                if (to.magnitude > R) continue;
                if (half < 180f && to.sqrMagnitude > 1e-4f && fw != Vector3.zero && Vector3.Angle(fw, to) > half) continue;
                if (v.crouching) { BodyDodge(v, true); Emit("evade", v.Chest, new Color(0.6f, 0.9f, 1f), "蹲下閃過！"); continue; }
                HitFoe(m, v, s, dmg);
            }
            Emit("minionattack", m.Feet, s.color, null);
        }

        void MinionStrikeCore(Fighter m, Fighter tgt, float d)
        {
            var claw = Skills.All["m_claw"];
            if (IsSpecial(m.minionKind)) { SpecialStrike(m); return; }
            switch (m.minionKind)
            {
                case 1:
                {
                    var fb = Skills.All["m_fireball"];
                    m.attackReadyAt = now + fb.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    Spawn(m, fb, from, (tgt.Chest - from).normalized, Mathf.RoundToInt(fb.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, fb.color, null);
                    m.shotCount++;   // 兩發後繞側面（PickRepositionPoint）
                    return;
                }
                case 2:
                {
                    // 岩石衝鋒獸：刨地完直線衝過去（衝過頭一點）；沒撞到人＝撞牆暈 2 秒（EndDash）
                    var ds = Skills.All["m_dash"];
                    m.attackReadyAt = now + ds.cooldown + ChargerStun;
                    StartDash(m, tgt, Mathf.Min(ChargerDash, d + 1.5f), ds);
                    Emit("minionattack", m.Feet, ds.color, null);
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
                    // 荊棘衝鋒獸：和岩石衝鋒獸一樣刨地後直衝；衝完沿路留下荊棘帶 3 秒（踩到會刺傷）；撞空暈 2 秒
                    var ds = Skills.All["m_dash"];
                    m.attackReadyAt = now + ds.cooldown + ChargerStun;
                    StartDash(m, tgt, Mathf.Min(ChargerDash, d + 1.5f), ds);
                    Emit("minionattack", m.Feet, ds.color, null);
                    return;
                }
                case 9:
                {
                    // 藤蔓術士：從自己長到玩家當下位置的藤蔓（0.6 秒長滿，趁這時候閃開），持續 3 秒，碰到就受傷
                    var vn = Skills.All["m_vine"];
                    m.attackReadyAt = now + vn.cooldown;
                    AddLane("vine", m.Feet, m.lockFwd != Vector3.zero ? m.Feet + m.lockFwd * Mathf.Min(vn.rangeMax, FlatDistance(m.Feet, tgt.Feet)) : tgt.Feet, 0.6f, VineStay, vn.radius, vn, Mathf.RoundToInt(vn.damage * m.dmgMul), false, m);   // 沿預警的路線長出來；碰到定身 1.5 秒（蹲下躲）
                    Emit("minionattack", m.Feet, vn.color, null);
                    return;
                }
                case 13:
                {
                    // 跳躍魚：從海裡（或礁石上）朝玩家現在的位置飛撲，拋物線跳起
                    var lp = Skills.All["m_leap"];
                    m.attackReadyAt = now + FishCycle;
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
                    if (d <= MeleeReach(16, pc.rangeMax) + 0.1f + tgt.bodyRadius && InMeleeFan(m, tgt))   // 前方扇形
                    {
                        int dmg = Mathf.RoundToInt(pc.damage * m.dmgMul);
                        if (tgt.dodgeUntil > now && SidestepInvuln) Emit("evade", tgt.Chest, pc.color, "閃避！");
                        else if (tgt.isAlly) { Emit("allyhit", tgt.Chest, pc.color, null); OnAllyHit?.Invoke(tgt, pc, dmg, tgt.Chest, 0); }
                        else { ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest, m); SoulVictim(m, tgt, dmg); }
                    }
                    return;
                }
                case 17:
                {
                    // 雷電水母：周圍放電（受傷＋麻痺）
                    var sh = Skills.All["m_shock"];
                    m.attackReadyAt = now + JellyEvery;
                    Emit("shock", m.Feet, sh.color, null);   // 地面點（特效自己往上長成電光球）
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (!v.Alive || FlatDistance(v.Feet, m.Feet) > sh.areaRadius + v.bodyRadius) continue;
                        int dmg = Mathf.RoundToInt(sh.damage * m.dmgMul);
                        if (v.dodgeUntil > now && SidestepInvuln) Emit("evade", v.Chest, sh.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, sh.color, null); OnAllyHit?.Invoke(v, sh, dmg, v.Chest, 0); }
                        else { incoming = sh; ApplyDamageAndEffect(v, dmg, sh.effect, sh.effectDps, sh.effectDur, v.Chest, m); incoming = null; SoulVictim(m, v, dmg); }
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
                        if (v.dodgeUntil > now && SidestepInvuln) Emit("evade", v.Chest, sp.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, sp.color, null); OnAllyHit?.Invoke(v, sp, dmg, v.Chest, 0); }
                        else { incoming = sp; ApplyDamageAndEffect(v, dmg, sp.effect, sp.effectDps, sp.effectDur, v.Chest, m); incoming = null; SoulVictim(m, v, dmg); }
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
                    // 虛空術士：不攻擊，每 8 秒在身邊召喚一隻熔岩小鬼，然後瞬移走（打死牠小鬼才停）
                    var vb = Skills.All["m_void"];
                    m.attackReadyAt = now + VoidSummonEvery;
                    int aliveNow = 0; foreach (var o in minions) if (o.Alive) aliveNow++;
                    if (aliveNow < (GroupMode ? groupSize : RhythmSpawns ? rhythmMaxAlive : MaxMinions))   // 召喚也算在「場上最多幾隻」裡
                    {
                        var at = m.Feet + RotateY(Fighter.Flat(m.forward), 90f * (rng.NextDouble() < 0.5 ? -1 : 1)) * 0.7f;
                        if (minionInside != null && !minionInside(at)) at = m.Feet;
                        pendingSummons.Add(at);   // 正在走訪 minions：迴圈結束後才真的生出來
                    }
                    Emit("minionattack", m.Feet, vb.color, null);
                    Emit("teleport", m.Feet, new Color(0.6f, 0.3f, 1f), null);
                    return;
                }
                case 5:
                {
                    // 熔岩狙擊手：瞄準 2 秒（紅線）後沿瞄準線射一發高威力子彈，之後重新瞄準 5 秒（紅線出現就側跨）
                    var sn = Skills.All["m_snipe"];
                    m.attackReadyAt = now + SniperRearm;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    var dir = m.lockFwd != Vector3.zero ? m.lockFwd : Fighter.Flat(tgt.Chest - from);
                    dir.y = (tgt.Chest.y - from.y) / Mathf.Max(1f, d); dir.Normalize();
                    Spawn(m, sn, from, dir, Mathf.RoundToInt(sn.damage * 1.5f * m.dmgMul), EffectKind.None, 0f, 0f, false);
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
                        if (v.dodgeUntil > now && SidestepInvuln) Emit("evade", v.Chest, bomb.color, "閃避！");
                        else if (v.isAlly) { Emit("allyhit", v.Chest, bomb.color, null); OnAllyHit?.Invoke(v, bomb, dmg, v.Chest, 0); }
                        else { ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest, m); SoulVictim(m, v, dmg); }
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
                    float reach = MeleeReach(m.minionKind, m.minionKind == 2 ? 1.6f : chomp ? bite.rangeMax + 0.2f : claw.rangeMax) + 0.1f;
                    if (d <= reach + tgt.bodyRadius && InMeleeFan(m, tgt))   // 前方扇形
                    {
                        if (tgt.dodgeUntil > now && SidestepInvuln) Emit("evade", tgt.Chest, claw.color, "閃避！");
                        else if (tgt.isAlly) { Emit("allyhit", tgt.Chest, claw.color, null); OnAllyHit?.Invoke(tgt, claw, dmg, tgt.Chest, 0); }
                        else { ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest, m); SoulVictim(m, tgt, dmg); }
                    }
                    return;
                }
            }
        }

        /// <summary>穿透箭穿過第一個敵人後，傷害降低的比例</summary>
        public const float PierceFalloff = 0.5f;
        /// <summary>法術有沒有打到小怪（玩家方的法術才會）；打到回傳 true</summary>
        bool HitMinions(Projectile p)
        {
            if ((minions.Count == 0 && npcs.Count == 0) || p.owner == enemy || p.owner.isMinion || p.owner.npcSide == 1) return false;
            foreach (var m in HittableBy(p.owner))
            {
                if (!m.Alive || m == p.owner || m.hidden) continue;   // 埋著的食人花打不到
                if (!Touches(p, m)) continue;
                if (p.pierced != null && p.pierced.Contains(m)) continue;   // 穿過的敵人不會再打一次
                bool limited = !p.skill.pierce && p.pierceLeft > 0;   // 冰槍穿透：還能穿幾個
                if (limited)
                {
                    if (p.pierced == null) p.pierced = new HashSet<Fighter>();
                    if (!p.pierced.Add(m)) continue;
                    p.pierceLeft--;
                }
                else if (p.skill.pierce)
                {
                    // 貫穿：同一隻只打一次，箭繼續飛
                    if (p.pierced == null) p.pierced = new HashSet<Fighter>();
                    if (!p.pierced.Add(m)) continue;
                }
                else p.alive = false;
                if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                LastProcs = RollProcs(p.owner, p.skill);
                int pd = PreHit(p.owner, m, p.skill, p.damage, p);
                if (remoteEnemy) { EmitHit(p.skill, p.pos); OnMinionHit?.Invoke(m.minionId, pd, p.skill); }
                else { HitSkill = p.skill; HitCrit = p.crit; ApplyDamageAndEffect(m, pd, p.effect, p.effectDps, p.effectDur, p.pos, p.owner); HitSkill = null; HitCrit = false; ApplyProcs(m, LastProcs, p.pos); }
                OnDealt(p.owner, pd, m, p.skill, p);
                OnProjectileHit?.Invoke(p);
                if (p.skill.groundZone) AddBurn(p, m.Feet);   // 審判砸中小怪：一樣留下審判之地
                if (limited) continue;   // 冰槍穿透：繼續飛
                if (p.skill.pierce)
                {
                    // 穿透第一個敵人之後：傷害 −50%（只降一次，之後打到的都是一半）
                    if (!p.pierceCut) { p.pierceCut = true; p.damage = Mathf.Max(1, Mathf.RoundToInt(p.damage * (1f - PierceFalloff))); }
                    continue;
                }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 雙人連線模式
        // enemy 是「對手的代理」：位置、血量由網路更新；對手的法術與陷阱只顯示。
        // 判定規則（攻擊方判定）：我的法術／陷阱打到對手 → OnRemoteHit 送給對手，由對手自己扣血與套用格擋、護盾、反擊；
        // 對手判定打到我 → ApplyRemoteHit。
        public bool remoteEnemy;
        /// <summary>電腦控制的敵人（小怪、Boss、電腦對手）；雙人對戰的對手是真人，不算</summary>
        public bool EnemyNpc(Fighter o) => o != null && (o.isMinion || (o == enemy && !(remoteEnemy && allies.Count == 0)));
        /// <summary>電腦敵人投射物：飛行速度、命中範圍倍率（使用者：太快、要跑很遠才躲得掉）</summary>
        public const float EnemyShotSpeed = 0.5f, EnemyShotHit = 0.5f;
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
            if (EnemyNpc(p.owner)) { p.speedMul *= EnemyShotSpeed; p.hitMul = EnemyShotHit; }   // 合作：主機送來的 Boss／小怪投射物，和主機一樣慢
            SetCurve(p, curveAmp, pathLen);
            Advance(p, s.speed * p.speedMul * Mathf.Max(0f, elapsed));
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
            incoming = s; ApplyDamageAndEffect(player, damage, s.effect, s.effectDps, s.effectDur, at); incoming = null;
            return false;
        }

        /// <summary>對手：玩家（和隊友）打 enemy；enemy 打它目前的目標（沒有就打玩家）</summary>
        public Fighter Opponent(Fighter f) => f == enemy || f.isMinion ? (f.target != null && f.target.Alive ? f.target : player) : enemy;
        public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        // ------------------------------------------------------------ 3D 範圍判定（身體＝直立膠囊：BodySegment＋bodyRadius）
        /// <summary>站在地上（或貼著地面）：地面型效果（陷阱、火海、毒液區、抗拒法環、冰封領域的冰晶以下）只對這種有效；飛在空中／漂浮的怪物不受影響</summary>
        public static bool OnGround(Fighter v) => v.alt < 0.6f;
        /// <summary>圓柱範圍（地面圓心 c、半徑 r、高度 h）：水平距離 ≤ r＋身體半徑，且身體最低點 ≤ h</summary>
        public static bool InCylinder(Fighter v, Vector3 c, float r, float h)
        {
            if (FlatDistance(c, v.Feet) > r + v.bodyRadius) return false;
            v.BodySegment(out var top, out var bottom);
            return Mathf.Min(top.y, bottom.y) <= c.y + h;
        }
        /// <summary>球形範圍：球心到身體線段的 3D 距離 ≤ r＋身體半徑</summary>
        public static bool InSphere(Fighter v, Vector3 c, float r)
        {
            v.BodySegment(out var a, out var b);
            return DistancePointSegment(c, a, b) <= r + v.bodyRadius;
        }
        /// <summary>近身攻擊：以攻擊者胸口為中心、朝前方的半球（半徑 r）。打得到身體任何一處就算</summary>
        public static bool InFrontHemisphere(Fighter f, Fighter v, float r)
        {
            var c = f.Chest;
            v.BodySegment(out var a, out var b);
            var ab = b - a;
            var q = a + ab * Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            var d = q - c;
            if (d.magnitude > r + v.bodyRadius) return false;
            var fw = Fighter.Flat(f.forward);
            if (fw == Vector3.zero) return true;
            return Vector3.Dot(new Vector3(d.x, 0f, d.z), fw) >= -v.bodyRadius;   // 身體有碰到前半邊就算
        }
        /// <summary>
        /// 法術有沒有碰到身體：一般法球＝球；新月形（劍氣、風刃）＝沿著新月弧取樣的一串小球（和畫面上的新月同樣大小、朝飛行方向）。
        /// </summary>
        public static bool Touches(Projectile p, Fighter v)
        {
            v.BodySegment(out var a, out var b);
            if (!p.skill.crescent) return DistancePointSegment(p.pos, a, b) < (p.skill.radius + v.bodyRadius) * p.hitMul;
            var fw = Fighter.Flat(p.Heading); if (fw == Vector3.zero) fw = Fighter.Flat(p.dir); if (fw == Vector3.zero) fw = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fw);
            float D = Mathf.Max(0.06f, p.skill.radius * 2f), tube = Mathf.Max(0.05f, CrescentTube * D);
            for (int i = 0; i < CrescentSamples; i++)
            {
                var l = CrescentPoint(i / (CrescentSamples - 1f)) * D;
                var w = p.pos + right * l.x + Vector3.up * l.y + fw * l.z;
                if (DistancePointSegment(w, a, b) < (tube + v.bodyRadius) * p.hitMul) return true;
            }
            return false;
        }
        const int CrescentSamples = 9;
        const float CrescentTube = 0.13f;
        /// <summary>新月弧上的點（t＝0..1，單位＝法術直徑；和 SpellFx.Crescent 的 R＝0.55、170° 一樣）</summary>
        public static Vector3 CrescentPoint(float t)
        {
            const float R = 0.55f;
            float a = (t - 0.5f) * 170f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * R, -0.3f * R * (1f - Mathf.Cos(a)), Mathf.Cos(a) * R - R * 0.6f);
        }

        /// <summary>範圍技能的 3D 形狀（配合特效）：神聖制裁＝光錘砸下的光柱、旋風斬＝身邊的刀刃旋風、冰封領域＝地面冰晶、其他＝圓柱</summary>
        static bool InAreaShape(SkillDef s, Fighter v, Vector3 c, float r)
        {
            switch (s.id)
            {
                case "sanction": return InCylinder(v, c, r, 2.2f);     // 光錘從約 2.2m 高砸下
                case "whirlwind": return InCylinder(v, c, r, 1.0f);    // 刀刃旋風在腰部以下
                case "frostfield": return InCylinder(v, c, r, 0.9f);   // 地面冰晶最高約 0.8m
                default: return InCylinder(v, c, r, 2f);
            }
        }
        public float Distance => FlatDistance(player.head, enemy.head);
        public bool Over => (!endless && !enemy.Alive) || stageCleared || stageFailed || (!player.Alive && !AnyAllyAlive);

        // ------------------------------------------------------------ 詠唱
        public bool TryChant(Fighter f, SkillDef s, out string why)
        {
            why = null;
            if (Over) { why = "戰鬥結束"; return false; }
            if (!f.Alive) { why = "你已倒下，等隊友"; return false; }
            if (f.manaCoat && s.cls != "basic") { why = "魔力盾開著只能用奧術飛彈（說「魔力盾 關」再比 3）"; return false; }
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) { why = $"{s.name} 冷卻中"; return false; }
            if (f.mp < CostOf(f, s)) { why = "MP 不足"; return false; }
            if (HpCostOf(f, s) > 0 && f.hp <= HpCostOf(f, s)) { why = "HP 不足（殉道：技能改用 HP 施放）"; return false; }
            if (f.snaredUntil > now && s.self != SelfKind.Cleanse) { why = "被困住，暫時不能施法（牧師可以淨化）"; return false; }
            if (f.silencedUntil > now && s.self != SelfKind.Cleanse) { why = $"被沉默，{Mathf.CeilToInt(f.silencedUntil - now)} 秒內不能唸技能（普攻可以）"; return false; }
            f.charging = s; f.chargeStart = now; f.chargeFrom = f.Feet;
            BonusOnChant(f, s);   // 小目標：施放幾種不同的技能
            if (s.releaseNear) f.hiddenTime = 0f;   // 每次開始蓄力伏擊技能，重新累計離開視線的秒數
            return true;
        }

        // ------------------------------------------------------------ 普通攻擊
        /// <summary>
        /// 普通攻擊：近身（劍士、刺客）＝砍身邊 rangeMax 公尺內的所有敵人；遠程＝朝 aimDir 射出一發（走一般的投射物流程，連線也會同步）。
        /// 正在詠唱技能時不能普攻（呼叫者負責）。回傳有沒有出手。
        /// </summary>
        public bool BasicAttack(Fighter f, SkillDef s, Vector3 aimDir, Vector3 floor, bool locked, Vector3 slashFrom = default, Vector3 slashTo = default)
        {
            if (s == null || f == null || !f.Alive || Over || f.charging != null) return false;
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) return false;
            if (!Skills.IsMeleeBasic(s))
            {
                if (f.isAlly) { f.charging = s; f.chargeStart = now - 1f; return TryRelease(f, aimDir, floor, out _, locked); }
                // 遠程普攻（法師、獵人、牧師；玩家、電腦都一樣）：先畫瞄準預警線，ShotWindup 秒後才朝同一個方向射出
                f.cooldownUntil[s.id] = now + ShotWindup + 0.05f;   // 預警中不重複觸發；射出時換成真正的冷卻
                var start = f.hasCastFrom ? f.castFrom : f.Chest; var dir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Fighter.Flat(f.forward);   // 玩家：預警線從手的位置畫出去
                OnShotWarn?.Invoke(f, start, start + dir * s.rangeMax * f.rangeMul);
                if (!f.isPlayer) f.windupUntil = now + ShotWindup;   // 電腦預警中站定：射出的方向就是預警線
                After(ShotWindup, () =>
                {
                    if (!f.Alive || Over || f.charging != null) return;   // 這段時間開始詠唱技能：這一發取消
                    f.charging = s; f.chargeStart = now - 1f;
                    bool shot = TryRelease(f, dir, floor, out _, locked);
                    if (shot && s.id == "na_kunai") KunaiProcs(f, start, dir);
                    // 法師「奧術連射」：機率馬上再射一發（不用再集氣，冷卻照第一發）
                    if (shot && s.id == "na_mage" && f.arcaneTwin > 0f && rng.NextDouble() < f.arcaneTwin)
                        After(ArcaneTwinGap, () =>
                        {
                            if (!f.Alive || Over || f.charging != null) return;
                            f.cooldownUntil.TryGetValue(s.id, out var keepCd); f.cooldownUntil.Remove(s.id);
                            f.charging = s; f.chargeStart = now - 1f;
                            if (TryRelease(f, dir, floor, out _, locked)) Emit("buff", f.Chest, s.color, "奧術連射！");
                            f.charging = null; f.cooldownUntil[s.id] = keepCd;
                        });
                });
                return true;
            }
            f.cooldownUntil[s.id] = now + s.cooldown * f.basicCdMul * CdFactor(f) * f.HasteMul(now) + (SwordComeOn(f, s) ? f.jianlaiSlow : 0f);   // 普攻冷卻看敏捷（不是精神）；劍來：長劍比較重，集氣多 0.5～2 秒
            if (Skills.IsSwipeBasic(s) && SwordSlash(f, s, slashFrom, slashTo)) return true;   // 劍士、初心者：照刀線砍
            if (s.id == "na_dagger" && f.isPlayer) { PlayerStab(f, s, aimDir); return true; }   // 玩家刺客：集氣完手往前突刺，當下就刺
            if (s.id == "na_dagger" && DaggerStab(f, s)) return true;
            MeleeSweep(f, s, Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now)));
            return true;
        }

        // ------------------------------------------------------------ 劍士普攻：只砍「手劃過的那條線」
        /// <summary>刀線從眼睛往外伸多長：普攻射程＋這段（手臂＋刀身）</summary>
        public const float SlashReach = 0.6f;
        /// <summary>刀線的半寬（公尺）</summary>
        public const float SlashHalfWidth = 0.08f;
        /// <summary>電腦劍士揮砍前的預警時間（秒）：看到紅線就蹲下或退開</summary>
        public const float SlashWindup = 0.45f;
        /// <summary>電腦橫砍的左右張角（0.9 ≈ 左右各 42°）</summary>
        const float SlashSpread = 0.9f;
        /// <summary>劍士揮砍的刀線（誰、刀線兩端（場地座標）、揮砍的起點、true＝電腦出手前的預警）</summary>
        public event Action<Fighter, Vector3, Vector3, Vector3, bool> OnSlashLine;

        /// <summary>
        /// 劍士普攻。玩家：from／to＝手掌劃過的起點、終點（從眼睛出發的方向），只砍到這條線掃過的扇面。
        /// 電腦（沒給方向）：朝目標胸口高度橫砍，先畫預警紅線，SlashWindup 秒後才出手（蹲下或退開就閃得掉）。
        /// 回傳 false＝沒有目標（或玩家沒給刀線），交給一般的半球揮砍。
        /// </summary>
        bool SwordSlash(Fighter f, SkillDef s, Vector3 from, Vector3 to)
        {
            var o = f.head;
            float reach = s.rangeMax * f.rangeMul + SlashReach + f.reachBonus + (SwordComeOn(f, s) ? JianlaiReach : 0f);
            if (from != Vector3.zero && to != Vector3.zero)
            {
                // 玩家也一樣先預警（對手看得到刀線），SlashWindup 秒後才砍下；能不能躲看刀線的位置（蹲低到線下面就砍不到）
                var pa = from.normalized; var pb = to.normalized;
                // 初心者的木棍敲擊（玩家）：手掌劃下去當下就敲（不先預警），一劃＝一下，看得出是自己打的
                if (f.isPlayer && s.id == "na_stick") { SlashStrike(f, s, o, pa, pb, reach, false); return true; }
                OnSlashLine?.Invoke(f, o + pa * reach, o + pb * reach, o, true);
                f.windupUntil = now + SlashWindup;
                After(SlashWindup, () => { if (f.Alive && !Over) SlashStrike(f, s, o, pa, pb, reach, false); });
                return true;
            }
            if (f.isPlayer || f.isAlly) return false;   // 玩家沒給刀線（不該發生）：照舊砍前方半球
            var tgt = f.target != null && f.target.Alive ? f.target : NearestFoe(f, out _);
            if (tgt == null) return false;
            var c = (new Vector3(tgt.head.x, tgt.head.y * 0.82f, tgt.head.z) - o).normalized;   // 胸口高度（依目標身高）
            var side = Vector3.Cross(Vector3.up, Fighter.Flat(c));
            if (side == Vector3.zero) side = Vector3.right;
            var a = (c - side * SlashSpread).normalized; var b = (c + side * SlashSpread).normalized;
            OnSlashLine?.Invoke(f, o + a * reach, o + b * reach, o, true);
            f.windupUntil = now + SlashWindup;   // 預警中站定（玩家退開就打不到）
            After(SlashWindup, () => { if (f.Alive && !Over) SlashStrike(f, s, o, a, b, reach, true); });
            return true;
        }

        /// <summary>忍者苦無普攻：依技能樹機率，同時多射出火／冰／雷苦無（只偏 2～3.5 度，跟主苦無打同一個目標）</summary>
        void KunaiProcs(Fighter f, Vector3 from, Vector3 dir)
        {
            int n = 0;
            void Try(float chance, string id)
            {
                if (chance <= 0f || rng.NextDouble() >= chance) return;
                var k = f.Derived(id); if (k == null) return;
                float ang = (n % 2 == 0 ? 1f : -1f) * (2f + 1.5f * (n / 2)); n++;   // 幾乎同一條線（偏太多會打不到）
                Spawn(f, k, from, RotateY(dir, ang), k.damage, k.effect, k.effectDps, k.effectDur, false);
            }
            Try(f.kunaiFire, "kunai_fire"); Try(f.kunaiIce, "kunai_ice"); Try(f.kunaiBolt, "kunai_bolt");
            if (n > 0) Emit("cast", from, new Color(1f, 0.85f, 0.4f), null);
        }

        /// <summary>遠程普攻的預警時間（秒）：瞄準線出現後才射出</summary>
        public const float ShotWindup = 0.35f;
        /// <summary>遠程普攻的瞄準預警線（誰、起點、終點（場地座標））</summary>
        public event Action<Fighter, Vector3, Vector3> OnShotWarn;

        /// <summary>電腦刺客連刺的預警時間（秒）：紅色扇形出現後才刺，退出 1m 或閃到它側面就打不到</summary>
        public const float StabWindup = 0.4f;

        /// <summary>刺客的普攻（電腦、玩家都一樣）：先畫預警扇形，StabWindup 秒後砍前方半球（電腦預警中站定）</summary>
        bool DaggerStab(Fighter f, SkillDef s)
        {
            if (f.isAlly) return false;
            var tgt = f.target != null && f.target.Alive ? f.target : NearestFoe(f, out _);
            if (tgt == null) return false;
            var o = f.Chest; var c = Fighter.Flat(tgt.Chest - o);
            if (c == Vector3.zero) c = Fighter.Flat(f.forward);
            var side = Vector3.Cross(Vector3.up, c);
            var a = (c - side * 1.2f).normalized; var b = (c + side * 1.2f).normalized;
            float reach = s.rangeMax * f.rangeMul + Fighter.BodyRadius + f.reachBonus;
            OnSlashLine?.Invoke(f, o + a * reach, o + b * reach, o, true);
            f.windupUntil = now + StabWindup;
            After(StabWindup, () => { if (f.Alive && !Over) MeleeSweep(f, s, Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now))); });
            return true;
        }

        /// <summary>玩家刺客的突刺可以刺到準星左右多少度內的敵人</summary>
        public const float StabHalfAngle = 35f;

        /// <summary>
        /// 玩家刺客普攻：集氣完成後手往前突刺，當下就往準星方向刺一下（不先預警）。
        /// 刺到準星方向左右 StabHalfAngle 度、射程內的敵人；發出 "stab" 事件（at＝刺出的尖端），沒刺到人就「刺空」。
        /// </summary>
        void PlayerStab(Fighter f, SkillDef s, Vector3 aimDir)
        {
            var o = f.Chest;
            var dir = Fighter.Flat(aimDir);
            dir = dir == Vector3.zero ? Fighter.Flat(f.forward) : dir.normalized;
            float reach = s.rangeMax * f.rangeMul + Fighter.BodyRadius + f.reachBonus;
            Emit("stab", o + dir * reach, s.color, s.id);
            int dmg = Riposte(f, Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now)));
            int landed = 0;
            foreach (var v in MeleeFoes(f))
            {
                if (v == null || !v.Alive) continue;
                var to = v.Chest - o; to.y = 0f;
                if (to.magnitude > reach + v.bodyRadius) continue;
                if (to.magnitude > v.bodyRadius && Vector3.Angle(dir, to) > StabHalfAngle) continue;   // 貼身（在身體半徑內）不看角度
                MeleeHit(f, s, v, dmg); landed++;
            }
            if (landed == 0) Emit("whiff", o + dir * 1.2f, s.color, "刺空（太遠或沒對準）");
        }

        /// <summary>劍仙・劍來：普攻傷害 +30%、距離 +2m</summary>
        public const float JianlaiReach = 2f, JianlaiDmg = 1.3f, FlyingSwordShare = 0.4f;
        bool SwordComeOn(Fighter f, SkillDef s) => f.swordUntil > now && s != null && s.cls == "basic";
        /// <summary>劍來的普攻集氣加長：「劍來延長」每級（持續 +2 秒）多 0.3 秒，0 級 0.5 秒、5 級 2 秒</summary>
        public static float JianlaiSlowOf(SkillDef s) { int rank = Mathf.Clamp(Mathf.RoundToInt((s.buffDur - 20f) / 2f), 0, 5); return 0.5f + 0.3f * rank; }

        /// <summary>劍仙・御劍術：射出一把飛劍（SoloBattle 在持續時間內照準星方向呼叫）；傷害＝這招傷害的 40%</summary>
        public void FireFlyingSword(Fighter f, Vector3 from, Vector3 dir)
        {
            var src = f.yujianSkill; if (src == null || !f.Alive || Over) return;
            var blade = Skills.All["yj_blade"];
            Spawn(f, blade, from, dir.normalized, Mathf.Max(1, Mathf.RoundToInt(src.damage * FlyingSwordShare)), EffectKind.None, 0f, 0f, false);
            Emit("cast", from, blade.color, null);
        }

        void SlashStrike(Fighter f, SkillDef s, Vector3 o, Vector3 a, Vector3 b, float reach, bool duckable)
        {
            int dmg = Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now) * (SwordComeOn(f, s) ? JianlaiDmg : 1f));
            OnSlashLine?.Invoke(f, o + a * reach, o + b * reach, o, false);
            if (f.comboStrike > 0f && ++f.basicHits % 3 == 0) { dmg = Mathf.RoundToInt(dmg * (1f + f.comboStrike)); Emit("buff", f.Chest, s.color, "連斬・重擊！"); }
            dmg = Riposte(f, dmg);
            int landed = 0;
            foreach (var v in MeleeFoes(f))
            {
                if (v == null || !v.Alive || !InSlash(o, a, b, reach, v)) continue;
                if (duckable && v.crouching) { BodyDodge(v, true); Emit("evade", v.Chest, s.color, "蹲下閃過！"); continue; }
                if (duckable && SidestepInvuln && v.dodgeUntil > now) { Emit("evade", v.Chest, s.color, "閃避！"); continue; }
                MeleeHit(f, s, v, dmg); landed++;
            }
            // 玩家的木棍沒打到人：告訴玩家「揮空」（太遠或方向不對）
            if (landed == 0 && f.isPlayer && (s.id == "na_stick" || s.id == "na_sword")) Emit("whiff", o + Fighter.Flat(Vector3.Lerp(a, b, 0.5f)).normalized * 1.2f, s.color, "揮空（太遠或沒對準）");
        }

        /// <summary>刀線（從 o 出發、方向 a 掃到 b、長 reach）有沒有碰到 v 的身體</summary>
        public static bool InSlash(Vector3 o, Vector3 a, Vector3 b, float reach, Fighter v)
        {
            v.BodySegment(out var top, out var bottom);
            float tol = v.bodyRadius + SlashHalfWidth;
            const int N = 12;
            for (int i = 0; i <= N; i++)
            {
                var d = Vector3.Lerp(a, b, i / (float)N).normalized;   // 刀線不會超過 180°
                if (SegmentDistance(o, o + d * reach, top, bottom) <= tol) return true;
            }
            return false;
        }

        /// <summary>兩條線段之間的最短距離</summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-8f && e <= 1e-8f) return r.magnitude;
            if (a <= 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float bb = Vector3.Dot(d1, d2), den = a * e - bb * bb;
                    s = den > 1e-8f ? Mathf.Clamp01((bb * f - c * e) / den) : 0f;
                    t = (bb * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((bb - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        // Room props share the NPC ID / mhit authority path; ordinary NPC targeting is unchanged.
        static bool IsHostileRoomProp(Fighter f) => f != null && f.npcSide == 1 &&
            f.npcId != null && f.npcId.StartsWith("ev_", StringComparison.Ordinal);

        List<Fighter> MeleeFoes(Fighter f)
        {
            var foes = new List<Fighter>();
            if (f == enemy || f.isMinion) { foreach (var pl in Players()) foes.Add(pl); }
            else
            {
                if (enemy != null) foes.Add(enemy); foes.AddRange(minions);
                if (f.npcSide != 1) foreach (var n in npcs)
                    if (n.Alive && IsHostileRoomProp(n)) foes.Add(n);
            }
            return foes;
        }

        void MeleeHit(Fighter f, SkillDef s, Fighter v, int dmg)
        {
            int dv = PreHit(f, v, s, dmg, null);
            if (remoteEnemy && v == enemy) { EmitHit(s, v.Chest); OnRemoteHit?.Invoke(s, dv, v.Chest, 0); OnDealt(f, dv, v, s); return; }
            if (remoteEnemy && (v.isMinion || IsHostileRoomProp(v))) { EmitHit(s, v.Chest); OnMinionHit?.Invoke(v.minionId, dv, s); OnDealt(f, dv, v, s); return; }
            Hit(v, new Projectile { owner = f, skill = s, pos = v.Chest, dir = Fighter.Flat(v.head - f.head), damage = dv });
        }

        /// <summary>近身揮砍（普攻、斬擊）：打到前方半球（半徑＝技能射程）內的所有敵人</summary>
        void MeleeSweep(Fighter f, SkillDef s, int dmg)
        {
            Emit("basicmelee", f.Feet + Fighter.Flat(f.forward) * 0.5f, s.color, s.id);
            // 劍士「連斬」：普攻每第 3 下是重擊
            if (s.id == "na_sword" && f.comboStrike > 0f && ++f.basicHits % 3 == 0) { dmg = Mathf.RoundToInt(dmg * (1f + f.comboStrike)); Emit("buff", f.Chest, s.color, "連斬・重擊！"); }
            dmg = Riposte(f, dmg);
            foreach (var v in MeleeFoes(f))
                if (v != null && v.Alive && InFrontHemisphere(f, v, s.rangeMax + f.reachBonus)) MeleeHit(f, s, v, dmg);   // 前方半球
        }

        /// <summary>刺客伏擊技能（刺殺、影襲、毒刃）：身邊這個半徑內不能有敵人才能蓄力；蓄力完成後靠近到這個距離就自動出手</summary>
        /// <summary>刺客技能樹「近身本能」：2m 內有敵人時，受到的傷害 −4%／級、閃避率 +3%／級（貼臉打刺客要付出代價）</summary>
        public const float CloseRange = 2f, CloseReducePer = 0.04f, CloseEvadePer = 0.03f;
        public bool CloseInstinct(Fighter f) => f != null && f.closeInstinct > 0f && NearestFoe(f, out var d) != null && d < CloseRange;
        public const float AmbushReach = 1.5f;
        /// <summary>法術飛超過技能射程後，再往前飄這麼遠（公尺）一邊淡出一邊消失</summary>
        public const float FadeDistance = 0.8f;   // 伏擊中靠近到半徑 1.5m（直徑 3m）內：自動出手

        /// <summary>f 最近的敵人（玩家方：Boss／對手＋小怪；敵方：玩家）</summary>
        /// <summary>刺殺流派：離最近的敵人超過這個距離也算「沒被發現」（Boss 一直盯著看也能蓄力）</summary>
        public const float StealthFarDist = 2.5f;
        public bool StealthFar(Fighter f) { var foe = NearestFoe(f, out var d); return foe == null || d > StealthFarDist; }

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
            foreach (var n in npcs) if (n.Alive && n.npcSide >= 1 && n != f) { float d = FlatDistance(f.head, n.head); if (d < dist) { dist = d; best = n; } }
            return best;
        }

        /// <summary>獵人蓄力強化：超過 100% 的部分（0～1，1＝蓄到 200%）</summary>
        public float OverchargeFrac(Fighter f)
        {
            if (f == null || !f.overcharge || f.charging == null || f.charging.charge <= 0f) return 0f;
            float t = ChargeTime(f, f.charging);
            return Mathf.Clamp01((now - f.chargeStart - t) / Mathf.Max(0.01f, t));
        }
        public const float OverchargeDamage = 1.5f;   // 蓄到 200% 時傷害 +150%

        public float ChargeProgress(Fighter f) => f.charging == null ? 0f : f.charging.charge <= 0f ? 1f : Mathf.Clamp01((now - f.chargeStart) / Mathf.Max(0.01f, ChargeTime(f, f.charging)));

        /// <summary>射程狀態：0＝可以、1＝太遠、-1＝太近</summary>
        public int RangeState(Fighter f, SkillDef s)
        {
            if (s.type != SkillType.Projectile) return 0;
            float d = FlatDistance(f.head, Opponent(f).head);
            if (d > s.rangeMax * f.rangeMul + (Skills.IsMeleeBasic(s) ? f.reachBonus : 0f)) return 1;   // 電腦的近戰普攻：多伸出去的距離
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
            if (f.mp < CostOf(f, s)) { f.charging = null; why = "MP 不足"; return false; }
            if (HpCostOf(f, s) > 0 && f.hp <= HpCostOf(f, s)) { f.charging = null; why = "HP 不足"; return false; }
            if (now - f.chargeStart < ChargeTime(f, s)) { why = $"蓄力中 {Mathf.FloorToInt(ChargeProgress(f) * 100)}%"; return false; }
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
            if (f.ampNext && s.cls == "mage") { f.chargeBonus += AmpBonus; Emit("buff", f.Chest, new Color(0.75f, 0.55f, 1f), "增幅！傷害 +50%"); }   // 大魔導士的增幅咒
            f.ampNext = false;
            int mpCost = CostOf(f, s), hpCost = HpCostOf(f, s);
            if (s.id == "wave" && f.waveFreeUntil > now) { f.waveFreeUntil = 0f; Emit("buff", f.Chest, s.color, "劍意：劍氣免費！"); }
            f.mp -= mpCost;
            if (hpCost > 0) { f.hp = Mathf.Max(1f, f.hp - hpCost); Emit("buff", f.Chest, new Color(1f, 0.4f, 0.4f), $"殉道 -{hpCost} HP"); }
            f.cooldownUntil[s.id] = now + s.cooldown * (s.cls == "basic" ? f.basicCdMul : f.cdMul * (f.focusUntil > now ? 0.5f : 1f)) * CdFactor(f);   // 普攻看敏捷、技能看精神
            { float cs = Sty(f, PassiveKind.CastShield); if (cs > 0f && s.cls != "basic") GrantShield(f, f.maxHp * cs, CastShieldDur, "施法護盾"); }   // 流派被動：每放一個技能得到護盾
            f.charging = null;
            if (s.id == "slash" || s.id == "nv_swing")   // 初心者的木棍揮擊和斬擊一樣砍前方半球
            {
                // 斬擊：砍前方半球（半徑＝射程 2m），同時斬開正面飛來的法球；不會飛出去
                if (s.id == "slash" && f.orbSlice > 0f) { f.sliceUntil = now + SliceWindow; SliceNow(f); }   // 斬開法球：要點劍士技能樹「斬開法球」；初心者的木棍揮擊不會
                if (f.waveFlow > 0f) f.waveFreeUntil = now + f.waveFlow;   // 劍意：斬擊後幾秒內劍氣免費
                MeleeSweep(f, s, Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now) * (1f + f.chargeBonus)));
                Emit("cast", f.head, s.color, null);
                return true;
            }

            if (s.releaseNear)
            {
                f.armed = s; f.armedUntil = now + (StealthAmbush(f) ? StealthAmbushWindow : AmbushWindow);
                Emit("armed", f.Chest, s.color, $"{s.name} 伏擊中");
                return true;
            }

            switch (s.type)
            {
                case SkillType.Self: CastSelf(f, s); break;
                case SkillType.Trap:
                {
                    // 連環陷阱：一次設好幾個，左右排開
                    var side = Vector3.Cross(Vector3.up, Fighter.Flat(floorPoint - f.Feet) == Vector3.zero ? Fighter.Flat(f.forward) : Fighter.Flat(floorPoint - f.Feet)).normalized;
                    int n = Mathf.Max(1, s.multi);
                    float gap = s.areaRadius > 0f ? 1.6f : 0.8f;   // 地雷爆炸範圍大：一次放好幾顆時排開一點
                    for (int i = 0; i < n; i++) PlaceTrap(f, s, floorPoint + side * ((i - (n - 1) / 2f) * gap));
                    break;
                }
                case SkillType.Projectile when s.areaFoes:
                {
                    // 玩家方的範圍攻擊（神聖制裁、旋風斬、冰封領域）
                    var c = f.Feet;
                    if (s.atTarget)
                    {
                        c = locked ? Opponent(f).Feet : floorPoint;
                        var off = c - f.Feet; off.y = 0f;
                        if (off.magnitude > s.rangeMax) off = off.normalized * s.rangeMax;
                        c = f.Feet + off; c.y = 0f;
                    }
                    int dmg = Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now));
                    Emit("aoe", c, s.color, s.id);
                    AreaFoes(f, s, c, s.areaRadius, dmg);
                    if (s.id == "sanction") { if (remoteEnemy) OnTaunt?.Invoke(s); else Taunt(f, s.buffAmount); }
                    break;
                }
                case SkillType.Projectile when s.areaRadius > 0f && !s.groundZone:   // 審判、天罰（groundZone）不是這種：以前被當成 Boss 的範圍重擊，光柱根本沒出來
                {
                    // 範圍重擊（Boss 地裂／踐踏；樹根爆發打在目標位置）：放招當下站在半徑內的玩家都受傷；側跨的無敵時間內閃得掉
                    var ac = s.atTarget ? new Vector3(f.aimPoint.x, 0f, f.aimPoint.z) : f.Feet;
                    Emit(s.atTarget ? "roots" : "slam", ac, s.color, s.id);
                    Emit("cast", f.head, s.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (v == f || !v.Alive || FlatDistance(ac, v.Feet) > s.areaRadius + v.bodyRadius) continue;
                        if (v.dodgeUntil > now && SidestepInvuln) { Emit("evade", v.Chest, s.color, "閃避！"); continue; }
                        int ad = Mathf.RoundToInt(s.damage * f.atkMul);   // Boss 攻擊力（人多時更痛）
                        if (v.isAlly) { Emit("allyhit", v.Chest, s.color, null); OnAllyHit?.Invoke(v, s, ad, v.Chest, 0); }
                        else { incoming = s; ApplyDamageAndEffect(v, ad, s.effect, s.effectDps, s.effectDur, v.Chest, f); incoming = null; }
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
                        SpawnLob(f, s, f.head + Vector3.up * 0.3f, at, Mathf.RoundToInt(s.damage * f.dmgMul * f.atkMul), 1.1f + 0.15f * i);
                    }
                    Emit("cast", f.head, s.color, null);
                    break;
                }
                case SkillType.Projectile when s.fromAbove:
                {
                    // 隕石、審判：從敵人（施放當下的位置）正上方砸下，敵人可以趁下落時移動閃開；
                    // 沒鎖定時落在準星指的地面（最遠到射程）
                    // 鎖定時落在「準星方向最近的敵人」腳下（之前固定打 Boss：故事關只有小怪、Boss 還沒出現時，光柱落在看不到的地方，看起來像什麼都沒發生）
                    // 使用者回報「審判放完沒東西出來」：Boss 不在畫面上（locked=false）時以前完全不找敵人，光柱落在準星指的地板（常常在場地外、看不到）。
                    // 現在：先找準星方向的敵人；沒有就找射程內最近的敵人；再沒有才落在準星指的地面
                    if (s.selfCenter)
                    {
                        // 使用者要求：審判改成以自己為中心（以前落在準星指的地方，常常看不到）——光柱落在自己腳下
                        var me = f.Feet; me.y = 0f;
                        Spawn(f, s, me + Vector3.up * 0.2f, Vector3.down, s.damage, s.effect, s.effectDps, s.effectDur, false);
                        Emit("cast", f.head, s.color, null);
                        break;
                    }
                    var aimed = AimedFoe(f, aimDir, s.rangeMax) ?? NearestFoeInRange(f, s.rangeMax);
                    var opp = Opponent(f);
                    var target = aimed != null ? aimed.Feet : opp != null ? opp.Feet : f.Feet + Fighter.Flat(f.forward) * 3f;
                    if (aimed == null && (!locked || opp == null || !opp.Alive))
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
                    if (f.hasCastFrom && s.originHeight <= 0f) hand = f.castFrom;   // 玩家：從畫面上手的位置射出
                    f.hasCastFrom = false;
                    aimDir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Fighter.Flat(f.forward);
                    if (s.originHeight > 0f)
                    {
                        // 水平射出（Boss 橫掃）：從身體前方、固定高度出發
                        hand = new Vector3(f.head.x, s.originHeight, f.head.z) + Fighter.Flat(f.forward) * (f.bodyRadius + 0.3f);
                        aimDir = Fighter.Flat(aimDir); if (aimDir == Vector3.zero) aimDir = Fighter.Flat(f.forward);
                    }
                    if (s.id == "ice")
                    {
                        // 冰槍：隨機從左或右、隨機大小的弧線飛向目標
                        float dist = Mathf.Max(1f, locked ? FlatDistance(f.head, Opponent(f).head) : s.rangeMax * 0.6f);
                        float amp = dist * (0.12f + 0.2f * (float)rng.NextDouble()) * (rng.Next(2) == 0 ? -1f : 1f);
                        Spawn(f, s, hand, aimDir, s.damage, s.effect, s.effectDps, s.effectDur, false, amp, dist);
                        Emit("cast", hand, s.color, null);
                        break;
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
        /// <summary>我打出的傷害（位置、數字、爆擊）：合作時送給隊友顯示</summary>
        public event Action<Vector3, float, bool> OnDealtNumber;
        public const float AmpBonus = 0.5f;   // 大魔導士增幅咒：傷害 +50%
        public event Action<SkillDef> OnTaunt;      // 合作的隊友端：我嘲諷了 → 告訴主機（主機的 Boss 才有仇恨）   // 合作：牧師的輔助技能也要作用在隊友（隊友那支手機自己套用）

        void CastSelf(Fighter f, SkillDef s)
        {
            if (f == player) NoteBuff(f, s);   // 畫面：自己的增益特效
            if (s.self == SelfKind.StyleBuff) { CastStyleBuff(f, s); return; }   // 流派爆發（只作用在自己）
            // 牧師的輔助技能：作用在所有己方單位（自己＋合作隊友）
            if (s.id == "prayheal" && f.isPlayer) { CastPrayHeal(f, s); return; }   // 治療術：補自己＋放出治療光球給隊友（Combat.Friends.cs）
            if ((s.cls == "priest" && s.self != SelfKind.Barrier) || s.team)
            {
                if (f.healMul != 1f && (s.heal > 0 || s.self == SelfKind.HealPct))
                {
                    s = s.Clone();
                    s.heal = Mathf.RoundToInt(s.heal * f.healMul);
                    if (s.self == SelfKind.HealPct) s.buffAmount = Mathf.RoundToInt(s.buffAmount * f.healMul);
                }
                // 治療的仇恨：補到的人數（自己＋活著的隊友）× 治療量
                if (s.heal > 0) { int n = 1; foreach (var a in allies) if (a.Alive) n++; AddThreat(f, s.heal * n); }
                ApplySupport(f, s, null);
                if (f == player && AnyAllyAlive) OnTeamBuff?.Invoke(s);
                return;
            }
            switch (s.self)
            {
                case SelfKind.Heal: AddThreat(f, s.heal); f.healDone += Mathf.Min(s.heal, f.maxHp - f.hp); f.hp = Mathf.Min(f.maxHp, f.hp + s.heal); Emit("heal", f.Chest, s.color, $"+{s.heal}"); return;
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
                // ---- 進階職業
                case SelfKind.Oath:
                    f.selfOathUntil = now + s.buffDur;
                    if (f == player && AnyAllyAlive) OnTeamBuff?.Invoke(s);   // 隊友那邊：受到的傷害轉一部分給我
                    Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內守護 3m 內的隊友");
                    return;
                case SelfKind.Frenzy:
                    f.hp = Mathf.Max(1f, Mathf.Round(f.hp * (1f - s.buffReduce)));
                    f.frenzyUntil = now + s.buffDur;
                    Emit("buff", f.Chest, s.color, $"{s.name}！{s.buffDur:0} 秒內攻速 ×1.5");
                    return;
                case SelfKind.SwordCome: f.swordUntil = now + s.buffDur; f.jianlaiSlow = JianlaiSlowOf(s); Emit("buff", f.Chest, s.color, $"{s.name}！{s.buffDur:0} 秒內普攻傷害 +30%、距離 +{JianlaiReach:0}m"); return;
                case SelfKind.FlyingSwords: f.yujianUntil = now + s.buffDur; f.yujianSkill = s; Emit("buff", f.Chest, s.color, $"{s.name}！飛劍朝準星射出"); return;
                case SelfKind.Undying: f.undyingUntil = now + s.buffDur; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內不會倒下"); return;
                case SelfKind.DeadMark: f.markNextUntil = now + s.buffDur; Emit("buff", f.Chest, s.color, $"{s.name}：下一箭必定爆擊"); return;
                case SelfKind.RollShot: f.rollUntil = now + s.buffDur; f.lastDodgeSeen = f.dodgeUntil; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內側跨會自動射箭"); return;
                case SelfKind.ManaShield: f.manaShieldUntil = now + s.buffDur; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內用 MP 擋傷害"); return;
                case SelfKind.Pet: SpawnPet(f, s, s.petKind, s.petLife); Emit("buff", f.Chest, s.color, $"召喚{s.name}！"); return;
                case SelfKind.Capture: Capture(f, s); return;
                case SelfKind.ShadowClone: SpawnShadowClone(f, s); return;
                case SelfKind.Barrier: AddBarrier(f, s); return;
                case SelfKind.Detonate:
                {
                    int n = 0;
                    foreach (var p in new List<Pet>(pets))
                    {
                        if (!p.alive || p.remote || p.owner != f || p.kind == "clone") continue;
                        n++;
                        var c = new Vector3(p.pos.x, 0f, p.pos.z);
                        Emit("aoe", c, s.color, s.id);
                        foreach (var v in FoesOf(f)) if (InSphere(v, c + Vector3.up * 0.3f, s.areaRadius)) HitFoe(f, v, Skills.All["pet_blast"], Mathf.RoundToInt(p.blast * p.power));   // 爆炸是球形（餵養過的炸得更痛）
                        KillPet(p);
                    }
                    Emit("buff", f.Chest, s.color, n > 0 ? $"{s.name}！引爆 {n} 隻召喚物" : "沒有召喚物可以引爆");
                    return;
                }
                case SelfKind.Substitute: f.subUntil = now + s.buffDur; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0.#} 秒內被打中會換成替身"); return;
                case SelfKind.Clone: f.cloneUntil = now + s.buffDur; SpawnPet(f, s, "clone", s.buffDur); Emit("buff", f.Chest, s.color, $"{s.name}！"); return;
                case SelfKind.Dance: f.danceUntil = now + s.buffDur; Emit("buff", f.Chest, s.color, $"{s.name}：{s.buffDur:0} 秒內伏擊不受限制、距離加倍"); return;
                case SelfKind.Fog:
                {
                    var c = f.Feet;
                    burns.Add(new BurnZone { owner = f, skill = s, pos = c, radius = s.areaRadius, until = now + s.buffDur, dps = s.effectDps });
                    Emit("fogzone", c, s.color, s.id);
                    return;
                }
                case SelfKind.Cleanse:
                    // 淨化：解除中毒、定身、致盲，回一點血
                    f.dots.Clear(); f.snaredUntil = 0f; f.blindUntil = 0f; f.silencedUntil = 0f;
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
                    int n = Mathf.Min(s.multi + ScaleAllies, MaxMinions - minions.Count);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 at = f.Feet;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.6f + (float)rng.NextDouble() * 0.8f;
                            at = f.Feet + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                            if (minionInside == null || minionInside(at)) break;
                        }
                        int sk = Theme == 1 ? (rng.Next(3) == 0 ? 7 : 10) : Theme == 2 ? (rng.Next(2) == 0 ? 13 : 16)
                            : Theme == ArenaTheme ? (rng.Next(2) == 0 ? 2 : 8) : Theme == CanyonTheme ? (rng.Next(2) == 0 ? 0 : 4) : 0;   // 森林：食人花為主；海上：跳躍魚與寄居蟹；角鬥場：衝鋒獸；山道：小鬼與投擲者
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
                case SelfKind.Heal: if (!t.Alive) return; if (from == null) { player.healDone += Mathf.Min(s.heal, t.maxHp - t.hp); Overheal(t, t.hp + s.heal - t.maxHp); } t.hp = Mathf.Min(t.maxHp, t.hp + s.heal); Emit("heal", t.Chest, s.color, $"{by}{s.name} +{s.heal}"); return;
                case SelfKind.Armor: t.armorUntil = now + s.buffDur; t.armorReduce = s.buffReduce; Emit("buff", t.Chest, s.color, by + s.name); return;
                case SelfKind.Cleanse:
                    t.dots.Clear(); t.snaredUntil = 0f; t.blindUntil = 0f; t.silencedUntil = 0f;
                    if (s.heal > 0 && t.Alive) t.hp = Mathf.Min(t.maxHp, t.hp + s.heal);
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
                case SelfKind.Revive:
                    if (!t.Alive) { if (t == player) playerDown = false; t.hp = Mathf.Round(t.maxHp * s.buffAmount / 100f); Emit("heal", t.Chest, s.color, $"{by}{s.name}：復活！"); }
                    else { t.blessUntil = now + s.buffDur; Emit("buff", t.Chest, s.color, $"{by}復活祝福（{s.buffDur:0} 秒）"); }
                    return;
                case SelfKind.HealPct:
                {
                    if (!t.Alive) return;   // 倒下的人只有復活術能救起來
                    float amt = Mathf.Round(t.maxHp * s.buffAmount / 100f);
                    if (from == null) { player.healDone += Mathf.Min(amt, t.maxHp - t.hp); Overheal(t, t.hp + amt - t.maxHp); }
                    t.hp = Mathf.Min(t.maxHp, t.hp + amt);
                    Emit("heal", t.Chest, s.color, $"{by}{s.name} +{amt:0}");
                    return;
                }
                case SelfKind.TeamShield: t.shieldUntil = now + s.buffDur; t.shieldLeft = s.buffAmount; Emit("buff", t.Chest, s.color, $"{by}{s.name}（護盾 {s.buffAmount}）"); return;
                case SelfKind.TeamAtk: t.teamAtkUntil = now + s.buffDur; t.teamAtk = s.buffAmount / 100f; Emit("buff", t.Chest, s.color, $"{by}{s.name}：攻擊 +{s.buffAmount}%（{s.buffDur:0} 秒）"); return;
                case SelfKind.TeamHaste: t.teamHasteUntil = now + s.buffDur; t.teamHaste = s.buffAmount / 100f; Emit("buff", t.Chest, s.color, $"{by}{s.name}：加快 {s.buffAmount}%（{s.buffDur:0} 秒）"); return;
                case SelfKind.TeamRegen: t.teamRegenUntil = now + s.buffDur; t.teamRegen = s.buffAmount; if (from == null) teamRegenBy = player; Emit("buff", t.Chest, s.color, $"{by}{s.name}：每秒 +{s.buffAmount} HP（{s.buffDur:0} 秒）"); return;
                case SelfKind.TeamCrit: t.teamCritUntil = now + s.buffDur; t.teamCrit = s.buffAmount / 100f; Emit("buff", t.Chest, s.color, $"{by}{s.name}：爆擊率 +{s.buffAmount}%（{s.buffDur:0} 秒）"); return;
                default: Emit("buff", t.Chest, s.color, by + s.name); return;
            }
        }

        /// <summary>生命湧泉：每秒回復（我這台只補我自己；是我放的就算進我的治療量）</summary>
        Fighter teamRegenBy;
        void UpdateTeamRegen(float dt)
        {
            var p = player;
            if (p == null || !p.Alive || p.teamRegenUntil <= now || p.teamRegen <= 0f) return;
            float h = Mathf.Min(p.teamRegen * dt, p.maxHp - p.hp);
            if (h <= 0f) return;
            p.hp += h;
            if (teamRegenBy == p) p.healDone += h;
        }

        /// <summary>合作：隊友（牧師）的輔助技能作用在我身上</summary>
        public void ApplyTeamBuff(SkillDef s, string from = "隊友", Fighter by = null)
        {
            if (Over || (!player.Alive && s.self != SelfKind.Revive)) return;
            NoteBuff(player, s);   // 畫面：隊友給我的增益
            if (s.self == SelfKind.Oath)
            {
                // 聖騎士的守護誓言：之後 8 秒，我在他 3m 內受到的傷害有 40% 轉給他
                player.oathUntil = now + s.buffDur; player.oathShare = s.buffReduce; player.oathBy = by;
                Emit("buff", player.Chest, s.color, $"{from}的{s.name}守護著你");
                return;
            }
            ApplySupport(player, s, from);
        }

        void PlaceTrap(Fighter f, SkillDef s, Vector3 at)
        {
            at.y = 0;
            // 同一種陷阱最多 trapMax 個，超過就移除最舊的
            int count = 0;
            for (int i = traps.Count - 1; i >= 0; i--)
                if (traps[i].alive && traps[i].owner == f && traps[i].skill == s && ++count >= s.trapMax * (f.adv == "ranger" ? 2 : 1)) traps[i].alive = false;   // 遊俠：陷阱上限加倍
            var trap = new Trap { owner = f, skill = s, pos = at, armAt = now + s.trapArm, until = now + s.trapLife, id = nextId++ };
            traps.Add(trap);
            Emit("trapSet", at, s.color, $"{s.name} 設置");
            if (f == player) OnTrapPlaced?.Invoke(trap);
        }

        Projectile Spawn(Fighter owner, SkillDef s, Vector3 from, Vector3 dir, int dmg, EffectKind eff, float dps, float dur, bool reflected, float curveAmp = 0f, float pathLen = 0f)
        {
            var p = new Projectile { owner = owner, skill = s, pos = from, basePos = from, dir = dir.normalized, damage = dmg, effect = eff, effectDps = dps, effectDur = dur, reflected = reflected, id = nextId++ };
            if (!reflected && s.id == "ice" && owner.icePierce > 0f) p.pierceLeft = Mathf.RoundToInt(owner.icePierce);   // 冰槍穿透
            if (!reflected && s.id == "wind" && owner.windOrbit > 0f) StartWindOrbit(owner, s, dmg);                        // 風刃迴旋
            float outMul = reflected ? 1f : owner.OutMul(now);
            int eagle = EagleStacks(owner);
            // 獵人「穩定瞄準」：站著不動 2 秒以上，箭的爆擊率提高（以前是必定爆擊，太強）
            bool steady = !reflected && owner.steadyAim > 0f && (s.cls == "archer" || s.id == "na_arrow") && now - owner.stillSince >= SteadyAimTime(owner);
            if (owner.atkMul != 1f || owner.critChance > 0f || owner.TeamCritAt(now) > 0f || owner.rangeMul != 1f || outMul != 1f || eagle > 0 || owner.markNextUntil > now || owner.faith >= FaithMax || steady)
            {
                p.damage = Mathf.RoundToInt(dmg * owner.atkMul * outMul);
                float crit = owner.critChance + owner.TeamCritAt(now) + EagleCrit * eagle + (steady ? owner.steadyAim : 0f);   // 神射手鷹眼：每層 +5%；穩定瞄準：站穩後 +4%／級
                bool forced = !reflected && owner.markNextUntil > now && s.cls == "archer" && s.type == SkillType.Projectile;   // 致命標記、獵殺時刻
                if (forced || (crit > 0f && rng.NextDouble() < crit)) { p.crit = true; p.damage = Mathf.RoundToInt(p.damage * (2f + owner.critDmg)); }
                if (forced) owner.markNextUntil = 0f;
                // 審判者：信仰滿了，這一招傷害 ×2
                if (!reflected && owner.adv == "inquisitor" && owner.faith >= FaithMax && p.damage > 0 && s.cls == "priest")
                { p.damage *= 2; owner.faith = 0; Emit("buff", owner.Chest, new Color(1f, 0.8f, 0.3f), "信仰爆發！傷害 ×2"); }
                p.rangeMul = owner.rangeMul * (1f + 0.2f * eagle);   // 鷹眼：每層射程 +20%
            }
            if (!reflected && p.damage > 0 && s.cls != "pet")
            {
                p.damage = Riposte(owner, p.damage);
                // 牧師「聖印」：普攻累積的聖印滿了，下一個攻擊技能傷害 ×2
                if (owner.holySeal > 0f && owner.seals >= SealNeed(owner) && s.cls == "priest")
                { p.damage *= 2; owner.seals = 0; Emit("buff", owner.Chest, new Color(1f, 0.9f, 0.5f), "聖印爆發！傷害 ×2"); }
            }
            if (!reflected && s.cls == "basic" && owner.basicHoming > 0f) p.homing = owner.basicHoming;
            if (s.id == "m_bubble") { p.homing = BubbleHoming; p.homingUntil = now + BubbleHomeTime; }
            if (weather == Weather.Wind && !FoeSide(owner)) p.speedMul *= WindSpeed;   // 順風：我方的投射物飛快一點   // 泡泡：飛得慢但會追你 3 秒（打得破）
            if ((owner == enemy || owner.isMinion || owner.npcSide >= 0) && owner.target != null) p.target = owner.target;
            if (EnemyNpc(owner)) { p.speedMul *= EnemyShotSpeed; p.hitMul = EnemyShotHit; }   // 電腦敵人的投射物：飛慢一半、命中範圍減半
            SetCurve(p, curveAmp, pathLen);
            projectiles.Add(p);
            // 要給另一支手機顯示的法術：雙人對戰的我方法術；合作模式的我方法術、主機上 Boss 的法術
            if ((owner == player && (remoteEnemy || allies.Count > 0)) || ((owner == enemy || owner.isMinion) && coopHost)) OnSpawn?.Invoke(p);
            return p;
        }

        // ------------------------------------------------------------ 每幀
        // ------------------------------------------------------------ 震地波（故事 Boss）
        /// <summary>從 Boss 腳下往外擴散的地面衝擊環：環經過時人在空中（跳起來）就沒事，否則受傷並短暫定身</summary>
        public class Shockwave { public Fighter owner; public SkillDef skill; public Vector3 center; public float born, speed, maxR, lastR; public int dmg; public readonly HashSet<Fighter> done = new HashSet<Fighter>(); }
        public readonly List<Shockwave> shockwaves = new List<Shockwave>();
        public const float QuakeBand = 0.25f;
        /// <summary>各主題 Boss 的「跳躍招」：熔岩震波、樹根波、潮汐環</summary>
        public static string QuakeOf(int theme) => theme == 1 ? "x_rootwave" : theme == 2 ? "x_ripple" : "x_quake";   // 角鬥場、山道的 Boss 用震地波
        public const float RegenEvery = 10f;   // 自然回復：每 10 秒一次
        public static bool IsQuake(SkillDef s) => s != null && (s.id == "x_quake" || s.id == "x_rootwave" || s.id == "x_ripple" || s.id == "w_quake" || s.id == "w_transition");

        public void SpawnShockwave(Fighter owner, SkillDef s)
        {
            var c = owner.Feet; c.y = 0f;
            shockwaves.Add(new Shockwave { owner = owner, skill = s, center = c, born = now, speed = s.speed, maxR = s.rangeMax, dmg = Mathf.RoundToInt(s.damage * owner.atkMul * owner.OutMul(now)) });
            owner.cooldownUntil[s.id] = now + s.cooldown * owner.cdMul;
            owner.charging = null;
            Emit("quake", c, s.color, s.name + "！");
        }

        void UpdateShockwaves()
        {
            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                var w = shockwaves[i];
                float r = (now - w.born) * w.speed;
                float lim = w.owner.isMinion ? w.maxR + QuakeBand : w.maxR + 1f;   // 小怪（特殊敵人）的震環：到範圍邊緣就停（走出範圍就打不到）
                if (!w.owner.Alive || r > lim) { shockwaves.RemoveAt(i); continue; }
                foreach (var v in FoesOf(w.owner))
                {
                    if (w.done.Contains(v)) continue;
                    if (w.owner.isMinion && v.isAlly && (coopHost || remoteEnemy)) continue;   // 合作：特殊敵人的震環，每個人只判定自己（跳是自己的手機才知道）
                    float d = FlatDistance(w.center, v.Feet);
                    if (EliteCatalog.IsElite(w.owner.minionKind) && w.owner.isMinion && d > w.maxR) continue;
                    if (d < w.lastR - QuakeBand || d > r + QuakeBand) continue;   // 環還沒到／已經過去
                    w.done.Add(v);
                    if (v.airborneUntil > now) { BodyDodge(v, false); Emit("evade", v.Chest, new Color(0.6f, 1f, 0.8f), "跳過了！"); continue; }
                    HitFoe(w.owner, v, w.skill, w.dmg);
                }
                w.lastR = r;
            }
        }

        /// <summary>合作：我倒下了（只有復活術、復活祝福能救起來；藥水、治療、每波回血都不算）</summary>
        bool playerDown;

        public void Update(float dt)
        {
            now += dt;
            if (allies.Count > 0) { if (!player.Alive) playerDown = true; else if (playerDown) player.hp = 0f; }
            if (shockwaves.Count > 0) UpdateShockwaves();
            UpdateTeamRegen(dt);
            if (windOrbits.Count > 0) UpdateWindOrbits(dt);
            // 計時的效果（旋風斬的後兩段…）
            for (int i = 0; i < timers.Count; i++) if (timers[i].Key <= now) { var a = timers[i].Value; timers.RemoveAt(i--); a(); }
            // 站著不動的時間（神射手鷹眼、主教祈禱領域）
            foreach (var pl in Players())
                if (FlatDistance(pl.Feet, pl.stillAnchor) > StillMove) { pl.stillAnchor = pl.Feet; pl.stillSince = now; }
            // 主教的祈禱領域：自己是站著不動的主教，或 3m 內有站著不動的主教隊友 → 每秒回復最大 HP 的 1.5%
            if (player.Alive && player.hp < player.maxHp)
            {
                bool field = player.adv == "bishop" && Still(player);
                foreach (var a in allies) if (!field && a.Alive && a.adv == "bishop" && Still(a) && FlatDistance(a.Feet, player.Feet) <= PrayerField) field = true;
                if (field) player.hp = Mathf.Min(player.maxHp, player.hp + player.maxHp * PrayerRegen * dt);
            }
            if (threat.Count > 0) { var ks = new List<Fighter>(threat.Keys); foreach (var k in ks) threat[k] *= 1f - ThreatDecay * dt; }
            foreach (var f in new[] { player, enemy })
            {
                if (remoteEnemy && f == enemy) continue;   // 對手的 MP、詠唱、持續傷害由對手自己算
                f.chargeBonus = 0f;
                UpdateAmbush(f);
                // 遊俠的翻滾射擊：側跨閃避的瞬間自動朝最近的敵人射一箭
                if (f.rollUntil > now && f.dodgeUntil > now && f.dodgeUntil > f.lastDodgeSeen + 0.01f && f.Alive)
                {
                    f.lastDodgeSeen = f.dodgeUntil;
                    var tgt = NearestFoe(f, out _);
                    if (tgt != null)
                    {
                        var ra = f.Derived("rollarrow");
                        var from = f.Chest + Fighter.Flat(f.forward) * 0.3f;
                        Spawn(f, ra, from, (tgt.Chest - from).normalized, ra.damage, EffectKind.None, 0f, 0f, false);
                        Emit("cast", from, ra.color, null);
                    }
                }
                f.mp = Mathf.Min(f.maxMp, f.mp + f.regen * dt);
                if (f.hpRegen > 0f && f.Alive) f.hp = Mathf.Min(f.maxHp, f.hp + f.hpRegen * dt);
                if (f.regenTick > 0f && f.Alive && now >= f.nextRegenAt)
                {
                    if (f.nextRegenAt > 0f && f.hp < f.maxHp) { f.hp = Mathf.Min(f.maxHp, f.hp + f.regenTick); Emit("heal", f.Chest, new Color(0.5f, 1f, 0.6f), $"+{f.regenTick:0}"); }
                    f.nextRegenAt = now + RegenEvery;
                }
                if (f.charging != null && now - f.chargeStart > f.charging.charge + chargeTimeout + (f.charging.releaseNear ? 8f : 0f))
                { f.charging = null; Emit("timeout", f.Chest, Color.gray, "詠唱逾時"); }
                // 刺客伏擊技能：手掌不在鏡頭前、或身邊 2m 內有敵人 → 蓄力暫停；蓄滿自動進入伏擊（不用放招動作）
                if (f.charging != null && f.charging.releaseNear && (!f.seenByFoe || StealthFar(f))) f.hiddenTime += dt;   // 刺殺流派：離開視線（或離所有敵人 2.5m 以上）的秒數會累計（被看見只是暫停，不會歸零）
                if (f.charging != null && f.charging.releaseNear)
                {
                    // 刺殺流派：離開敵人視線 1 秒＝判定完成（不用等蓄力、不用手掌）
                    if (StealthAmbush(f) && ChargeProgress(f) < 1f && f.hiddenTime >= StealthTime)
                    { f.chargeStart = now - ChargeTime(f, f.charging) - 0.01f; Emit("buff", f.Chest, f.charging.color, "躲開視線！刺殺準備完成，靠近就出手"); }
                    if (ChargeProgress(f) < 1f)
                    {
                        if (!f.palmVisible) f.chargeStart += dt;   // 手掌不在鏡頭前：蓄力暫停（敵人貼身也照樣蓄力，蓄滿時已經在身邊就直接出手）
                    }
                    else TryRelease(f, Fighter.Flat(f.forward), f.Feet, out _);
                }
                // 刺殺準備：蓄力完成前離開原地太遠 → 失敗
                // 獵人：整個蓄力期間（包含蓄滿後拉著弓）都不能離開原地直徑 1m
                if (f.charging != null && f.charging.stillRadius > 0f && !(f.adv == "ranger" && f.charging.cls == "archer") && (ChargeProgress(f) < 1f || f.charging.cls == "archer") && FlatDistance(f.Feet, f.chargeFrom) > f.charging.stillRadius)
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
            UpdateClash();   // 法術角力
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
            if (explore != null) UpdateExplore(dt);   // 故事探索（Combat.Explore.cs）
            UpdateNpcs(dt);   // 副本的劇情人物（Combat.Npc.cs）
            Time_dt = dt;
            UpdateEndless();
            UpdateTraps();
            UpdateFalconPatrol();
            UpdateBurns(dt);
            UpdateBarriers();   // 主教的聖域障壁（Combat.Friends.cs）
            UpdatePools();
            UpdateLanes();
            UpdatePets(dt);
            UpdateGalleryTraps();
            UpdateEnemyCues(dt);   // 補師、輔助型、人形敵人的招式：判定自己有沒有躲過（Combat.EnemySkills.cs）
            UpdateCarpet(dt);   // 天穹飛毯：判定自己有沒有躲過障礙（Combat.Carpet.cs）
            UpdateHazards();   // 角鬥場的斧頭與地刺、山道的落石（Combat.Hazards.cs）
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
            if ((owner == enemy || owner.isMinion || owner.npcSide >= 0) && owner.target != null) p.target = owner.target;
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
                    if (v.dodgeUntil > now && SidestepInvuln) { Emit("evade", v.Chest, p.skill.color, "閃避！"); continue; }
                    if (v.isAlly) { Emit("allyhit", v.Chest, p.skill.color, null); OnAllyHit?.Invoke(v, p.skill, p.damage, v.Chest, 0); }
                    else { incoming = p.skill; ApplyDamageAndEffect(v, p.damage, p.effect, p.effectDps, p.effectDur, v.Chest, p.owner); incoming = null; }
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
        /// <summary>
        /// 加上持續傷害。打在玩家身上時，同一種（每秒傷害一樣）的只會刷新時間、不會疊加
        /// （以前站在毒液區裡每秒疊一層中毒，毒液噴花的傷害是其他小怪的 4～5 倍）。打在敵人身上照舊可以疊。
        /// </summary>
        void AddDot(Fighter target, float dps, float dur)
        {
            if (target.isPlayer || target.isAlly)
            {
                var same = target.dots.Find(d => Mathf.Abs(d.dps - dps) < 0.01f);
                if (same != null) { same.until = Mathf.Max(same.until, now + dur); return; }
            }
            target.dots.Add(new Fighter.Dot { dps = dps, until = now + dur });
        }

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
                    if (!v.Alive || !OnGround(v) || FlatDistance(v.Feet, pl.pos) > pl.radius + 0.1f) continue;
                    if (pl.next.TryGetValue(v, out var t) && now < t) continue;
                    pl.next[v] = now + 1f;
                    if (v.isAlly) { Emit("allyhit", v.Chest, ps.color, null); OnAllyHit?.Invoke(v, ps, ps.damage, v.Chest, 0); }
                    else { incoming = ps; ApplyDamageAndEffect(v, ps.damage, ps.effect, ps.effectDps, ps.effectDur, v.Chest); incoming = null; Emit("poolstep", v.Feet, ps.color, "踩到毒液！"); }
                }
            }
            pools.RemoveAll(x => !x.alive);
        }

        /// <summary>地上的長條：藤蔓（碰到就受傷）、衝刺路線（只是提示）</summary>
        public class Lane
        {
            public string kind; public Vector3 a, b; public float start, grow, until, width; public int damage;
            public SkillDef skill; public bool remote, alive = true;
            public Fighter owner;   // 放的小怪（死了這條就消失）
            public readonly Dictionary<Fighter, float> next = new Dictionary<Fighter, float>();
            public Vector3 Tip(float now) => grow <= 0f ? b : Vector3.Lerp(a, b, Mathf.Clamp01((now - start) / grow));
        }
        public readonly List<Lane> lanes = new List<Lane>();
        public event Action<Lane> OnLane;   // 新的長條（畫面用；主機也拿來送給隊友）

        Lane AddLane(string kind, Vector3 a, Vector3 b, float grow, float life, float width, SkillDef s, int dmg, bool remote, Fighter owner = null)
        {
            a.y = 0f; b.y = 0f;
            var ln = new Lane { kind = kind, a = a, b = b, start = now, grow = grow, until = now + life, width = width, skill = s, damage = dmg, remote = remote, owner = owner };
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
                if (now > ln.until || (ln.owner != null && !ln.owner.Alive)) { ln.alive = false; continue; }   // 放的小怪死了：藤蔓、荊棘、預警線一起消失
                if (ln.remote || (ln.kind != "vine" && ln.kind != "thorn")) continue;
                bool thorn = ln.kind == "thorn";
                var tip = ln.Tip(now);
                foreach (var v in new List<Fighter>(Players()))
                {
                    if (!v.Alive || DistancePointSegment(v.Feet, ln.a, tip) > ln.width + v.bodyRadius) continue;
                    if (ln.next.TryGetValue(v, out var t) && now < t) continue;
                    ln.next[v] = now + 1f;
                    if (v.dodgeUntil > now && SidestepInvuln) { Emit("evade", v.Chest, ln.skill.color, "閃避！"); continue; }
                    if (!thorn && v.crouching) { Emit("evade", v.Chest, ln.skill.color, "蹲下躲過藤蔓！"); continue; }   // 藤蔓從腰的高度掃過：蹲下躲得掉
                    if (v.isAlly) { Emit("allyhit", v.Chest, ln.skill.color, null); OnAllyHit?.Invoke(v, ln.skill, ln.damage, v.Chest, 0); }
                    else if (thorn) { ApplyDamageAndEffect(v, ln.damage, EffectKind.None, 0f, 0f, v.Chest); Emit("vinehit", v.Chest, ln.skill.color, "踩到荊棘！"); }
                    else { ApplyDamageAndEffect(v, ln.damage, EffectKind.Snare, 0f, VineSnare, v.Chest); Emit("vinehit", v.Chest, ln.skill.color, "被藤蔓纏住！"); }   // 定身 1.5 秒
                }
            }
            lanes.RemoveAll(x => !x.alive);
        }

        /// <summary>荊棘衝鋒獸衝刺一步：撞到玩家就受傷並停下；衝出場地也停</summary>
        static SkillDef DashSkill(int kind) => Skills.All[kind == 13 ? "m_leap" : kind == 14 ? "m_dive" : "m_dash"];
        /// <summary>衝刺速度（海鳥吃 birdSpeedMul）</summary>
        float DashSpeed(Fighter m) => DashSkill(m.minionKind).speed * (m.minionKind == 14 ? birdSpeedMul : 1f);

        /// <summary>開始衝刺：朝目標現在的位置衝 len 公尺；地上畫出路線（衝刺開始才畫）</summary>
        void StartDash(Fighter m, Fighter tgt, float len, SkillDef ds)
        {
            var dir = Fighter.Flat(tgt.Feet - m.Feet); if (dir == Vector3.zero) dir = Fighter.Flat(m.forward);
            float sp = DashSpeed(m);
            m.dashDir = dir; m.dashHit = false; m.dashStart = now; m.dashUntil = now + len / sp; m.dashFrom = m.Feet;
            AddLane("dash", m.Feet, m.Feet + dir * len, 0f, len / sp + 0.15f, m.bodyRadius, ds, 0, false, m);
        }

        void EndDash(Fighter m)
        {
            m.dashUntil = 0f;
            int kind = m.minionKind;
            if (kind == 8 && FlatDistance(m.dashFrom, m.Feet) > 0.5f) AddLane("thorn", m.dashFrom, m.Feet, 0f, ThornLife, 0.35f, Skills.All["m_dash"], Mathf.RoundToInt(3f * m.dmgMul), false, m);   // 沿路留下荊棘帶
            if (kind == 13)
            {
                // 跳躍魚落地（掉回海裡或摔在礁石上），停 1.5 秒
                m.alt = 0f; m.head.y = MinionHead[kind];
                Emit("splash", m.Feet, Skills.All["m_leap"].color, null);
                m.recoverUntil = now + 0.8f;
                m.teleportAt = now + 0.8f;   // 停一下就跳回海裡
            }
            else if (kind == 14) LandToRest(m);   // 海鳥：俯衝完落到場內地上休息，再飛回空中
            else if ((kind == 2 || kind == 8) && !m.dashHit) { m.recoverUntil = now + ChargerStun; Emit("minionstun", m.Feet, Skills.All["m_dash"].color, "撞牆了！"); }   // 衝鋒獸衝過頭：暈 2 秒（趁現在打）
            else m.recoverUntil = now + MinionRecover;
        }

        /// <summary>浮空／飛行的小怪出手後降落的休息時間（秒）、降落速度（公尺／秒）</summary>
        public const float AirRest = 3f, AirLandSpeed = 3f;
        /// <summary>降到場內地上休息：在場外就先拉回場內最近的地方</summary>
        void LandToRest(Fighter m)
        {
            if (!CanAttackFrom(m.Feet))
            {
                var p = m.Feet;
                for (int i = 0; i < 60 && !CanAttackFrom(p); i++) p = Vector3.MoveTowards(p, Vector3.zero, 0.15f);   // 往場地中心拉
                m.head = new Vector3(p.x, m.head.y, p.z);
            }
            m.restUntil = now + AirRest; m.recoverUntil = m.restUntil;
            Emit("minionland", m.Feet, Color.white, null);
        }

        void DashStep(Fighter m, float dt)
        {
            int kind = m.minionKind;
            var ds = DashSkill(kind);
            var next = m.Feet + m.dashDir * DashSpeed(m) * dt;
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
                if (v.dodgeUntil > now && SidestepInvuln) Emit("evade", v.Chest, ds.color, "閃避！");
                else if (v.isAlly) { Emit("allyhit", v.Chest, ds.color, null); OnAllyHit?.Invoke(v, ds, dmg, v.Chest, 0); }
                else { ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest, m); SoulVictim(m, v, dmg); }
                if (kind == 8 || kind == 2) { EndDash(m); m.recoverUntil = now + MinionRecover; }   // 衝鋒獸撞到就停（荊棘帶也留）；魚和鳥衝完全程
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
                if (p.sliceClashed) continue;
                // 使用者要求：斬開法球改成「對撞」——斬擊和法術撞上就爆炸；劍士維持手勢＝斬碎法術，放掉＝法術突破、加速衝過來
                p.sliceClashed = true;
                Emit("clash", p.pos, p.skill.color, "💥 劍氣與法術對撞！");
                bool hold = f == player ? (playerSpellHold?.Invoke() ?? false) : (remoteEnemy || rng.NextDouble() < 0.5);   // 對手手機上的斬擊：由對手那支手機決定（會送 slice 過來）
                if (!hold)
                {
                    p.speedMul *= ClashBoost;
                    if (f == player) Emit("clashbreak", f.Chest, p.skill.color, "斬擊被突破了！");
                    if (f == player && p.remote) OnClash?.Invoke(-1, p.id);   // 告訴對手：他那一發突破了，加速
                    continue;
                }
                p.alive = false;
                Emit("slice", p.pos, p.skill.color, $"斬開 {p.skill.name}！");
                if (p.remote) { slicedRemote.Add(p.id); OnSlice?.Invoke(p.id, true); }
                else if (remoteEnemy || allies.Count > 0) OnSlice?.Invoke(p.id, false);
                return true;
            }
            return false;
        }

        void SliceNow(Fighter f) { foreach (var p in projectiles) TrySlice(p); }

        // ------------------------------------------------------------ 法術對撞（使用者要求：真人法術對轟）
        // 我的法術和對手的法術在半空撞上 → 爆炸；
        //   施法的人「維持手勢」（手還留在鏡頭前、沒有握拳；或手指按著螢幕）→ 這一發撐住、繼續往前飛
        //   放掉手勢 → 這一發被擊破，對方那一發加速（×ClashBoost）衝過來
        // 每支手機只決定「自己那一發」的命運，結果用 OnClash 告訴對方（被擊破的編號、要加速的對方那一發）
        public const float ClashBoost = 1.8f, ClashPad = 0.25f;
        /// <summary>本機玩家現在有沒有維持手勢（SoloBattle 設定）</summary>
        public Func<bool> playerSpellHold;
        /// <summary>我的法術被擊破了：（我那一發的編號、對方要加速的那一發的編號）</summary>
        public event Action<int, int> OnClash;
        bool ClashOwner(Fighter f) => f != null && (f == player || f == enemy && !enemy.isMinion && enemy.cls != null && !enemy.cls.id.StartsWith("boss"));
        static bool Clashable(Projectile p) => p.alive && !p.Fading && !p.reflected && p.damage > 0 && Sliceable(p.skill) && p.skill.cls != "minion";
        bool HoldsSpell(Projectile p) => p.owner == player ? (playerSpellHold?.Invoke() ?? false) : rng.NextDouble() < 0.5;   // 電腦對手：一半機率撐住

        /// <summary>Boss 的法球（使用者要求）：玩家的法術抵銷不了，但打中會讓它變慢（×BossOrbSlow，最慢 BossOrbMinSpeed 倍）；我的法術被吃掉</summary>
        public const float BossOrbSlow = 0.6f, BossOrbMinSpeed = 0.3f;
        bool IsBossOrb(Projectile q) => q.alive && !q.Fading && q.owner == enemy && enemy.cls != null && enemy.cls.id.StartsWith("boss") && q.damage > 0 && Sliceable(q.skill);

        /// <summary>多顆小法球（御劍術的飛劍、連射、普攻…）：攻擊力不到對方 ChipRatio 倍就不角力，改成「削」——小的那一發消失，大的那一發的攻擊力扣掉它的攻擊力，扣到 0 就碎掉</summary>
        public const float ChipRatio = 0.4f;
        static bool IsChip(Projectile small, Projectile big) => small.damage < big.damage * ChipRatio;
        /// <summary>連線：我的小法球削了對手的法術（對手那一發的編號、削掉多少）</summary>
        public event Action<int, int> OnChip;

        bool TryClash(Projectile p)
        {
            if (!Clashable(p) || p.owner != player && !ClashOwner(p.owner)) return false;
            // 角力中：雙方之後的法術（御劍術的飛劍、普攻…）飛到撞擊點 → 被吸進去、幫自己那邊推
            if (clash != null && p != clash.mine && p != clash.theirs && (p.owner == player || p.owner == clash.foe) && Vector3.Distance(p.pos, ClashPoint(clash.t)) <= p.skill.radius + 0.35f)
            {
                p.alive = false;
                if (!p.remote) ClashFeed(p.owner == player, p.damage);   // 對手的：他那邊會送 clp（帶攻擊力）過來
                return true;
            }
            if (p.remote) return false;
            if (p.owner == player)
            {
                foreach (var q in projectiles)
                {
                    if (!IsBossOrb(q) || Vector3.Distance(p.pos, q.pos) > p.skill.radius + q.skill.radius + ClashPad) continue;
                    p.alive = false;
                    float before = q.speedMul;
                    if (q.slowBase < 0f) q.slowBase = q.speedMul;
                    q.speedMul = Mathf.Max(q.slowBase * BossOrbMinSpeed, q.speedMul * BossOrbSlow);
                    Emit("clashbreak", q.pos, q.skill.color, q.speedMul < before ? "法球減速！" : "法球已經最慢了");
                    return true;
                }
                // 一般小怪的投射物：直接打碎，我的法術繼續飛（使用者要求）
                foreach (var q in projectiles)
                {
                    if (!q.alive || q.Fading || q.locked || q.owner == null || !q.owner.isMinion || q.damage <= 0 || !Sliceable(q.skill)) continue;
                    if (Vector3.Distance(p.pos, q.pos) > p.skill.radius + q.skill.radius + ClashPad) continue;
                    q.alive = false;
                    Emit("clashbreak", q.pos, q.skill.color, "擊碎！");
                }
                if (TryCueClash(p)) return !p.alive;
            }
            if (!ClashOwner(p.owner)) return false;
            foreach (var q in projectiles)
            {
                if (q == p || q.locked || !Clashable(q) || !ClashOwner(q.owner) || q.owner == p.owner) continue;
                if (p.clashedWith == q || q.clashedWith == p) continue;
                if (Vector3.Distance(p.pos, q.pos) > p.skill.radius + q.skill.radius + ClashPad) continue;
                // 多顆小法球：削，不角力（小的那一發的手機負責：對手的小法球由對手那邊算，這裡只把它收掉）
                if (IsChip(p, q) || IsChip(q, p))
                {
                    var small = IsChip(p, q) ? p : q; var big = small == p ? q : p;
                    small.alive = false; small.clashedWith = big;
                    Emit("clashpush", small.pos, small.skill.color, null);
                    if (small.remote) return !p.alive;   // 對手的小法球：他的手機會送 chip 過來
                    big.damage -= small.damage;
                    if (big.remote) OnChip?.Invoke(big.id, small.damage);
                    if (big.damage <= 0) { big.alive = false; Emit("clashbreak", big.pos, big.skill.color, big.owner == player ? "法術被削碎了！" : "削碎了對手的法術！"); }
                    return !p.alive;
                }
                if (clash != null) continue;
                if (!clashAuthority && (q.remote || p.remote)) return false;   // 連線：角力由房主開始（房主送 cls 過來）
                var mine = p.owner == player ? p : q; var theirs = mine == p ? q : p;
                StartClash(mine, theirs, enemy, ClashT((p.pos + q.pos) * 0.5f, enemy), false);
                if (theirs.remote) OnClashStart?.Invoke(mine.id, theirs.id, clash.t);
                return false;
            }
            return false;
        }

        /// <summary>連線：對手的小法球削了我的法術</summary>
        public void ApplyRemoteChip(int myId, int dmg)
        {
            foreach (var p in projectiles)
                if (p.alive && !p.remote && p.id == myId)
                {
                    p.damage -= dmg;
                    if (p.damage <= 0) { p.alive = false; Emit("clashbreak", p.pos, p.skill.color, "法術被削碎了！"); }
                }
        }

        /// <summary>
        /// 我的法術撞上小怪的遠程招（EnemyCue 的投射物：菁英、人形、第二招）：
        ///   菁英、人形 → 角力（電腦會推），輸了那一發照它的傷害往我衝；一般小怪 → 直接打碎
        /// </summary>
        bool TryCueClash(Projectile p)
        {
            foreach (var c in enemyCues)
            {
                if (c.speed <= 0f || c.judged || c.cancelled || now < c.hit) continue;
                var dir = c.dir; dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) continue; dir.Normalize();
                float d = (now - c.hit) * c.speed; if (d > c.size) continue;
                var at = c.origin + Vector3.up * 1.2f + dir * d;
                if (Vector3.Distance(p.pos, at) > p.skill.radius + 0.3f + ClashPad) continue;
                var owner = MinionById(c.owner);
                c.cancelled = true; OnEnemyCueCancel?.Invoke(c);
                bool strong = owner != null && owner.Alive && (owner.human > 0 || EliteCatalog.IsElite(owner.minionKind)) && clash == null;
                if (!strong) { Emit("clashbreak", at, p.skill.color, "擊碎！"); return true; }
                // 用一發本機的替身投射物和我的法術角力（輸了它就照原本的傷害往我衝）
                var proxySkill = Skills.All.TryGetValue("m_fireball", out var fb) ? fb : p.skill;
                var proxy = Spawn(owner, proxySkill, at, dir, c.dmg, EffectKind.None, 0f, 0f, false);
                proxy.damage = c.dmg; proxy.target = player;
                StartClash(p, proxy, owner, ClashT(at, owner), true);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 法術角力（使用者要求）
        // 兩發法術撞上 → 卡在半空互推：每做一次手勢（手舉到鏡頭前／點一下螢幕）就往對方推一段，
        // 推的距離看「自己那一發的攻擊力 ÷ 兩發平均」：攻擊力高的推一下走很多、低的只推一點點。
        // 被壓過 ClashBreakDist 公尺（或推到對方面前）→ 贏的那一發穿破、加速衝過去；ClashTime 秒內沒分出勝負 → 兩發一起炸掉。
        // 角力中雙方再打過來的法術（御劍術的飛劍、普攻…）飛到撞擊點會被吸進去，照它的攻擊力幫自己那邊推。
        // 對手：真人（房主算，隊友的手勢送過去）、電腦對手；菁英、人形敵人的遠程招（本機算，電腦會推）。
        public sealed class SpellClash { public Projectile mine, theirs; public Fighter foe; public bool local; public float t, t0, until, nextSync, nextAi; }
        public SpellClash clash;
        public bool clashAuthority = true;
        public const float ClashTime = 5f, ClashWin = 0.9f, ClashPushMeters = 0.5f, ClashSync = 0.1f;
        /// <summary>使用者要求：被壓過去 ClashBreakDist 公尺（從撞上的地方算）就穿破被壓倒的那一發，以 ×ClashBoost 飛出去</summary>
        public const float ClashBreakDist = 1.2f;
        public event Action<int, int, float> OnClashStart;   // 我那一發、對方那一發、位置（0＝我、1＝對方）
        public event Action<float> OnClashState;
        public event Action<int> OnClashEnd;                 // 1＝房主那邊贏、−1＝對方贏、0＝平手
        public event Action<int> OnClashPulse;               // 隊友推了一下（推的攻擊力；0＝用角力中那一發的）

        float ClashT(Vector3 at, Fighter foe)
        {
            var a = player.Chest; var b = foe.Chest; var ab = b - a;
            return Mathf.Clamp(Vector3.Dot(at - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude), 0.15f, 0.85f);
        }
        Fighter ClashFoe => clash != null && clash.foe != null ? clash.foe : enemy;
        Vector3 ClashPoint(float t) => Vector3.Lerp(player.Chest, ClashFoe.Chest, t);
        float ClashLen => Mathf.Max(1f, FlatDistance(player.Feet, ClashFoe.Feet));

        void StartClash(Projectile mine, Projectile theirs, Fighter foe, float t, bool local)
        {
            mine.locked = theirs.locked = true; mine.clashedWith = theirs; theirs.clashedWith = mine;
            clash = new SpellClash { mine = mine, theirs = theirs, foe = foe, local = local, t = t, t0 = t, until = now + ClashTime, nextAi = now + 0.5f };
            Emit("clash", ClashPoint(t), Color.Lerp(mine.skill.color, theirs.skill.color, 0.5f), "💥 法術角力！快做手勢往前推！");
        }

        /// <summary>攻擊力 dmg 推一下走多遠（0～1 的比例）：dmg ÷ 兩發平均 × ClashPushMeters ÷ 兩人距離</summary>
        float ClashPushBy(int dmg)
        {
            float avg = Mathf.Max(1f, (clash.mine.damage + clash.theirs.damage) * 0.5f);
            return ClashPushMeters * Mathf.Clamp(dmg / avg, 0.1f, 3f) / ClashLen;
        }
        bool ClashSolo => clash != null && (clash.local || clashAuthority);

        /// <summary>本機玩家做了一次手勢</summary>
        public void ClashPulse()
        {
            if (clash == null) return;
            Emit("clashpush", ClashPoint(clash.t), clash.mine.skill.color, null);
            if (!ClashSolo) { OnClashPulse?.Invoke(0); return; }
            clash.t += ClashPushBy(clash.mine.damage);
        }
        /// <summary>角力中又有法術飛進撞擊點：照它的攻擊力幫那一邊推一下</summary>
        void ClashFeed(bool mineSide, int dmg)
        {
            Emit("clashpush", ClashPoint(clash.t), mineSide ? clash.mine.skill.color : clash.theirs.skill.color, null);
            if (!ClashSolo) { if (mineSide) OnClashPulse?.Invoke(Mathf.Max(1, dmg)); return; }
            clash.t += (mineSide ? 1f : -1f) * ClashPushBy(dmg);
        }
        /// <summary>對手推了一下（房主收到；dmg＝0：用他那一發的攻擊力）</summary>
        public void RemoteClashPulse(int dmg = 0)
        {
            if (clash == null || !clashAuthority || clash.local) return;
            clash.t -= ClashPushBy(dmg > 0 ? dmg : clash.theirs.damage);
        }

        void UpdateClash()
        {
            var c = clash; if (c == null) return;
            if (!c.mine.alive || !c.theirs.alive && !c.theirs.remote || c.foe == null || !c.foe.Alive) { EndClashLocal(c.mine.alive ? 1 : -1); return; }
            var axis = (c.foe.Chest - player.Chest).normalized;
            var pt = ClashPoint(c.t);
            c.mine.pos = pt - axis * 0.12f; c.theirs.pos = pt + axis * 0.12f;
            if (!ClashSolo) return;
            if ((c.local || !remoteEnemy) && now >= c.nextAi) { c.nextAi = now + 0.35f + (float)rng.NextDouble() * 0.4f; c.t -= ClashPushBy(c.theirs.damage); }   // 電腦對手、菁英、人形也會推
            if (!c.local && now >= c.nextSync) { c.nextSync = now + ClashSync; OnClashState?.Invoke(c.t); }
            float moved = (c.t - c.t0) * ClashLen;   // 從撞上的地方被推了幾公尺（正＝往對手）
            int r = c.t >= ClashWin || moved >= ClashBreakDist ? 1 : c.t <= 1f - ClashWin || moved <= -ClashBreakDist ? -1 : now >= c.until ? 0 : 2;
            if (r != 2) { if (!c.local) OnClashEnd?.Invoke(r); EndClashLocal(r); }
        }

        /// <summary>結束角力（r：以這支手機來看，1＝我贏、−1＝我輸、0＝平手）</summary>
        void EndClashLocal(int r)
        {
            var c = clash; if (c == null) return;
            var axis = (ClashFoe.Chest - player.Chest).normalized;
            var pt = ClashPoint(Mathf.Clamp01(c.t));
            clash = null;
            c.mine.locked = c.theirs.locked = false;
            if (r == 1)
            {
                c.theirs.alive = false;
                c.mine.pos = pt; c.mine.dir = axis; c.mine.speedMul *= ClashBoost;
                Emit("clashbreak", pt, c.mine.skill.color, "💥 穿破了！法術加速衝過去");
            }
            else if (r == -1)
            {
                c.mine.alive = false;
                c.theirs.pos = pt; c.theirs.dir = -axis; c.theirs.speedMul *= ClashBoost;
                Emit("clashbreak", pt, c.theirs.skill.color, "被穿破了！快閃開");
            }
            else
            {
                c.mine.alive = false; c.theirs.alive = false;
                Emit("clash", pt, Color.white, "兩發法術一起炸開了！");
            }
        }

        /// <summary>隊友端：房主開始角力（房主那一發＝我這裡的遠端那一發、我那一發；位置是房主看過去的）</summary>
        public void ApplyRemoteClashStart(int hostSpellId, int mySpellId, float hostT)
        {
            if (clash != null) return;
            Projectile mine = null, theirs = null;
            foreach (var p in projectiles) { if (!p.alive) continue; if (!p.remote && p.id == mySpellId) mine = p; else if (p.remote && p.id == hostSpellId) theirs = p; }
            if (mine == null || theirs == null) return;
            StartClash(mine, theirs, enemy, 1f - hostT, false);
        }
        public void ApplyRemoteClashState(float hostT) { if (clash != null && !clash.local) clash.t = 1f - hostT; }
        public void ApplyRemoteClashEnd(int hostResult) { if (clash != null && !clash.local) EndClashLocal(-hostResult); }

        /// <summary>對手說他那一發被我的法術擊破了：移除他那一發，我這一發加速</summary>
        public void ApplyRemoteClash(int theirId, int myId)
        {
            foreach (var p in projectiles)
            {
                if (p.alive && p.remote && p.id == theirId) { p.alive = false; Emit("clashbreak", p.pos, p.skill.color, "擊破對手的法術！"); }
                else if (p.alive && !p.remote && p.id == myId) p.speedMul *= ClashBoost;
            }
        }   // 放招當下已經在範圍內的

        /// <summary>騎魚泡泡怪的泡泡：被我方（玩家、隊友、召喚物）的法術碰到就破</summary>
        bool PopBubbles(Projectile p)
        {
            if (p.owner == null || p.owner.isMinion || p.owner == enemy || p.skill.id == "m_bubble") return false;
            bool any = false;
            foreach (var q in projectiles)
            {
                if (!q.alive || q == p || q.skill.id != "m_bubble" || q.owner == null || !q.owner.isMinion) continue;
                if (Vector3.Distance(p.pos, q.pos) > q.skill.radius + 0.15f) continue;
                q.alive = false; any = true;
                Emit("slice", q.pos, q.skill.color, "泡泡破了！");
            }
            return any;
        }

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
                if (p.locked) continue;   // 法術角力中（UpdateClash 管位置）
                if (p.owner != null && p.owner.isMinion && !p.owner.Alive) { p.alive = false; continue; }   // 小怪死了：牠放出去還在飛的招式一起消失（使用者要求：死前放的不會造成傷害）
                if (p.skill != null && p.skill.id == HealOrbId) { StepHealOrb(p, dt); continue; }   // 治療光球：只追隊友、只治療
                if (p.lob) { StepLob(p, dt); continue; }
                var target = p.target != null && p.target.Alive ? p.target : Opponent(p.owner);
                if (p.homing > 0f && (p.homingUntil <= 0f || now < p.homingUntil) && !p.Curved && !p.Fading && !p.remote)
                {
                    // 追蹤：往最近的敵人慢慢轉（每秒最多 homing 度）
                    var ht = NearestFoeOf(p.owner, p.pos);
                    if (ht != null)
                    {
                        var want = (ht.Chest - p.pos).normalized;
                        float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(p.dir, want), -1f, 1f));
                        if (ang > 1e-4f) { float k = Mathf.Min(1f, p.homing * Mathf.Deg2Rad * dt / ang); p.dir = (p.dir * (1f - k) + want * k).normalized; p.basePos = p.pos; }
                    }
                }
                if (p.skill.steer && p.owner != null && p.owner.Alive && !p.Fading && p.traveled > SteerAfter) SteerToAim(p, dt);   // 操魔光龍：跟著準心轉向
                float step = p.skill.speed * p.speedMul * dt;
                // 分小段前進，快的箭也不會穿過身體
                int sub = Mathf.Max(1, Mathf.CeilToInt(step / 0.08f));
                for (int k = 0; k < sub && p.alive; k++)
                {
                    Advance(p, step / sub);
                    if (explore != null) { var wc = explore.map.Cell(p.pos); if (!explore.map.Open(wc.x, wc.y)) { p.alive = false; break; } }   // 故事探索：飛行的招式飛進牆裡就消失
                    if (p.Fading) { if (p.traveled >= p.fadeEnd || p.pos.y < 0f) p.alive = false; continue; }   // 淡出中：不再打到人
                    if (TrySlice(p)) break;
                    if (TryClash(p)) break;   // 法術對撞（使用者要求）：撞到就爆；維持手勢撐住、放掉就被擊破
                    if (!p.remote && PopBubbles(p)) { }     // 我方的攻擊打破泡泡（法術繼續飛）
                    if (!p.remote && TryFeedPet(p)) break;   // 召喚師：打中自己的召喚物＝餵養
                    if (!p.remote && TryHealFriend(p)) break;   // 主教：普攻打中友軍＝治療
                    // 隕石落地：留下火海（我方的才判定傷害；對手的只顯示）
                    if ((p.skill.id == "meteor" || p.skill.groundZone) && p.pos.y <= 0.05f)
                    {
                        p.alive = false;
                        AddBurn(p, p.pos);
                        if (p.skill.selfCenter && !p.remote) SelfCenterBurst(p);   // 審判：周圍的敵人立刻受傷
                        break;
                    }
                    if (p.skill.selfCenter) continue;   // 審判：落到自己腳下前不撞人（不會被旁邊的小怪擋掉、偏離中心）
                    target.BodySegment(out var a, out var b);
                    if (!p.remote && HitMinions(p)) break;   // 先擋在前面的小怪會吃到這一發
                    if (!target.Alive)
                    {
                        // 目標不在了（無盡模式 Boss 還沒出現）：只飛，飛完消失
                        OutOfRange(p, p.skill.rangeMax * p.rangeMul);
                    }
                    else if (p.remote)
                    {
                        // 對手的法術：命中由對手判定（收到結果時移除）；這裡只讓它飛，飛完消失
                        OutOfRange(p, p.skill.rangeMax);
                    }
                    else if (Touches(p, target))
                    {
                        // 閃避：蹲下躲過橫向攻擊、側跨的無敵時間內 → 法術穿過去
                        if (Evades(target, p))
                        {
                            if (!p.evaded) { p.evaded = true; Emit("evade", target.Chest, p.skill.color, p.skill.jumpable ? "跳過了！" : "蹲下躲過！"); if (p.skill.jumpable || p.skill.duckable) BodyDodge(target, p.skill.duckable); }
                            continue;
                        }
                        p.alive = false;
                        if ((p.skill.id == "meteor" || p.skill.groundZone) && !p.reflected) AddBurn(p, target.Feet);   // 直接砸中也留下火海／審判之地
                        if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                        LastProcs = RollProcs(p.owner, p.skill);
                        p.damage = PreHit(p.owner, target, p.skill, p.damage, p);
                        if (remoteEnemy && target == enemy) { EmitHit(p.skill, p.pos, p.crit); OnRemoteHit?.Invoke(p.skill, p.damage, p.pos, p.id); OnDealt(p.owner, p.damage, target, p.skill, p); }
                        else if (target.isAlly) { Emit("allyhit", p.pos, p.skill.color, null); OnAllyHit?.Invoke(target, p.skill, p.damage, p.pos, p.id); OnProjectileHit?.Invoke(p); }   // 主機：Boss 打到隊友
                        else { Hit(target, p); OnProjectileHit?.Invoke(p); }
                    }
                    else if (OutOfRange(p, p.skill.rangeMax * p.rangeMul)) Emit("miss", p.pos, p.skill.color, null);
                }
            }
            projectiles.RemoveAll(x => !x.alive);
        }

        /// <summary>抗拒法環打到 v（v 的腳在 center 半徑 r 內）：取消蓄力／伏擊，那個技能重新冷卻。回傳有沒有打斷</summary>
        public bool DispelAt(Fighter v, Vector3 center, float r)
        {
            if (v == null || !v.Alive || !OnGround(v) || FlatDistance(v.Feet, center) > r + v.bodyRadius) return false;   // 地面法環：飛在空中的不受影響
            bool any = false;
            if (v.charging != null) { v.cooldownUntil[v.charging.id] = now + v.charging.cooldown; v.charging = null; any = true; }
            if (v.armed != null) { v.cooldownUntil[v.armed.id] = now + v.armed.cooldown; v.armed = null; any = true; }
            if (any) Emit("dispelled", v.Chest, new Color(0.94f, 0.67f, 0.99f), "被打斷！");
            return any;
        }

        /// <summary>打到地面就消失；飛超過射程 range 就開始淡出（再飄 FadeDistance 才消失）。回傳是不是結束了</summary>
        static bool OutOfRange(Projectile p, float range)
        {
            if (p.pos.y < 0f) { p.alive = false; return true; }
            if (p.traveled > range) { p.fadeEnd = p.traveled + FadeDistance; return true; }
            return false;
        }

        /// <summary>這一發會不會被閃過：蹲著時橫向攻擊（duckable，Boss 的橫掃）打不到</summary>
        public bool Evades(Fighter target, Projectile p) => (SidestepInvuln && target.dodgeUntil > now) || (target.crouching && p.skill.duckable) || (target.airborneUntil > now && p.skill.jumpable);
        /// <summary>側跨沒有無敵時間：要真的移開、讓法術碰不到身體才算閃過（側跨的偵測仍保留給遊俠的翻滾射擊）</summary>
        public const bool SidestepInvuln = false;

        /// <summary>伏擊：時限內和對手的水平距離 ≤ 技能射程 → 自動出手一次</summary>
        void UpdateAmbush(Fighter f)
        {
            var s = f.armed;
            if (s == null) return;
            if (now > f.armedUntil || !f.Alive) { f.armed = null; Emit("armedExpire", f.Chest, Color.gray, $"{s.name} 落空"); return; }
            // 前方半球內最近的敵人（影舞：出手距離加倍）
            float reach = AmbushReach * (f.danceUntil > now ? 2f : 1f) + f.reachBonus, best = float.MaxValue;
            Fighter target = null;
            foreach (var v in FoesOf(f))
            {
                if (!InFrontHemisphere(f, v, reach)) continue;
                float d = FlatDistance(f.Feet, v.Feet);
                if (d < best) { best = d; target = v; }
            }
            if (target == null) return;
            f.armed = null;
            Emit("ambush", target.Chest, s.color, s.name + "！");
            int dmg = Mathf.RoundToInt(s.damage * f.atkMul * f.OutMul(now));
            if (s.execute && target.maxHp > 0 && target.hp < target.maxHp * 0.25f) { dmg *= 3; Emit("crit", target.Chest, s.color, "處決！"); }
            if (f.adv == "shadowdancer")
            {
                // 連殺：3 秒內伏擊技能冷卻歸零、蓄力 −70%
                f.comboUntil = now + 3f;
                foreach (var sk in f.loadout) if (sk.releaseNear) f.cooldownUntil[sk.id] = now;
                Emit("buff", f.Chest, new Color(0.9f, 0.4f, 1f), "連殺！馬上再伏擊");
            }
            dmg = Riposte(f, PreHit(f, target, s, dmg, null));
            if (f.vanish > 0f) { f.vanishUntil = now + f.vanish; Emit("buff", f.Chest, new Color(0.7f, 0.5f, 1f), $"遁影：{f.vanish:0} 秒內下一次物理攻擊必閃"); }
            if (target == enemy) AddThreat(f, dmg);
            if (remoteEnemy && (target.isMinion || IsHostileRoomProp(target))) { EmitHit(s, target.Chest); OnMinionHit?.Invoke(target.minionId, dmg, s); OnDealt(f, dmg, target, s); }
            else if (remoteEnemy && target == enemy) { EmitHit(s, target.Chest); OnRemoteHit?.Invoke(s, dmg, target.Chest, 0); OnDealt(f, dmg, target, s); }
            else Hit(target, new Projectile { owner = f, skill = s, pos = target.Chest, dir = Fighter.Flat(target.head - f.head), damage = dmg, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur });
        }

        /// <summary>以自己為中心的招式（審判）落地：範圍內的敵人各吃一次傷害</summary>
        void SelfCenterBurst(Projectile p)
        {
            var s = p.skill; var f = p.owner; if (f == null) return;
            var c = new Vector3(p.pos.x, 0f, p.pos.z);
            foreach (var t in new List<Fighter>(FoesOf(f)))
            {
                if (t == null || !t.Alive || FlatDistance(t.Feet, c) > s.areaRadius + t.bodyRadius) continue;
                int dmg = p.damage;
                if (remoteEnemy && (t.isMinion || IsHostileRoomProp(t))) { EmitHit(s, t.Chest); OnMinionHit?.Invoke(t.minionId, dmg, s); OnDealt(f, dmg, t, s); }
                else if (remoteEnemy && t == enemy) { EmitHit(s, t.Chest); OnRemoteHit?.Invoke(s, dmg, t.Chest, 0); OnDealt(f, dmg, t, s); }
                else Hit(t, new Projectile { owner = f, skill = s, pos = t.Chest, dir = Fighter.Flat(t.head - f.head), damage = dmg, crit = p.crit, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur });
            }
        }

        void AddBurn(Projectile p, Vector3 at)
        {
            var c = new Vector3(at.x, 0f, at.z);
            if (p.skill.groundZone)
            {
                // 審判／天罰：留下審判之地（技能樹的持續時間、每秒傷害都算進去；攻擊力加成也算）
                var zs = p.skill; float mul = p.owner != null ? p.owner.atkMul : 1f;
                burns.Add(new BurnZone { owner = p.owner, skill = zs, pos = c, radius = zs.areaRadius, until = now + zs.buffDur, dps = zs.effectDps * mul, remote = p.remote });
                Emit("holyzone", c, zs.color, zs.id);
                return;
            }
            burns.Add(new BurnZone { owner = p.owner, skill = p.skill, pos = c, radius = BurnRadius, until = now + BurnDuration, dps = BurnDps, remote = p.remote });
            Emit("burnzone", c, p.skill.color, null);
        }

        void UpdateBurns(float dt)
        {
            var victims = new List<Fighter>();
            foreach (var z in burns)
            {
                if (!z.alive) continue;
                if (now > z.until) { z.alive = false; continue; }
                if (z.remote) continue;   // 對手的火海由對手判定
                // 敵方的火海燒玩家；玩家的火海／毒霧燒 Boss、對手和所有小怪
                victims.Clear();
                if (FoeSide(z.owner)) victims.Add(Opponent(z.owner)); else victims.AddRange(FoesOf(z.owner));
                foreach (var victim in victims)
                {
                    // 火海是地面型（飛在空中的燒不到）；毒霧是約 1.5m 高的霧團
                    bool inside = z.skill.id == "poisonfog" ? InCylinder(victim, z.pos, z.radius, 1.5f) : OnGround(victim) && FlatDistance(victim.Feet, z.pos) <= z.radius + (victim.isMinion ? victim.bodyRadius : 0f);
                    if (!victim.Alive || !inside) { z.accs[victim] = 0f; continue; }
                    z.accs.TryGetValue(victim, out var acc);
                    acc += dt;
                    while (acc >= 1f)
                    {
                        acc -= 1f;
                        if (remoteEnemy && victim == enemy) { EmitHit(z.skill, victim.Chest); OnRemoteBurn?.Invoke(z.skill, z.dps, victim.Feet); }
                        else if (remoteEnemy && (victim.isMinion || IsHostileRoomProp(victim)))
                        {
                            // Host burn ticks call Damage, not EvPropHit: puzzle props ignore
                            // sustained damage. Do not turn a peer tick into a puzzle answer.
                            bool puzzle = IsHostileRoomProp(victim) && (victim.npcId.StartsWith("ev_statue") || victim.npcId.StartsWith("ev_chest"));
                            if (!puzzle) { EmitHit(z.skill, victim.Chest); OnMinionHit?.Invoke(victim.minionId, Mathf.RoundToInt(z.dps), z.skill); }
                        }
                        else
                        {
                            if (victim == enemy) AddThreat(z.owner, z.dps);
                            Damage(victim, z.dps, "burn", victim.isMinion ? victim.Chest : (Vector3?)null);
                            if (z.skill.groundZone) GainDmgShield(z.owner, z.dps);
                            if (z.skill.id == "poisonfog" && z.owner != null && z.owner.venom > 0f) PostHit(z.owner, victim, Skills.All["fogtick"], z.dps);   // 影舞者「毒霧疊毒」
                        }
                    }
                    z.accs[victim] = acc;
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
                // 踩到的人：陷阱主人的任何敵人（Boss、小怪、對手；使用者要求地雷，原本只有主要對手會踩到）；飛行怪踩不到
                Fighter victim = null; float best = float.MaxValue;
                foreach (var v in FoesOf(t.owner))
                {
                    if (!OnGround(v)) continue;
                    float d = FlatDistance(v.Feet, t.pos);
                    if (d < t.skill.radius + 0.15f + (v.isMinion ? v.bodyRadius * 0.5f : 0f) && d < best) { best = d; victim = v; }
                }
                if (victim == null) continue;
                t.alive = false;
                bool area = t.skill.areaRadius > 0f;
                Emit("trap", t.pos, t.skill.color, $"{t.skill.name}！");
                if (t.owner != null && t.owner.trapCrit > 0f) { t.owner.markNextUntil = now + t.owner.trapCrit; Emit("buff", t.owner.Chest, t.skill.color, $"獵殺時刻：{t.owner.trapCrit:0} 秒內下一箭必定爆擊"); }
                if (!area && remoteEnemy && victim == enemy) { OnRemoteHit?.Invoke(t.skill, t.skill.damage, t.pos, -t.id); OnTrapGone?.Invoke(t.id); continue; }
                // 地雷：爆炸範圍內所有敵人；一般陷阱：踩到的那一個
                var hits = new List<Fighter>();
                if (area) { foreach (var v in FoesOf(t.owner)) if (FlatDistance(v.Feet, t.pos) <= t.skill.areaRadius + v.bodyRadius) hits.Add(v); }
                else hits.Add(victim);
                foreach (var v in hits)
                {
                    if (v == enemy) AddThreat(t.owner, t.skill.damage);
                    if (t.owner != null && t.owner.Alive) HitFoe(t.owner, v, t.skill, t.skill.damage, t.skill.effect, t.skill.effectDps, t.skill.effectDur);
                    else { HitSkill = t.skill; incoming = t.skill; ApplyDamageAndEffect(v, t.skill.damage, t.skill.effect, t.skill.effectDps, t.skill.effectDur, t.pos, t.owner); HitSkill = null; incoming = null; }
                }
                if (remoteEnemy || allies.Count > 0) OnTrapGone?.Invoke(t.id);   // 連線：告訴對方陷阱沒了
            }
            traps.RemoveAll(x => !x.alive);
        }

        // ------------------------------------------------------------ 遊俠的獵鷹主動出擊（技能樹「獵鷹突襲」「獵鷹巡獵」）
        /// <summary>獵鷹巡獵的間隔：基本 14 秒，每一級 −1.8 秒（5 級＝5 秒）</summary>
        public static float FalconPatrolEvery(float ranks) => Mathf.Max(4f, 14f - 1.8f * ranks);
        public const float FalconPatrolRange = 9f;
        /// <summary>獵鷹俯衝：0.35 秒後打中目標（傷害＝獵鷹突襲的基礎傷害 × 攻擊加成，經過一般的命中流程）</summary>
        void FalconDive(Fighter o, Fighter t, string label)
        {
            o.falconDiveReady = now + 0.8f;
            var s = o.Derived("falcondive");
            Emit("buff", t.Chest + Vector3.up * 0.4f, s.color, "🦅 " + label);
            OnFalconDive?.Invoke(o, t, 0.35f);
            var tt = t;
            After(0.35f, () => { if (tt.Alive && o.Alive) HitFoe(o, tt, s, s.damage); });
        }
        void UpdateFalconPatrol()
        {
            var o = player;
            if (o == null || !o.Alive || o.falconPatrol <= 0f) return;
            if (o.falconPatrolAt <= 0f) { o.falconPatrolAt = now + FalconPatrolEvery(o.falconPatrol); return; }
            if (now < o.falconPatrolAt) return;
            Fighter best = null; float bd = FalconPatrolRange;
            foreach (var v in FoesOf(o)) { float d = FlatDistance(v.Feet, o.Feet); if (d < bd) { bd = d; best = v; } }
            if (best == null) { o.falconPatrolAt = now + 1f; return; }   // 附近沒人：1 秒後再看
            o.falconPatrolAt = now + FalconPatrolEvery(o.falconPatrol);
            FalconDive(o, best, "獵鷹巡獵！");
        }

        // ------------------------------------------------------------ 命中：反擊 → 格擋 → 護盾 → 傷害 → 附加效果
        void Hit(Fighter target, Projectile p)
        {
            // 故事探索：打不穿牆（使用者要求）——攻擊的人和目標之間有牆、樹叢、岩壁擋著就不算打到
            if (explore != null && p.owner != null && target != null && p.owner != target && !ExSight(p.owner.Feet, target.Feet)) return;
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
            HitSkill = p.skill; HitCrit = p.crit; incoming = p.skill;
            ApplyDamageAndEffect(target, p.damage, p.effect, p.effectDps, p.effectDur, p.pos, p.owner);
            HitSkill = null; HitCrit = false; incoming = null;
            ApplyProcs(target, LastProcs, p.pos);
            OnDealt(p.owner, p.damage, target, p.skill, p);
        }

        // ------------------------------------------------------------ 技能樹的流派效果
        public const float LongShotDist = 6f;   // 獵人「遠射」：飛超過 6m 才加傷害
        public const int VenomMax = 5;          // 刺客「劇毒連刺」：疊滿 5 層毒爆
        /// <summary>這招要花多少 MP（殉道的牧師攻擊技能改用 HP；劍意期間劍氣免費）</summary>
        public int CostOf(Fighter f, SkillDef s)
        {
            if (HpCostOf(f, s) > 0) return 0;
            if (s.id == "wave" && f.waveFreeUntil > now) return 0;
            return s.cost;
        }
        /// <summary>殉道：牧師的攻擊技能改用 HP 施放（MP 的一半）</summary>
        public int HpCostOf(Fighter f, SkillDef s) => f.martyr > 0f && s.cls == "priest" && s.damage > 0 && s.cost > 0 ? Mathf.Max(1, Mathf.RoundToInt(s.cost * 0.5f)) : 0;
        public float SteadyAimTime(Fighter f) => SteadyAimHold;
        public const float SteadyAimHold = 2f, EagleCrit = 0.05f;   // 穩定瞄準要站穩幾秒；鷹眼每層爆擊率
        public int SealNeed(Fighter f) => Mathf.Max(2, 6 - Mathf.RoundToInt(f.holySeal));
        /// <summary>
        /// 可操控的法術（SkillDef.steer）：飛行中往施放者準心指的方向轉（每秒最多 SteerTurn 度）。
        /// 準心方向＝施放者的 forward（玩家＝手機鏡頭方向，隊友／對手＝他們回報的面向），
        /// 目標點取「施放者頭部沿準心方向、比法術目前距離再遠 SteerLead」的位置，所以轉頭就能帶著它繞。
        /// 隊友、對手放的在這台手機也照他們的面向轉（不另外同步）。
        /// </summary>
        public const float SteerTurn = 220f, SteerLead = 1.5f, SteerAfter = 0.3f;
        void SteerToAim(Projectile p, float dt)
        {
            var o = p.owner; var aimDir = o.forward;
            if (aimDir.sqrMagnitude < 1e-6f) return;
            aimDir.Normalize();
            float dist = Vector3.Distance(p.pos, o.head);
            var aim = o.head + aimDir * (dist + SteerLead);
            var want = aim - p.pos; if (want.sqrMagnitude < 1e-6f) return;
            want.Normalize();
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(p.dir, want), -1f, 1f));
            if (ang < 1e-4f) return;
            float k = Mathf.Min(1f, SteerTurn * Mathf.Deg2Rad * dt / ang);
            p.dir = (p.dir * (1f - k) + want * k).normalized; p.basePos = p.pos;
        }

        Fighter NearestFoeOf(Fighter f, Vector3 at)
        {
            Fighter best = null; float bd = float.MaxValue;
            foreach (var v in FoesOf(f)) { float d = (v.Chest - at).sqrMagnitude; if (d < bd) { bd = d; best = v; } }
            return best;
        }
        /// <summary>劍士「反擊之刃」：格擋成功後的下一擊加傷害（用掉）</summary>
        int Riposte(Fighter f, int dmg)
        {
            if (f == null || f.riposte <= 0f || f.riposteUntil <= now || dmg <= 0) return dmg;
            f.riposteUntil = 0f;
            Emit("buff", f.Chest, new Color(0.6f, 0.8f, 1f), "反擊之刃！");
            return Mathf.RoundToInt(dmg * (1f + f.riposte));
        }
        /// <summary>命中前的傷害加成：融化、遠射、刺殺</summary>
        /// <summary>寶箱怪：打中的傷害一律算 1（吸血、傷害統計、隊友端送主機的數字也是 1）</summary>
        int PreHit(Fighter o, Fighter t, SkillDef s, int dmg, Projectile p) { int d = PreHitRaw(o, t, s, dmg, p); return t != null && t.treasure && d > 0 ? 1 : d; }
        int PreHitRaw(Fighter o, Fighter t, SkillDef s, int dmg, Projectile p)
        {
            if (o == null || t == null || s == null || dmg <= 0) return dmg;
            float m = 1f;
            // 法師「融化」：火系打中被冰凍（定身）的敵人，傷害加成並解除冰凍
            if (o.melt > 0f && t.snaredUntil > now && (s.id == "fire" || s.id == "meteor" || s.id == "echo_fire" || s.id == "pet_fire"))
            { m *= 1f + o.melt; t.snaredUntil = now; Emit("crit", t.Chest, new Color(1f, 0.55f, 0.2f), "融化！"); }
            // 獵人「遠射」：飛超過 6m 的箭
            if (o.longShot > 0f && p != null && p.traveled >= LongShotDist && (s.cls == "archer" || s.id == "na_arrow")) m *= 1f + o.longShot;
            // 刺客「刺殺」：伏擊技能加傷害；目標 HP 低於 40% 再加一次
            if (o.assassinate > 0f && s.releaseNear)
            {
                m *= 1f + o.assassinate;
                if (t.maxHp > 0f && t.hp < t.maxHp * 0.4f) { m *= 1f + o.assassinate; Emit("crit", t.Chest, s.color, "斬殺！"); }
            }
            m *= StyleMul(o, t);
            return Mathf.RoundToInt(dmg * m);
        }

        // ------------------------------------------------------------ 流派被動（使用者要求：進階職業要有不同流派的玩法）
        public const int HitStackMax = 10; public const float HitStackWindow = 3f;
        public const float ChainRange = 4f, ChainShare = 0.6f, KillRushHaste = 0.3f, KillSoulMp = 3f, KillSoulHp = 0.01f, CastShieldDur = 4f, ShieldCapPct = 0.4f;
        /// <summary>流派被動的目前數值：技能樹＋流派爆發技能的暫時加成</summary>
        float Sty(Fighter f, PassiveKind k)
        {
            float v = k switch
            {
                PassiveKind.Thorns => f.styleThorns, PassiveKind.Execute => f.executeDmg, PassiveKind.FirstStrike => f.firstStrike, PassiveKind.StatusAmp => f.statusAmp,
                PassiveKind.HitStack => f.hitStack, PassiveKind.ChainHit => f.chainHit, PassiveKind.KillRush => f.killRush, PassiveKind.KillSoul => f.killSoul,
                PassiveKind.CastShield => f.castShield, PassiveKind.OverhealShield => f.overhealShield, _ => 0f,
            };
            if (f.styleBuffs.TryGetValue(k, out var b) && b.Value > now) v += b.Key;
            return v;
        }
        void CastStyleBuff(Fighter f, SkillDef s)
        {
            f.styleBuffs[s.styleKind] = new KeyValuePair<float, float>(s.styleAmt, now + s.buffDur);
            Emit("buff", f.Chest, s.color, $"{s.name}（{s.buffDur:0} 秒）");
        }
        /// <summary>畫面：流派被動的觸發（kind＝chain／thorns／execute／first／killrush／soul；a＝觸發的人、b＝另一方、at＝位置）</summary>
        public event Action<string, Fighter, Fighter, Vector3> OnStyleFx;
        /// <summary>畫面：獵鷹俯衝（遊俠、目標、秒數）</summary>
        public event Action<Fighter, Fighter, float> OnFalconDive;
        void NoteBuff(Fighter f, SkillDef s)
        {
            if (s == null || s.type != SkillType.Self) return;
            float dur = s.buffDur > 0f ? s.buffDur : 1.5f;   // 瞬間的（回血、冥想、淨化）：短短亮一下
            f.buffFx.RemoveAll(x => x.id == s.id);
            f.buffFx.Add(new Fighter.BuffFx { id = s.id, start = now, until = now + Mathf.Max(0.5f, dur) });
        }
        float StyleMul(Fighter o, Fighter t)
        {
            float m = 1f;
            if (t.maxHp > 0f)
            {
                { float ex = Sty(o, PassiveKind.Execute); if (ex > 0f && t.hp < t.maxHp * 0.3f) { m *= 1f + ex; OnStyleFx?.Invoke("execute", o, t, t.Chest); } }      // 斬殺
                { float fs = Sty(o, PassiveKind.FirstStrike); if (fs > 0f && t.hp >= t.maxHp * 0.9f) { m *= 1f + fs; OnStyleFx?.Invoke("first", o, t, t.Chest); } }   // 先發制人
            }
            float sa = Sty(o, PassiveKind.StatusAmp); if (sa > 0f && (t.snaredUntil > now || t.dots.Count > 0 || t.blindUntil > now || t.markedUntil > now)) m *= 1f + sa;   // 趁虛而入
            float hs = Sty(o, PassiveKind.HitStack); if (hs > 0f && o.stackUntil > now) m *= 1f + hs * o.stackCount;   // 連擊疊層
            return m;
        }
        /// <summary>命中後：疊層、連鎖（PostHit 呼叫）</summary>
        void StyleOnHit(Fighter o, Fighter t, SkillDef s, float dmg)
        {
            if (Sty(o, PassiveKind.HitStack) > 0f)
            {
                if (o.stackUntil <= now) o.stackCount = 0;
                o.stackCount = Mathf.Min(HitStackMax, o.stackCount + 1); o.stackUntil = now + HitStackWindow;
            }
            if (s.id != "chainhit" && s.cls != "basic" && s.cls != "pet" && dmg > 0f && t != null && rng.NextDouble() < Sty(o, PassiveKind.ChainHit))
            {
                Fighter next = null; float bd = ChainRange;
                foreach (var v in FoesOf(o)) { if (v == t) continue; float d = FlatDistance(v.Feet, t.Feet); if (d < bd) { bd = d; next = v; } }
                if (next != null)
                {
                    var cs = Skills.All["chainhit"]; int cd = Mathf.Max(1, Mathf.RoundToInt(dmg * ChainShare)); var nn = next;
                    Emit("buff", t.Chest, s.color, "連鎖！"); OnStyleFx?.Invoke("chain", t, next, t.Chest);
                    After(0.12f, () => { if (nn.Alive && o.Alive) HitFoe(o, nn, cs, cd); });
                }
            }
        }
        /// <summary>護盾：取比較大的那個（不疊加），最多最大 HP 的 40%</summary>
        void GrantShield(Fighter f, float amount, float dur, string label)
        {
            int a = Mathf.RoundToInt(Mathf.Min(amount, f.maxHp * ShieldCapPct)); if (a <= 0) return;
            if (f.shieldUntil > now && f.shieldLeft >= a) { f.shieldUntil = Mathf.Max(f.shieldUntil, now + dur); return; }
            f.shieldLeft = a; f.shieldUntil = Mathf.Max(f.shieldUntil, now + dur);
            if (label != null) Emit("buff", f.Chest, new Color(0.6f, 0.85f, 1f), $"{label}（護盾 {a}）");
        }
        Vector3 lastKillAt;
        /// <summary>我擊倒敵人：追獵、吸魂</summary>
        void StyleOnKill(Fighter o)
        {
            if (o == null || !o.Alive) return;
            float kr = Sty(o, PassiveKind.KillRush), ks = Sty(o, PassiveKind.KillSoul);
            if (kr > 0f) { o.killRushUntil = now + kr; Emit("buff", o.Chest, new Color(0.5f, 1f, 0.7f), $"追獵！{kr:0.#} 秒加快"); OnStyleFx?.Invoke("killrush", o, null, o.Chest); }
            if (ks > 0f)
            {
                o.mp = Mathf.Min(o.maxMp, o.mp + ks * KillSoulMp);
                float h = Mathf.Min(o.maxHp - o.hp, o.maxHp * ks * KillSoulHp); o.hp += h;
                Emit("heal", o.Chest, new Color(0.7f, 0.6f, 1f), $"吸魂 +{ks * KillSoulMp:0} MP"); OnStyleFx?.Invoke("soul", o, null, lastKillAt);
            }
        }
        /// <summary>溢療護盾：我放的治療補超過上限的部分</summary>
        void Overheal(Fighter t, float overflow)
        {
            float ov = Sty(player, PassiveKind.OverhealShield);
            if (ov <= 0f || overflow <= 0f || !t.Alive) return;
            GrantShield(t, overflow * ov, 6f, "溢光聖盾");
        }
        // ------------------------------------------------------------ 法師「風刃迴旋」：放風刃時，另一把風刃在身邊轉圈，碰到的敵人受傷
        public class WindOrbit { public Fighter owner; public SkillDef skill; public float until, angle; public int dmg; public readonly Dictionary<Fighter, float> lastHit = new Dictionary<Fighter, float>(); public Vector3 pos; }
        public readonly List<WindOrbit> windOrbits = new List<WindOrbit>();
        public const float WindOrbitBase = 3f, WindOrbitRadius = 1.3f, WindOrbitSpin = 0.8f, WindOrbitHitGap = 0.6f, WindOrbitDmg = 0.5f, WindOrbitTouch = 0.45f;

        void StartWindOrbit(Fighter o, SkillDef s, int dmg)
        {
            float dur = WindOrbitBase + o.windOrbitTime;
            var w = windOrbits.Find(x => x.owner == o);
            int d = Mathf.Max(1, Mathf.RoundToInt(dmg * o.atkMul * WindOrbitDmg));
            if (w != null) { w.until = Mathf.Max(w.until, now + dur); w.dmg = Mathf.Max(w.dmg, d); return; }   // 已經在轉：延長
            windOrbits.Add(new WindOrbit { owner = o, skill = s, until = now + dur, dmg = d, angle = Mathf.Atan2(o.forward.z, o.forward.x) });
            Emit("buff", o.Chest, s.color, $"風刃迴旋 {dur:0} 秒");
        }

        void UpdateWindOrbits(float dt)
        {
            for (int i = windOrbits.Count - 1; i >= 0; i--)
            {
                var w = windOrbits[i];
                if (!w.owner.Alive || now >= w.until || Over) { windOrbits.RemoveAt(i); continue; }
                w.angle += Mathf.PI * 2f * WindOrbitSpin * dt;
                var c = w.owner.Feet;
                w.pos = new Vector3(c.x + Mathf.Cos(w.angle) * WindOrbitRadius, Mathf.Max(0.8f, w.owner.Chest.y - 0.2f), c.z + Mathf.Sin(w.angle) * WindOrbitRadius);
                foreach (var v in FoesOf(w.owner))
                {
                    if (!v.Alive || FlatDistance(w.pos, v.Feet) > WindOrbitTouch + v.bodyRadius) continue;
                    if (w.lastHit.TryGetValue(v, out var lt) && now - lt < WindOrbitHitGap) continue;
                    w.lastHit[v] = now;
                    HitFoe(w.owner, v, w.skill, w.dmg);
                }
            }
        }

        /// <summary>奧術爆裂的半徑、奧術連射兩發的間隔（秒）</summary>
        public const float ArcaneBurstRadius = 1.5f, ArcaneTwinGap = 0.18f;
        static SkillDef BurstSkill => Skills.All["arcaneburst"];

        /// <summary>命中後：奧術回響、奧術迴路、飲血、劇毒、聖印、飛刀節奏、寒冰</summary>
        void PostHit(Fighter o, Fighter t, SkillDef s, float dmg)
        {
            if (o != null && s != null) StyleOnHit(o, t, s, dmg);   // 流派被動：疊層、連鎖
            if (s == null) return;
            // 法師「奧術爆裂」「火球爆裂」：奧術飛彈／火球命中爆炸，周圍的其他敵人受到一部分傷害
            float burstK = s.id == "na_mage" ? o.arcaneBurst : s.id == "fire" ? o.fireBurst : 0f;
            if (burstK > 0f && t != null && dmg > 0f)
            {
                int bd = Mathf.Max(1, Mathf.RoundToInt(dmg * burstK));
                var c = t.Feet; var hitT = t;
                Emit("aoe", c, s.color, s.id == "fire" ? "fireburst" : "arcaneburst");
                foreach (var v in FoesOf(o)) if (v != hitT && v.Alive && FlatDistance(v.Feet, c) <= ArcaneBurstRadius + v.bodyRadius) HitFoe(o, v, BurstSkill, bd);
            }
            bool basicHit = s.cls == "basic";
            if (basicHit && o.basicMp > 0f) o.mp = Mathf.Min(o.maxMp, o.mp + o.basicMp);
            if (basicHit && o.basicShield > 0f)
            {
                // 劍氣護體：同一揮砍到好幾隻只算一次（取最高的那一下）
                float want = dmg * o.basicShield;
                if (o.shieldSwingAt != now) { o.shieldSwingAt = now; o.shieldSwingBest = 0f; }
                if (want > o.shieldSwingBest) { AddShieldFrom(o, want - o.shieldSwingBest); o.shieldSwingBest = want; }
            }
            if (basicHit && o.basicLeech > 0f)
            {
                // 飲血：同一揮砍到好幾隻只算一次（取最高的那一下），不是每隻都回
                float want = dmg * o.basicLeech;
                if (o.leechSwingAt != now) { o.leechSwingAt = now; o.leechSwingBest = 0f; }
                if (want > o.leechSwingBest) { Leech(o, want - o.leechSwingBest); o.leechSwingBest = want; }
            }
            // 法師「奧術回響」：普攻命中有機率追加一發小火球／小冰錐
            if (s.id == "na_mage" && o.spellEcho > 0f && t != null && rng.NextDouble() < o.spellEcho)
            {
                var echo = o.Derived(rng.Next(2) == 0 ? "echo_fire" : "echo_ice");   // 跟著火球／冰槍的強化等級
                var tgt = t;
                After(0.08f, () =>
                {
                    if (!o.Alive || tgt == null || !tgt.Alive) return;
                    var from = o.head + Fighter.Flat(o.forward) * 0.25f + Vector3.down * 0.25f;
                    Spawn(o, echo, from, (tgt.Chest - from).normalized, echo.damage, echo.effect, echo.effectDps, echo.effectDur, false);
                    Emit("buff", o.Chest, echo.color, "奧術回響！");
                });
            }
            // 法師「寒冰」：冰槍命中有機率冰凍
            if (s.id == "ice" && o.iceFreeze > 0f && t != null && !(remoteEnemy && t == enemy) && rng.NextDouble() < o.iceFreeze) ApplyProcs(t, 4, t.Chest);
            // 刺客「劇毒連刺」：普攻疊毒，滿 5 層毒爆
            if (t != null && o.venom > 0f && (s.id == "na_dagger" || s.id == "poison" || s.id == "fogtick"))
            {
                if (t.venomUntil < now) t.venomStacks = 0;
                t.venomStacks += s.id == "poison" ? 2 : 1; t.venomUntil = now + 6f;
                if (t.venomStacks >= VenomMax)
                {
                    t.venomStacks = 0;
                    Emit("crit", t.Chest, new Color(0.4f, 1f, 0.4f), "毒爆！");
                    var burst = Skills.All["venom_burst"]; var vt = t;
                    Emit("aoetick", t.Feet, burst.color, "venom_burst");
                    After(0.05f, () => HitFoe(o, vt, burst, Mathf.RoundToInt(o.venom * o.atkMul)));
                }
                else Emit("effect", t.Chest, new Color(0.4f, 1f, 0.4f), $"毒 {t.venomStacks}/{VenomMax}");
            }
            // 聖騎士「盾擊格擋」：聖盾衝鋒命中後自動格擋一下
            if (s.id == "shieldbash" && o.bashGuard > 0f) { o.blockUntil = Mathf.Max(o.blockUntil, now + o.bashGuard); o.blockReduce = Mathf.Max(o.blockReduce, 0.4f); }
            // 聖騎士「制裁連斬」：神聖制裁命中後，下一下普攻是重擊
            if (s.id == "sanction" && o.sanctionCombo > 0f && o.basicHits % 3 != 2) { o.basicHits = 2; Emit("buff", o.Chest, s.color, "制裁連斬：下一下普攻是重擊"); }
            // 遊俠「鷹眼標記」：獵鷹攻擊時機率標記，下一個弓箭技能必定爆擊
            if (s.id == "pet_falcon" && o.falconMark > 0f && o.markNextUntil <= now && rng.NextDouble() < o.falconMark) { o.markNextUntil = now + 4f; Emit("buff", o.Chest, s.color, "鷹眼標記：下一箭必定爆擊"); }
            // 遊俠「獵鷹突襲」：弓箭命中時機率叫獵鷹俯衝追擊（不用召喚獵鷹；同一隻 0.8 秒內最多一次）
            if (o.falconAssault > 0f && t != null && t.Alive && (s.cls == "archer" || s.cls == "basic" && o.cls != null && o.cls.id == "archer") && now >= o.falconDiveReady && rng.NextDouble() < o.falconAssault)
                FalconDive(o, t, "獵鷹突襲！");
            // 召喚師「契約魔力」：召喚物命中回 MP
            if (s.cls == "pet" && s.id != "fogtick" && o.petMp > 0f) o.mp = Mathf.Min(o.maxMp, o.mp + o.petMp);
            // 牧師「聖印」：普攻累積聖印
            if (s.id == "na_holy" && o.holySeal > 0f && o.seals < SealNeed(o) && ++o.seals == SealNeed(o)) Emit("buff", o.Chest, new Color(1f, 0.9f, 0.5f), "聖印已滿！下一個攻擊技能傷害 ×2");
            // 刺客「飛刀節奏」：飛刀命中，其他技能冷卻縮短
            if (s.id == "knife" && o.knifeCdr > 0f)
                foreach (var k in new List<string>(o.cooldownUntil.Keys)) if (k != "knife") o.cooldownUntil[k] -= o.knifeCdr;
        }

        /// <summary>被包圍：1.5m 內第一個敵人不扣，之後每多一個敵人閃避率 −10%（3 個 −20%、4 個 −30%…）</summary>
        public const float CrowdRange = 1.5f, CrowdEvadeLoss = 0.10f;
        /// <summary>target 身邊 1.5m 內有幾個活著的敵人</summary>
        int CrowdedBy(Fighter target)
        {
            int n = 0;
            foreach (var v in FoesOf(target)) if (v.Alive && !v.hidden && FlatDistance(v.Feet, target.Feet) <= CrowdRange) n++;
            return n;
        }

        /// <summary>吸血回 HP（沒有上限；強度由技能樹倍率決定）</summary>
        /// <summary>吸血每秒最多回最大 HP 的這個比例（使用者回報劍仙一下就回滿；裝備、精魄、飲血斬都算），最多存 LeechBurst 秒的額度</summary>
        public const float LeechPerSec = 0.04f, LeechBurst = 1.5f;
        void Leech(Fighter a, float amount)
        {
            if (amount <= 0f || !a.Alive) return;
            float cap = a.maxHp * LeechPerSec;
            if (a.leechBudget < 0f) { a.leechBudget = cap * LeechBurst; a.leechBudgetAt = now; }
            a.leechBudget = Mathf.Min(cap * LeechBurst, a.leechBudget + (now - a.leechBudgetAt) * cap); a.leechBudgetAt = now;
            amount = Mathf.Min(amount, a.leechBudget);
            if (amount <= 0f) return;
            a.leechBudget -= amount;
            a.hp = Mathf.Min(a.maxHp, a.hp + amount);
        }

        /// <summary>打中別人之後：吸血、命中回 MP（角色養成的特殊能力）</summary>
        void OnDealt(Fighter a, float dmg, Fighter target = null, SkillDef s = null, Projectile p = null)
        {
            if (a == null || !a.Alive || dmg <= 0f) return;
            a.dealtDone += dmg;
            if (a == player && target != null) OnDealtNumber?.Invoke(target.Chest, dmg, p != null && p.crit);   // 合作：我的傷害數字也給隊友看
            PostHit(a, target, s, dmg);
            if (a.lifesteal > 0f) Leech(a, dmg * a.lifesteal);
            if (a.hitShield > 0f) AddShieldFrom(a, dmg * a.hitShield);   // 劍氣凝盾
            if (a.mpOnHit > 0f) a.mp = Mathf.Min(a.maxMp, a.mp + a.mpOnHit);
            if (a.adv == "inquisitor" && a.faith < FaithMax && ++a.faith == FaithMax) Emit("buff", a.Chest, new Color(1f, 0.8f, 0.3f), "信仰已滿！下一招傷害 ×2");
            GainDmgShield(a, dmg);   // 審判者「聖盾轉化」
            if (s != null && s.id == "plague" && target != null && !(remoteEnemy && (target.isMinion || IsHostileRoomProp(target) || target == enemy))) Plague(target);   // 影舞者：毒爆（隊友端交給主機）
            SoulOnHit(a, target, dmg, s);
        }

        // ------------------------------------------------------------ 精魄（Souls.cs）的觸發效果
        /// <summary>命中、打倒：攻擊者的精魄；被打：受害者的反彈、反擊中毒</summary>
        void SoulOnHit(Fighter a, Fighter target, float dmg, SkillDef s)
        {
            if (target == null || target == a) return;
            SoulVictim(a, target, dmg);
            if (a.soulProcs.Count == 0 || (s != null && s.cls == Souls.SoulCls)) return;   // 精魄發動的招式不再觸發精魄（不會連鎖）
            bool killed = !target.Alive, cast = false;   // 好幾顆會發動招式的精魄：一次命中最多發動一招
            foreach (var x in a.soulProcs)
            {
                switch (x.op)
                {
                    case SoulOp.HitSnare:
                        if (!killed && !CcImmune(target) && rng.NextDouble() < x.chance) { target.snaredUntil = now + (target.bodyRadius > 0.5f ? x.dur * 0.5f : x.dur); target.charging = null; Emit("effect", target.Chest, new Color(0.3f, 0.85f, 0.3f), "藤蔓纏繞：定身"); }
                        break;
                    case SoulOp.HitCast:
                        if (!killed && !cast && rng.NextDouble() < x.chance) { SoulCast(a, target, x.skill); cast = true; }
                        break;
                    case SoulOp.KillHeal:
                        if (killed && rng.NextDouble() < x.chance) { float h = a.maxHp * x.v; a.hp = Mathf.Min(a.maxHp, a.hp + h); Emit("heal", a.Chest, new Color(0.6f, 1f, 0.6f), $"+{h:0}"); }
                        break;
                    case SoulOp.KillMp:
                        if (killed && rng.NextDouble() < x.chance) a.mp = Mathf.Min(a.maxMp, a.mp + x.v);
                        break;
                }
            }
        }

        /// <summary>被打到的人身上的精魄：反彈、讓攻擊者中毒（小怪的近戰沒經過 OnDealt，也直接呼叫這個）</summary>
        void SoulVictim(Fighter a, Fighter target, float dmg)
        {
            if (a == null || target == null || a == target || !a.Alive || dmg <= 0f) return;
            if (target.thorns > 0f) { Damage(a, dmg * target.thorns, "thorns"); Emit("effect", a.Chest, new Color(0.5f, 0.8f, 0.3f), $"反彈 {dmg * target.thorns:0}"); }
            foreach (var x in target.soulProcs)
                if (x.op == SoulOp.Retaliate && a.Alive && rng.NextDouble() < x.chance) { AddDot(a, x.v, x.dur); Emit("effect", a.Chest, Color.green, "河豚之刺：中毒"); }
        }

        /// <summary>精魄發動技能（Lv1、不花 MP）：從胸口朝目標射出</summary>
        void SoulCast(Fighter a, Fighter target, string id)
        {
            if (id == null || !Skills.All.TryGetValue(id, out var b) || b.type != SkillType.Projectile) return;
            var s = b.Clone(); s.cls = Souls.SoulCls;
            var from = a.hasCastFrom ? a.castFrom : a.Chest;
            var dir = (target.Chest - from); if (dir.sqrMagnitude < 1e-4f) dir = Fighter.Flat(a.forward);
            Spawn(a, s, from, dir.normalized, Mathf.Max(1, Mathf.RoundToInt(s.damage * a.atkMul)), s.effect, s.effectDps, s.effectDur, false);
            Emit("buff", a.Chest, b.color, $"💠 精魄發動：{b.name}");
        }

        /// <summary>被打到之後：精魄的護盾、無敵、回血</summary>
        void SoulOnHurt(Fighter f)
        {
            foreach (var x in f.soulProcs)
            {
                if (rng.NextDouble() >= x.chance) continue;
                switch (x.op)
                {
                    case SoulOp.HurtShield: f.shieldUntil = now + x.dur; f.shieldLeft = Mathf.Max(f.shieldLeft, Mathf.RoundToInt(x.v)); Emit("buff", f.Chest, new Color(0.5f, 0.85f, 1f), $"💠 泡泡護盾 {x.v:0}"); break;
                    case SoulOp.HurtInvuln: f.invulnUntil = Mathf.Max(f.invulnUntil, now + x.dur); Emit("buff", f.Chest, new Color(0.65f, 0.4f, 1f), "💠 虛空閃現：無敵"); break;
                    case SoulOp.HurtHeal: { float h = f.maxHp * x.v; f.hp = Mathf.Min(f.maxHp, f.hp + h); Emit("heal", f.Chest, new Color(0.6f, 1f, 0.6f), $"+{h:0}"); break; }
                }
            }
        }

        /// <summary>打倒的敵人（"m"＋小怪種類、Boss 的職業 id）：SoloBattle 用來擲精魄</summary>
        public readonly List<string> killLog = new List<string>();

        // ------------------------------------------------------------ 命中附加效果（無盡模式升級）
        /// <summary>最近一次命中擲出的附加效果（1 灼燒、2 中毒、4 冰凍、8 出血、16 沉默）；連線時送給對方套用</summary>
        public int LastProcs;
        /// <summary>出血：每秒 BleedDps、BleedDur 秒（可疊）；沉默：SilenceDur 秒（Boss 減半）</summary>
        public const float BleedDps = 4f, BleedDur = 5f, SilenceDur = 2f;
        public static bool IsArrow(SkillDef s) => s != null && (s.id == "na_arrow" || (s.cls == "archer" && s.type == SkillType.Projectile));
        int RollProcs(Fighter attacker, SkillDef s = null)
        {
            if (attacker == null) return 0;
            int r = 0;
            if (attacker.burnChance > 0f && rng.NextDouble() < attacker.burnChance) r |= 1;
            if (attacker.poisonChance > 0f && rng.NextDouble() < attacker.poisonChance) r |= 2;
            if (attacker.freezeChance > 0f && rng.NextDouble() < attacker.freezeChance) r |= 4;
            // 獵人技能樹：箭命中時有機率出血、中毒、沉默
            if (IsArrow(s))
            {
                if (attacker.arrowBleed > 0f && rng.NextDouble() < attacker.arrowBleed) r |= 8;
                if (attacker.arrowPoison > 0f && rng.NextDouble() < attacker.arrowPoison) r |= 2;
                if (attacker.arrowSilence > 0f && rng.NextDouble() < attacker.arrowSilence) r |= 16;
            }
            return r;
        }
        /// <summary>套用附加效果：灼燒（每秒 5、4 秒）、中毒（每秒 3、6 秒）、冰凍（定身 1.5 秒，打斷蓄力）、出血（每秒 4、5 秒）、沉默（2 秒不能唸技能）</summary>
        /// <summary>Boss 對異常狀態（冰凍、定身、沉默、致盲）免疫；灼燒、中毒、出血這類持續傷害照常</summary>
        public static bool CcImmune(Fighter t) => t != null && t.cls != null && t.cls.id != null && t.cls.id.StartsWith("boss");
        void ImmuneNote(Vector3 at) => Emit("effect", at, new Color(0.8f, 0.8f, 0.85f), "免疫");

        public void ApplyProcs(Fighter target, int procs, Vector3 at)
        {
            if (procs == 0 || target == null || !target.Alive) return;
            if (CcImmune(target) && (procs & (4 | 16)) != 0) { procs &= ~(4 | 16); ImmuneNote(at); }   // Boss：冰凍、沉默無效
            if ((procs & 1) != 0) { target.dots.Add(new Fighter.Dot { dps = 5f, until = now + 4f }); Emit("effect", at, new Color(1f, 0.5f, 0.15f), "灼燒"); }
            if ((procs & 2) != 0) { target.dots.Add(new Fighter.Dot { dps = 3f, until = now + 6f }); Emit("effect", at, Color.green, "中毒"); }
            if ((procs & 4) != 0) { target.snaredUntil = now + 1.5f; target.charging = null; target.windupUntil = 0f; Emit("effect", at, new Color(0.6f, 0.9f, 1f), "冰凍"); }
            if ((procs & 8) != 0) { target.dots.Add(new Fighter.Dot { dps = BleedDps, until = now + BleedDur }); Emit("effect", at, new Color(0.9f, 0.15f, 0.2f), "出血"); }
            if ((procs & 16) != 0)
            {
                target.silencedUntil = now + (target.bodyRadius > 0.5f ? SilenceDur * 0.5f : SilenceDur);
                if (target.charging != null && target.charging.cls != "basic") target.charging = null;   // 正在唸的技能被打斷
                Emit("effect", at, new Color(0.75f, 0.6f, 1f), "沉默");
            }
        }

        void ApplyDamageAndEffect(Fighter target, int damage, EffectKind eff, float dps, float dur, Vector3 at, Fighter by = null)
        {
            if (target.npcSide == 1 && target.npcId != null && target.npcId.StartsWith("ev_") && EvPropHit(target, by)) return;   // 副本：雕像、寶箱（Combat.RoomEvents.cs）
            if (weather != Weather.None && damage > 0) damage = Mathf.Max(1, Mathf.RoundToInt(damage * WeatherMul(incoming ?? HitSkill, by ?? (FoeSide(target) ? player : enemy))));   // 故事關卡的天候（Combat.StageMods.cs）
            if (by == null) by = FoeSide(target) ? player : enemy;   // 不知道是誰打的：對面那一方的主角
            // 閃避率（刺客技能樹）：整次攻擊（傷害＋效果）完全閃開
            // 閃避只對物理攻擊有效（使用者要求）：魔法攻擊（火球、毒液、放電、泡泡…）閃不掉，要自己躲
            // 命中率（使用者要求）：攻擊者的命中率直接抵掉目標的閃避率（命中 10%、閃避 40% → 實際 30%）
            bool magicHit = incoming != null && Skills.IsMagic(incoming);
            // 遁影／隱遁／舞影（使用者要求）：伏擊後一段時間內，下一次物理攻擊必定閃開（用掉就沒了；命中率抵不掉）
            if (!magicHit && damage > 0 && target.vanishUntil > now) { target.vanishUntil = 0f; Emit("evade", target.Chest, new Color(0.7f, 0.5f, 1f), "遁影！"); return; }
            float evade = magicHit ? 0f : target.evadeChance + (CloseInstinct(target) ? CloseEvadePer * target.closeInstinct : 0f);
            evade -= by != null ? by.accuracy : 0f;
            if (evade > 0f) evade -= CrowdEvadeLoss * Mathf.Max(0, CrowdedBy(target) - 1);   // 1.5m 內第二個敵人起，每多一個閃避 −10%（使用者要求）
            if (evade > 0f && damage > 0 && rng.NextDouble() < evade) { Emit("evade", target.Chest, new Color(0.7f, 0.6f, 1f), "閃避！"); return; }
            if (damage > 0 && target.invulnUntil > now) { Emit("evade", target.Chest, new Color(1f, 0.4f, 0.4f), "無敵！"); return; }
            // 忍者：替身術（下一次攻擊完全無效）、分身術（一半的攻擊打到分身）
            if (damage > 0 && target.subUntil > now) { target.subUntil = 0f; Emit("evade", target.Chest, new Color(0.6f, 0.65f, 0.7f), "替身術！"); return; }
            if (damage > 0 && target.cloneUntil > now && rng.NextDouble() < 0.5) { Emit("evade", target.Chest, new Color(0.8f, 0.85f, 0.9f), "打中了分身！"); return; }
            BonusOnHurt(target, damage);   // 小目標：被打中幾次（閃掉的不算）
            float dmg = damage;
            if (target.airReduce > 0f && target.airborneUntil > now && dmg > 0) dmg *= 1f - target.airReduce;   // 翔羽心：跳起來時減傷
            string note = null;
            if (target.blockUntil > now && dmg > 0)
            {
                dmg = Mathf.Round(dmg * (1f - target.blockReduce)); target.blockUntil = 0; note = "格擋";
                if (target.riposte > 0f) { target.riposteUntil = now + 4f; note += $"・反擊之刃（下一擊 +{target.riposte * 100f:0}%）"; }   // 劍士「反擊之刃」
            }
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
                case EffectKind.Dot: AddDot(target, dps, dur); Emit("effect", at, Color.green, "中毒"); break;
                case EffectKind.Blind when CcImmune(target): case EffectKind.Snare when CcImmune(target): ImmuneNote(at); break;   // Boss：致盲、定身無效
                case EffectKind.Blind: target.blindUntil = now + dur; Emit("effect", at, Color.gray, "致盲"); break;
                case EffectKind.Snare: target.snaredUntil = now + dur; target.charging = null; Emit("effect", at, new Color(1f, 0.6f, 0.2f), "定身"); break;
                case EffectKind.Mark: target.markedUntil = now + dur; Emit("effect", at, new Color(1f, 0.6f, 0.2f), "懲戒：受到傷害 +20%"); break;
                case EffectKind.Burn: AddDot(target, dps, dur); Emit("effect", at, new Color(1f, 0.5f, 0.15f), "灼燒"); break;
            }
            bool wasAlive = target.Alive; float hp0 = target.hp;
            if (dmg > 0 || note == null) Damage(target, dmg, "hit", at);
            // 流派被動：反傷（被打中，把扣掉的 HP 一部分反彈給攻擊者）、擊倒（追獵、吸魂）
            float lost = hp0 - target.hp;
            float th = Sty(target, PassiveKind.Thorns);
            if (th > 0f && lost > 0f && by != null && by != target && by.Alive && !(remoteEnemy && by == enemy) && FoeSide(by) != FoeSide(target))
            { Damage(by, Mathf.Max(1f, Mathf.Round(lost * th)), "thorns", by.Chest); OnStyleFx?.Invoke("thorns", target, by, by.Chest); }
            if (wasAlive && !target.Alive && by == player) { lastKillAt = target.Chest; StyleOnKill(player); }
        }

        void Damage(Fighter f, float dmg, string kind, Vector3? at = null)
        {
            if (!f.Alive) return;
            if (f.friendOwner >= 0) { if (dmg > 0f) { OnFriendHurt?.Invoke(f, dmg); Emit("hit", at ?? f.Chest, Color.red, $"-{dmg:0}"); } return; }   // 別台手機的召喚獸／影分身：傷害交給那台算
            if (dmg > 0f && kind != "dot" && kind != "burn" && kind != "oath" && kind != "thorns" && BarrierGuards(f) && TryBarrier(f, at)) return;   // 主教的聖域障壁
            if (f.invulnUntil > now && dmg > 0f) return;   // 狂戰士不死之軀的無敵
            if (f.isMinion && f.hidden) return;   // 埋著的食人花
            if (f.esGuardUntil > now && dmg > 0f) dmg *= 0.5f;   // 桂冠應戰：架盾中減傷 50%
            if (f.esCast != null && kind != "dot" && kind != "burn") OnSkillUserHurt(f, dmg);   // 讀條中被打：招牌技中斷、架盾反擊（Combat.EnemySkills.cs）
            if (f.esCast == null && (f.healer || f.summoner || f.support > 0) && f.windupUntil > 0f && dmg > 0f && kind != "dot" && kind != "burn") { f.windupUntil = 0f; f.attackReadyAt = now + 2f; Emit("healbreak", f.Chest, new Color(1f, 0.85f, 0.4f), f.healer ? "治療被打斷！" : f.support > 0 ? "讀條被打斷！" : "召喚被打斷！"); }   // 補師、召喚師讀條被打中：打斷
            if (f.treasure && (kind == "dot" || kind == "burn")) return;   // 寶箱怪：毒、灼燒沒用（每一下攻擊只扣 1）
            if (f.isMinion && dmg > 0f && kind != "dot" && kind != "burn") OnMinionHurt(f);
            if (f.isMinion && f.minionKind == 16 && dmg > 0f)
            {
                // 寄居蟹的殼：打擊點（沒有就用玩家位置）在牠正面半球 → 減傷 50%
                var from = Fighter.Flat((at ?? player.Feet) - f.Feet);
                if (from != Vector3.zero && Vector3.Dot(from.normalized, Fighter.Flat(f.forward).normalized) > 0f) { dmg *= CrabShellMul; Emit("mitigate", f.Chest, new Color(0.7f, 0.8f, 0.9f), "殼擋住了！"); }
            }
            // 物理／魔法減傷：持續傷害（毒、灼燒）和魔法攻擊（Skills.IsMagic）用魔法減傷，其他用物理減傷
            bool magic = kind == "dot" || kind == "burn" || (incoming != null && Skills.IsMagic(incoming));
            dmg *= f.damageTakenMul * (1f - Mathf.Min(magic ? f.magicReduce : f.gearReduce, Fighter.MaxReduce)) * f.soulTaken;
            // 精魄：格擋（這一下傷害減半；持續傷害不算）
            if (f.blockChance > 0f && dmg > 0f && kind != "dot" && kind != "burn" && kind != "thorns" && rng.NextDouble() < f.blockChance) { dmg *= 0.5f; Emit("mitigate", at ?? f.Chest, new Color(0.8f, 0.8f, 0.7f), "格擋！"); }
            if (f.lastStand > 0f && f.hp < f.maxHp * 0.3f) dmg *= 1f - f.lastStand;   // 不屈：低血量減傷
            if (f.markedUntil > now) dmg *= 1.2f;                                       // 被懲戒
            if (dmg > 0f && kind != "dot" && kind != "burn" && CloseInstinct(f)) dmg *= 1f - CloseReducePer * f.closeInstinct;   // 刺客近身本能
            if (f == player && dmg > 0f)
            {
                if (InAura(f)) dmg *= 1f - AuraReduce;                                  // 聖騎士守護光環
                if (f.selfOathUntil > now) dmg *= 0.8f;                                 // 聖騎士自己的守護誓言
                if (f.adv == "summoner" && HasPet(f)) dmg *= 0.85f;                     // 召喚師：契約
                // 守護誓言：在聖騎士 3m 內，一部分傷害轉給他
                if (kind != "oath" && f.oathUntil > now && f.oathBy != null && f.oathBy.Alive && FlatDistance(f.oathBy.Feet, f.Feet) <= AuraRange)
                { float share = dmg * f.oathShare; dmg -= share; OnOathShare?.Invoke(f.oathBy, share); }
                // 石像守衛：替主人承受 80% 的傷害
                var golem = pets.Find(x => x.alive && !x.remote && x.owner == f && x.kind == "golem" && x.hp > 0f);
                if (golem != null && !galleryTrapDamage)
                {
                    float share = Mathf.Min(golem.hp, dmg * GolemShare);
                    golem.hp -= share; dmg -= share;
                    if (golem.hp <= 0f) KillPet(golem);
                }
                // 魔力護盾（大魔導士的限時護盾）、魔力盾（法師的開關）：先扣 MP
                if ((f.manaShieldUntil > now || f.manaCoat) && f.mp > 0f)
                {
                    float absorb = Mathf.Min(f.mp, dmg);
                    f.mp -= absorb; dmg -= absorb;
                    if (absorb > 0f) Emit("mitigate", at ?? f.Chest, new Color(0.5f, 0.55f, 1f), $"{(f.manaCoat ? "魔力盾" : "魔力護盾")} -{absorb:0} MP");
                }
            }
            if (f.treasure && dmg > 0f) dmg = 1f;   // 寶箱怪：不管多痛，每下只扣 1
            dmg = Mathf.Round(Mathf.Max(0f, dmg));
            // 不死之軀：致命傷害留 1 HP，無敵 3 秒
            if (f.undyingUntil > now && f.hp - dmg < 1f) { dmg = Mathf.Max(0f, f.hp - 1f); f.undyingUntil = 0f; f.invulnUntil = now + 3f; Emit("buff", f.Chest, new Color(0.8f, 0.1f, 0.1f), "不死之軀！無敵 3 秒"); }
            f.hp = Mathf.Max(0, f.hp - dmg);
            if (f.isMinion && f.Alive && dmg > 0f) CheckStagger(f);   // 每掉 1/3 血：受擊硬直、取消這次攻擊
            if (f.isMinion && f.lureUntil > now && !f.lureRolled && f.Alive && f.hp < f.maxHp * Pets.CaptureHp && TryCapture(f)) return;   // 吃餌中第一次低於 30%：擲收服；成功＝離開戰場、不算擊倒
            // 主教的復活祝福：倒下時以 30% HP 站起來
            if (!f.Alive && f.blessUntil > now) { if (f == player) playerDown = false; f.blessUntil = 0f; f.hp = Mathf.Round(f.maxHp * 0.3f); Emit("heal", f.Chest, new Color(1f, 0.95f, 0.6f), "復活祝福！"); }
            if (dmg > 0 && f.prepUntil > now) { f.prepUntil = 0f; Emit("fail", f.Chest, Color.gray, "刺殺準備被打斷！"); }
            if (dmg > 0 && kind != "dot" && kind != "burn" && f.armed != null && f.armed.releaseNear && !(NearestFoe(f, out var ad) != null && ad <= AmbushReach + 0.5f + f.reachBonus)) { f.cooldownUntil[f.armed.id] = now; Emit("fail", f.Chest, Color.gray, $"{f.armed.name}被打斷！要重新蓄力"); f.armed = null; }
            Emit(kind == "dot" || kind == "burn" ? kind : (f.isPlayer ? "hurt" : "hit"), at ?? f.Chest, Color.red, dmg > 0 ? $"-{dmg:0}" : "");
            if (f.Alive && dmg > 0f && f.soulProcs.Count > 0 && kind != "dot" && kind != "burn" && kind != "thorns") SoulOnHurt(f);
            if (!f.Alive)
            {
                if (f.isMinion && !f.summoned) killLog.Add("m" + LogKind(f));   // 召喚物：不掉寶
                else if (!f.isPlayer && f.npcSide < 0 && f == enemy && f.cls != null) killLog.Add(f.cls.id);
                if (f.isMinion && f.summoned) Emit("miniondie", f.Chest, Color.white, null);   // 召喚物：沒有經驗、不算擊倒（也就沒有金幣）
                else if (f.isMinion) { kills++; if (endless) xp += Mathf.RoundToInt(MinionXp[f.minionKind] * (1f + 0.03f * wave)); Emit("miniondie", f.Chest, Color.white, null); }
                else if (f.npcSide >= 0) Emit("npcdown", f.Chest, Color.white, $"{f.name}倒下了");
                else if (f.isPlayer) Emit(AnyAllyAlive ? "down" : "lose", f.Chest, Color.white, null);
                else if (endless && f == enemy) OnBossDown();
                else Emit("win", f.Chest, Color.white, null);
            }
        }

        // ------------------------------------------------------------ 受擊硬直（使用者要求：小怪、菁英、稀有怪；Boss 不算）
        public const float StaggerTime = 0.9f;
        /// <summary>目前掉到第幾個 1/3（0＝還在 2/3 以上、1＝2/3～1/3、2＝1/3 以下）</summary>
        public static int StaggerTierOf(float hp, float maxHp) => maxHp <= 0f ? 0 : Mathf.Clamp(Mathf.FloorToInt((1f - hp / maxHp) * 3f + 1e-4f), 0, 2);
        void CheckStagger(Fighter m)
        {
            int tier = StaggerTierOf(m.hp, m.maxHp);
            if (tier <= m.staggerTier) return;
            m.staggerTier = tier;   // 一下打掉超過 1/3：只硬直一次
            if (m.esCast != null) CancelEnemyCast(m, null);                 // 招式讀條：預警一起取消（也通知隊友）
            if (eliteStates.TryGetValue(m, out var es)) es.until = 0f;      // 菁英的招式預備
            m.windupUntil = 0f; m.dashUntil = 0f; m.fuseAt = 0f; m.charging = null; m.armed = null;
            m.staggerUntil = now + StaggerTime;
            m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + StaggerTime + 0.3f);
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

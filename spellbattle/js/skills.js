// 職業與技能資料庫：選一個職業，從該職業的技能中裝備 3 個。
//
// 技能欄位：
//   type        projectile（飛行攻擊）/ self（對自己：治療、防禦）/ trap（地面陷阱）
//   chargeMs    蓄力時間：唸咒後至少要等這麼久才能發射
//   range       射程（公尺）[最短, 最遠]；對手距離由畫面中身體大小估算，射程外打中也不算
//   releaseNear true＝距離不在射程內時「不能出手」（保留蓄力，靠近後再放）；否則照樣發射但會落空
//   reach       準星距離：準星從指尖沿手指方向延伸多遠（螢幕高度的比例）
//   radius      命中判定半徑（螢幕短邊的比例），越大越好打中
//   travelMs    飛行時間，越短對手越難閃
//   multi       一次射出多發 { count, spread（螢幕寬比例）}
//   effect      命中附加效果 dot（持續傷害）/ blind（致盲）/ snare（定身：暫時不能施法）
//   keywords    線上語音辨識的關鍵字（含常見同音誤判）
export const SKILLS = {
  // ---------------------------------------------------------- 弓箭手：蓄力長、箭速快、遠距離、判定小
  quickshot: {
    cls: 'archer', type: 'projectile', fx: 'arrow',
    id: 'quickshot', name: '速射', icon: '🏹', color: '#d9f99d', glow: '#ffffff',
    keywords: ['速射', '素射', '速設', '快射', 'quick'],
    cost: 12, damage: 12, chargeMs: 1000, travelMs: 220, radius: 0.035, range: [0, 12], reach: 0.4, cooldownMs: 900,
    desc: '基本箭矢，蓄力 1 秒',
  },
  snipe: {
    cls: 'archer', type: 'projectile', fx: 'arrow',
    id: 'snipe', name: '狙擊', icon: '🎯', color: '#fde047', glow: '#ffffff',
    keywords: ['狙擊', '狙击', '阻擊', '組擊', 'snipe'],
    cost: 30, damage: 34, chargeMs: 2200, travelMs: 260, radius: 0.025, range: [3, 15], reach: 0.5, cooldownMs: 3000,
    desc: '蓄力最久的重箭，3 公尺內無法使用',
  },
  triple: {
    cls: 'archer', type: 'projectile', fx: 'arrow', multi: { count: 3, spread: 0.09 },
    id: 'triple', name: '三連矢', icon: '🔱', color: '#a3e635', glow: '#ecfccb',
    keywords: ['三連矢', '三連', '三连', '三聯', '散射', 'triple'],
    cost: 24, damage: 9, chargeMs: 1600, travelMs: 260, radius: 0.03, range: [0, 10], reach: 0.4, cooldownMs: 2000,
    desc: '一次三箭扇形散開，每箭 9 傷害',
  },
  snaretrap: {
    cls: 'archer', type: 'trap', fx: 'trap',
    id: 'snaretrap', name: '捕獸夾', icon: '🪤', color: '#fb923c', glow: '#fed7aa',
    keywords: ['捕獸夾', '捕兽夹', '補獸夾', '獸夾', '陷阱', 'trap'],
    cost: 18, damage: 12, chargeMs: 1200, radius: 0.09, reach: 0.25, cooldownMs: 4000,
    trap: { lifeMs: 30000, armMs: 1200, max: 2 }, effect: { kind: 'snare', dur: 2500 },
    desc: '設在地上，踩到受傷並定身 2.5 秒（不能施法）；對手的雷達看得到',
  },
  blasttrap: {
    cls: 'archer', type: 'trap', fx: 'trap',
    id: 'blasttrap', name: '爆裂陷阱', icon: '💣', color: '#f87171', glow: '#fecaca',
    keywords: ['爆裂陷阱', '爆裂', '爆炸', '炸彈', 'bomb'],
    cost: 28, damage: 28, chargeMs: 1500, radius: 0.12, reach: 0.25, cooldownMs: 6000,
    trap: { lifeMs: 30000, armMs: 1500, max: 2 },
    desc: '設在地上，踩到爆炸 28 傷害',
  },

  // ---------------------------------------------------------- 法師：攻擊力最強、蓄力最長、範圍多樣
  fire: {
    cls: 'mage', type: 'projectile', fx: 'orb',
    id: 'fire', name: '火球術', icon: '🔥', color: '#ff7a1a', glow: '#ffd04a',
    keywords: ['火球', '火求', '火秋', '活球', 'fireball', 'fire'],
    cost: 20, damage: 24, chargeMs: 1500, travelMs: 900, radius: 0.09, range: [0, 5], reach: 0.22, cooldownMs: 1200,
    desc: '中速火球，平衡型',
  },
  ice: {
    cls: 'mage', type: 'projectile', fx: 'orb',
    id: 'ice', name: '冰槍', icon: '🧊', color: '#5fd7ff', glow: '#e0f8ff',
    keywords: ['冰槍', '冰枪', '兵槍', '冰矛', '冰箭', 'ice'],
    cost: 25, damage: 28, chargeMs: 1400, travelMs: 650, radius: 0.06, range: [0, 6], reach: 0.32, cooldownMs: 1500,
    desc: '快速穿刺，判定較窄',
  },
  thunder: {
    cls: 'mage', type: 'projectile', fx: 'orb',
    id: 'thunder', name: '雷擊', icon: '⚡', color: '#c08bff', glow: '#ffffff',
    keywords: ['雷擊', '雷击', '雷電', '雷电', '打雷', '落雷', 'thunder'],
    cost: 35, damage: 38, chargeMs: 2200, travelMs: 300, radius: 0.045, range: [0, 9], reach: 0.48, cooldownMs: 2500,
    desc: '幾乎瞬發，判定極窄',
  },
  wind: {
    cls: 'mage', type: 'projectile', fx: 'orb',
    id: 'wind', name: '風刃', icon: '🌪️', color: '#7dffb0', glow: '#e8fff0',
    keywords: ['風刃', '风刃', '風刀', '风刀', '風人', '封刃', 'wind'],
    cost: 12, damage: 16, chargeMs: 1000, travelMs: 600, radius: 0.14, range: [0, 2.5], reach: 0.1, cooldownMs: 800,
    desc: '範圍超大，只能近身',
  },
  meteor: {
    cls: 'mage', type: 'projectile', fx: 'orb',
    id: 'meteor', name: '隕石', icon: '☄️', color: '#ff3b3b', glow: '#ffb36b',
    keywords: ['隕石', '陨石', '引石', '允石', '流星', 'meteor'],
    cost: 50, damage: 58, chargeMs: 3000, travelMs: 1700, radius: 0.18, range: [2, 7], reach: 0.38, cooldownMs: 4000,
    desc: '蓄力 3 秒、超大範圍，2 公尺內不能用',
  },
  heal: {
    cls: 'mage', type: 'self', self: 'heal', fx: 'orb',
    id: 'heal', name: '治癒', icon: '💚', color: '#3dff7a', glow: '#d6ffe2',
    keywords: ['治癒', '治愈', '治療', '治疗', '回復', '恢復', '恢复', 'heal'],
    cost: 30, heal: 25, chargeMs: 1500, cooldownMs: 5000,
    desc: '回復 25 HP',
  },

  // ---------------------------------------------------------- 刺客：遠處蓄力、靠近才能出手、判定小、攻擊力強
  backstab: {
    cls: 'assassin', type: 'projectile', fx: 'dagger', releaseNear: true,
    id: 'backstab', name: '背刺', icon: '🗡️', color: '#e879f9', glow: '#fae8ff',
    keywords: ['背刺', '被刺', '倍刺', '背次', 'backstab'],
    cost: 25, damage: 42, chargeMs: 1300, travelMs: 140, radius: 0.05, range: [0, 1.5], reach: 0.15, cooldownMs: 2500,
    desc: '1.5 公尺內才能出手，42 傷害',
  },
  shadow: {
    cls: 'assassin', type: 'projectile', fx: 'dagger', releaseNear: true,
    id: 'shadow', name: '影襲', icon: '🌑', color: '#a78bfa', glow: '#ede9fe',
    keywords: ['影襲', '影袭', '影習', '隱襲', '影子', 'shadow'],
    cost: 20, damage: 28, chargeMs: 1000, travelMs: 180, radius: 0.05, range: [0, 2.5], reach: 0.18, cooldownMs: 1500,
    desc: '2.5 公尺內才能出手',
  },
  poison: {
    cls: 'assassin', type: 'projectile', fx: 'dagger', releaseNear: true,
    id: 'poison', name: '毒刃', icon: '🧪', color: '#4ade80', glow: '#dcfce7',
    keywords: ['毒刃', '毒人', '讀刃', '毒刀', '下毒', 'poison'],
    cost: 22, damage: 12, chargeMs: 1100, travelMs: 160, radius: 0.05, range: [0, 2], reach: 0.15, cooldownMs: 3000,
    effect: { kind: 'dot', dps: 4, dur: 5000 },
    desc: '2 公尺內出手，12 傷害＋中毒 5 秒（每秒 4）',
  },
  knife: {
    cls: 'assassin', type: 'projectile', fx: 'dagger',
    id: 'knife', name: '飛刀', icon: '🔪', color: '#cbd5e1', glow: '#ffffff',
    keywords: ['飛刀', '飞刀', '非刀', '飛到', 'knife'],
    cost: 10, damage: 10, chargeMs: 700, travelMs: 280, radius: 0.035, range: [0, 5], reach: 0.3, cooldownMs: 800,
    desc: '遠程牽制用的小刀',
  },
  smoke: {
    cls: 'assassin', type: 'projectile', fx: 'smoke',
    id: 'smoke', name: '煙霧彈', icon: '💨', color: '#9ca3af', glow: '#f3f4f6',
    keywords: ['煙霧彈', '烟雾弹', '煙霧', '煙幕', '烟幕', 'smoke'],
    cost: 18, damage: 0, chargeMs: 600, travelMs: 550, radius: 0.15, range: [0, 6], reach: 0.3, cooldownMs: 8000,
    effect: { kind: 'blind', dur: 4000 },
    desc: '命中後對手畫面被煙霧遮住 4 秒',
  },

  // ---------------------------------------------------------- 劍士：遠近攻擊都有、多種防禦
  slash: {
    cls: 'swordsman', type: 'projectile', fx: 'slash',
    id: 'slash', name: '斬擊', icon: '⚔️', color: '#f8fafc', glow: '#ffffff',
    keywords: ['斬擊', '斩击', '展擊', '砍擊', 'slash'],
    cost: 15, damage: 26, chargeMs: 600, travelMs: 160, radius: 0.13, range: [0, 2], reach: 0.15, cooldownMs: 1000,
    desc: '近身大範圍橫斬',
  },
  thrust: {
    cls: 'swordsman', type: 'projectile', fx: 'dagger',
    id: 'thrust', name: '突刺', icon: '🤺', color: '#fcd34d', glow: '#fffbeb',
    keywords: ['突刺', '突次', '圖刺', '突擊', 'thrust'],
    cost: 15, damage: 22, chargeMs: 700, travelMs: 200, radius: 0.05, range: [0, 3], reach: 0.22, cooldownMs: 1200,
    desc: '中距離直刺',
  },
  wave: {
    cls: 'swordsman', type: 'projectile', fx: 'slash',
    id: 'wave', name: '劍氣', icon: '🌙', color: '#93c5fd', glow: '#eff6ff',
    keywords: ['劍氣', '剑气', '見氣', '建企', '劍波', 'wave'],
    cost: 20, damage: 16, chargeMs: 900, travelMs: 450, radius: 0.1, range: [0, 7], reach: 0.35, cooldownMs: 1500,
    desc: '遠程劍氣',
  },
  block: {
    cls: 'swordsman', type: 'self', self: 'block', fx: 'shield',
    id: 'block', name: '格擋', icon: '🛡️', color: '#60a5fa', glow: '#dbeafe',
    keywords: ['格擋', '格挡', '隔擋', '個擋', 'block'],
    cost: 12, chargeMs: 200, cooldownMs: 4000, buff: { dur: 3000, reduce: 0.7 },
    desc: '3 秒內下一次受到的傷害 -70%',
  },
  ironwall: {
    cls: 'swordsman', type: 'self', self: 'shield', fx: 'shield',
    id: 'ironwall', name: '鐵壁', icon: '🧱', color: '#94a3b8', glow: '#f1f5f9',
    keywords: ['鐵壁', '铁壁', '鐵比', '貼壁', '鐵牆', 'wall'],
    cost: 30, chargeMs: 600, cooldownMs: 12000, buff: { dur: 8000, amount: 35 },
    desc: '8 秒護盾，吸收 35 傷害',
  },
  counter: {
    cls: 'swordsman', type: 'self', self: 'counter', fx: 'shield',
    id: 'counter', name: '反擊', icon: '↩️', color: '#f472b6', glow: '#fce7f3',
    keywords: ['反擊', '反击', '返擊', '反機', 'counter'],
    cost: 18, chargeMs: 200, cooldownMs: 6000, buff: { dur: 1500 },
    desc: '1.5 秒內被打中：無傷，並把對手的攻擊打回去（對手可閃避）',
  },
};

export const CLASSES = {
  archer: {
    id: 'archer', name: '弓箭手', icon: '🏹', color: '#a3e635',
    desc: '箭速快、射程遠、判定小；蓄力時間長。可在地上設陷阱，對手踩到就觸發。',
    stats: { maxHp: 100, maxMp: 100, mpRegen: 7 },
    skills: ['quickshot', 'snipe', 'triple', 'snaretrap', 'blasttrap'],
    defaultLoadout: ['quickshot', 'snipe', 'snaretrap'],
  },
  mage: {
    id: 'mage', name: '法師', icon: '🔮', color: '#c084fc',
    desc: '攻擊力最強、蓄力時間最長；法術的射程與範圍變化最多。',
    stats: { maxHp: 90, maxMp: 120, mpRegen: 7 },
    skills: ['fire', 'ice', 'thunder', 'wind', 'meteor', 'heal'],
    defaultLoadout: ['fire', 'thunder', 'meteor'],
  },
  assassin: {
    id: 'assassin', name: '刺客', icon: '🗡️', color: '#e879f9',
    desc: '攻擊力強、判定小。可在遠處先蓄力，必須靠近到射程內才能出手。',
    stats: { maxHp: 90, maxMp: 100, mpRegen: 8 },
    skills: ['backstab', 'shadow', 'poison', 'knife', 'smoke'],
    defaultLoadout: ['backstab', 'poison', 'smoke'],
  },
  swordsman: {
    id: 'swordsman', name: '劍士', icon: '⚔️', color: '#60a5fa',
    desc: '近戰與遠程攻擊都有，搭配格擋、鐵壁、反擊等防禦技能。血量最高。',
    stats: { maxHp: 130, maxMp: 90, mpRegen: 6 },
    skills: ['slash', 'thrust', 'wave', 'block', 'ironwall', 'counter'],
    defaultLoadout: ['slash', 'wave', 'block'],
  },
};

export const MAX_EQUIP = 3;

// 版本號：每次更新要和 index.html 的 data-version、game.js 的 VERSION 一起改（用來偵測檔案新舊混在一起）
export const SKILLS_VERSION = '2026.10.03-1';

export const STATS = {
  chargeTimeoutMs: 7000,  // 蓄力完成後還能維持多久（刺客另外加長，方便走近）
  // 距離估算用的相機/人體假設（可在戰鬥前用「📏 校正距離」修正）
  cameraFovDeg: 65,       // 鏡頭長邊方向的視角
  shoulderM: 0.38,        // 肩寬
  torsoM: 0.5,            // 肩膀中點到髖部中點
  bodyM: 1.65,            // 身高（只有人體框可用時）
};

export const rangeText = (s) => (s.range ? `${s.range[0] ? `${s.range[0]}–` : '≤'}${s.range[1]}m` : '—');

// 技能卡上的效果說明
export function effectText(s) {
  if (s.type === 'self') {
    if (s.self === 'heal') return `回復 ${s.heal}`;
    return '防禦';
  }
  const dmg = s.multi ? `${s.damage}×${s.multi.count}` : s.damage;
  if (s.type === 'trap') return `陷阱 ${dmg} 傷害`;
  return `傷害 ${dmg} · 射程 ${rangeText(s)}`;
}

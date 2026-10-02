// 技能資料庫：一個角色戰鬥時只能裝備其中 3 個。
// keywords 包含繁簡體與語音辨識常見的同音誤判，提升唸咒命中率。
// reach＝準星距離：準星從指尖往手指方向延伸多遠（螢幕高度的比例）。
//   短 → 準星貼著手，打得到畫面下方；長 → 準星遠離手，適合打畫面上方的目標。
// range＝射程（公尺）：[最短, 最遠]。對手的真實距離由畫面中的身體大小估算，射程外打中也不算。
export const SKILLS = {
  fire: {
    reach: 0.22, range: [0, 5],
    id: 'fire', name: '火球術', icon: '🔥', color: '#ff7a1a', glow: '#ffd04a',
    keywords: ['火球', '火求', '火秋', '活球', 'fireball', 'fire'],
    cost: 20, damage: 18, travelMs: 900, radius: 0.09, cooldownMs: 1200,
    desc: '中速火球，平衡型',
  },
  ice: {
    reach: 0.32, range: [0, 6],
    id: 'ice', name: '冰槍', icon: '🧊', color: '#5fd7ff', glow: '#e0f8ff',
    keywords: ['冰槍', '冰枪', '兵槍', '冰矛', '冰箭', 'ice'],
    cost: 25, damage: 22, travelMs: 650, radius: 0.06, cooldownMs: 1500,
    desc: '快速穿刺，判定較窄',
  },
  thunder: {
    reach: 0.48, range: [0, 9],
    id: 'thunder', name: '雷擊', icon: '⚡', color: '#c08bff', glow: '#ffffff',
    keywords: ['雷擊', '雷击', '雷電', '雷电', '打雷', '落雷', 'thunder'],
    cost: 35, damage: 30, travelMs: 300, radius: 0.045, cooldownMs: 2500,
    desc: '幾乎瞬發，判定極窄，要瞄準',
  },
  wind: {
    reach: 0.10, range: [0, 2.5],
    id: 'wind', name: '風刃', icon: '🌪️', color: '#7dffb0', glow: '#e8fff0',
    keywords: ['風刃', '风刃', '風刀', '风刀', '風人', '封刃', 'wind'],
    cost: 12, damage: 10, travelMs: 600, radius: 0.12, cooldownMs: 800,
    desc: '便宜、範圍大，只能近身',
  },
  meteor: {
    reach: 0.38, range: [2, 7],
    id: 'meteor', name: '隕石', icon: '☄️', color: '#ff3b3b', glow: '#ffb36b',
    keywords: ['隕石', '陨石', '引石', '允石', '流星', 'meteor'],
    cost: 50, damage: 45, travelMs: 1700, radius: 0.16, cooldownMs: 4000,
    desc: '超慢超痛，太近（2m 內）不能用',
  },
  heal: {
    id: 'heal', name: '治癒', icon: '💚', color: '#3dff7a', glow: '#d6ffe2',
    keywords: ['治癒', '治愈', '治療', '治疗', '回復', '恢復', '恢复', 'heal'],
    cost: 30, heal: 25, self: true, cooldownMs: 5000,
    desc: '回復自身 25 HP（手勢觸發，不用瞄準）',
  },
};

export const DEFAULT_LOADOUT = ['fire', 'ice', 'thunder'];
export const MAX_EQUIP = 3;

export const STATS = {
  maxHp: 100,
  maxMp: 100,
  mpRegenPerSec: 6,
  chargeTimeoutMs: 7000,
  // 距離估算用的相機/人體假設（可在戰鬥前用「📏 校正距離」修正）
  cameraFovDeg: 65,       // 鏡頭長邊方向的視角
  shoulderM: 0.38,        // 肩寬
  torsoM: 0.5,            // 肩膀中點到髖部中點
  bodyM: 1.65,            // 身高（只有人體框可用時）
};

export const rangeText = (s) => (s.range ? `${s.range[0] ? `${s.range[0]}–` : '≤'}${s.range[1]}m` : '—');

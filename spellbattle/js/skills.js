// 技能資料庫：一個角色戰鬥時只能裝備其中 3 個。
// keywords 包含繁簡體與語音辨識常見的同音誤判，提升唸咒命中率。
export const SKILLS = {
  fire: {
    id: 'fire', name: '火球術', icon: '🔥', color: '#ff7a1a', glow: '#ffd04a',
    keywords: ['火球', '火求', '火秋', '活球', 'fireball', 'fire'],
    cost: 20, damage: 18, travelMs: 900, radius: 0.09, cooldownMs: 1200,
    desc: '中速火球，平衡型',
  },
  ice: {
    id: 'ice', name: '冰槍', icon: '🧊', color: '#5fd7ff', glow: '#e0f8ff',
    keywords: ['冰槍', '冰枪', '兵槍', '冰矛', '冰箭', 'ice'],
    cost: 25, damage: 22, travelMs: 650, radius: 0.06, cooldownMs: 1500,
    desc: '快速穿刺，判定較窄',
  },
  thunder: {
    id: 'thunder', name: '雷擊', icon: '⚡', color: '#c08bff', glow: '#ffffff',
    keywords: ['雷擊', '雷击', '雷電', '雷电', '打雷', '落雷', 'thunder'],
    cost: 35, damage: 30, travelMs: 300, radius: 0.045, cooldownMs: 2500,
    desc: '幾乎瞬發，判定極窄，要瞄準',
  },
  wind: {
    id: 'wind', name: '風刃', icon: '🌪️', color: '#7dffb0', glow: '#e8fff0',
    keywords: ['風刃', '风刃', '風刀', '风刀', '風人', '封刃', 'wind'],
    cost: 12, damage: 10, travelMs: 600, radius: 0.12, cooldownMs: 800,
    desc: '便宜、範圍大、傷害低',
  },
  meteor: {
    id: 'meteor', name: '隕石', icon: '☄️', color: '#ff3b3b', glow: '#ffb36b',
    keywords: ['隕石', '陨石', '引石', '允石', '流星', 'meteor'],
    cost: 50, damage: 45, travelMs: 1700, radius: 0.16, cooldownMs: 4000,
    desc: '超慢超痛，對手有時間閃',
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
};

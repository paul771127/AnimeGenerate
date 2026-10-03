// 咒術對決 主遊戲邏輯
import { SKILLS, CLASSES, MAX_EQUIP, STATS, rangeText, effectText, SKILLS_VERSION } from './skills.js';
import { Orientation } from './orient.js';
import { ZoomTracker } from './odometry.js';
import { VoiceCaster } from './voice.js';
import { LocalSpotter, classify, finalizeSkill, loadTemplates, saveTemplates, hasTemplates } from './voice-local.js';
import { Net } from './net.js';

const $ = (id) => document.getElementById(id);

// 版本檢查：githack 會各別更新每個檔案，剛推新版時可能新舊混在一起
const VERSION = '2026.10.03-4';
{
  const htmlVer = document.documentElement.dataset.version;
  $('verText').textContent = VERSION;
  if (htmlVer !== VERSION || SKILLS_VERSION !== VERSION) {
    window.__showLoadError(`檔案版本不一致（頁面 ${htmlVer}／主程式 ${VERSION}／技能 ${SKILLS_VERSION}）`);
  }
}
const now = () => performance.now();
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;

// ---------------------------------------------------------------- 主選單
let cls = CLASSES[loadPref('cls', 'mage')] ? loadPref('cls', 'mage') : 'mage';
let loadout = [];
function loadLoadout() {
  const C = CLASSES[cls];
  loadout = loadPref('loadout_' + cls, C.defaultLoadout).filter((id) => C.skills.includes(id)).slice(0, MAX_EQUIP);
}
loadLoadout();
$('nameInput').value = loadPref('name', '');

function loadPref(k, d) { try { const v = localStorage.getItem('sb_' + k); return v ? JSON.parse(v) : d; } catch (_) { return d; } }
function savePref(k, v) { try { localStorage.setItem('sb_' + k, JSON.stringify(v)); } catch (_) {} }

function renderClasses() {
  const box = $('classPicker');
  box.innerHTML = '';
  for (const C of Object.values(CLASSES)) {
    const b = document.createElement('button');
    b.className = 'class-card' + (C.id === cls ? ' on' : '');
    b.style.setProperty('--c', C.color);
    b.innerHTML = `<span class="ic">${C.icon}</span>${C.name}`;
    b.onclick = () => {
      cls = C.id;
      savePref('cls', cls);
      loadLoadout();
      renderClasses(); renderPicker(); renderEnrollState();
    };
    box.appendChild(b);
  }
  const C = CLASSES[cls];
  $('classDesc').innerHTML = `${C.desc}<br><span class="tip">HP ${C.stats.maxHp} · MP ${C.stats.maxMp} · 每秒回魔 ${C.stats.mpRegen}</span>`;
}

function renderPicker() {
  const box = $('skillPicker');
  box.innerHTML = '';
  for (const s of CLASSES[cls].skills.map((id) => SKILLS[id])) {
    const b = document.createElement('button');
    b.className = 'skill-card' + (loadout.includes(s.id) ? ' on' : '');
    b.style.setProperty('--c', s.color);
    b.innerHTML = `<div class="t">${s.icon} ${s.name}</div><div class="s">MP ${s.cost} · 蓄力 ${(s.chargeMs / 1000).toFixed(1)}s · ${effectText(s)}<br>${s.desc}</div>`;
    b.onclick = () => {
      if (loadout.includes(s.id)) loadout = loadout.filter((x) => x !== s.id);
      else if (loadout.length < MAX_EQUIP) loadout.push(s.id);
      else { loadout.shift(); loadout.push(s.id); }
      savePref('loadout_' + cls, loadout);
      renderPicker();
      renderEnrollState();
    };
    box.appendChild(b);
  }
  $('equipCount').textContent = `${loadout.length}/${MAX_EQUIP}`;
}
renderClasses();
renderPicker();

// ---------------------------------------------------------------- 語音設定 / 錄製咒語
$('voiceMode').value = loadPref('voiceMode', 'auto');
$('voiceMode').onchange = () => { savePref('voiceMode', $('voiceMode').value); renderEnrollState(); };
$('btnEnroll').onclick = () => {
  if (loadout.length !== MAX_EQUIP) { $('homeStatus').textContent = `請先裝備 ${MAX_EQUIP} 個技能`; return; }
  initAudio();
  runEnrollment(loadout.map((id) => SKILLS[id])).then(renderEnrollState);
};

function renderEnrollState() {
  const t = loadTemplates();
  $('enrollState').textContent = '已錄製：' + loadout.map((id) => `${SKILLS[id].name} ${t[id] ? '✅' : '—'}`).join('　');
}

// 錄製咒語：每個技能唸 2 次 → 計算門檻 → 進入試唸測試
function runEnrollment(skills, reason) {
  return new Promise((resolve) => {
    const box = $('enroll');
    const all = loadTemplates();
    const queue = skills.flatMap((s) => [s, s]);
    const fresh = {};
    let testing = false;
    box.classList.add('show');
    $('enrollReason').textContent = reason || '';
    $('enrollDone').disabled = true;
    $('enrollResult').textContent = '';

    const renderList = () => {
      $('enrollList').innerHTML = skills.map((s) => {
        const n = fresh[s.id] ? fresh[s.id].templates.length : 0;
        return `<span style="--c:${s.color}">${s.icon} ${s.name} ${'●'.repeat(n)}${'○'.repeat(2 - n)}</span>`;
      }).join('');
      if (queue.length) {
        const s = queue[0];
        $('enrollPrompt').innerHTML = `請對手機唸：<b style="color:${s.color}">「${s.name}」</b>（第 ${(fresh[s.id]?.templates.length || 0) + 1}/2 次）`;
      } else {
        $('enrollPrompt').textContent = '✅ 錄製完成！試唸看看任一咒語，確認辨識正確';
      }
    };

    const spotter = new LocalSpotter({
      audioCtx: audio,
      onLevel: (rms, thr) => {
        $('enrollLevel').style.width = `${Math.min(100, (rms / (thr * 3)) * 100)}%`;
        $('enrollLevel').style.background = rms > thr ? '#3dff7a' : '#4da3ff';
      },
      onUtterance: (u) => {
        if (!testing) {
          const s = queue.shift();
          const e = (fresh[s.id] ||= { templates: [], peaks: [] });
          e.templates.push(u.seq); e.peaks.push(u.peak);
          if (e.templates.length === 2) {
            e.peak = (e.peaks[0] + e.peaks[1]) / 2;
            finalizeSkill(e);
            delete e.peaks;
            all[s.id] = e;
          }
          sfx('tick');
          if (!queue.length) { saveTemplates(all); testing = true; $('enrollDone').disabled = false; sfx('lock'); }
          renderList();
          return;
        }
        const r = classify(u.seq, u.peak, skills.map((s) => s.id), all);
        $('enrollResult').textContent = r.id
          ? `辨識為：${SKILLS[r.id].icon} ${SKILLS[r.id].name}（差異 ${r.best.d.toFixed(1)} / 門檻 ${r.best.thr.toFixed(1)}）`
          : `沒有觸發：${r.reason || '—'}${r.best ? `（最像 ${SKILLS[r.best.id].name}，差異 ${r.best.d.toFixed(1)} / 門檻 ${r.best.thr.toFixed(1)}）` : ''}`;
        if (r.id) sfx('chant'); else sfx('fail');
      },
    });

    const close = () => { spotter.stop(); box.classList.remove('show'); resolve(); };
    $('enrollRedo').onclick = () => {
      // 重錄：把目前這個技能（或測試中則全部）清掉重來
      if (testing) { testing = false; queue.length = 0; queue.push(...skills.flatMap((s) => [s, s])); for (const k in fresh) delete fresh[k]; $('enrollDone').disabled = true; }
      else { const s = queue[0]; if (fresh[s.id]) { const n = fresh[s.id].templates.length; delete fresh[s.id]; for (let i = 0; i < n; i++) queue.unshift(s); } }
      renderList();
    };
    $('enrollDone').onclick = close;
    $('enrollCancel').onclick = close;
    renderList();
    spotter.start().catch((err) => { $('enrollPrompt').textContent = '無法開啟麥克風：' + err.message; });
  });
}
renderEnrollState();

function canStart() {
  if (loadout.length !== MAX_EQUIP) { $('homeStatus').textContent = `請裝備剛好 ${MAX_EQUIP} 個技能`; return false; }
  savePref('name', $('nameInput').value.trim());
  return true;
}

$('btnPractice').onclick = () => { if (canStart()) enterGame('practice'); };
$('btnHost').onclick = () => { if (canStart()) enterGame('host'); };
$('btnJoin').onclick = () => {
  const code = $('codeInput').value.trim();
  if (!/^\d{4}$/.test(code)) { $('homeStatus').textContent = '請輸入 4 位數房號'; return; }
  if (canStart()) enterGame('join', code);
};

// ---------------------------------------------------------------- 遊戲狀態
const video = $('cam');
const canvas = $('fx');
const ctx = canvas.getContext('2d');
let W = 0, H = 0, DPR = 1;

let vision = null;
let voice = null;
let net = null;
let audio = null;
const orient = new Orientation();
const zoom = new ZoomTracker();   // 背景追蹤：玩家前後移動、轉動手機

const S = {
  mode: 'practice',          // practice | online
  phase: 'loading',          // loading | scan | waiting | countdown | battle | over
  cls: 'mage',
  me: { name: '', hp: 100, mp: 100, maxHp: 100, maxMp: 100, regen: 6, cooldowns: {},
    dots: [], buffs: {}, snaredUntil: 0 },
  blindUntil: 0,             // 被煙霧彈致盲
  traps: [],                 // 我方設置的陷阱
  enemyTraps: [],            // 對手的陷阱（相對我的位置，公尺）
  enemyTrapsSeen: true,
  ai: null,                  // 練習模式的電腦對手
  skills: [],
  enemy: { name: '對手', cls: null, buffs: {}, dots: [], hp: 100, maxHp: 100, locked: false, virtual: false,
    signature: null, box: null, lastSeen: 0, missFrames: 99, distance: null, distMethod: '', ready: false, loadout: [] },
  candidate: null,           // 掃描階段偵測到的人
  charging: null,            // { skill, since }
  hand: null,                // 螢幕座標的手部資訊
  aim: null,                 // 瞄準點（螢幕座標）
  lastFist: 0,
  palmHist: [],
  projectiles: [],           // 我方發出的法術
  incoming: [],              // 對手飛向我的法術
  particles: [],
  floaters: [],
  flash: 0, shake: 0,
  meReady: false,
  nextId: 1,
};
window.__spellduel = S;   // 方便除錯
window.__skills = SKILLS;

// ---------------------------------------------------------------- 驗證用統計
const DEBUG = new URLSearchParams(location.search).has('debug');
const M = {};
function resetMetrics() {
  Object.assign(M, {
    voiceChants: 0, tapChants: 0, timeouts: 0,
    fires: { fist: 0, flick: 0, tap: 0 },
    hits: 0, misses: 0, dodged: 0, hurt: 0, outOfRange: 0, mitigated: 0, trapHits: 0,
    chantToFire: [], voiceDelay: [], voiceLog: [], fps: 0, lostFrames: 0, frames: 0,
  });
}
resetMetrics();
const avg = (a) => (a.length ? Math.round(a.reduce((x, y) => x + y, 0) / a.length) : '-');
const pct = (a, b) => (a + b ? `${Math.round((a / (a + b)) * 100)}%` : '-');

function resize() {
  DPR = Math.min(window.devicePixelRatio || 1, 2);
  W = window.innerWidth; H = window.innerHeight;
  canvas.width = W * DPR; canvas.height = H * DPR;
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
}
window.addEventListener('resize', resize);

// 影片使用 object-fit: cover，換算正規化影片座標 → 螢幕座標
function v2s(nx, ny) {
  const vw = video.videoWidth || W, vh = video.videoHeight || H;
  const sc = Math.max(W / vw, H / vh);
  const dw = vw * sc, dh = vh * sc;
  return { x: (W - dw) / 2 + nx * dw, y: (H - dh) / 2 + ny * dh };
}
function boxToScreen(b) {
  const p0 = v2s(b.x0, b.y0), p1 = v2s(b.x1, b.y1);
  return { x0: p0.x, y0: p0.y, x1: p1.x, y1: p1.y };
}

// ---------------------------------------------------------------- 語音：本機辨識優先，線上失敗時自動切換
function voiceLog(line) { M.voiceLog.push(line); if (M.voiceLog.length > 6) M.voiceLog.shift(); }
function setVoiceStatus(t) { $('voiceStatus').textContent = t; }

function startVoice() {
  const mode = loadPref('voiceMode', 'auto');
  if (mode === 'local' || (mode === 'auto' && hasTemplates(loadout))) return startLocalVoice();
  const cloud = new VoiceCaster({
    skills: S.skills,
    onSkill: (s, info) => { if (chant(s, 'voice')) M.voiceDelay.push(info.sinceFirstTextMs); },
    onTranscript: (t, isFinal, skill) => { $('transcript').textContent = t; if (isFinal) voiceLog(`${skill ? '✅' : '❌'} ${t}`); },
    onStatus: setVoiceStatus,
    onFatal: (code, msg) => {
      voiceLog(`⚠ 線上辨識錯誤 ${code}`);
      if (mode === 'cloud') setVoiceStatus(`⚠ 語音錯誤：${msg}`);
      else fallbackToLocal(`線上語音辨識無法使用：${msg}`);
    },
  });
  voice = cloud;
  if (!cloud.supported) {
    if (mode === 'cloud') setVoiceStatus('⚠ 此瀏覽器不支援線上語音辨識');
    else fallbackToLocal('此瀏覽器不支援線上語音辨識');
    return;
  }
  cloud.start();
}

// 點語音狀態列＝手動改用本機辨識（或重錄咒語）
$('voiceBar').onclick = () => {
  if (voice && voice.stop) voice.stop();
  voice = null;
  runEnrollment(S.skills, '重新錄製咒語').then(startLocalVoice);
};

async function fallbackToLocal(reason) {
  setVoiceStatus('改用本機咒語辨識');
  if (!hasTemplates(loadout)) await runEnrollment(S.skills, `${reason}\n改用本機咒語辨識：先錄下你唸的咒語（每個 2 次，約 20 秒）。`);
  startLocalVoice();
}

async function startLocalVoice() {
  if (!hasTemplates(loadout)) return fallbackToLocal('尚未錄製咒語');
  const templates = loadTemplates();
  const ids = loadout.slice();
  const local = new LocalSpotter({
    audioCtx: audio,
    onUtterance: (u) => {
      const r = classify(u.seq, u.peak, ids, templates);
      const b = r.best;
      const detail = b ? `${SKILLS[b.id].name} ${b.d.toFixed(1)}/${b.thr.toFixed(1)}` : '';
      voiceLog(r.id ? `✅ ${detail}` : `❌ ${r.reason} ${detail}`);
      $('transcript').textContent = r.id ? `${SKILLS[r.id].icon} ${SKILLS[r.id].name}` : `（${r.reason}）`;
      if (r.id && chant(SKILLS[r.id], 'voice')) M.voiceDelay.push(now() - u.endedAt);
    },
  });
  voice = local;
  try {
    await local.start();
    setVoiceStatus('🎙️ 本機咒語辨識：聆聽中');
  } catch (e) {
    setVoiceStatus('⚠ 無法開啟麥克風：' + e.message);
  }
}

// ---------------------------------------------------------------- 進入遊戲
async function enterGame(mode, code) {
  $('home').classList.remove('active');
  $('game').classList.add('active');
  resize();
  S.mode = mode === 'practice' ? 'practice' : 'online';
  S.cls = cls;
  const C = CLASSES[cls];
  S.me.name = $('nameInput').value.trim() || C.name;
  Object.assign(S.me, { maxHp: C.stats.maxHp, maxMp: C.stats.maxMp, regen: C.stats.mpRegen });
  S.skills = loadout.map((id) => SKILLS[id]);
  orient.request();   // 陀螺儀（陷阱定位用）；iOS 需在點擊當下請求權限
  buildSlots();
  setLoading('開啟相機…');
  initAudio();

  try {
    const stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
      audio: false,
    });
    video.srcObject = stream;
    await video.play();
  } catch (e) {
    setLoading('無法開啟相機：' + e.message + '\n（需要 HTTPS 並允許相機權限）');
    return;
  }

  try {
    const mod = await import('./vision.js');
    vision = new mod.Vision();
    await vision.init(setLoading);
    S.vision = vision;   // 方便除錯
    S.torsoSignature = mod.torsoSignature;
    S.signatureSimilarity = mod.signatureSimilarity;
    S.estimateDistance = mod.estimateDistance;
  } catch (e) {
    console.error(e);
    vision = null;
    toast('影像辨識載入失敗，改用點擊模式', '#ffd04a');
  }


  if (S.mode === 'online') {
    net = new Net({
      onMessage: onNet,
      onStatus: (t) => { $('netStatus').textContent = t; },
      onOpen: () => net.send({ t: 'hello', name: S.me.name, loadout, cls: S.cls, maxHp: S.me.maxHp }),
      onClose: () => { if (S.phase !== 'over') banner('對手斷線', 2000); },
    });
    setLoading('連線中…');
    try {
      if (mode === 'host') {
        const room = await net.host();
        $('netStatus').textContent = `房號 ${room}：請對手輸入此號碼`;
      } else {
        await net.join(code);
      }
    } catch (e) {
      // 狀態已顯示在 netStatus，仍可先掃描目標
    }
  } else {
    $('netStatus').textContent = '單人練習';
  }

  hideLoading();
  enterScan();
  startVoice();
  requestAnimationFrame(loop);
}

function setLoading(t) { $('loadingText').textContent = t; $('loading').classList.add('show'); }
function hideLoading() { $('loading').classList.remove('show'); }

function enterScan() {
  S.phase = 'scan';
  S.enemy.locked = false;
  S.enemy.virtual = false;
  $('scanPanel').classList.add('show');
  $('btnDummy').style.display = S.mode === 'practice' ? '' : 'none';
  hint('');
}

$('btnLock').onclick = () => lockTarget(false);
$('btnDummy').onclick = () => lockTarget(true);

function lockTarget(virtual) {
  const e = S.enemy;
  e.virtual = virtual;
  if (!virtual) {
    const c = S.candidate;
    if (!c) return;
    e.signature = S.torsoSignature ? S.torsoSignature(video, c.landmarks) : null;
    e.box = boxToScreen(c.box);
    $('enemyThumb').src = snapshot(c.box);
  } else {
    e.signature = null;
    $('enemyThumb').removeAttribute('src');
  }
  e.locked = true;
  e.distance = null;
  if (virtual) resetVirtualEnemy();
  e.lastSeen = now();
  e.missFrames = 0;
  $('scanPanel').classList.remove('show');
  sfx('lock');
  burst(W / 2, H / 2, '#ff4d6d', 30);
  toast('🎯 目標已鎖定', '#ff4d6d');
  if (S.mode === 'practice') startCountdown();
  else {
    S.meReady = true;
    S.phase = 'waiting';
    net.send({ t: 'ready' });
    maybeStart();
  }
}

function targetVisible() {
  const e = S.enemy;
  return !!e.box && e.missFrames < 6;
}

function snapshot(b) {
  const c = document.createElement('canvas');
  c.width = 96; c.height = 96;
  const vw = video.videoWidth, vh = video.videoHeight;
  const bw = (b.x1 - b.x0) * vw, bh = Math.min((b.y1 - b.y0) * vh, bw * 1.2);
  try { c.getContext('2d').drawImage(video, b.x0 * vw, b.y0 * vh, bw, bh, 0, 0, 96, 96); } catch (_) {}
  return c.toDataURL('image/jpeg', 0.7);
}

function maybeStart() {
  if (S.mode !== 'online') return;
  if (S.phase === 'waiting') hint(S.enemy.ready ? '' : '等待對手鎖定目標…');
  if (net.isHost && S.meReady && S.enemy.ready && S.phase === 'waiting') {
    net.send({ t: 'start' });
    startCountdown();
  }
}

function startCountdown() {
  S.phase = 'countdown';
  if (S.mode === 'practice') setupAI();   // 每局隨機一個電腦職業
  resetStats();
  hint('');
  let n = 3;
  const tick = () => {
    if (n > 0) { banner(String(n), 800); sfx('tick'); n--; setTimeout(tick, 1000); }
    else { banner('FIGHT!', 900); sfx('fight'); S.phase = 'battle'; hint('唸出技能名稱開始詠唱'); }
  };
  tick();
}

function resetStats() {
  Object.assign(S.me, { hp: S.me.maxHp, mp: S.me.maxMp, cooldowns: {}, dots: [], buffs: {}, snaredUntil: 0 });
  S.enemy.hp = S.enemy.maxHp;
  S.enemy.buffs = {}; S.enemy.dots = [];
  S.charging = null; S.projectiles = []; S.incoming = []; S.traps = []; S.enemyTraps = []; S.blindUntil = 0;
  resetMetrics();
  $('result').classList.remove('show');
}

// ---------------------------------------------------------------- 技能：詠唱與施放
function buildSlots() {
  const box = $('slots');
  box.innerHTML = '';
  S.slotEls = {};
  for (const s of S.skills) {
    const d = document.createElement('div');
    d.className = 'slot';
    d.style.setProperty('--c', s.color);
    d.innerHTML = `<span class="ic">${s.icon}</span>${s.name}<div class="cost">MP ${s.cost}</div><div class="cd"></div>`;
    d.onpointerdown = (ev) => { ev.stopPropagation(); chant(s, 'tap'); };
    box.appendChild(d);
    S.slotEls[s.id] = d;
  }
}

function chant(skill, via) {
  if (S.phase !== 'battle') { if (S.phase !== 'over') hint('戰鬥開始後才能施法'); return false; }
  const t = now();
  if ((S.me.cooldowns[skill.id] || 0) > t) { toast(`${skill.name} 冷卻中`, '#aaa'); return false; }
  if (S.me.mp < skill.cost) { toast('MP 不足', '#4da3ff'); sfx('fail'); return false; }
  if (S.me.snaredUntil > t) { toast('🪤 被困住，暫時不能施法', '#fb923c'); sfx('fail'); return false; }
  S.charging = { skill, since: t };
  if (via === 'voice') M.voiceChants++; else M.tapChants++;
  sfx('chant');
  const how = skill.type === 'self' ? '握拳後張開手掌發動'
    : skill.type === 'trap' ? '手指指向地面，握拳→張開手掌設置陷阱'
    : skill.releaseNear ? '可先蓄力，靠近到射程內再握拳→張開出手'
    : '手指對準敵人，握拳→張開手掌發射';
  hint(`${skill.icon} ${skill.name}：${how}`);
  if (net) net.send({ t: 'charge', skill: skill.id });
  return true;
}

// 回傳 true＝已出手；false＝條件不足（保留蓄力）
function release(aim, via) {
  const c = S.charging;
  if (!c || S.phase !== 'battle') return false;
  const s = c.skill;
  if (S.me.mp < s.cost) { S.charging = null; toast('MP 不足', '#4da3ff'); return false; }
  const elapsed = now() - c.since;
  if (elapsed < s.chargeMs) {
    toast(`蓄力中 ${Math.floor((elapsed / s.chargeMs) * 100)}%`, s.color);
    sfx('fail');
    return false;
  }
  if (s.releaseNear && rangeState(s) === 'far') {
    toast(`再靠近！${S.enemy.distance.toFixed(1)}m → ${rangeText(s)}`, s.color);
    sfx('fail');
    return false;
  }
  M.fires[via]++;
  M.chantToFire.push(now() - c.since);
  S.me.mp -= s.cost;
  S.me.cooldowns[s.id] = now() + s.cooldownMs;
  S.charging = null;
  hint('');

  if (s.type === 'self') {
    castSelf(s);
    return true;
  }

  const to = aim || S.aim || { x: W / 2, y: H * 0.4 };
  if (s.type === 'trap') {
    placeTrap(s, to);
    return true;
  }

  const from = S.hand ? { x: S.hand.tip.x, y: S.hand.tip.y } : { x: W / 2, y: H * 0.9 };
  const n = s.multi ? s.multi.count : 1;
  for (let i = 0; i < n; i++) {
    const off = s.multi ? (i - (n - 1) / 2) * s.multi.spread * W : 0;
    const p = { x: to.x + off, y: to.y };
    const id = S.nextId++;
    S.projectiles.push({ id, skill: s, from, to: p, start: now(), dur: s.travelMs });
    if (net) net.send({ t: 'cast', id, skill: s.id, ax: p.x / W, ay: p.y / H, dur: s.travelMs });
  }
  sfx(s.fx === 'arrow' ? 'arrow' : s.fx === 'slash' || s.fx === 'dagger' ? 'slash' : 'cast');
  return true;
}

// 治療 / 防禦
function castSelf(s) {
  const t = now();
  if (s.self === 'heal') {
    S.me.hp = Math.min(S.me.maxHp, S.me.hp + s.heal);
    floater(W / 2, H * 0.7, `+${s.heal}`, s.color);
    sfx('heal');
  } else {
    const b = { until: t + s.buff.dur, ...s.buff };
    if (s.self === 'shield') b.left = s.buff.amount;
    S.me.buffs[s.self] = b;
    floater(W / 2, H * 0.7, `${s.icon} ${s.name}`, s.color);
    sfx('shield');
    if (net) net.send({ t: 'buff', kind: s.self, dur: s.buff.dur });
  }
  burst(W / 2, H * 0.8, s.color, 50);
  if (net) { net.send({ t: 'cast', skill: s.id }); sendState(); }
}

function impact(p) {
  const s = p.skill, e = S.enemy;
  const dmg = p.dmg ?? s.damage, eff = p.eff !== undefined ? p.eff : s.effect;   // 反擊打回去的攻擊沿用原本的傷害/效果
  const r = (s.radius || 0.08) * Math.min(W, H);
  const b = targetVisible() ? e.box : null;
  const onTarget = !!b && p.to.x > b.x0 - r && p.to.x < b.x1 + r && p.to.y > b.y0 - r && p.to.y < b.y1 + r;
  const range = p.reflected ? null : rangeState(s);
  const hit = onTarget && range !== 'far' && range !== 'near';
  burst(p.to.x, p.to.y, s.color, hit ? 60 : 20, hit ? 1.4 : 0.7);
  if (hit) {
    floater(p.to.x, p.to.y - 30, dmg ? `-${dmg}` : `${s.icon}`, '#ff4d6d', 1.4);
    if (eff) floater(p.to.x, p.to.y - 60, effectLabel(eff), s.color, 0.8);
    sfx('hit');
    M.hits++;
    if (S.ai) aiTakeHit(dmg, eff, p); else hitEnemyLocal(dmg, eff);
  } else {
    const why = onTarget && range === 'far' ? '射程外' : onTarget && range === 'near' ? '太近' : 'MISS';
    floater(p.to.x, p.to.y - 30, why, '#ccc');
    M.misses++;
    if (why !== 'MISS') M.outOfRange++;
    sfx('miss');
  }
  if (net) net.send({ t: 'result', id: p.id, hit, dmg: hit ? dmg : 0, eff: hit ? eff : null, skill: s.id });
}

// 對手（或木人）的 HP 預測：連線時以對手回報的 state 為準
function hitEnemyLocal(dmg, eff) {
  const e = S.enemy;
  e.hp = Math.max(0, e.hp - dmg);
  if (S.mode === 'practice') {
    if (eff && eff.kind === 'dot') e.dots.push({ dps: eff.dps, until: now() + eff.dur, acc: 0 });
    if (e.hp <= 0) finish(true);
  }
}

function effectLabel(eff) {
  return { dot: '☠ 中毒', blind: '💨 致盲', snare: '🪤 定身' }[eff.kind] || '';
}

// 被打中：先算防禦（反擊 → 格擋 → 護盾），再套用附加效果
// 回傳實際受到的傷害
function receiveHit(dmg, skillId, eff, attackId) {
  const t = now(), B = S.me.buffs;
  if (B.counter && B.counter.until > t) {
    delete B.counter;
    reflectAttack(SKILLS[skillId] || DUMMY_SKILL, dmg, eff, attackId);
    return 0;
  }
  let note = '';
  if (B.block && B.block.until > t && dmg > 0) {
    dmg = Math.round(dmg * (1 - B.block.reduce));
    delete B.block;
    note = '🛡️ 格擋';
  }
  if (B.shield && B.shield.until > t && dmg > 0) {
    const absorb = Math.min(B.shield.left, dmg);
    B.shield.left -= absorb; dmg -= absorb;
    if (B.shield.left <= 0) delete B.shield;
    note = note ? note + '＋🧱' : '🧱 護盾吸收';
  }
  if (note) { M.mitigated++; floater(W / 2, H * 0.36, note, '#93c5fd', 1.1); if (net) net.send({ t: 'mitigated', note }); }
  if (eff) applyEffect(eff);
  if (dmg > 0 || !note) takeDamage(dmg, skillId);
  else sendState();
  return dmg;
}

// 反擊：把飛來的攻擊原樣打回去，朝對手目前的位置飛（對手移動就能閃開）
function reflectAttack(sk, dmg, eff, attackId) {
  const t = now();
  floater(W / 2, H * 0.45, '↩️ 反擊！打回去', '#f472b6', 1.5);
  sfx('shield');
  M.mitigated++;
  const inc = S.incoming.find((i) => i.id === attackId);
  if (inc) inc.resolved = true;
  const from = inc ? { x: lerp(inc.ax * W, W / 2, 0.4), y: H * 0.5 } : { x: W / 2, y: H * 0.6 };
  const b = targetVisible() ? S.enemy.box : null;
  const to = b ? { x: (b.x0 + b.x1) / 2, y: b.y0 + (b.y1 - b.y0) * 0.35 } : { x: W / 2, y: H * 0.35 };
  const dur = Math.max(sk.travelMs || 600, 600);
  const id = S.nextId++;
  S.projectiles.push({ id, skill: sk, from, to, start: t, dur, dmg, eff: eff || null, reflected: true });
  burst(from.x, from.y, '#f472b6', 40, 1.2);
  if (net) {
    net.send({ t: 'countered', id: attackId });
    net.send({ t: 'cast', id, skill: sk.id, ax: to.x / W, ay: to.y / H, dur, refl: true });
    sendState();   // 修正對手畫面上「預測已命中」的血量
  }
}

function applyEffect(eff) {
  const t = now();
  if (eff.kind === 'dot') S.me.dots.push({ dps: eff.dps, until: t + eff.dur, acc: 0 });
  if (eff.kind === 'blind') S.blindUntil = t + eff.dur;
  if (eff.kind === 'snare') { S.me.snaredUntil = t + eff.dur; S.charging = null; }
  floater(W / 2, H * 0.55, effectLabel(eff), '#fbbf24', 1.2);
}

function takeDamage(dmg, skillId) {
  S.me.hp = Math.max(0, S.me.hp - dmg);
  M.hurt++;
  S.flash = 1; S.shake = 18;
  const s = SKILLS[skillId];
  burst(W / 2, H / 2, s ? s.color : '#ff4d6d', 70, 1.6);
  if (dmg > 0) floater(W / 2, H * 0.45, `-${dmg}`, '#ff4d6d', 1.8);
  sfx('hurt');
  if (navigator.vibrate) navigator.vibrate(220);
  sendState();
  if (S.me.hp <= 0) { if (net) net.send({ t: 'ko' }); finish(false); }
}

function sendState() { if (net) net.send({ t: 'state', hp: Math.ceil(S.me.hp), maxHp: S.me.maxHp, mp: Math.round(S.me.mp) }); }

function finish(win) {
  if (S.phase === 'over') return;
  S.phase = 'over';
  S.charging = null;
  $('resultText').textContent = win ? '🏆 勝利！' : '💀 敗北…';
  $('resultStats').innerHTML = statsHtml();
  $('result').classList.add('show');
  sfx(win ? 'win' : 'lose');
}

$('btnRematch').onclick = () => {
  if (S.mode === 'practice') startCountdown();
  else if (net && net.connected) { net.send({ t: 'rematch' }); startCountdown(); }
};
$('btnHome').onclick = () => location.reload();

// ---------------------------------------------------------------- 網路訊息
function onNet(m) {
  const e = S.enemy;
  switch (m.t) {
    case 'hello':
      e.name = m.name || '對手';
      e.loadout = m.loadout || [];
      e.cls = CLASSES[m.cls] ? m.cls : null;
      if (m.maxHp) e.maxHp = e.hp = m.maxHp;
      if (e.cls) e.name = `${CLASSES[e.cls].icon} ${e.name}`;
      toast(`⚔️ ${e.name} 加入對戰`, '#c04dff');
      break;
    case 'ready':
      e.ready = true;
      maybeStart();
      break;
    case 'start':
    case 'rematch':
      e.ready = true;
      startCountdown();
      break;
    case 'charge': {
      const s = SKILLS[m.skill];
      if (s) warn(`⚠ ${e.name} 正在詠唱 ${s.icon}${s.name}`);
      break;
    }
    case 'cast': {
      const s = SKILLS[m.skill];
      if (!s) break;
      warn('');
      if (s.type === 'self') { floater(W / 2, H * 0.25, `${e.name} ${s.icon} ${s.name}`, s.color); break; }
      // 對手瞄準我的位置，左右在我的視角是鏡像
      S.incoming.push({ id: m.id, skill: s, ax: 1 - m.ax, ay: m.ay, start: now(), dur: m.dur, resolved: false, refl: !!m.refl });
      if (m.refl) warn('↩️ 你的攻擊被打回來了！快閃開', m.dur);
      sfx('incoming');
      break;
    }
    case 'result': {
      const inc = S.incoming.find((i) => i.id === m.id);
      if (inc) inc.resolved = true;
      if (m.hit) receiveHit(m.dmg, m.skill || (inc && inc.skill.id), m.eff, m.id);
      else { floater(W / 2, H * 0.45, '閃避成功！', '#7dffb0', 1.4); sfx('dodge'); M.dodged++; }
      break;
    }
    case 'state':
      e.hp = m.hp;
      if (m.maxHp) e.maxHp = m.maxHp;
      break;
    case 'buff':        // 對手開了防禦，畫在對手身上
      e.buffs[m.kind] = now() + m.dur;
      break;
    case 'mitigated':   // 我的攻擊被對手防禦
      if (e.box) floater((e.box.x0 + e.box.x1) / 2, e.box.y0 + 20, m.note, '#93c5fd', 1.1);
      break;
    case 'countered':   // 我的攻擊被反擊，接著會收到打回來的 cast
      floater(W / 2, H * 0.4, '被反擊！攻擊被打回來', '#f472b6', 1.2);
      e.buffs.counter = 0;
      break;
    case 'trapSet':
      warn(`⚠ ${e.name} 設了 ${SKILLS[m.skill] ? SKILLS[m.skill].icon : ''} 陷阱，看右上角雷達`, 3500);
      break;
    case 'traps':       // 對手陷阱在我周圍的位置（由對手的鏡頭計算）
      S.enemyTraps = m.list;
      S.enemyTrapsSeen = m.seen;
      break;
    case 'trap':        // 我踩到對手的陷阱
      banner('踩到陷阱！', 1200);
      receiveHit(m.dmg, m.skill, m.eff, null);
      break;
    case 'ko':
      e.hp = 0;
      finish(true);
      break;
  }
}

// ---------------------------------------------------------------- 主迴圈
let lastT = now();
function loop() {
  const t = now();
  const rawDt = Math.max(0.001, (t - lastT) / 1000);
  const dt = Math.min(0.05, rawDt);      // 動畫用（避免卡頓時跳太大）
  const realDt = Math.min(0.5, rawDt);   // 遊戲數值用（回魔、中毒），幀率低時也不會變慢
  lastT = t;

  if (vision) {
    try { vision.update(video); } catch (err) { console.warn(err); }
    updateTarget(t);
    updateHand(t);
  } else if (S.enemy.virtual) {
    updateTarget(t);
  }

  if (S.phase === 'battle') {
    S.me.mp = Math.min(S.me.maxMp, S.me.mp + S.me.regen * realDt);
    const c = S.charging;
    // 蓄力完成後還能維持一段時間；刺客（靠近才能出手）多給 8 秒走過去
    if (c && t - c.since > c.skill.chargeMs + STATS.chargeTimeoutMs + (c.skill.releaseNear ? 8000 : 0)) {
      S.charging = null; hint('詠唱逾時'); sfx('fail'); M.timeouts++;
    }
    tickDots(realDt);
    updateTraps(t);
    if (S.ai) aiUpdate(t, realDt);
  }

  for (const p of S.projectiles) if (!p.done && t - p.start >= p.dur) { p.done = true; impact(p); }
  for (const i of S.incoming) if (i.ai && !i.resolved && t - i.start >= i.dur) aiResolve(i);
  S.projectiles = S.projectiles.filter((p) => !p.done);
  S.incoming = S.incoming.filter((i) => t - i.start < i.dur + 1500 && !(i.resolved && t - i.start > i.dur));

  M.fps = M.fps ? lerp(M.fps, 1 / rawDt, 0.05) : 1 / rawDt;
  if (S.phase === 'battle' && S.enemy.locked) { M.frames++; if (!targetVisible()) M.lostFrames++; }
  render(t, dt);
  updateHud(t);
  if (DEBUG) updateDebug(t);
  requestAnimationFrame(loop);
}

function updateTarget(t) {
  const e = S.enemy;
  if (e.virtual) {
    updateVirtualEnemy(t);
    return;
  }
  if (!vision) return;
  const people = vision.people;
  if (S.phase === 'scan') {
    // 掃描階段：取畫面中最大的人當候選
    let best = null, area = 0;
    for (const p of people) {
      const a = (p.box.x1 - p.box.x0) * (p.box.y1 - p.box.y0);
      if (a > area) { area = a; best = p; }
    }
    S.candidate = best;
    $('btnLock').disabled = !best;
    $('scanText').textContent = best ? '偵測到人物！按下鎖定' : '將鏡頭對準對手全身…';
    return;
  }
  if (!e.locked) return;
  // 以「連續幾次人體偵測沒看到」判斷目標遺失，不受幀率影響
  if (!people.length) { if (vision.frame % 2 === 0) e.missFrames++; return; }
  let pick = people[0];
  if (people.length > 1 && e.signature && S.torsoSignature) {
    let bestSim = -1;
    for (const p of people) {
      const sim = S.signatureSimilarity(e.signature, S.torsoSignature(video, p.landmarks));
      if (sim > bestSim) { bestSim = sim; pick = p; }
    }
  }
  const nb = boxToScreen(pick.box);
  if (e.box && e.missFrames < 3) {
    const k = 0.5;
    e.box = { x0: lerp(e.box.x0, nb.x0, k), x1: lerp(e.box.x1, nb.x1, k), y0: lerp(e.box.y0, nb.y0, k), y1: lerp(e.box.y1, nb.y1, k) };
  } else e.box = nb;
  e.lastSeen = t;
  e.missFrames = 0;
  const est = rawDistance(pick);
  if (est) {
    const d = est.d * distCalib;
    e.distance = e.distance ? lerp(e.distance, d, 0.25) : d;
    e.distMethod = est.method;
  }
}

// ---------------------------------------------------------------- 虛擬對手（電腦）放在真實空間裡
// 背景追蹤得到畫面縮放 S 與平移 T：
//   距離 = 電腦自己的距離 ÷ S（玩家後退 → 背景變小 → S 變小 → 距離變遠）
//   位置 = 以畫面中心縮放後再加上平移（轉動手機時電腦留在原地）
// 電腦自己也會走位：刺客逼近、劍士約 2 公尺、法師/弓箭手拉開距離，並左右移動。
const AI_PREF_DIST = { assassin: 1.2, swordsman: 2, archer: 5, mage: 4.5 };
const V = { dist: 3, strafe: 0, offX: 0, offscreenSince: 0, lastT: 0, frame: 0 };

function resetVirtualEnemy() {
  zoom.reset();
  Object.assign(V, { dist: 3, strafe: 0, offX: 0, offscreenSince: 0, lastT: now(), frame: 0 });
}

function updateVirtualEnemy(t) {
  const e = S.enemy;
  const dt = Math.min(0.5, (t - (V.lastT || t)) / 1000);
  V.lastT = t;
  if (video.videoWidth && V.frame++ % 2 === 0) zoom.update(video);
  const Sz = clamp(zoom.scale, 0.2, 5);
  // 電腦走位（只在戰鬥中）：朝偏好距離移動，每秒最多 0.5 公尺
  if (S.phase === 'battle' && S.ai && !(S.ai.snaredUntil > t)) {
    const pref = AI_PREF_DIST[S.ai.cls] || 3;
    const eff = V.dist / Sz;
    if (Math.abs(eff - pref) > 0.2) V.dist -= Math.sign(eff - pref) * 0.5 * dt * Sz;
    V.strafe += dt;
  }
  V.dist = clamp(V.dist, 0.3 * Sz, 12 * Sz);
  e.distance = V.dist / Sz;
  e.distMethod = `模擬（背景縮放 ${Sz.toFixed(2)}）`;
  // 平移：追蹤影像比例 → 螢幕像素
  const vw = video.videoWidth || W, vh = video.videoHeight || H;
  const sc = Math.max(W / vw, H / vh);
  const Tx = zoom.txNorm * vw * sc, Ty = zoom.tyNorm * vh * sc;
  const worldX = W / 2 + W * 0.25 * Math.sin(V.strafe / 1.6) + V.offX;
  let cx = W / 2 + Sz * (worldX - W / 2) + Tx;
  const cy = H * 0.45 + Ty;
  // 跑出畫面太久（例如手機轉開又轉回來時追蹤偏掉）：慢慢走回畫面中
  if (cx < -W * 0.1 || cx > W * 1.1) {
    if (!V.offscreenSince) V.offscreenSince = t;
    if (t - V.offscreenSince > 4000) V.offX -= (cx - W / 2) * Math.min(1, dt * 1.5) / Sz;
  } else V.offscreenSince = 0;
  const bh = clamp((H * 1.3) / e.distance, 40, H * 0.95), bw = bh / 2.2;
  e.box = { x0: cx - bw / 2, x1: cx + bw / 2, y0: cy - bh / 2, y1: cy + bh / 2 };
  // 完全在畫面外就算看不到（打不中）
  const inView = e.box.x1 > 0 && e.box.x0 < W && e.box.y1 > 0 && e.box.y0 < H;
  if (inView) { e.lastSeen = t; e.missFrames = 0; } else e.missFrames = 99;
}

// ---------------------------------------------------------------- 距離 / 射程
let distCalib = loadPref('distCalib', 1);
function rawDistance(person) {
  if (!S.estimateDistance || !video.videoWidth) return null;
  return S.estimateDistance(person.landmarks, video.videoWidth, video.videoHeight, STATS);
}
// 回傳 null（距離未知，不限制）、'ok'、'far'、'near'
function rangeState(skill) {
  const d = S.enemy.distance;
  if (!skill.range || !d) return null;
  if (d > skill.range[1]) return 'far';
  if (d < skill.range[0]) return 'near';
  return 'ok';
}

// ---------------------------------------------------------------- 持續傷害
function tickDots(dt) {
  for (const [who, list] of [['me', S.me.dots], ['enemy', S.enemy.dots]]) {
    for (const d of list) {
      d.acc += dt;
      if (d.acc < 1) continue;
      d.acc -= 1;
      if (who === 'me') {
        S.me.hp = Math.max(0, S.me.hp - d.dps);
        floater(W / 2 + 60, H * 0.5, `☠ -${d.dps}`, '#4ade80', 0.9);
        sendState();
        if (S.me.hp <= 0) { if (net) net.send({ t: 'ko' }); finish(false); }
      } else {
        if (S.enemy.box) floater((S.enemy.box.x0 + S.enemy.box.x1) / 2, S.enemy.box.y0 + 30, `☠ -${d.dps}`, '#4ade80', 0.9);
        hitEnemyLocal(d.dps);
      }
    }
    const t = now();
    const keep = list.filter((d) => d.until > t);
    list.length = 0; list.push(...keep);
  }
}

// ---------------------------------------------------------------- 陷阱
// 螢幕上的焦距（px）：影片焦距 × object-fit: cover 的縮放
function focalScreen() {
  const vw = video.videoWidth || W, vh = video.videoHeight || H;
  const f = Math.max(vw, vh) / 2 / Math.tan(((STATS.cameraFovDeg / 2) * Math.PI) / 180);
  return f * Math.max(W / vw, H / vh);
}

function placeTrap(s, at) {
  const t = now();
  const world = orient.toWorld(at.x, at.y, focalScreen(), W / 2, H / 2);
  const mine = S.traps.filter((x) => x.skill.id === s.id);
  if (mine.length >= s.trap.max) S.traps.splice(S.traps.indexOf(mine[0]), 1);
  S.traps.push({ id: S.nextId++, skill: s, world, x: at.x, y: at.y, pos: { ...at }, armAt: t + s.trap.armMs, until: t + s.trap.lifeMs });
  floater(at.x, at.y - 30, `${s.icon} 陷阱設置`, s.color);
  burst(at.x, at.y, s.color, 25, 0.6);
  sfx('trap');
  if (net) net.send({ t: 'trapSet', skill: s.id });
}

// 陷阱相對於對手的位置（公尺，對手視角：x 右、z 前＝朝向我）
// 用「對手腳的位置＋距離」反推地平線，再把陷阱在畫面上的位置換算成地面距離（假設手機離地約 1.3m）
const CAM_HEIGHT = 1.3;
function trapRelative(pos) {
  const e = S.enemy, b = e.box, f = focalScreen();
  const d = e.distance || 3;
  const feet = { x: (b.x0 + b.x1) / 2, y: b.y1 };
  const horizon = feet.y - (f * CAM_HEIGHT) / d;
  const dt = pos.y - horizon > 5 ? clamp((f * CAM_HEIGHT) / (pos.y - horizon), 0.3, d + 5) : d + 5;
  const lateral = ((pos.x - feet.x) * dt) / f;   // 我的畫面右方＝對手的左方
  return { x: +(-lateral).toFixed(2), z: +(d - dt).toFixed(2) };
}

let trapSyncAt = 0, trapSyncCount = 0;
function syncTraps(t) {
  if (!net || (t - trapSyncAt < 250 && S.traps.length === trapSyncCount)) return;
  trapSyncAt = t; trapSyncCount = S.traps.length;
  const seen = targetVisible();
  const list = S.traps.filter((tr) => tr.rel).map((tr) => ({ id: tr.id, s: tr.skill.id, x: tr.rel.x, z: tr.rel.z, armed: t >= tr.armAt }));
  net.send({ t: 'traps', list, seen });
}

function updateTraps(t) {
  const e = S.enemy, f = focalScreen();
  S.traps = S.traps.filter((tr) => tr.until > t);
  syncTraps(t);
  for (const tr of S.traps) {
    // 有陀螺儀：陷阱固定在世界方向上，手機轉動時跟著移動；沒有：固定在螢幕上
    tr.pos = tr.world ? orient.toScreen(tr.world, f, W / 2, H / 2) : { x: tr.x, y: tr.y };
    if (tr.pos && targetVisible()) tr.rel = trapRelative(tr.pos);
    if (!tr.pos || t < tr.armAt || !targetVisible()) continue;
    const b = e.box;
    const feet = { x: (b.x0 + b.x1) / 2, y: b.y1 };
    const r = tr.skill.radius * Math.min(W, H) + (b.x1 - b.x0) * 0.3;
    if (Math.hypot(feet.x - tr.pos.x, feet.y - tr.pos.y) > r) continue;
    // 觸發！
    tr.until = 0;
    const s = tr.skill;
    burst(tr.pos.x, tr.pos.y, s.color, 70, 1.5);
    floater(tr.pos.x, tr.pos.y - 30, `${s.icon} -${s.damage}`, '#ff4d6d', 1.4);
    if (s.effect) floater(tr.pos.x, tr.pos.y - 60, effectLabel(s.effect), s.color, 0.8);
    sfx('boom');
    M.trapHits++; M.hits++;
    if (S.ai) aiTakeHit(s.damage, s.effect, null); else hitEnemyLocal(s.damage, s.effect);
    if (net) net.send({ t: 'trap', skill: s.id, dmg: s.damage, eff: s.effect || null });
  }
}

// ---------------------------------------------------------------- 練習模式：電腦對手
// 每局隨機一個職業與 3 個技能，跟玩家用一樣的規則：MP、冷卻、蓄力時間、射程、防禦、陷阱。
// 閃避判定：沒有對手的鏡頭，所以改用手機加速度感測器——法術飛行期間玩家有明顯移動就算閃開；
// 沒有感測器（例如電腦）時依飛行時間給閃避機率。
const DUMMY_SKILL = { id: 'dummy', name: '木人拳', icon: '🪵', color: '#d08a40', glow: '#ffe0b0', fx: 'orb', travelMs: 1000 };
const DODGE_ACCEL = 3;            // m/s²：側移一步大約 3～6
const AI_THINK_MS = [2500, 5000]; // 出招間隔（越短越難）
const AI_DAMAGE = 0.8;             // 電腦傷害倍率（越大越難）
const rand = (a, b) => a + Math.random() * (b - a);
const shuffle = (arr) => arr.map((x) => [Math.random(), x]).sort((a, b) => a[0] - b[0]).map((x) => x[1]);

function setupAI() {
  const forced = new URLSearchParams(location.search).get('ai');   // 網址加 ?ai=archer 可指定電腦職業
  const C = CLASSES[forced] || CLASSES[shuffle(Object.keys(CLASSES))[0]];
  const atk = shuffle(C.skills.filter((id) => SKILLS[id].type !== 'self'));
  const rest = shuffle([...atk.slice(2), ...C.skills.filter((id) => SKILLS[id].type === 'self')]);
  const skills = [...atk.slice(0, 2), rest[0]].map((id) => SKILLS[id]);   // 至少 2 個攻擊技能
  S.ai = {
    cls: C.id, skills, mp: C.stats.maxMp, maxMp: C.stats.maxMp, regen: C.stats.mpRegen,
    cooldowns: {}, charging: null, nextThink: now() + 4500, buffs: {}, snaredUntil: 0, blindUntil: 0, reactedAt: 0,
  };
  const e = S.enemy;
  e.cls = C.id;
  e.name = `${C.icon} 電腦${C.name}`;
  e.maxHp = C.stats.maxHp;
  setTimeout(() => warn(`🤖 對手：${C.icon}${C.name}｜${skills.map((x) => x.icon + x.name).join('・')}`, 3500), 50);
}

function aiInRange(s) {
  const d = S.enemy.distance;
  return !s.range || !d || (d >= s.range[0] && d <= s.range[1]);
}

function aiUpdate(t, dt) {
  const ai = S.ai, e = S.enemy;
  ai.mp = Math.min(ai.maxMp, ai.mp + ai.regen * dt);
  updateAiTraps(t);
  if (ai.snaredUntil > t) return;

  // 蓄力中：時間到、而且距離在射程內才出手（刺客會等你走近、狙擊會等你拉遠）
  if (ai.charging) {
    const c = ai.charging, s = c.skill;
    if (t - c.since < s.chargeMs + c.delay) return;
    if (s.type === 'projectile' && !aiInRange(s)) {
      if (t - c.since > s.chargeMs + 8000) { ai.charging = null; warn(''); }
      return;
    }
    ai.charging = null;
    warn('');
    aiRelease(s, t);
    return;
  }

  const ready = ai.skills.filter((s) => (ai.cooldowns[s.id] || 0) <= t && ai.mp >= s.cost);
  const guards = ready.filter((s) => s.type === 'self' && s.self !== 'heal');
  // 看到玩家的攻擊飛來：有一半機率立刻開防禦
  const newShot = S.projectiles.find((p) => !p.reflected && p.start > ai.reactedAt);
  if (newShot) {
    ai.reactedAt = t;
    if (guards.length && Math.random() < 0.5) return aiCharge(guards[0], t, rand(0, 150));
  }
  if (t < ai.nextThink) return;
  ai.nextThink = t + rand(...AI_THINK_MS);
  let pick = null;
  if (e.hp < e.maxHp * 0.4) pick = ready.find((s) => s.self === 'heal');
  if (!pick) {
    const atk = ready.filter((s) => s.type !== 'self');
    const good = atk.filter(aiInRange);
    const list = good.length ? good : atk;
    pick = list[Math.floor(Math.random() * list.length)];
  }
  if (pick) aiCharge(pick, t, rand(200, 900));
}

function aiCharge(s, t, delay) {
  const ai = S.ai;
  ai.mp -= s.cost;
  ai.cooldowns[s.id] = t + s.cooldownMs;
  ai.charging = { skill: s, since: t, delay };
  warn(`⚠ ${S.enemy.name} 正在詠唱 ${s.icon}${s.name}`, s.chargeMs + delay + 1500);
}

function aiRelease(s, t) {
  const ai = S.ai, e = S.enemy, b = e.box;
  const at = b ? { x: (b.x0 + b.x1) / 2, y: (b.y0 + b.y1) / 2 } : { x: W / 2, y: H / 2 };
  if (s.type === 'self') {
    if (s.self === 'heal') {
      e.hp = Math.min(e.maxHp, e.hp + s.heal);
      floater(at.x, at.y - 40, `+${s.heal}`, s.color, 1.2);
      sfx('heal');
    } else {
      ai.buffs[s.self] = { until: t + s.buff.dur, ...s.buff, left: s.buff.amount };
      e.buffs[s.self] = t + s.buff.dur;
      floater(at.x, at.y - 40, `${s.icon} ${s.name}`, s.color, 1.1);
      sfx('shield');
    }
    burst(at.x, at.y, s.color, 40);
    return;
  }
  if (s.type === 'trap') {
    // 電腦的陷阱直接設在你腳下：在生效前移動身體就能避開
    const armAt = t + Math.max(s.trap.armMs, 2000);
    S.enemyTraps.push({ id: 'ai' + S.nextId++, s: s.id, x: rand(-0.25, 0.25), z: rand(-0.25, 0.25), armed: false, armAt, placedAt: t, ai: true });
    warn(`⚠ ${s.icon} 陷阱設在你腳下！${((armAt - t) / 1000).toFixed(0)} 秒內移動身體避開`, armAt - t);
    sfx('trap');
    return;
  }
  const n = s.multi ? s.multi.count : 1;
  const base = rand(0.35, 0.65);
  for (let k = 0; k < n; k++) {
    const ax = base + (s.multi ? (k - (n - 1) / 2) * s.multi.spread : 0);
    S.incoming.push({ id: 'ai' + S.nextId++, skill: s, ax, ay: 0.4, start: t, dur: s.travelMs, resolved: false, ai: true, dmg: Math.round(s.damage * AI_DAMAGE), eff: s.effect || null });
  }
  if (s.travelMs > 400) warn('⚠ 法術飛來了！移動身體閃避', s.travelMs);
  sfx('incoming');
}

// 電腦的攻擊飛到：判斷玩家有沒有閃開
function playerDodged(since, dur) {
  if (orient.motionOk) return orient.peakSince(since) >= DODGE_ACCEL;
  return Math.random() < clamp((dur - 250) / 2500, 0.05, 0.5);
}

function aiResolve(i) {
  i.resolved = true;
  const ai = S.ai;
  const accuracy = ai && ai.blindUntil > now() ? 0.4 : 0.9;   // 被煙霧彈致盲時很容易打偏
  if (playerDodged(i.start, i.dur) || Math.random() > accuracy) {
    floater(W / 2, H * 0.45, '閃避成功！', '#7dffb0', 1.4);
    sfx('dodge');
    M.dodged++;
    return;
  }
  receiveHit(i.dmg, i.skill.id, i.eff, i.id);
}

function updateAiTraps(t) {
  for (const tr of S.enemyTraps) {
    if (!tr.ai || tr.armed || t < tr.armAt) continue;
    const s = SKILLS[tr.s];
    tr.done = true;
    if (playerDodged(tr.placedAt, tr.armAt - tr.placedAt + 800)) {
      floater(W / 2, H * 0.5, '避開陷阱！', '#7dffb0', 1.3);
      sfx('dodge');
      M.dodged++;
    } else {
      banner('踩到陷阱！', 1200);
      receiveHit(Math.round(s.damage * AI_DAMAGE), s.id, s.effect || null, null);
    }
  }
  if (S.enemyTraps.some((x) => x.done)) S.enemyTraps = S.enemyTraps.filter((x) => !x.done);
}

// 玩家打中電腦：電腦也會格擋、護盾、反擊
function aiTakeHit(dmg, eff, p) {
  const ai = S.ai, e = S.enemy, t = now(), B = ai.buffs;
  const at = e.box ? { x: (e.box.x0 + e.box.x1) / 2, y: e.box.y0 + 20 } : { x: W / 2, y: H * 0.3 };
  if (B.counter && B.counter.until > t && p) {
    delete B.counter; e.buffs.counter = 0;
    const dur = Math.max(p.skill.travelMs || 600, 600);
    S.incoming.push({ id: 'ai' + S.nextId++, skill: p.skill, ax: clamp(p.to.x / W, 0.2, 0.8), ay: 0.4, start: t, dur, resolved: false, ai: true, dmg, eff, refl: true });
    floater(at.x, at.y, '↩️ 反擊！', '#f472b6', 1.3);
    warn('↩️ 你的攻擊被打回來了！快閃開', dur);
    sfx('shield');
    return;
  }
  let note = '';
  if (B.block && B.block.until > t && dmg > 0) { dmg = Math.round(dmg * (1 - B.block.reduce)); delete B.block; e.buffs.block = 0; note = '🛡️ 格擋'; }
  if (B.shield && B.shield.until > t && dmg > 0) {
    const absorb = Math.min(B.shield.left, dmg);
    B.shield.left -= absorb; dmg -= absorb;
    if (B.shield.left <= 0) { delete B.shield; e.buffs.shield = 0; }
    note = note ? note + '＋🧱' : '🧱 護盾吸收';
  }
  if (note) floater(at.x, at.y + 30, note, '#93c5fd', 1.1);
  if (eff && eff.kind === 'blind') ai.blindUntil = t + eff.dur;
  if (eff && eff.kind === 'snare') { ai.snaredUntil = t + eff.dur; ai.charging = null; warn(''); }
  hitEnemyLocal(dmg, eff);
}

// 掃描時校正：輸入對手實際距離，修正鏡頭視角等假設造成的誤差
$('btnCalib').onclick = () => {
  const c = S.candidate;
  const est = c && rawDistance(c);
  if (!est) { $('scanText').textContent = '需要先偵測到對手的肩膀'; return; }
  const ans = prompt(`估算距離 ${(est.d * distCalib).toFixed(1)} 公尺。\n請輸入對手實際距離（公尺）：`);
  const real = parseFloat(ans);
  if (!(real > 0.3 && real < 30)) return;
  distCalib = real / est.d;
  savePref('distCalib', distCalib);
  $('scanText').textContent = `已校正（係數 ${distCalib.toFixed(2)}）`;
};

function updateHand(t) {
  const h = vision.hand;
  if (!h) { S.hand = null; S.palmHist.length = 0; return; }
  const lm = h.landmarks.map((p) => v2s(p.x, p.y));
  const ids = [0, 5, 9, 13, 17];
  const palm = { x: ids.reduce((a, i) => a + lm[i].x, 0) / 5, y: ids.reduce((a, i) => a + lm[i].y, 0) / 5 };
  // 瞄準：方向＝手腕 → 中指根部（握拳、張開都穩定），
  // 起點＝指尖附近（中指根部再往前一個手掌長），準星＝起點沿方向延伸「技能射程」
  let dx = lm[9].x - lm[0].x, dy = lm[9].y - lm[0].y;
  const len = Math.hypot(dx, dy) || 1;
  dx /= len; dy /= len;
  const tip = { x: lm[9].x + dx * len * 0.9, y: lm[9].y + dy * len * 0.9 };
  const sk = S.charging && !S.charging.skill.self ? S.charging.skill : null;
  const reach = H * (sk ? sk.reach : 0.2);
  const raw = { x: clamp(tip.x + dx * reach, 10, W - 10), y: clamp(tip.y + dy * reach, 10, H - 10) };
  S.aim = S.aim ? { x: lerp(S.aim.x, raw.x, 0.35), y: lerp(S.aim.y, raw.y, 0.35) } : raw;
  S.hand = { lm, palm, tip, gesture: h.gesture };

  // 速度偵測（快速前揮）
  S.palmHist.push({ t, x: palm.x, y: palm.y });
  while (S.palmHist.length && t - S.palmHist[0].t > 150) S.palmHist.shift();

  if (h.gesture === 'Closed_Fist') S.lastFist = t;
  if (!S.charging) return;
  if (h.gesture === 'Open_Palm' && t - S.lastFist < 1500 && t - S.charging.since > 250) {
    S.lastFist = 0;
    release(S.aim, 'fist');
    return;
  }
  const h0 = S.palmHist[0];
  if (h0 && t - h0.t > 60) {
    const vy = (palm.y - h0.y) / ((t - h0.t) / 1000);
    if (vy < -H * 2.2 && t - S.charging.since > 250) { S.palmHist.length = 0; release(S.aim, 'flick'); }
  }
}

// 詠唱中點擊畫面＝往點擊處發射（測試/備援用）
canvas.addEventListener('pointerdown', (ev) => {
  if (S.charging) release({ x: ev.clientX, y: ev.clientY }, 'tap');
});

// ---------------------------------------------------------------- 繪圖
function render(t, dt) {
  ctx.save();
  ctx.clearRect(0, 0, W, H);
  if (S.shake > 0) {
    ctx.translate((Math.random() - 0.5) * S.shake, (Math.random() - 0.5) * S.shake);
    S.shake = Math.max(0, S.shake - dt * 60);
  }

  if (S.phase === 'scan' && S.candidate) drawBrackets(boxToScreen(S.candidate.box), '#ffd04a', t, true);
  for (const tr of S.traps) drawTrap(tr, t);
  if (S.enemy.locked) drawEnemy(t);
  if (S.hand) drawHand(t);

  for (const p of S.projectiles) drawProjectile(p, t);
  for (const i of S.incoming) drawIncoming(i, t);
  drawParticles(dt);
  drawFloaters(dt);
  ctx.restore();

  if (S.enemyTraps.length && S.phase === 'battle') drawRadar(t);
  if (t < S.blindUntil) drawSmoke(t);
  if (S.flash > 0) {
    ctx.fillStyle = `rgba(255,30,60,${S.flash * 0.45})`;
    ctx.fillRect(0, 0, W, H);
    S.flash = Math.max(0, S.flash - dt * 2.5);
  }
}

function drawBrackets(b, color, t, dashed) {
  const L = Math.min(30, (b.x1 - b.x0) / 3);
  ctx.strokeStyle = color; ctx.lineWidth = 4; ctx.shadowColor = color; ctx.shadowBlur = 12;
  ctx.beginPath();
  for (const [x, y, sx, sy] of [[b.x0, b.y0, 1, 1], [b.x1, b.y0, -1, 1], [b.x0, b.y1, 1, -1], [b.x1, b.y1, -1, -1]]) {
    ctx.moveTo(x + sx * L, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * L);
  }
  ctx.stroke();
  if (dashed) {
    ctx.lineWidth = 1; ctx.setLineDash([6, 6]); ctx.lineDashOffset = -t / 30;
    ctx.strokeRect(b.x0, b.y0, b.x1 - b.x0, b.y1 - b.y0);
    ctx.setLineDash([]);
  }
  ctx.shadowBlur = 0;
}

function drawEnemy(t) {
  const e = S.enemy;
  if (!targetVisible()) {
    ctx.fillStyle = 'rgba(255,77,109,.9)'; ctx.font = 'bold 18px system-ui'; ctx.textAlign = 'center';
    ctx.fillText('⚠ 目標離開畫面', W / 2, H * 0.22);
    return;
  }
  const b = e.box;
  const aimed = S.aim && S.charging && S.aim.x > b.x0 && S.aim.x < b.x1 && S.aim.y > b.y0 && S.aim.y < b.y1;
  drawBrackets(b, aimed ? '#ff2d55' : '#ff7a8a', t, false);
  if (e.virtual) drawDummy(b);
  // 對手的防禦狀態
  const tn = now();
  const shielded = ['block', 'shield', 'counter'].filter((k) => e.buffs[k] > tn);
  if (shielded.length) {
    const col = { block: '#60a5fa', shield: '#94a3b8', counter: '#f472b6' }[shielded[0]];
    ctx.save();
    ctx.strokeStyle = col; ctx.fillStyle = col; ctx.globalAlpha = 0.25 + 0.1 * Math.sin(t / 120);
    ctx.beginPath();
    ctx.ellipse((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2, (b.x1 - b.x0) * 0.75, (b.y1 - b.y0) * 0.6, 0, 0, Math.PI * 2);
    ctx.fill(); ctx.globalAlpha = 0.9; ctx.lineWidth = 3; ctx.stroke();
    ctx.restore();
    ctx.font = 'bold 13px system-ui'; ctx.textAlign = 'center'; ctx.fillStyle = col;
    ctx.fillText(shielded.map((k) => ({ block: '🛡️格擋', shield: '🧱鐵壁', counter: '↩️反擊' }[k])).join(' '), (b.x0 + b.x1) / 2, b.y1 + 18);
  }
  if (e.dots.length) { ctx.fillStyle = '#4ade80'; ctx.font = 'bold 13px system-ui'; ctx.fillText('☠ 中毒', (b.x0 + b.x1) / 2, b.y1 + 34); }
  // 頭頂血條
  const bw = Math.max(90, b.x1 - b.x0), bx = (b.x0 + b.x1) / 2 - bw / 2, by = Math.max(24, b.y0 - 22);
  ctx.fillStyle = 'rgba(0,0,0,.6)'; roundRect(bx - 2, by - 2, bw + 4, 14, 6); ctx.fill();
  ctx.fillStyle = '#ff2d55'; roundRect(bx, by, bw * (e.hp / e.maxHp), 10, 5); ctx.fill();
  ctx.fillStyle = '#fff'; ctx.font = 'bold 13px system-ui'; ctx.textAlign = 'center';
  const dist = e.distance ? `  ·  ${e.distance.toFixed(1)}m` : '';
  ctx.fillText(`${e.name}  ${Math.ceil(e.hp)}/${e.maxHp}${dist}`, (b.x0 + b.x1) / 2, by - 6);
}

function drawDummy(b) {
  const cx = (b.x0 + b.x1) / 2, w = b.x1 - b.x0, h = b.y1 - b.y0;
  ctx.fillStyle = 'rgba(190,140,80,.85)';
  ctx.beginPath(); ctx.arc(cx, b.y0 + w * 0.25, w * 0.22, 0, Math.PI * 2); ctx.fill();
  roundRect(cx - w * 0.3, b.y0 + w * 0.5, w * 0.6, h * 0.5, 10); ctx.fill();
  ctx.fillRect(cx - w * 0.5, b.y0 + w * 0.6, w, w * 0.15);
  ctx.fillRect(cx - w * 0.06, b.y0 + w * 0.5 + h * 0.45, w * 0.12, h * 0.3);
  ctx.strokeStyle = '#c33'; ctx.lineWidth = 3;
  ctx.beginPath(); ctx.arc(cx, b.y0 + w * 0.5 + h * 0.2, w * 0.15, 0, Math.PI * 2); ctx.stroke();
  if (S.enemy.cls) {
    ctx.font = `${Math.round(w * 0.3)}px system-ui`; ctx.textAlign = 'center';
    ctx.fillText(CLASSES[S.enemy.cls].icon, cx, b.y0 + w * 0.36);
  }
  // 電腦蓄力中：頭上顯示蓄力環
  const c = S.ai && S.ai.charging;
  if (c) {
    const prog = Math.min(1, (now() - c.since) / Math.max(1, c.skill.chargeMs));
    ctx.strokeStyle = c.skill.color; ctx.lineWidth = 4;
    ctx.beginPath(); ctx.arc(cx, b.y0 + w * 0.25, w * 0.32, -Math.PI / 2, -Math.PI / 2 + prog * Math.PI * 2); ctx.stroke();
  }
}

const BONES = [[0, 1], [1, 2], [2, 3], [3, 4], [0, 5], [5, 6], [6, 7], [7, 8], [5, 9], [9, 10], [10, 11], [11, 12],
  [9, 13], [13, 14], [14, 15], [15, 16], [13, 17], [0, 17], [17, 18], [18, 19], [19, 20]];

function drawHand(t) {
  const { lm, palm, gesture } = S.hand;
  const c = S.charging ? S.charging.skill.color : 'rgba(255,255,255,.6)';
  ctx.strokeStyle = c; ctx.lineWidth = 2; ctx.globalAlpha = 0.7;
  ctx.beginPath();
  for (const [a, b] of BONES) { ctx.moveTo(lm[a].x, lm[a].y); ctx.lineTo(lm[b].x, lm[b].y); }
  ctx.stroke();
  ctx.globalAlpha = 1;

  if (S.charging) {
    const s = S.charging.skill;
    const prog = Math.min(1, (t - S.charging.since) / Math.max(1, s.chargeMs));
    const r = 40 + 25 * prog + (gesture === 'Closed_Fist' ? 10 * Math.sin(t / 60) : 0);
    magicCircle(palm.x, palm.y, r, s.color, t);
    drawChargeRing(palm.x, palm.y, r + 14, prog, s, t);
    if (Math.random() < 0.6) spawn(palm.x + (Math.random() - 0.5) * r, palm.y + (Math.random() - 0.5) * r, s.color, 0.5);
    if (s.type === 'trap' && S.aim) {
      drawTrapMarker(S.aim.x, S.aim.y, s.radius * Math.min(W, H), s.color, t, 0.6);
    } else if (s.type === 'projectile' && S.aim) {
      // 射程線：指尖 → 準星
      const tip = S.hand.tip;
      ctx.save();
      ctx.strokeStyle = s.color; ctx.globalAlpha = 0.6; ctx.lineWidth = 2; ctx.setLineDash([4, 8]); ctx.lineDashOffset = -t / 20;
      ctx.beginPath(); ctx.moveTo(tip.x, tip.y); ctx.lineTo(S.aim.x, S.aim.y); ctx.stroke();
      ctx.restore();
      const rs = rangeState(s);
      const bad = rs === 'far' || rs === 'near';
      drawReticle(S.aim, bad ? '#888' : s.color, t);
      const d = S.enemy.distance;
      const label = `射程 ${rangeText(s)}` + (d ? (bad ? `　✗ ${rs === 'far' ? '太遠' : '太近'} ${d.toFixed(1)}m` : `　✓ ${d.toFixed(1)}m`) : '');
      ctx.font = 'bold 12px system-ui'; ctx.textAlign = 'center';
      const lw = ctx.measureText(label).width / 2 + 6, lx = clamp(S.aim.x, lw, W - lw);
      ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.7)'; ctx.strokeText(label, lx, S.aim.y - 30);
      ctx.fillStyle = bad ? '#bbb' : s.color; ctx.fillText(label, lx, S.aim.y - 30);
    }
  } else if (S.aim && S.phase === 'battle') {
    drawReticle(S.aim, 'rgba(255,255,255,.35)', t);
  }
  ctx.fillStyle = '#fff'; ctx.font = '12px system-ui'; ctx.textAlign = 'center';
  ctx.fillText(gestureLabel(gesture), palm.x, palm.y + 70);
}

// 蓄力環：滿了才能發射；刺客技能在射程外時提示靠近
function drawChargeRing(x, y, r, prog, s, t) {
  ctx.save();
  ctx.lineWidth = 6; ctx.strokeStyle = 'rgba(255,255,255,.15)';
  ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.stroke();
  ctx.strokeStyle = prog >= 1 ? '#fff' : s.color; ctx.shadowColor = s.color; ctx.shadowBlur = prog >= 1 ? 20 : 6;
  ctx.beginPath(); ctx.arc(x, y, r, -Math.PI / 2, -Math.PI / 2 + prog * Math.PI * 2); ctx.stroke();
  ctx.restore();
  ctx.font = 'bold 13px system-ui'; ctx.textAlign = 'center';
  let label = prog >= 1 ? '蓄力完成！' : `蓄力 ${Math.floor(prog * 100)}%`;
  let col = prog >= 1 ? '#fff' : s.color;
  if (prog >= 1 && s.releaseNear && rangeState(s) === 'far') { label = `靠近到 ${rangeText(s)} 才能出手`; col = '#fbbf24'; }
  const lw = ctx.measureText(label).width / 2 + 6, lx = clamp(x, lw, W - lw);
  ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.7)'; ctx.strokeText(label, lx, y - r - 10);
  ctx.fillStyle = col; ctx.fillText(label, lx, y - r - 10);
}

function gestureLabel(g) {
  return { Closed_Fist: '✊ 蓄力', Open_Palm: '🖐️ 張開', Pointing_Up: '☝️', Victory: '✌️', Thumb_Up: '👍', Thumb_Down: '👎', ILoveYou: '🤟' }[g] || '';
}

function magicCircle(x, y, r, color, t) {
  ctx.save();
  ctx.translate(x, y);
  ctx.strokeStyle = color; ctx.shadowColor = color; ctx.shadowBlur = 16; ctx.lineWidth = 2;
  ctx.rotate(t / 600);
  ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath(); ctx.arc(0, 0, r * 0.75, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath();
  for (let i = 0; i <= 5; i++) {
    const a = (i * 4 * Math.PI) / 5 - Math.PI / 2;
    const px = Math.cos(a) * r * 0.75, py = Math.sin(a) * r * 0.75;
    i ? ctx.lineTo(px, py) : ctx.moveTo(px, py);
  }
  ctx.stroke();
  ctx.rotate(-t / 300);
  for (let i = 0; i < 8; i++) {
    ctx.rotate(Math.PI / 4);
    ctx.fillStyle = color;
    ctx.fillRect(r * 0.9, -2, 8, 4);
  }
  ctx.restore();
}

function drawReticle(p, color, t) {
  const r = 18 + 3 * Math.sin(t / 120);
  ctx.strokeStyle = color; ctx.lineWidth = 2;
  ctx.beginPath(); ctx.arc(p.x, p.y, r, 0, Math.PI * 2); ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(p.x - r - 8, p.y); ctx.lineTo(p.x - r + 6, p.y);
  ctx.moveTo(p.x + r - 6, p.y); ctx.lineTo(p.x + r + 8, p.y);
  ctx.moveTo(p.x, p.y - r - 8); ctx.lineTo(p.x, p.y - r + 6);
  ctx.moveTo(p.x, p.y + r - 6); ctx.lineTo(p.x, p.y + r + 8);
  ctx.stroke();
}

// 依技能外觀畫出飛行物：ang＝飛行方向（弧度）
function drawShape(x, y, r, s, t, ang) {
  const fx = s.fx || 'orb';
  if (fx === 'orb') return orb(x, y, r, s, t);
  ctx.save();
  ctx.translate(x, y); ctx.rotate(ang);
  ctx.shadowColor = s.color; ctx.shadowBlur = 14;
  if (fx === 'arrow') {
    const L = Math.max(34, r * 3);
    ctx.strokeStyle = s.glow; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(-L, 0); ctx.lineTo(L * 0.6, 0); ctx.stroke();
    ctx.fillStyle = s.color;
    ctx.beginPath(); ctx.moveTo(L * 0.9, 0); ctx.lineTo(L * 0.5, -7); ctx.lineTo(L * 0.5, 7); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = s.color; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(-L, 0); ctx.lineTo(-L - 8, -7); ctx.moveTo(-L, 0); ctx.lineTo(-L - 8, 7); ctx.stroke();
  } else if (fx === 'dagger') {
    const L = Math.max(26, r * 2.2);
    ctx.fillStyle = s.glow;
    ctx.beginPath(); ctx.moveTo(L, 0); ctx.lineTo(0, -6); ctx.lineTo(-L * 0.3, 0); ctx.lineTo(0, 6); ctx.closePath(); ctx.fill();
    ctx.fillStyle = s.color; ctx.fillRect(-L * 0.6, -3, L * 0.3, 6); ctx.fillRect(-L * 0.32, -9, 4, 18);
  } else if (fx === 'slash') {
    const R = Math.max(30, r * 1.6);
    ctx.strokeStyle = s.glow; ctx.lineWidth = 7; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.arc(-R * 0.4, 0, R, -1.1, 1.1); ctx.stroke();
    ctx.strokeStyle = s.color; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(-R * 0.55, 0, R, -1.0, 1.0); ctx.stroke();
  } else if (fx === 'smoke') {
    ctx.fillStyle = s.color;
    for (let i = 0; i < 5; i++) {
      ctx.globalAlpha = 0.5;
      ctx.beginPath(); ctx.arc(Math.cos(i * 1.3 + t / 200) * r * 0.5, Math.sin(i * 1.7 + t / 260) * r * 0.5, r * 0.6, 0, Math.PI * 2); ctx.fill();
    }
  }
  ctx.restore();
}

function orb(x, y, r, s, t) {
  if (s.id === 'thunder') {
    ctx.strokeStyle = s.glow; ctx.lineWidth = 3; ctx.shadowColor = s.color; ctx.shadowBlur = 20;
    ctx.beginPath();
    for (let i = 0; i < 6; i++) {
      const a = Math.random() * Math.PI * 2;
      ctx.moveTo(x, y); ctx.lineTo(x + Math.cos(a) * r * 1.6, y + Math.sin(a) * r * 1.6);
    }
    ctx.stroke(); ctx.shadowBlur = 0;
  }
  const g = ctx.createRadialGradient(x, y, 0, x, y, r);
  g.addColorStop(0, '#fff'); g.addColorStop(0.3, s.glow); g.addColorStop(0.7, s.color); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill();
}

// 我方法術：從手飛向瞄準點，越遠越小（透視感）
function drawProjectile(p, t) {
  const k = clamp((t - p.start) / p.dur, 0, 1);
  const e = 1 - Math.pow(1 - k, 2);
  const x = lerp(p.from.x, p.to.x, e), y = lerp(p.from.y, p.to.y, e) - Math.sin(k * Math.PI) * 30;
  const r = lerp(55, Math.max(14, p.skill.radius * Math.min(W, H)), e);
  const ang = Math.atan2(p.to.y - p.from.y, p.to.x - p.from.x);
  drawShape(x, y, p.skill.fx === 'orb' ? r : r * 0.6, p.skill, t, ang);
  spawn(x, y, p.skill.color, 0.6);
  if (p.skill.id === 'thunder') {
    ctx.strokeStyle = p.skill.glow; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(p.from.x, p.from.y);
    for (let i = 1; i <= 6; i++) {
      const f = (i / 6) * e;
      ctx.lineTo(lerp(p.from.x, p.to.x, f) + (Math.random() - 0.5) * 30, lerp(p.from.y, p.to.y, f));
    }
    ctx.stroke();
  }
}

// 對手法術：從遠處朝鏡頭飛來，越來越大
function drawIncoming(i, t) {
  const k = clamp((t - i.start) / i.dur, 0, 1);
  const sx = i.ax * W, sy = H * 0.3;
  const ex = lerp(sx, W / 2, 0.4), ey = H * 0.5;
  const x = lerp(sx, ex, k), y = lerp(sy, ey, k);
  const r = lerp(10, Math.min(W, H) * 0.32, k * k);
  ctx.globalAlpha = i.resolved ? 0.3 : 1;
  // 朝鏡頭飛來：箭/刀從畫面上方斜斜飛向觀看者
  drawShape(x, y, i.skill.fx === 'orb' || i.skill.fx === 'smoke' ? r : r * 0.5, i.skill, t, Math.atan2(ey - sy, ex - sx));
  ctx.globalAlpha = 1;
  if (!i.resolved && k < 1) {
    ctx.strokeStyle = `rgba(255,60,80,${0.5 + 0.5 * Math.sin(t / 60)})`; ctx.lineWidth = 6;
    ctx.strokeRect(3, 3, W - 6, H - 6);
    ctx.fillStyle = '#fff'; ctx.font = 'bold 20px system-ui'; ctx.textAlign = 'center';
    ctx.fillText('快閃開！', W / 2, H * 0.2);
  }
  if (Math.random() < 0.7) spawn(x, y, i.skill.color, 0.4 + k);
}

function drawTrapMarker(x, y, r, color, t, alpha) {
  ctx.save();
  ctx.translate(x, y); ctx.scale(1, 0.4);   // 壓扁成地面上的橢圓
  ctx.globalAlpha = alpha;
  ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.setLineDash([8, 6]); ctx.lineDashOffset = -t / 30;
  ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.stroke();
  ctx.restore();
}

function drawTrap(tr, t) {
  if (!tr.pos) return;
  const s = tr.skill, r = s.radius * Math.min(W, H);
  const armed = t >= tr.armAt;
  const fade = Math.min(1, (tr.until - t) / 3000);
  drawTrapMarker(tr.pos.x, tr.pos.y, r, armed ? s.color : '#999', t, 0.4 + 0.5 * fade);
  ctx.globalAlpha = 0.5 + 0.5 * fade;
  ctx.font = '22px system-ui'; ctx.textAlign = 'center';
  ctx.fillText(s.icon, tr.pos.x, tr.pos.y + 8);
  ctx.font = 'bold 11px system-ui'; ctx.fillStyle = armed ? s.color : '#ccc';
  ctx.fillText(armed ? `${Math.ceil((tr.until - t) / 1000)}s` : '佈置中…', tr.pos.x, tr.pos.y + 26);
  ctx.globalAlpha = 1;
}

// 腳下雷達：中心是自己，上方是對手的方向；外圈 2.5 公尺
let radarBeepAt = 0;
function drawRadar(t) {
  const R = 58, cx = W - R - 12, cy = 70 + R, scale = R / 2.5;
  ctx.save();
  ctx.fillStyle = 'rgba(10,8,30,.75)';
  ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,.25)'; ctx.lineWidth = 1;
  for (const m of [1, 2]) { ctx.beginPath(); ctx.arc(cx, cy, m * scale, 0, Math.PI * 2); ctx.stroke(); }
  ctx.font = '10px system-ui'; ctx.textAlign = 'center'; ctx.fillStyle = 'rgba(255,255,255,.6)';
  ctx.fillText('▲ 對手方向', cx, cy - R + 12);
  ctx.fillStyle = '#7dd3fc';
  ctx.beginPath(); ctx.moveTo(cx, cy - 7); ctx.lineTo(cx - 5, cy + 5); ctx.lineTo(cx + 5, cy + 5); ctx.closePath(); ctx.fill();
  let nearest = null;
  for (const tr of S.enemyTraps) {
    const s = SKILLS[tr.s] || {};
    let px = tr.x * scale, py = -tr.z * scale;
    const len = Math.hypot(px, py);
    if (len > R - 8) { px *= (R - 8) / len; py *= (R - 8) / len; }
    const dist = Math.hypot(tr.x, tr.z);
    if (tr.armed && (!nearest || dist < nearest.dist)) nearest = { ...tr, dist };
    ctx.globalAlpha = tr.armed ? 1 : 0.5;
    ctx.fillStyle = tr.armed ? (s.color || '#f87171') : '#999';
    ctx.beginPath(); ctx.arc(cx + px, cy + py, 7, 0, Math.PI * 2); ctx.fill();
    ctx.font = '12px system-ui'; ctx.fillText(s.icon || '!', cx + px, cy + py - 9);
  }
  ctx.globalAlpha = 1;
  ctx.restore();
  if (nearest) {
    const dir = (nearest.x > 0.3 ? '右' : nearest.x < -0.3 ? '左' : '') + (nearest.z > 0.3 ? '前' : nearest.z < -0.3 ? '後' : '');
    const danger = nearest.dist < 0.8;
    ctx.font = 'bold 12px system-ui'; ctx.textAlign = 'center';
    ctx.fillStyle = danger ? `rgba(255,80,80,${0.6 + 0.4 * Math.sin(t / 80)})` : '#fbbf24';
    ctx.fillText(`${danger ? '⚠ ' : ''}陷阱 ${dir || '腳下'} ${nearest.dist.toFixed(1)}m`, cx, cy + R + 16);
    if (danger && t - radarBeepAt > 600) { radarBeepAt = t; sfx('tick'); }
  }
  if (!S.enemyTrapsSeen) {
    ctx.font = '10px system-ui'; ctx.fillStyle = '#aaa'; ctx.textAlign = 'center';
    ctx.fillText('（對手看不到你，位置可能過時）', cx - 20, cy + R + 30);
  }
}

function drawSmoke(t) {
  const left = (S.blindUntil - t) / 1000;
  ctx.save();
  ctx.fillStyle = `rgba(120,120,130,${Math.min(0.9, left)})`;
  ctx.fillRect(0, 0, W, H);
  ctx.globalAlpha = Math.min(0.6, left);
  ctx.fillStyle = '#d4d4d8';
  for (let i = 0; i < 9; i++) {
    const x = W * (0.5 + 0.45 * Math.sin(i * 2.1 + t / 900)), y = H * (0.5 + 0.4 * Math.cos(i * 1.7 + t / 1100));
    ctx.beginPath(); ctx.arc(x, y, Math.min(W, H) * 0.25, 0, Math.PI * 2); ctx.fill();
  }
  ctx.restore();
  ctx.fillStyle = '#fff'; ctx.font = 'bold 20px system-ui'; ctx.textAlign = 'center';
  ctx.fillText(`💨 煙霧中… ${left.toFixed(1)}s`, W / 2, H * 0.3);
}

function spawn(x, y, color, speed = 1) {
  const a = Math.random() * Math.PI * 2, v = (20 + Math.random() * 120) * speed;
  S.particles.push({ x, y, vx: Math.cos(a) * v, vy: Math.sin(a) * v, life: 0, max: 0.4 + Math.random() * 0.5, color, size: 2 + Math.random() * 4 });
}
function burst(x, y, color, n, speed = 1) { for (let i = 0; i < n; i++) spawn(x, y, color, speed * 3); }

function drawParticles(dt) {
  ctx.globalCompositeOperation = 'lighter';
  for (const p of S.particles) {
    p.life += dt; p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.96; p.vy *= 0.96;
    ctx.globalAlpha = Math.max(0, 1 - p.life / p.max);
    ctx.fillStyle = p.color;
    ctx.beginPath(); ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2); ctx.fill();
  }
  ctx.globalAlpha = 1; ctx.globalCompositeOperation = 'source-over';
  S.particles = S.particles.filter((p) => p.life < p.max);
  if (S.particles.length > 600) S.particles.splice(0, S.particles.length - 600);
}

function floater(x, y, text, color, scale = 1) { S.floaters.push({ x, y, text, color, scale, life: 0 }); }
function drawFloaters(dt) {
  ctx.textAlign = 'center';
  for (const f of S.floaters) {
    f.life += dt; f.y -= 40 * dt;
    ctx.globalAlpha = Math.max(0, 1 - f.life / 1.2);
    ctx.font = `900 ${Math.round(26 * f.scale)}px system-ui`;
    ctx.lineWidth = 4; ctx.strokeStyle = 'rgba(0,0,0,.7)'; ctx.strokeText(f.text, f.x, f.y);
    ctx.fillStyle = f.color; ctx.fillText(f.text, f.x, f.y);
  }
  ctx.globalAlpha = 1;
  S.floaters = S.floaters.filter((f) => f.life < 1.2);
}

function roundRect(x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath();
}

// ---------------------------------------------------------------- HUD
function updateHud(t) {
  const me = S.me, e = S.enemy;
  $('hpBar').style.width = `${(me.hp / me.maxHp) * 100}%`;
  $('mpBar').style.width = `${(me.mp / me.maxMp) * 100}%`;
  // 自身狀態：防禦、中毒、定身
  const st = [];
  for (const [k, ic] of [['block', '🛡️'], ['shield', '🧱'], ['counter', '↩️']]) {
    const b = me.buffs[k];
    if (b && b.until > t) st.push(`${ic}${Math.ceil((b.until - t) / 1000)}s${k === 'shield' ? `(${b.left})` : ''}`);
  }
  if (me.dots.length) st.push('☠中毒');
  if (me.snaredUntil > t) st.push(`🪤定身${((me.snaredUntil - t) / 1000).toFixed(1)}s`);
  if (S.traps.length) st.push(`陷阱×${S.traps.length}`);
  $('status').textContent = st.join('　');
  $('hpText').textContent = Math.ceil(me.hp);
  $('mpText').textContent = Math.floor(me.mp);
  $('enemyHpBar').style.width = `${(e.hp / e.maxHp) * 100}%`;
  $('enemyHpText').textContent = Math.ceil(e.hp);
  $('enemyName').textContent = e.name;
  for (const s of S.skills) {
    const el = S.slotEls[s.id];
    const cd = Math.max(0, (me.cooldowns[s.id] || 0) - t);
    el.querySelector('.cd').style.height = `${(cd / s.cooldownMs) * 100}%`;
    el.classList.toggle('charging', !!S.charging && S.charging.skill.id === s.id);
    el.classList.toggle('nomp', me.mp < s.cost);
  }
}

// 驗證用：結算畫面的統計
function statsHtml() {
  const f = M.fires;
  const rows = [
    ['詠唱（語音 / 點擊）', `${M.voiceChants} / ${M.tapChants}`],
    ['發射（握拳張開 / 揮手 / 點擊）', `${f.fist} / ${f.flick} / ${f.tap}`],
    ['詠唱逾時（手勢沒觸發）', M.timeouts],
    ['詠唱→發射 平均', `${avg(M.chantToFire)} ms`],
    ['命中率', `${pct(M.hits, M.misses)}（${M.hits} 中 / ${M.misses} 失，其中射程外 ${M.outOfRange}）`],
    ['閃避率', `${pct(M.dodged, M.hurt)}（${M.dodged} 閃 / ${M.hurt} 中）`],
    ['防禦成功（格擋/護盾/反擊）', M.mitigated],
    ['陷阱觸發', M.trapHits],
    ['目標遺失時間', M.frames ? `${Math.round((M.lostFrames / M.frames) * 100)}%` : '-'],
    ['平均 FPS', Math.round(M.fps)],
  ];
  return '<table>' + rows.map(([k, v]) => `<tr><td>${k}</td><td>${v}</td></tr>`).join('') + '</table>';
}

// 除錯面板（網址加 ?debug=1）
let debugAt = 0;
function updateDebug(t) {
  if (t - debugAt < 250) return;
  debugAt = t;
  const h = vision && vision.hand;
  const v = video.videoWidth ? `${video.videoWidth}x${video.videoHeight}` : '-';
  $('debug').textContent = [
    `FPS ${Math.round(M.fps)} | 辨識 ${vision ? Math.round(vision.lastMs || 0) : '-'}ms | ${v}`,
    `人數 ${vision ? vision.people.length : '-'} | 目標 ${targetVisible() ? '可見' : '遺失'} (miss ${S.enemy.missFrames})`,
    `距離 ${S.enemy.distance ? S.enemy.distance.toFixed(2) + 'm' : '-'} (${S.enemy.distMethod || '-'}) 校正 ${distCalib.toFixed(2)}`,
    S.enemy.virtual ? `背景追蹤 縮放 ${zoom.scale.toFixed(3)} 平移 ${zoom.txNorm.toFixed(2)},${zoom.tyNorm.toFixed(2)} 誤差 ${zoom.quality.toFixed(1)}` : '',
    `手 ${h ? `${h.gesture} ${(h.score * 100) | 0}%` : '無'} | 動作感測 ${orient.motionOk ? `有 ${orient.peakSince(now() - 500).toFixed(1)}m/s²` : '無（閃避用機率）'}`,
    `語音延遲 ${avg(M.voiceDelay)}ms（本機：說完→觸發；線上：首字→觸發）`,
    ...M.voiceLog,
  ].join('\n');
}

function hint(t) { $('hint').textContent = t; }

let bannerTimer = 0;
function banner(text, ms) {
  const b = $('banner');
  b.textContent = text; b.classList.add('show');
  clearTimeout(bannerTimer);
  bannerTimer = setTimeout(() => b.classList.remove('show'), ms);
}
function toast(text, color) { floater(W / 2, H * 0.32, text, color || '#fff', 0.8); }

let warnTimer = 0;
function warn(text, ms = STATS.chargeTimeoutMs) {
  const w = $('warn');
  clearTimeout(warnTimer);
  if (!text) { w.classList.remove('show'); return; }
  w.textContent = text; w.classList.add('show');
  warnTimer = setTimeout(() => w.classList.remove('show'), ms);
}

// ---------------------------------------------------------------- 音效（WebAudio 合成）
function initAudio() {
  if (audio) { audio.resume(); return; }
  try { audio = new (window.AudioContext || window.webkitAudioContext)(); audio.resume(); } catch (_) { audio = null; }
}
function tone(freq, dur, type = 'sine', vol = 0.15, slide = 0, delay = 0) {
  if (!audio) return;
  const t0 = audio.currentTime + delay;
  const o = audio.createOscillator(), g = audio.createGain();
  o.type = type; o.frequency.setValueAtTime(freq, t0);
  if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(20, freq + slide), t0 + dur);
  g.gain.setValueAtTime(vol, t0); g.gain.exponentialRampToValueAtTime(0.001, t0 + dur);
  o.connect(g).connect(audio.destination);
  o.start(t0); o.stop(t0 + dur);
}
function sfx(name) {
  switch (name) {
    case 'chant': tone(440, 0.15, 'triangle'); tone(660, 0.2, 'triangle', 0.12, 0, 0.1); break;
    case 'cast': tone(300, 0.35, 'sawtooth', 0.12, 600); break;
    case 'hit': tone(120, 0.4, 'square', 0.2, -80); tone(880, 0.1, 'triangle', 0.15); break;
    case 'miss': tone(500, 0.2, 'sine', 0.08, -300); break;
    case 'hurt': tone(90, 0.5, 'sawtooth', 0.25, -50); break;
    case 'dodge': tone(700, 0.12, 'triangle'); tone(1050, 0.15, 'triangle', 0.12, 0, 0.1); break;
    case 'incoming': tone(1200, 0.3, 'square', 0.06, -700); break;
    case 'heal': [523, 659, 784].forEach((f, i) => tone(f, 0.25, 'sine', 0.12, 0, i * 0.08)); break;
    case 'lock': tone(880, 0.1, 'square', 0.08); tone(1320, 0.15, 'square', 0.08, 0, 0.1); break;
    case 'tick': tone(660, 0.12, 'square', 0.08); break;
    case 'fight': tone(330, 0.4, 'sawtooth', 0.15, 330); break;
    case 'fail': tone(200, 0.2, 'square', 0.08); break;
    case 'win': [523, 659, 784, 1047].forEach((f, i) => tone(f, 0.3, 'triangle', 0.15, 0, i * 0.12)); break;
    case 'arrow': tone(900, 0.08, 'triangle', 0.12, -500); tone(180, 0.15, 'sine', 0.1, -60); break;
    case 'slash': tone(1600, 0.12, 'sawtooth', 0.06, -1300); break;
    case 'shield': tone(520, 0.25, 'square', 0.08, 120); tone(780, 0.3, 'triangle', 0.08, 0, 0.05); break;
    case 'trap': tone(300, 0.08, 'square', 0.1); tone(200, 0.1, 'square', 0.1, 0, 0.08); break;
    case 'boom': tone(80, 0.6, 'sawtooth', 0.3, -40); tone(400, 0.2, 'square', 0.15, -300); break;
    case 'lose': [400, 300, 200].forEach((f, i) => tone(f, 0.4, 'sawtooth', 0.12, 0, i * 0.2)); break;
  }
}

// 咒術對決 主遊戲邏輯
import { SKILLS, DEFAULT_LOADOUT, MAX_EQUIP, STATS, rangeText } from './skills.js';
import { VoiceCaster } from './voice.js';
import { LocalSpotter, classify, finalizeSkill, loadTemplates, saveTemplates, hasTemplates } from './voice-local.js';
import { Net } from './net.js';

const $ = (id) => document.getElementById(id);
const now = () => performance.now();
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerp = (a, b, t) => a + (b - a) * t;

// ---------------------------------------------------------------- 主選單
let loadout = loadPref('loadout', DEFAULT_LOADOUT).filter((id) => SKILLS[id]).slice(0, MAX_EQUIP);
$('nameInput').value = loadPref('name', '');

function loadPref(k, d) { try { const v = localStorage.getItem('sb_' + k); return v ? JSON.parse(v) : d; } catch (_) { return d; } }
function savePref(k, v) { try { localStorage.setItem('sb_' + k, JSON.stringify(v)); } catch (_) {} }

function renderPicker() {
  const box = $('skillPicker');
  box.innerHTML = '';
  for (const s of Object.values(SKILLS)) {
    const b = document.createElement('button');
    b.className = 'skill-card' + (loadout.includes(s.id) ? ' on' : '');
    b.style.setProperty('--c', s.color);
    const eff = s.self ? `回復 ${s.heal}` : `傷害 ${s.damage} · 射程 ${rangeText(s)}`;
    b.innerHTML = `<div class="t">${s.icon} ${s.name}</div><div class="s">MP ${s.cost} · ${eff}<br>${s.desc}</div>`;
    b.onclick = () => {
      if (loadout.includes(s.id)) loadout = loadout.filter((x) => x !== s.id);
      else if (loadout.length < MAX_EQUIP) loadout.push(s.id);
      else { loadout.shift(); loadout.push(s.id); }
      savePref('loadout', loadout);
      renderPicker();
    };
    box.appendChild(b);
  }
  $('equipCount').textContent = `${loadout.length}/${MAX_EQUIP}`;
}
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

const S = {
  mode: 'practice',          // practice | online
  phase: 'loading',          // loading | scan | waiting | countdown | battle | over
  me: { name: '', hp: STATS.maxHp, mp: STATS.maxMp, cooldowns: {} },
  skills: [],
  enemy: { name: '對手', hp: STATS.maxHp, maxHp: STATS.maxHp, locked: false, virtual: false,
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

// ---------------------------------------------------------------- 驗證用統計
const DEBUG = new URLSearchParams(location.search).has('debug');
const M = {};
function resetMetrics() {
  Object.assign(M, {
    voiceChants: 0, tapChants: 0, timeouts: 0,
    fires: { fist: 0, flick: 0, tap: 0 },
    hits: 0, misses: 0, dodged: 0, hurt: 0, outOfRange: 0,
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
  S.me.name = $('nameInput').value.trim() || '魔法師';
  S.skills = loadout.map((id) => SKILLS[id]);
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
      onOpen: () => net.send({ t: 'hello', name: S.me.name, loadout }),
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
    if (S.mode === 'practice') e.name = '訓練木人';
  }
  e.locked = true;
  e.distance = null;
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
  S.me.hp = STATS.maxHp; S.me.mp = STATS.maxMp; S.me.cooldowns = {};
  S.enemy.hp = S.enemy.maxHp = STATS.maxHp;
  S.charging = null; S.projectiles = []; S.incoming = [];
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
  S.charging = { skill, since: t };
  if (via === 'voice') M.voiceChants++; else M.tapChants++;
  sfx('chant');
  hint(skill.self ? `${skill.icon} ${skill.name}：握拳後張開手掌發動` : `${skill.icon} ${skill.name}：手指對準敵人，握拳→張開手掌發射`);
  if (net) net.send({ t: 'charge', skill: skill.id });
  return true;
}

function release(aim, via) {
  const c = S.charging;
  if (!c || S.phase !== 'battle') return;
  const s = c.skill;
  if (S.me.mp < s.cost) { S.charging = null; toast('MP 不足', '#4da3ff'); return; }
  M.fires[via]++;
  M.chantToFire.push(now() - c.since);
  S.me.mp -= s.cost;
  S.me.cooldowns[s.id] = now() + s.cooldownMs;
  S.charging = null;
  hint('');

  if (s.self) {
    S.me.hp = Math.min(STATS.maxHp, S.me.hp + s.heal);
    burst(W / 2, H * 0.8, s.color, 50);
    floater(W / 2, H * 0.7, `+${s.heal}`, s.color);
    sfx('heal');
    if (net) { net.send({ t: 'cast', skill: s.id }); sendState(); }
    return;
  }

  const from = S.hand ? { x: S.hand.tip.x, y: S.hand.tip.y } : { x: W / 2, y: H * 0.9 };
  const to = aim || S.aim || { x: W / 2, y: H * 0.4 };
  const id = S.nextId++;
  S.projectiles.push({ id, skill: s, from, to: { ...to }, start: now(), dur: s.travelMs });
  sfx('cast');
  if (net) net.send({ t: 'cast', id, skill: s.id, ax: to.x / W, ay: to.y / H, dur: s.travelMs });
}

function impact(p) {
  const s = p.skill, e = S.enemy;
  const r = s.radius * Math.min(W, H);
  const b = targetVisible() ? e.box : null;
  const onTarget = !!b && p.to.x > b.x0 - r && p.to.x < b.x1 + r && p.to.y > b.y0 - r && p.to.y < b.y1 + r;
  const range = rangeState(s);
  const hit = onTarget && range !== 'far' && range !== 'near';
  burst(p.to.x, p.to.y, s.color, hit ? 60 : 20, hit ? 1.4 : 0.7);
  if (hit) {
    floater(p.to.x, p.to.y - 30, `-${s.damage}`, '#ff4d6d', 1.4);
    sfx('hit');
    M.hits++;
    e.hp = Math.max(0, e.hp - s.damage);   // 先行預測，連線時以對手回報為準
    if (S.mode === 'practice' && e.hp <= 0) finish(true);
  } else {
    const why = onTarget && range === 'far' ? '射程外' : onTarget && range === 'near' ? '太近' : 'MISS';
    floater(p.to.x, p.to.y - 30, why, '#ccc');
    M.misses++;
    if (why !== 'MISS') M.outOfRange++;
    sfx('miss');
  }
  if (net) net.send({ t: 'result', id: p.id, hit, dmg: hit ? s.damage : 0 });
}

function takeDamage(dmg, skillId) {
  S.me.hp = Math.max(0, S.me.hp - dmg);
  M.hurt++;
  S.flash = 1; S.shake = 18;
  const s = SKILLS[skillId];
  burst(W / 2, H / 2, s ? s.color : '#ff4d6d', 70, 1.6);
  floater(W / 2, H * 0.45, `-${dmg}`, '#ff4d6d', 1.8);
  sfx('hurt');
  if (navigator.vibrate) navigator.vibrate(220);
  sendState();
  if (S.me.hp <= 0) { if (net) net.send({ t: 'ko' }); finish(false); }
}

function sendState() { if (net) net.send({ t: 'state', hp: S.me.hp, mp: Math.round(S.me.mp) }); }

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
      if (s.self) { floater(W / 2, H * 0.25, `${e.name} 治癒 +${s.heal}`, s.color); break; }
      // 對手瞄準我的位置，左右在我的視角是鏡像
      S.incoming.push({ id: m.id, skill: s, ax: 1 - m.ax, ay: m.ay, start: now(), dur: m.dur, resolved: false });
      sfx('incoming');
      break;
    }
    case 'result': {
      const inc = S.incoming.find((i) => i.id === m.id);
      if (inc) inc.resolved = true;
      if (m.hit) takeDamage(m.dmg, inc && inc.skill.id);
      else { floater(W / 2, H * 0.45, '閃避成功！', '#7dffb0', 1.4); sfx('dodge'); M.dodged++; }
      break;
    }
    case 'state':
      e.hp = m.hp;
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
  const dt = Math.min(0.05, rawDt);
  lastT = t;

  if (vision) {
    try { vision.update(video); } catch (err) { console.warn(err); }
    updateTarget(t);
    updateHand(t);
  } else if (S.enemy.virtual) {
    updateTarget(t);
  }

  if (S.phase === 'battle') {
    S.me.mp = Math.min(STATS.maxMp, S.me.mp + STATS.mpRegenPerSec * dt);
    if (S.charging && t - S.charging.since > STATS.chargeTimeoutMs) {
      S.charging = null; hint('詠唱逾時'); sfx('fail'); M.timeouts++;
    }
  }

  for (const p of S.projectiles) if (!p.done && t - p.start >= p.dur) { p.done = true; impact(p); }
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
    // 木人前後移動 1～6 公尺，畫面大小跟著變
    e.distance = 3.5 + 2.5 * Math.sin(t / 2600);
    e.distMethod = '模擬';
    const cx = W * (0.5 + 0.3 * Math.sin(t / 1300)), cy = H * 0.45;
    const bh = clamp(H * 1.3 / e.distance, 60, H * 0.9), bw = bh / 2.2;
    e.box = { x0: cx - bw / 2, x1: cx + bw / 2, y0: cy - bh / 2, y1: cy + bh / 2 };
    e.lastSeen = t;
    e.missFrames = 0;
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
  if (S.enemy.locked) drawEnemy(t);
  if (S.hand) drawHand(t);

  for (const p of S.projectiles) drawProjectile(p, t);
  for (const i of S.incoming) drawIncoming(i, t);
  drawParticles(dt);
  drawFloaters(dt);
  ctx.restore();

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
    const prog = Math.min(1, (t - S.charging.since) / 600);
    const r = 40 + 25 * prog + (gesture === 'Closed_Fist' ? 10 * Math.sin(t / 60) : 0);
    magicCircle(palm.x, palm.y, r, s.color, t);
    if (Math.random() < 0.6) spawn(palm.x + (Math.random() - 0.5) * r, palm.y + (Math.random() - 0.5) * r, s.color, 0.5);
    if (!s.self && S.aim) {
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
      ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.7)'; ctx.strokeText(label, S.aim.x, S.aim.y - 30);
      ctx.fillStyle = bad ? '#bbb' : s.color; ctx.fillText(label, S.aim.x, S.aim.y - 30);
    }
  } else if (S.aim && S.phase === 'battle') {
    drawReticle(S.aim, 'rgba(255,255,255,.35)', t);
  }
  ctx.fillStyle = '#fff'; ctx.font = '12px system-ui'; ctx.textAlign = 'center';
  ctx.fillText(gestureLabel(gesture), palm.x, palm.y + 70);
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
  orb(x, y, r, p.skill, t);
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
  orb(x, y, r, i.skill, t);
  ctx.globalAlpha = 1;
  if (!i.resolved && k < 1) {
    ctx.strokeStyle = `rgba(255,60,80,${0.5 + 0.5 * Math.sin(t / 60)})`; ctx.lineWidth = 6;
    ctx.strokeRect(3, 3, W - 6, H - 6);
    ctx.fillStyle = '#fff'; ctx.font = 'bold 20px system-ui'; ctx.textAlign = 'center';
    ctx.fillText('快閃開！', W / 2, H * 0.2);
  }
  if (Math.random() < 0.7) spawn(x, y, i.skill.color, 0.4 + k);
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
  $('hpBar').style.width = `${(me.hp / STATS.maxHp) * 100}%`;
  $('mpBar').style.width = `${(me.mp / STATS.maxMp) * 100}%`;
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
    `手 ${h ? `${h.gesture} ${(h.score * 100) | 0}%` : '無'}`,
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
function warn(text) {
  const w = $('warn');
  clearTimeout(warnTimer);
  if (!text) { w.classList.remove('show'); return; }
  w.textContent = text; w.classList.add('show');
  warnTimer = setTimeout(() => w.classList.remove('show'), STATS.chargeTimeoutMs);
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
    case 'lose': [400, 300, 200].forEach((f, i) => tone(f, 0.4, 'sawtooth', 0.12, 0, i * 0.2)); break;
  }
}

// 語音唸咒（線上）：Web Speech API 辨識，比對已裝備技能的關鍵字。
// 注意：Chrome 會把聲音送到 Google 伺服器辨識，iOS 需開啟「聽寫」功能；失敗時改用本機咒語辨識 (voice-local.js)。

// 錯誤碼 → 中文說明；fatal 表示重試也沒用，應改用本機辨識
export const SPEECH_ERRORS = {
  'not-allowed': { fatal: true, msg: '麥克風權限被拒絕（請到瀏覽器網站設定允許麥克風）' },
  'service-not-allowed': { fatal: true, msg: '瀏覽器不允許語音辨識服務（iPhone 需在 設定→一般→鍵盤 開啟「聽寫」）' },
  'audio-capture': { fatal: true, msg: '抓不到麥克風（可能被其他 App 佔用）' },
  network: { fatal: true, msg: '連不到 Google 語音伺服器（網路受限或被阻擋）' },
  'language-not-supported': { fatal: true, msg: '不支援中文語音辨識' },
  'restart-loop': { fatal: true, msg: '語音辨識一直中斷，無法持續聆聽' },
  'no-start': { fatal: true, msg: '語音辨識服務沒有回應' },
  'no-speech': { fatal: false },
  aborted: { fatal: false },
};

const IS_ANDROID = /Android/i.test(navigator.userAgent);

export class VoiceCaster {
  constructor({ skills, onSkill, onTranscript, onStatus, onFatal }) {
    this.skills = skills;            // 已裝備技能物件陣列
    this.onSkill = onSkill;
    this.onTranscript = onTranscript || (() => {});
    this.onStatus = onStatus || (() => {});
    this.onFatal = onFatal || (() => {});
    this.running = false;
    this.lastFire = new Map();       // 去抖動：同一句話的 interim 結果不要重複觸發
    this.firstSeen = new Map();      // 除錯用：每句話第一次出現文字的時間
    this.starts = [];                // 偵測「一啟動就結束」的無限重啟
    this.session = 0;
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    this.supported = !!SR;
    if (!SR) return;
    const rec = new SR();
    rec.lang = 'zh-TW';
    // Android Chrome 的連續模式很不穩定，改成「一句一句辨識、結束就重啟」
    rec.continuous = !IS_ANDROID;
    rec.interimResults = true;
    rec.maxAlternatives = 3;
    rec.onresult = (e) => this._handle(e);
    rec.onerror = (e) => {
      const info = SPEECH_ERRORS[e.error] || { fatal: false };
      if (info.fatal) this._fatal(e.error);
    };
    rec.onend = () => {
      if (!this.running) return;
      const t = performance.now();
      this.starts = this.starts.filter((x) => t - x < 10000);
      if (this.starts.length > 12) { this._fatal('restart-loop'); return; }
      setTimeout(() => { if (this.running) { try { rec.start(); } catch (_) {} } }, 100);
    };
    rec.onstart = () => {
      this.starts.push(performance.now());
      this.session++;
      this.firstSeen.clear();
      this.onStatus('🎙️ 線上語音辨識：聆聽中');
    };
    this.rec = rec;
  }

  _fatal(code) {
    if (!this.running) return;
    this.stop();
    const info = SPEECH_ERRORS[code] || {};
    this.onFatal(code, info.msg || code);
  }

  start() {
    if (!this.supported || this.running) return;
    this.running = true;
    try { this.rec.start(); } catch (_) {}
    // 有些瀏覽器不報錯也不啟動，5 秒沒反應就視為失敗
    setTimeout(() => { if (this.running && !this.starts.length) this._fatal('no-start'); }, 5000);
  }

  stop() {
    this.running = false;
    try { this.rec && this.rec.stop(); } catch (_) {}
  }

  _handle(e) {
    for (let i = e.resultIndex; i < e.results.length; i++) {
      const res = e.results[i];
      const texts = [];
      for (let a = 0; a < res.length; a++) texts.push(res[a].transcript);
      const text = texts.join(' ').toLowerCase().replace(/\s+/g, '');
      const t = performance.now();
      if (!this.firstSeen.has(i)) this.firstSeen.set(i, t);
      const skill = this.match(text);
      this.onTranscript(res[0].transcript, res.isFinal, skill);
      if (res.isFinal) this.firstSeen.delete(i);
      if (!skill) continue;
      // 同一個 result（含 interim 更新）只觸發一次
      const key = `${this.session}:${i}:${skill.id}`;
      const now = performance.now();
      if (this.lastFire.has(key) && now - this.lastFire.get(key) < 4000) continue;
      this.lastFire.set(key, now);
      this.onSkill(skill, { sinceFirstTextMs: t - (this.firstSeen.get(i) ?? t) });
    }
  }

  match(text) {
    let best = null, bestPos = -1;
    for (const s of this.skills) {
      for (const k of s.keywords) {
        const p = text.lastIndexOf(k.toLowerCase());
        if (p > bestPos) { bestPos = p; best = s; }   // 取最後唸到的那招
      }
    }
    return best;
  }
}

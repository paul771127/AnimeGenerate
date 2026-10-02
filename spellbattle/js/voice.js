// 語音唸咒：Web Speech API 連續辨識，比對已裝備技能的關鍵字。
export class VoiceCaster {
  constructor({ skills, onSkill, onTranscript, onStatus }) {
    this.skills = skills;            // 已裝備技能物件陣列
    this.onSkill = onSkill;
    this.onTranscript = onTranscript || (() => {});
    this.onStatus = onStatus || (() => {});
    this.running = false;
    this.lastFire = new Map();       // 去抖動：同一句話的 interim 結果不要重複觸發
    this.firstSeen = new Map();      // 除錯用：每句話第一次出現文字的時間
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    this.supported = !!SR;
    if (!SR) return;
    const rec = new SR();
    rec.lang = 'zh-TW';
    rec.continuous = true;
    rec.interimResults = true;
    rec.maxAlternatives = 3;
    rec.onresult = (e) => this._handle(e);
    rec.onerror = (e) => {
      this.onStatus(`語音錯誤：${e.error}`);
      if (e.error === 'not-allowed' || e.error === 'service-not-allowed') this.running = false;
    };
    rec.onend = () => {
      // 瀏覽器會自動停止辨識，持續重啟以保持監聽
      if (this.running) setTimeout(() => { try { rec.start(); } catch (_) {} }, 150);
    };
    rec.onstart = () => { this.firstSeen.clear(); this.onStatus('🎙️ 聆聽中…'); };
    this.rec = rec;
  }

  start() {
    if (!this.supported || this.running) return;
    this.running = true;
    try { this.rec.start(); } catch (_) {}
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
      const key = `${i}:${skill.id}`;
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

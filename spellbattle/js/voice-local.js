// 本機咒語辨識（離線、不需 Google 語音服務）
// 做法：玩家先把每個技能的咒語各唸 2 次當樣本 → 對戰時以音量偵測切出每一句話，
// 計算 MFCC 特徵，用 DTW 與樣本比對，最像且夠像的技能即觸發。
// 因為比對的是「自己的聲音」，任何語言、任何咒語都可以。

const STORE_KEY = 'sb_kws_v1';
const FRAME_MS = 25, HOP_MS = 10;
const N_MEL = 26, N_CEP = 12;
const HANGOVER = 18;          // 安靜幾個 hop (×10ms) 算一句話結束
const MIN_FRAMES = 15, MAX_FRAMES = 250, PREROLL = 12;

// ------------------------------------------------------------ 樣本儲存
export function loadTemplates() {
  try { return JSON.parse(localStorage.getItem(STORE_KEY)) || {}; } catch (_) { return {}; }
}
export function saveTemplates(t) {
  try { localStorage.setItem(STORE_KEY, JSON.stringify(t)); } catch (_) {}
}
export function hasTemplates(skillIds) {
  const t = loadTemplates();
  return skillIds.every((id) => t[id] && t[id].templates && t[id].templates.length >= 2);
}

// ------------------------------------------------------------ 訊號處理
function fft(re, im) {
  const n = re.length;
  for (let i = 1, j = 0; i < n; i++) {
    let bit = n >> 1;
    for (; j & bit; bit >>= 1) j ^= bit;
    j ^= bit;
    if (i < j) { [re[i], re[j]] = [re[j], re[i]]; [im[i], im[j]] = [im[j], im[i]]; }
  }
  for (let len = 2; len <= n; len <<= 1) {
    const ang = (-2 * Math.PI) / len, wr = Math.cos(ang), wi = Math.sin(ang);
    for (let i = 0; i < n; i += len) {
      let cr = 1, ci = 0;
      for (let k = 0; k < len / 2; k++) {
        const a = i + k, b = a + len / 2;
        const tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
        re[b] = re[a] - tr; im[b] = im[a] - ti;
        re[a] += tr; im[a] += ti;
        const nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
      }
    }
  }
}

class Mfcc {
  constructor(sr) {
    this.sr = sr;
    this.frameLen = Math.round((sr * FRAME_MS) / 1000);
    this.hop = Math.round((sr * HOP_MS) / 1000);
    this.nfft = 1 << Math.ceil(Math.log2(this.frameLen));
    this.win = Float32Array.from({ length: this.frameLen }, (_, i) => 0.54 - 0.46 * Math.cos((2 * Math.PI * i) / (this.frameLen - 1)));
    const mel = (f) => 2595 * Math.log10(1 + f / 700), inv = (m) => 700 * (10 ** (m / 2595) - 1);
    const lo = mel(100), hi = mel(Math.min(7000, sr / 2));
    const pts = Array.from({ length: N_MEL + 2 }, (_, i) => Math.floor(((this.nfft + 1) * inv(lo + ((hi - lo) * i) / (N_MEL + 1))) / sr));
    this.bank = [];
    for (let m = 1; m <= N_MEL; m++) {
      const f = new Float32Array(this.nfft / 2 + 1);
      for (let k = pts[m - 1]; k < pts[m]; k++) f[k] = (k - pts[m - 1]) / Math.max(1, pts[m] - pts[m - 1]);
      for (let k = pts[m]; k < pts[m + 1]; k++) f[k] = (pts[m + 1] - k) / Math.max(1, pts[m + 1] - pts[m]);
      this.bank.push(f);
    }
    this.re = new Float32Array(this.nfft); this.im = new Float32Array(this.nfft);
  }

  // 回傳 { cep: Float32Array(12), rms }
  frame(buf, off) {
    const { re, im, frameLen, win } = this;
    re.fill(0); im.fill(0);
    let e = 0, prev = off > 0 ? buf[off - 1] : 0;
    for (let i = 0; i < frameLen; i++) {
      const x = buf[off + i];
      e += x * x;
      re[i] = (x - 0.97 * prev) * win[i];
      prev = x;
    }
    fft(re, im);
    const half = this.nfft / 2 + 1;
    const logMel = new Float32Array(N_MEL);
    for (let m = 0; m < N_MEL; m++) {
      const f = this.bank[m];
      let s = 0;
      for (let k = 0; k < half; k++) if (f[k]) s += f[k] * (re[k] * re[k] + im[k] * im[k]);
      logMel[m] = Math.log(s + 1e-10);
    }
    const cep = new Float32Array(N_CEP);
    for (let c = 1; c <= N_CEP; c++) {
      let s = 0;
      for (let m = 0; m < N_MEL; m++) s += logMel[m] * Math.cos((Math.PI * c * (m + 0.5)) / N_MEL);
      cep[c - 1] = s;
    }
    return { cep, rms: Math.sqrt(e / frameLen) };
  }
}

// 倒頻譜平均正規化：消除麥克風/距離造成的整體音色差
function normalize(seq) {
  const mean = new Float32Array(N_CEP);
  for (const f of seq) for (let i = 0; i < N_CEP; i++) mean[i] += f[i] / seq.length;
  return seq.map((f) => Array.from(f, (v, i) => +(v - mean[i]).toFixed(3)));
}

export function dtw(a, b) {
  const n = a.length, m = b.length;
  const band = Math.max(Math.abs(n - m) + 10, Math.round(Math.max(n, m) * 0.35));
  let prev = new Float64Array(m + 1).fill(Infinity), cur = new Float64Array(m + 1);
  prev[0] = 0;
  for (let i = 1; i <= n; i++) {
    cur.fill(Infinity);
    const c = Math.round((i * m) / n);
    const j0 = Math.max(1, c - band), j1 = Math.min(m, c + band);
    for (let j = j0; j <= j1; j++) {
      let d = 0;
      const x = a[i - 1], y = b[j - 1];
      for (let k = 0; k < N_CEP; k++) { const t = x[k] - y[k]; d += t * t; }
      cur[j] = Math.sqrt(d) + Math.min(prev[j], cur[j - 1], prev[j - 1]);
    }
    [prev, cur] = [cur, prev];
  }
  return prev[m] / (n + m);
}

// ------------------------------------------------------------ 麥克風 + 斷句
// AudioWorklet：每累積約 40ms 的樣本就丟回主執行緒
const WORKLET_SRC = `
class SbCapture extends AudioWorkletProcessor {
  constructor() { super(); this.buf = new Float32Array(2048); this.n = 0; }
  process(inputs) {
    const ch = inputs[0] && inputs[0][0];
    if (ch) {
      for (let i = 0; i < ch.length; i++) {
        this.buf[this.n++] = ch[i];
        if (this.n === this.buf.length) { this.port.postMessage(this.buf); this.buf = new Float32Array(2048); this.n = 0; }
      }
    }
    return true;
  }
}
registerProcessor('sb-capture', SbCapture);
`;
const workletLoaded = new WeakMap();

export class LocalSpotter {
  // onUtterance({ seq, peak, endedAt }) 每切出一句話呼叫一次
  constructor({ audioCtx, onUtterance, onLevel }) {
    this.ctx = audioCtx;
    this.onUtterance = onUtterance;
    this.onLevel = onLevel || (() => {});
    this.running = false;
  }

  async start() {
    if (this.running) return;
    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
    });
    if (this.ctx.state !== 'running') await this.ctx.resume().catch(() => {});
    this.mfcc = new Mfcc(this.ctx.sampleRate);
    this.src = this.ctx.createMediaStreamSource(this.stream);
    this.mute = this.ctx.createGain();
    this.mute.gain.value = 0;
    // 優先用 AudioWorklet：在音訊執行緒收音，主執行緒被影像辨識卡住時也不會掉資料
    if (this.ctx.audioWorklet && window.AudioWorkletNode) {
      if (!workletLoaded.has(this.ctx)) {
        const url = URL.createObjectURL(new Blob([WORKLET_SRC], { type: 'application/javascript' }));
        workletLoaded.set(this.ctx, this.ctx.audioWorklet.addModule(url));
      }
      await workletLoaded.get(this.ctx);
      this.proc = new AudioWorkletNode(this.ctx, 'sb-capture');
      this.proc.port.onmessage = (e) => { if (this.running) this._push(e.data); };
    } else {
      this.proc = this.ctx.createScriptProcessor(2048, 1, 1);
      this.proc.onaudioprocess = (e) => { if (this.running) this._push(e.inputBuffer.getChannelData(0)); };
    }
    this.src.connect(this.proc); this.proc.connect(this.mute); this.mute.connect(this.ctx.destination);
    this.buf = new Float32Array(0);
    this.history = [];        // 最近幾個 frame（含 preroll）
    this.floor = 0; this.calib = 0;
    this.inSpeech = false; this.loud = 0; this.quiet = 0; this.utt = [];
    this.running = true;
  }

  stop() {
    this.running = false;
    try { if (this.proc.port) this.proc.port.onmessage = null; this.proc.disconnect(); this.src.disconnect(); this.mute.disconnect(); } catch (_) {}
    if (this.stream) this.stream.getTracks().forEach((t) => t.stop());
  }

  _push(data) {
    const merged = new Float32Array(this.buf.length + data.length);
    merged.set(this.buf); merged.set(data, this.buf.length);
    const { frameLen, hop } = this.mfcc;
    let off = 0;
    while (off + frameLen <= merged.length) { this._frame(this.mfcc.frame(merged, off)); off += hop; }
    this.buf = merged.slice(off);
  }

  _frame(f) {
    // 噪音底：先用前 30 個 frame 校正，之後只在非說話時慢慢追蹤
    if (this.calib < 30) { this.floor += f.rms / 30; this.calib++; return; }
    const startThr = Math.max(this.floor * 3.2, 0.006), endThr = Math.max(this.floor * 2, 0.004);
    this.onLevel(f.rms, startThr);
    if (!this.inSpeech) {
      this.floor = f.rms < this.floor * 2.5 ? this.floor * 0.99 + f.rms * 0.01 : this.floor * 0.999 + f.rms * 0.001;
      this.history.push(f);
      if (this.history.length > PREROLL) this.history.shift();
      this.loud = f.rms > startThr ? this.loud + 1 : 0;
      if (this.loud >= 3) { this.inSpeech = true; this.utt = this.history.slice(); this.quiet = 0; }
      return;
    }
    this.utt.push(f);
    this.quiet = f.rms < endThr ? this.quiet + 1 : 0;
    if (this.quiet >= HANGOVER || this.utt.length >= MAX_FRAMES) {
      this.inSpeech = false; this.history = []; this.loud = 0;
      const voiced = this.utt.slice(0, this.utt.length - this.quiet + 3);
      if (voiced.length >= MIN_FRAMES) {
        const peak = Math.max(...voiced.map((x) => x.rms));
        this.onUtterance({ seq: normalize(voiced.map((x) => x.cep)), peak, endedAt: performance.now() - this.quiet * HOP_MS });
      }
      this.utt = [];
    }
  }
}

// ------------------------------------------------------------ 比對
// templates: { skillId: { templates:[seq,seq], peak, thr } }
export function classify(seq, peak, skillIds, templates) {
  const scores = [];
  for (const id of skillIds) {
    const t = templates[id];
    if (!t) continue;
    const d = Math.min(...t.templates.map((tp) => dtw(seq, tp)));
    scores.push({ id, d, thr: t.thr, peakRatio: peak / (t.peak || peak) });
  }
  scores.sort((a, b) => a.d - b.d);
  const best = scores[0], second = scores[1];
  if (!best) return { id: null, scores };
  let reason = '';
  if (best.d > best.thr) reason = '不夠像';
  else if (second && best.d > second.d * 0.92) reason = '分不清';
  else if (best.peakRatio < 0.3) reason = '太小聲（可能是對手的聲音）';
  return { id: reason ? null : best.id, best, reason, scores };
}

// 錄完兩個樣本後計算該技能的門檻
export function finalizeSkill(entry) {
  const [a, b] = entry.templates;
  const intra = dtw(a, b);
  entry.thr = Math.max(intra * 1.6, intra + 3);
  return entry;
}

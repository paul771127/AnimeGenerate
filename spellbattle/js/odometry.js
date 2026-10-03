// 背景追蹤（簡易視覺里程計）：比對相機畫面，估計整個畫面的「縮放」與「平移」。
//   玩家後退 → 背景變小（scale < 1）；前進 → 背景變大；轉動手機 → 背景平移。
// 用來讓虛擬的電腦對手「站在真實空間裡」：玩家後退時它變遠變小，轉動手機時它留在原地。
//
// 做法：把畫面縮成 96×72 灰階，在「關鍵幀」上挑出紋理明顯的點，
// 每次用暴力搜尋找出讓這些點最吻合的 (縮放, 平移)；變化太大時換新的關鍵幀並把變換累積起來。
// 只用畫面上方 70%（下方常有玩家自己的手）。

const W = 96, H = 72, CX = W / 2, CY = H / 2;
const USE_ROWS = Math.floor(H * 0.7);
const MAX_POINTS = 260;

export class ZoomTracker {
  constructor() {
    this.canvas = document.createElement('canvas');
    this.canvas.width = W; this.canvas.height = H;
    this.ctx = this.canvas.getContext('2d', { willReadFrequently: true });
    this.reset();
  }

  // 累積變換：原始畫面上的點 p0 → 目前畫面 p = scale·(p0 − c) + c + (tx, ty)（單位：追蹤影像像素）
  reset() {
    this.key = null; this.points = null;
    this.baseS = 1; this.baseTx = 0; this.baseTy = 0;   // 原始 → 關鍵幀
    this.curS = 1; this.curTx = 0; this.curTy = 0;      // 關鍵幀 → 目前
    this.quality = 0;
  }

  get scale() { return this.curS * this.baseS; }
  get tx() { return this.curS * this.baseTx + this.curTx; }
  get ty() { return this.curS * this.baseTy + this.curTy; }

  // 平移換算成畫面寬/高的比例
  get txNorm() { return this.tx / W; }
  get tyNorm() { return this.ty / H; }

  _gray() {
    const d = this.ctx.getImageData(0, 0, W, H).data;
    const g = new Float32Array(W * H);
    for (let i = 0, j = 0; i < g.length; i++, j += 4) g[i] = d[j] * 0.3 + d[j + 1] * 0.59 + d[j + 2] * 0.11;
    return g;
  }

  // 在關鍵幀上挑紋理明顯（梯度大）的點
  _pickPoints(g) {
    const cand = [];
    for (let y = 6; y < USE_ROWS - 2; y += 2) {
      for (let x = 6; x < W - 6; x += 2) {
        const i = y * W + x;
        const gx = g[i + 1] - g[i - 1], gy = g[i + W] - g[i - W];
        const m = Math.abs(gx) + Math.abs(gy);
        if (m > 12) cand.push([m, x, y]);
      }
    }
    cand.sort((a, b) => b[0] - a[0]);
    const pts = cand.slice(0, MAX_POINTS).map(([, x, y]) => ({ x, y, v: g[y * W + x] }));
    const mean = pts.reduce((a, p) => a + p.v, 0) / (pts.length || 1);
    for (const p of pts) p.v -= mean;
    return pts;
  }

  _cost(g, s, tx, ty) {
    const pts = this.points;
    let sum = 0, n = 0;
    const vals = new Float32Array(pts.length);
    for (let k = 0; k < pts.length; k++) {
      const p = pts[k];
      const x = Math.round(s * (p.x - CX) + CX + tx), y = Math.round(s * (p.y - CY) + CY + ty);
      if (x < 0 || x >= W || y < 0 || y >= H) { vals[k] = NaN; continue; }
      vals[k] = g[y * W + x]; sum += vals[k]; n++;
    }
    if (n < pts.length * 0.6) return Infinity;
    const mean = sum / n;
    let c = 0;
    for (let k = 0; k < pts.length; k++) {
      if (Number.isNaN(vals[k])) continue;
      c += Math.min(Math.abs(vals[k] - mean - pts[k].v), 40);   // 截斷：手或移動物體不會影響太多
    }
    return c / n;
  }

  _search(g, s0, tx0, ty0, ds, sRange, dt, tRange) {
    let best = { c: Infinity, s: s0, tx: tx0, ty: ty0 };
    for (let s = s0 - sRange; s <= s0 + sRange + 1e-9; s += ds) {
      for (let tx = tx0 - tRange; tx <= tx0 + tRange + 1e-9; tx += dt) {
        for (let ty = ty0 - tRange; ty <= ty0 + tRange + 1e-9; ty += dt) {
          const c = this._cost(g, s, tx, ty);
          if (c < best.c) best = { c, s, tx, ty };
        }
      }
    }
    return best;
  }

  update(video) {
    if (!video.videoWidth) return;
    this.ctx.drawImage(video, 0, 0, W, H);
    const g = this._gray();
    if (!this.key) {
      this.key = g; this.points = this._pickPoints(g);
      this.curS = 1; this.curTx = 0; this.curTy = 0;
      return;
    }
    if (this.points.length < 40) {   // 畫面太單調（白牆）無法追蹤，換關鍵幀再試
      this.key = g; this.points = this._pickPoints(g);
      this.quality = 0;
      return;
    }
    // 從上一次的估計附近搜尋：先粗後細
    let b = this._search(g, this.curS, this.curTx, this.curTy, 0.01, 0.05, 2, 8);
    b = this._search(g, b.s, b.tx, b.ty, 0.0025, 0.0075, 0.5, 1);
    this.quality = b.c;
    if (b.c > 30) return;   // 對不上（快速晃動、遮擋）：這次不更新
    this.curS = b.s; this.curTx = b.tx; this.curTy = b.ty;
    // 變化夠大就把目前畫面當新關鍵幀，並把變換累積進 base（new_base = 目前 ∘ 舊 base）
    if (Math.abs(b.s - 1) > 0.04 || Math.abs(b.tx) > 6 || Math.abs(b.ty) > 6) {
      const S = this.curS;
      this.baseTx = S * this.baseTx + this.curTx;
      this.baseTy = S * this.baseTy + this.curTy;
      this.baseS = S * this.baseS;
      this.key = g; this.points = this._pickPoints(g);
      this.curS = 1; this.curTx = 0; this.curTy = 0;
    }
  }
}

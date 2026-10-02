// 影像辨識：MediaPipe 手勢 (自己的手腕/手指) + 人體姿勢 (對手位置)。
// 同一支後鏡頭同時拍到「自己伸到鏡頭前的手」與「遠處的對手」。
import {
  FilesetResolver, GestureRecognizer, PoseLandmarker,
} from 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/vision_bundle.mjs';

const WASM = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm';
const GESTURE_MODEL = 'https://storage.googleapis.com/mediapipe-models/gesture_recognizer/gesture_recognizer/float16/1/gesture_recognizer.task';
const POSE_MODEL = 'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task';

async function create(Klass, fileset, opts) {
  try {
    return await Klass.createFromOptions(fileset, { ...opts, baseOptions: { ...opts.baseOptions, delegate: 'GPU' } });
  } catch (e) {
    console.warn('GPU delegate 失敗，改用 CPU', e);
    return Klass.createFromOptions(fileset, { ...opts, baseOptions: { ...opts.baseOptions, delegate: 'CPU' } });
  }
}

export class Vision {
  async init(onProgress = () => {}) {
    onProgress('載入辨識引擎…');
    const fileset = await FilesetResolver.forVisionTasks(WASM);
    onProgress('載入手勢模型…');
    this.gesture = await create(GestureRecognizer, fileset, {
      baseOptions: { modelAssetPath: GESTURE_MODEL },
      runningMode: 'VIDEO', numHands: 1,
      minHandDetectionConfidence: 0.5, minTrackingConfidence: 0.5,
    });
    onProgress('載入人體模型…');
    this.pose = await create(PoseLandmarker, fileset, {
      baseOptions: { modelAssetPath: POSE_MODEL },
      runningMode: 'VIDEO', numPoses: 2,
      minPoseDetectionConfidence: 0.5,
    });
    this.lastTs = 0;
    this.frame = 0;
    this.hand = null;     // { landmarks, gesture, score }
    this.people = [];     // [{ box:{x0,y0,x1,y1}, landmarks }]，正規化影片座標
  }

  // 每個畫面呼叫一次；手勢每幀跑，人體隔幀跑以節省效能
  update(video) {
    if (!video.videoWidth || video.readyState < 2) return;
    let ts = performance.now();
    if (ts <= this.lastTs) ts = this.lastTs + 1;
    this.lastTs = ts;
    this.frame++;
    const t0 = performance.now();

    const g = this.gesture.recognizeForVideo(video, ts);
    if (g.landmarks && g.landmarks.length) {
      const cat = g.gestures[0] && g.gestures[0][0];
      this.hand = { landmarks: g.landmarks[0], gesture: cat ? cat.categoryName : 'None', score: cat ? cat.score : 0 };
    } else {
      this.hand = null;
    }

    if (this.frame % 2 === 0) {
      const p = this.pose.detectForVideo(video, ts + 0.5);
      this.lastTs = ts + 0.5;
      this.people = (p.landmarks || []).map((lm) => ({ landmarks: lm, box: poseBox(lm) })).filter((x) => x.box);
    }
    this.lastMs = performance.now() - t0;   // 除錯用：本幀辨識耗時
  }
}

function poseBox(lm) {
  let x0 = 1, y0 = 1, x1 = 0, y1 = 0, n = 0;
  for (const p of lm) {
    if ((p.visibility ?? 1) < 0.4) continue;
    if (p.x < -0.1 || p.x > 1.1 || p.y < -0.1 || p.y > 1.1) continue;
    x0 = Math.min(x0, p.x); y0 = Math.min(y0, p.y);
    x1 = Math.max(x1, p.x); y1 = Math.max(y1, p.y);
    n++;
  }
  if (n < 5) return null;
  // 臉部特徵點只到額頭，往上補一點頭頂；左右補身體寬度
  const w = x1 - x0, h = y1 - y0;
  return {
    x0: Math.max(0, x0 - w * 0.15), x1: Math.min(1, x1 + w * 0.15),
    y0: Math.max(0, y0 - h * 0.12), y1: Math.min(1, y1 + h * 0.04),
  };
}

// 目標外觀特徵：軀幹區域的色相直方圖 (12 色相 + 3 灰階)，用來在多人畫面中認出鎖定的對手
const sigCanvas = document.createElement('canvas');
sigCanvas.width = 32; sigCanvas.height = 32;
const sigCtx = sigCanvas.getContext('2d', { willReadFrequently: true });

export function torsoSignature(video, landmarks) {
  const pick = [11, 12, 23, 24].map((i) => landmarks[i]).filter((p) => p && (p.visibility ?? 1) > 0.3);
  if (pick.length < 2) return null;
  let x0 = Math.min(...pick.map((p) => p.x)), x1 = Math.max(...pick.map((p) => p.x));
  let y0 = Math.min(...pick.map((p) => p.y)), y1 = Math.max(...pick.map((p) => p.y));
  if (y1 - y0 < 0.05) y1 = y0 + (x1 - x0) * 1.2;
  x0 = Math.max(0, x0); y0 = Math.max(0, y0); x1 = Math.min(1, x1); y1 = Math.min(1, y1);
  const vw = video.videoWidth, vh = video.videoHeight;
  const sw = (x1 - x0) * vw, sh = (y1 - y0) * vh;
  if (sw < 4 || sh < 4) return null;
  sigCtx.drawImage(video, x0 * vw, y0 * vh, sw, sh, 0, 0, 32, 32);
  const data = sigCtx.getImageData(0, 0, 32, 32).data;
  const hist = new Float32Array(15);
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i] / 255, g = data[i + 1] / 255, b = data[i + 2] / 255;
    const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
    if (d < 0.12 || max < 0.15) {
      hist[12 + Math.min(2, Math.floor(max * 3))]++;
    } else {
      let h;
      if (max === r) h = ((g - b) / d) % 6; else if (max === g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
      h = (h * 60 + 360) % 360;
      hist[Math.floor(h / 30) % 12]++;
    }
  }
  const sum = hist.reduce((a, b) => a + b, 0) || 1;
  for (let i = 0; i < 15; i++) hist[i] /= sum;
  return hist;
}

export function signatureSimilarity(a, b) {
  if (!a || !b) return 0;
  let s = 0;
  for (let i = 0; i < a.length; i++) s += Math.min(a[i], b[i]);
  return s;
}

// 用畫面中的身體大小估算對手距離（公尺）。
// 針孔相機模型：距離 = 焦距(px) × 真實長度(m) / 畫面長度(px)。
// 取「肩寬」與「軀幹長」中換算比例較大的那個（較不受側身、彎腰影響）。
export function estimateDistance(lm, vw, vh, cfg) {
  const f = Math.max(vw, vh) / 2 / Math.tan(((cfg.cameraFovDeg / 2) * Math.PI) / 180);
  const ok = (i) => lm[i] && (lm[i].visibility ?? 1) > 0.5;
  const px = (a, b) => Math.hypot((a.x - b.x) * vw, (a.y - b.y) * vh);
  const mid = (a, b) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
  let ppm = 0, method = '';
  if (ok(11) && ok(12)) { ppm = px(lm[11], lm[12]) / cfg.shoulderM; method = '肩寬'; }
  if (ok(11) && ok(12) && ok(23) && ok(24)) {
    const t = px(mid(lm[11], lm[12]), mid(lm[23], lm[24])) / cfg.torsoM;
    if (t > ppm) { ppm = t; method = '軀幹'; }
  }
  if (!ppm) {
    const ys = lm.filter((p) => (p.visibility ?? 1) > 0.4).map((p) => p.y);
    if (ys.length < 5) return null;
    ppm = ((Math.max(...ys) - Math.min(...ys)) * vh) / cfg.bodyM;
    method = '身高';
  }
  return ppm > 0 ? { d: f / ppm, method } : null;
}

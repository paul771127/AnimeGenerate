// 手機姿態（陀螺儀）：把「畫面上的一點」換成「世界中的方向」，
// 之後手機轉動時再投影回畫面，讓地面陷阱固定在真實位置（假設玩家原地轉動手機、沒有大幅走動）。
// 不支援或沒有權限時 ok=false，陷阱改為固定在螢幕上。

const D2R = Math.PI / 180;

// W3C DeviceOrientation：裝置座標 → 地球座標的旋轉矩陣（Z-X'-Y'' 順序）
function rotationMatrix(alpha, beta, gamma) {
  const cX = Math.cos(beta * D2R), cY = Math.cos(gamma * D2R), cZ = Math.cos(alpha * D2R);
  const sX = Math.sin(beta * D2R), sY = Math.sin(gamma * D2R), sZ = Math.sin(alpha * D2R);
  return [
    cZ * cY - sZ * sX * sY, -cX * sZ, cY * sZ * sX + cZ * sY,
    cY * sZ + cZ * sX * sY, cZ * cX, sZ * sY - cZ * cY * sX,
    -cX * sY, sX, cX * cY,
  ];
}

export class Orientation {
  constructor() {
    this.R = null;
    this.ok = false;
    // 加速度感測（單人模式判斷玩家有沒有移動身體閃避）
    this.motionOk = false;
    this.motion = [];          // 最近幾秒的 { t, a }，a＝去掉重力後的加速度大小 (m/s²)
    this.gravity = null;
  }

  // iOS 必須在使用者點擊當下呼叫（requestPermission 需要使用者手勢）
  request() {
    this._requestMotion();
    const DOE = window.DeviceOrientationEvent;
    if (!DOE) return Promise.resolve(false);
    const listen = () => {
      window.addEventListener('deviceorientation', (e) => {
        if (e.alpha == null || e.beta == null || e.gamma == null) return;
        this.R = rotationMatrix(e.alpha, e.beta, e.gamma);
        this.ok = true;
      });
      return true;
    };
    if (typeof DOE.requestPermission === 'function') {
      return DOE.requestPermission().then((r) => (r === 'granted' ? listen() : false)).catch(() => false);
    }
    return Promise.resolve(listen());
  }

  _requestMotion() {
    const DME = window.DeviceMotionEvent;
    if (!DME) return;
    const listen = () => window.addEventListener('devicemotion', (e) => this._onMotion(e));
    if (typeof DME.requestPermission === 'function') DME.requestPermission().then((r) => r === 'granted' && listen()).catch(() => {});
    else listen();
  }

  _onMotion(e) {
    let mag;
    const a = e.acceleration;
    if (a && a.x != null) {
      mag = Math.hypot(a.x, a.y, a.z);
    } else {
      // 只有含重力的數值：用低通濾波估出重力再扣掉
      const g = e.accelerationIncludingGravity;
      if (!g || g.x == null) return;
      const v = [g.x, g.y, g.z];
      this.gravity = this.gravity ? this.gravity.map((x, i) => x * 0.9 + v[i] * 0.1) : v;
      mag = Math.hypot(v[0] - this.gravity[0], v[1] - this.gravity[1], v[2] - this.gravity[2]);
    }
    const t = performance.now();
    this.motionOk = true;
    this.motion.push({ t, a: mag });
    while (this.motion.length && t - this.motion[0].t > 6000) this.motion.shift();
  }

  // 從 t0 到現在的最大加速度（m/s²）
  peakSince(t0) {
    let m = 0;
    for (const x of this.motion) if (x.t >= t0 && x.a > m) m = x.a;
    return m;
  }

  // 螢幕座標的旋轉角（直拿 0、橫拿 90/270）
  static screenAngle() {
    return ((screen.orientation && screen.orientation.angle) || window.orientation || 0) * D2R;
  }

  // 螢幕點 (x, y) → 世界方向向量。f＝螢幕上的焦距(px)，(cx, cy)＝畫面中心
  toWorld(x, y, f, cx, cy) {
    if (!this.ok) return null;
    const sx = (x - cx) / f, sy = -(y - cy) / f;            // 螢幕座標：x 右、y 上、後鏡頭朝 -z
    const a = Orientation.screenAngle();
    const dx = Math.cos(a) * sx - Math.sin(a) * sy, dy = Math.sin(a) * sx + Math.cos(a) * sy, dz = -1;
    const R = this.R;
    return [R[0] * dx + R[1] * dy + R[2] * dz, R[3] * dx + R[4] * dy + R[5] * dz, R[6] * dx + R[7] * dy + R[8] * dz];
  }

  // 世界方向向量 → 螢幕點；在鏡頭後方時回傳 null
  toScreen(w, f, cx, cy) {
    if (!this.ok) return null;
    const R = this.R;
    const dx = R[0] * w[0] + R[3] * w[1] + R[6] * w[2];
    const dy = R[1] * w[0] + R[4] * w[1] + R[7] * w[2];
    const dz = R[2] * w[0] + R[5] * w[1] + R[8] * w[2];
    if (dz >= -0.05) return null;
    const a = -Orientation.screenAngle();
    const sx = Math.cos(a) * dx - Math.sin(a) * dy, sy = Math.sin(a) * dx + Math.cos(a) * dy;
    return { x: cx + (f * sx) / -dz, y: cy - (f * sy) / -dz };
  }
}

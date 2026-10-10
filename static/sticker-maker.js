// HomeChat 動畫貼圖製作器:自己畫 / 照片動起來 / 文字動畫 → GIF。
// 全部在瀏覽器裡做,GIF 編碼器也是自己寫的,不用任何外部套件。
// 包在自己的範圍裡:跟主網頁的變數(例如 COLORS)不會互相衝突
(() => {
  "use strict";

  // ---------------------------------------------------------------- GIF 編碼器
  const GifEncoder = (() => {
    // 從所有影格取樣顏色,用 median cut 挑出最多 maxColors 個代表色
    function buildPalette(frames, maxColors, transparent) {
      const samples = [];
      const total = frames.reduce((n, f) => n + f.data.length / 4, 0);
      const step = Math.max(1, Math.floor(total / 60000));
      for (const f of frames) {
        const d = f.data;
        for (let i = 0; i < d.length; i += 4 * step) {
          if (transparent && d[i + 3] < 128) continue;
          samples.push([d[i], d[i + 1], d[i + 2]]);
        }
      }
      if (!samples.length) return [[0, 0, 0]];
      let boxes = [samples];
      while (boxes.length < maxColors) {
        let best = -1, bestRange = 0, bestCh = 0;
        boxes.forEach((box, i) => {
          if (box.length < 2) return;
          for (let ch = 0; ch < 3; ch++) {
            let lo = 255, hi = 0;
            for (const c of box) { if (c[ch] < lo) lo = c[ch]; if (c[ch] > hi) hi = c[ch]; }
            if (hi - lo > bestRange) { bestRange = hi - lo; best = i; bestCh = ch; }
          }
        });
        if (best < 0) break;  // 顏色已經夠少了
        const box = boxes[best].sort((a, b) => a[bestCh] - b[bestCh]);
        const mid = box.length >> 1;
        boxes.splice(best, 1, box.slice(0, mid), box.slice(mid));
      }
      return boxes.map(box => {
        const sum = [0, 0, 0];
        for (const c of box) { sum[0] += c[0]; sum[1] += c[1]; sum[2] += c[2]; }
        return sum.map(v => Math.round(v / box.length));
      });
    }

    // 標準 GIF LZW(最小碼長 8)
    function lzw(indices, out) {
      const minCodeSize = 8, clearCode = 256, eoiCode = 257;
      let codeSize = minCodeSize + 1, nextCode = eoiCode + 1;
      let table = new Map();
      let cur = 0, curBits = 0;
      const block = [];
      const flushBlock = () => { if (block.length) { out.push(block.length, ...block); block.length = 0; } };
      const emit = code => {
        cur |= code << curBits;
        curBits += codeSize;
        while (curBits >= 8) {
          block.push(cur & 0xff);
          if (block.length === 255) flushBlock();
          cur >>>= 8; curBits -= 8;
        }
      };
      out.push(minCodeSize);
      emit(clearCode);
      let prefix = indices[0];
      for (let i = 1; i < indices.length; i++) {
        const k = indices[i];
        const key = (prefix << 8) | k;
        const code = table.get(key);
        if (code !== undefined) { prefix = code; continue; }
        emit(prefix);
        if (nextCode === 4096) {
          emit(clearCode);
          table = new Map(); nextCode = eoiCode + 1; codeSize = minCodeSize + 1;
        } else {
          if (nextCode >= (1 << codeSize)) codeSize++;
          table.set(key, nextCode++);
        }
        prefix = k;
      }
      emit(prefix);
      emit(eoiCode);
      if (curBits > 0) block.push(cur & 0xff);
      flushBlock();
      out.push(0);
    }

    // frames: ImageData 陣列(同樣大小);delayMs: 每格毫秒;transparent: 透明度 < 50% 的像素變透明
    function encode({ frames, delayMs = 100, transparent = true }) {
      const width = frames[0].width, height = frames[0].height;
      const colors = buildPalette(frames, transparent ? 255 : 256, transparent);
      const palette = transparent ? [[0, 0, 0], ...colors] : colors;
      let bits = 1;
      while ((1 << bits) < palette.length) bits++;
      const tableSize = 1 << bits;
      const lookup = new Int16Array(1 << 18).fill(-1);  // 6-bit RGB → 調色盤編號
      const base = transparent ? 1 : 0;
      const nearest = (r, g, b) => {
        const key = ((r >> 2) << 12) | ((g >> 2) << 6) | (b >> 2);
        let idx = lookup[key];
        if (idx >= 0) return idx;
        let best = Infinity;
        for (let i = base; i < palette.length; i++) {
          const c = palette[i];
          const d = (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2;
          if (d < best) { best = d; idx = i; }
        }
        lookup[key] = idx;
        return idx;
      };
      const out = [];
      const u16 = v => out.push(v & 0xff, (v >> 8) & 0xff);
      for (const ch of "GIF89a") out.push(ch.charCodeAt(0));
      u16(width); u16(height);
      out.push(0x80 | ((bits - 1) << 4) | (bits - 1), 0, 0);
      for (let i = 0; i < tableSize; i++) out.push(...(palette[i] || [0, 0, 0]));
      // 無限循環播放
      out.push(0x21, 0xff, 0x0b);
      for (const ch of "NETSCAPE2.0") out.push(ch.charCodeAt(0));
      out.push(0x03, 0x01, 0x00, 0x00, 0x00);
      const delay = Math.max(2, Math.round(delayMs / 10));
      for (const f of frames) {
        // 透明動畫每格播完要清掉,不然會跟下一格疊在一起
        out.push(0x21, 0xf9, 0x04, ((transparent ? 2 : 1) << 2) | (transparent ? 1 : 0));
        u16(delay);
        out.push(0, 0);
        out.push(0x2c); u16(0); u16(0); u16(width); u16(height); out.push(0);
        const d = f.data, n = width * height;
        const indices = new Uint8Array(n);
        for (let p = 0, i = 0; p < n; p++, i += 4) {
          indices[p] = transparent && d[i + 3] < 128 ? 0 : nearest(d[i], d[i + 1], d[i + 2]);
        }
        lzw(indices, out);
      }
      out.push(0x3b);
      return new Uint8Array(out);
    }
    return { encode };
  })();


  // ---------------------------------------------------------------- 動畫效果
  const SIZE = 240;
  const TAU = Math.PI * 2;
  const PHOTO_EFFECTS = {
    bounce: ["彈跳", t => { const h = Math.abs(Math.sin(Math.PI * t)); return { y: -h * 40, sx: 1 + (1 - h) * 0.12, sy: 1 - (1 - h) * 0.12 }; }],
    shake: ["搖晃", t => ({ x: Math.sin(TAU * t * 2) * 14, r: Math.sin(TAU * t * 2) * 0.08 })],
    spin: ["旋轉", t => ({ r: TAU * t })],
    pulse: ["心跳", t => { const s = 1 + 0.14 * Math.max(0, Math.sin(TAU * t * 2)) ** 3; return { sx: s, sy: s }; }],
    float: ["漂浮", t => ({ y: Math.sin(TAU * t) * 14, r: Math.sin(TAU * t) * 0.05 })],
    zoom: ["放大縮小", t => { const s = 0.8 + 0.3 * (0.5 - 0.5 * Math.cos(TAU * t)); return { sx: s, sy: s }; }],
    jelly: ["果凍", t => ({ sx: 1 + 0.12 * Math.sin(TAU * t), sy: 1 - 0.12 * Math.sin(TAU * t) })],
    swing: ["鐘擺", t => ({ r: Math.sin(TAU * t) * 0.35, pivot: "top" })],
  };
  const TEXT_EFFECTS = {
    wave: "波浪", bounce: "彈跳", blink: "閃爍", rainbow: "彩虹", typing: "打字機", zoom: "放大", shake: "發抖",
  };
  const COLORS = ["#111111", "#ffffff", "#e5484d", "#f76b15", "#ffc53d", "#30a46c", "#0090ff", "#8e4ec6", "#e93d82", "#8d6e63"];

  function newCanvas() {
    const c = document.createElement("canvas");
    c.width = c.height = SIZE;
    return c;
  }

  function photoFrames(img, effect, round, count = 16) {
    const fit = Math.min(190 / img.width, 190 / img.height);
    const w = img.width * fit, h = img.height * fit;
    const fn = PHOTO_EFFECTS[effect][1];
    const frames = [];
    for (let i = 0; i < count; i++) {
      const c = newCanvas(), g = c.getContext("2d");
      const tr = { x: 0, y: 0, r: 0, sx: 1, sy: 1, ...fn(i / count) };
      g.save();
      if (tr.pivot === "top") { g.translate(SIZE / 2, SIZE / 2 - h / 2); g.rotate(tr.r); g.translate(0, h / 2); }
      else { g.translate(SIZE / 2 + tr.x, SIZE / 2 + tr.y); g.rotate(tr.r); g.scale(tr.sx, tr.sy); }
      if (round) { g.beginPath(); g.arc(0, 0, Math.min(w, h) / 2, 0, TAU); g.clip(); }
      g.drawImage(img, -w / 2, -h / 2, w, h);
      g.restore();
      frames.push(c);
    }
    return frames;
  }

  function textFrames(text, effect, color, count = 16) {
    const lines = text.split("\n").map(l => [...l]).filter(l => l.length).slice(0, 4);
    if (!lines.length) return [newCanvas()];
    const probe = newCanvas().getContext("2d");
    const font = s => `900 ${s}px -apple-system, "Noto Sans TC", "PingFang TC", "Microsoft JhengHei", sans-serif`;
    let size = 96;
    const widest = s => { probe.font = font(s); return Math.max(...lines.map(l => probe.measureText(l.join("")).width)); };
    while (size > 14 && (widest(size) > 212 || size * 1.15 * lines.length > 212)) size -= 2;
    const total = lines.reduce((n, l) => n + l.length, 0);
    const frames = [];
    for (let f = 0; f < count; f++) {
      const t = f / count;
      const c = newCanvas(), g = c.getContext("2d");
      g.font = font(size);
      g.textBaseline = "middle";
      g.lineJoin = "round";
      let shown = effect === "typing" ? Math.ceil(t * 1.4 * total) : total;
      let n = 0;
      lines.forEach((chars, li) => {
        const lineW = g.measureText(chars.join("")).width;
        let x = SIZE / 2 - lineW / 2;
        const y0 = SIZE / 2 + (li - (lines.length - 1) / 2) * size * 1.15;
        chars.forEach(ch => {
          const cw = g.measureText(ch).width;
          n++;
          if (n > shown) return;
          let dx = 0, dy = 0, scale = 1, fill = color, alpha = 1;
          if (effect === "wave") dy = Math.sin(TAU * (t - n / total)) * size * 0.15;
          if (effect === "bounce") dy = -Math.abs(Math.sin(Math.PI * ((t * 2 + n / total) % 1))) * size * 0.3;
          if (effect === "blink") alpha = f % 4 < 2 ? 1 : 0.15;
          if (effect === "rainbow") fill = `hsl(${(n * 40 + t * 360) % 360}, 90%, 55%)`;
          if (effect === "zoom") scale = 0.75 + 0.35 * (0.5 - 0.5 * Math.cos(TAU * t));
          if (effect === "shake") { dx = Math.sin(f * 2.7 + n) * size * 0.06; dy = Math.cos(f * 3.1 + n * 2) * size * 0.06; }
          g.save();
          g.globalAlpha = alpha;
          g.translate(x + cw / 2 + dx, y0 + dy);
          if (effect === "zoom") { g.translate(SIZE / 2 - (x + cw / 2), SIZE / 2 - y0); g.scale(scale, scale); g.translate(-(SIZE / 2 - (x + cw / 2)), -(SIZE / 2 - y0)); }
          g.lineWidth = Math.max(4, size * 0.16);
          g.strokeStyle = fill.toLowerCase() === "#ffffff" ? "#111111" : "#ffffff";  // 外框:在任何背景都看得清楚
          g.strokeText(ch, -cw / 2, 0);
          g.fillStyle = fill;
          g.fillText(ch, -cw / 2, 0);
          g.restore();
          x += cw;
        });
      });
      frames.push(c);
    }
    return frames;
  }

  // ---------------------------------------------------------------- 製作器畫面
  const StickerMaker = (() => {
    let dlg, st;
    const h = (tag, attrs = {}, ...kids) => {
      const e = document.createElement(tag);
      for (const [k, v] of Object.entries(attrs)) {
        if (k === "class") e.className = v;
        else if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
        else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? "" : v);
      }
      for (const kid of kids) if (kid !== null && kid !== undefined) e.append(kid);
      return e;
    };

    function css() {
      if (document.getElementById("maker-css")) return;
      const s = document.createElement("style");
      s.id = "maker-css";
      s.textContent = `
  #dlg-maker { width: min(560px, calc(100vw - 16px)); max-height: calc(100dvh - 16px); }
  #dlg-maker .dlg { padding: 14px; display: flex; flex-direction: column; gap: 10px; }
  .mk-stage { position: relative; width: min(300px, 74vw); aspect-ratio: 1; margin: 0 auto; border-radius: 12px; overflow: hidden;
    background: repeating-conic-gradient(#e7e9ee 0% 25%, #ffffff 0% 50%) 0 0 / 20px 20px; touch-action: none; border: 1px solid var(--line); }
  .mk-stage canvas { position: absolute; inset: 0; width: 100%; height: 100%; }
  .mk-row { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; justify-content: center; }
  .mk-sw { width: 28px; height: 28px; border-radius: 50%; border: 2px solid var(--line); padding: 0; }
  .mk-sw.on { outline: 3px solid var(--green); outline-offset: 1px; }
  .mk-chip { border: 1px solid var(--line); background: var(--bg); border-radius: 16px; padding: 5px 12px; font-size: 14px; }
  .mk-chip.on { background: var(--green); color: #fff; border-color: var(--green); }
  .mk-frames { display: flex; gap: 6px; overflow-x: auto; padding: 2px; }
  .mk-frames canvas { width: 52px; height: 52px; border-radius: 6px; border: 2px solid var(--line); flex-shrink: 0; cursor: pointer;
    background: repeating-conic-gradient(#e7e9ee 0% 25%, #ffffff 0% 50%) 0 0 / 10px 10px; }
  .mk-frames canvas.on { border-color: var(--green); }
  .mk-label { font-size: 13px; color: var(--muted); }
  #dlg-maker textarea { min-height: 60px; resize: vertical; text-align: center; font-size: 18px; }
  .mk-busy { text-align: center; color: var(--muted); font-size: 14px; }`;
      document.head.append(s);
    }

    function open(onDone) {
      css();
      st = {
        mode: "draw", onDone, playing: false, timer: null, fps: 6,
        // 畫畫
        frames: [newCanvas()], cur: 0, color: COLORS[0], brush: 8, eraser: false, onion: true, undo: [[]],
        // 照片
        img: null, effect: "bounce", round: false,
        // 文字
        text: "", textEffect: "wave", textColor: "#e5484d",
      };
      if (dlg) dlg.remove();
      dlg = h("dialog", { id: "dlg-maker" });
      document.body.append(dlg);
      dlg.addEventListener("close", stop);
      render();
      dlg.showModal();
    }

    function stop() { clearInterval(st.timer); st.timer = null; st.playing = false; }

    function preview(frames, fps) {
      // 照片 / 文字模式:一直播放預覽
      stop();
      const view = dlg.querySelector("#mk-view");
      if (!view) return;
      const g = view.getContext("2d");
      let i = 0;
      const show = () => { g.clearRect(0, 0, SIZE, SIZE); g.drawImage(frames[i % frames.length], 0, 0); i++; };
      show();
      st.timer = setInterval(show, 1000 / fps);
      st.preview = frames;
    }

    function render() {
      stop();
      const tabs = h("div", { class: "tabs" },
        ...[["draw", "🖌 自己畫"], ["photo", "📷 照片動起來"], ["text", "🔤 文字動畫"]].map(([m, label]) =>
          h("button", { type: "button", class: st.mode === m ? "on" : "", onclick: () => { st.mode = m; render(); } }, label)));
      const body = st.mode === "draw" ? drawUI() : st.mode === "photo" ? photoUI() : textUI();
      const speed = h("div", { class: "mk-row" },
        h("span", { class: "mk-label" }, "速度"),
        h("input", { type: "range", min: 1, max: 12, value: st.fps, oninput: e => { st.fps = +e.target.value; refresh(); } }),
        h("span", { class: "mk-label", id: "mk-fps" }, `每秒 ${st.fps} 格`));
      const status = h("div", { class: "mk-busy", id: "mk-status" });
      const actions = h("div", { class: "actions", style: "margin-top:0" },
        h("button", { type: "button", class: "btn secondary", onclick: () => dlg.close() }, "取消"),
        h("button", { type: "button", class: "btn secondary", onclick: () => finish("send") }, "存起來並傳送"),
        h("button", { type: "button", class: "btn", onclick: () => finish("save") }, "存成貼圖"));
      dlg.replaceChildren(h("div", { class: "dlg" }, h("h3", { style: "margin:0" }, "做動畫貼圖"), tabs, ...body, speed, status, actions));
      refresh();
    }

    function refresh() {
      const label = dlg.querySelector("#mk-fps");
      if (label) label.textContent = `每秒 ${st.fps} 格`;
      if (st.mode === "draw") { if (st.playing) play(true); else paintStage(); }
      else preview(buildFrames(), st.fps);
    }

    // ---------- 自己畫
    function drawUI() {
      const stage = h("div", { class: "mk-stage" });
      const onion = h("canvas", { width: SIZE, height: SIZE, id: "mk-onion", style: "opacity:.25" });
      const view = h("canvas", { width: SIZE, height: SIZE, id: "mk-view" });
      stage.append(onion, view);
      let last = null;
      const pos = e => {
        const r = view.getBoundingClientRect();
        return [(e.clientX - r.left) * SIZE / r.width, (e.clientY - r.top) * SIZE / r.height];
      };
      const line = (a, b) => {
        const g = st.frames[st.cur].getContext("2d");
        g.globalCompositeOperation = st.eraser ? "destination-out" : "source-over";
        g.strokeStyle = st.color; g.fillStyle = st.color;
        g.lineWidth = st.brush; g.lineCap = g.lineJoin = "round";
        g.beginPath(); g.moveTo(...a); g.lineTo(...b); g.stroke();
        g.globalCompositeOperation = "source-over";
      };
      view.addEventListener("pointerdown", e => {
        if (st.playing) { stop(); paintStage(); }
        view.setPointerCapture(e.pointerId);
        const f = st.frames[st.cur];
        const stack = st.undo[st.cur];
        stack.push(f.getContext("2d").getImageData(0, 0, SIZE, SIZE));
        if (stack.length > 20) stack.shift();
        last = pos(e);
        line(last, [last[0] + 0.01, last[1]]);
        paintStage();
      });
      view.addEventListener("pointermove", e => {
        if (!last) return;
        const p = pos(e);
        line(last, p); last = p;
        paintStage();
      });
      const end = () => { if (last) { last = null; renderStrip(); } };
      view.addEventListener("pointerup", end);
      view.addEventListener("pointercancel", end);

      const colors = h("div", { class: "mk-row" },
        ...COLORS.map(c => h("button", { type: "button", class: "mk-sw" + (!st.eraser && st.color === c ? " on" : ""), style: `background:${c}`,
          title: c, onclick: () => { st.color = c; st.eraser = false; render(); } })),
        h("input", { type: "color", value: st.color, title: "其他顏色", style: "width:32px;height:28px;border:0;padding:0;background:none",
          onchange: e => { st.color = e.target.value; st.eraser = false; render(); } }));
      const tools = h("div", { class: "mk-row" },
        ...[[4, "細"], [8, "中"], [16, "粗"]].map(([s, l]) =>
          h("button", { type: "button", class: "mk-chip" + (st.brush === s ? " on" : ""), onclick: () => { st.brush = s; render(); } }, l)),
        h("button", { type: "button", class: "mk-chip" + (st.eraser ? " on" : ""), onclick: () => { st.eraser = !st.eraser; render(); } }, "橡皮擦"),
        h("button", { type: "button", class: "mk-chip", onclick: () => {
          const prev = st.undo[st.cur].pop();
          if (prev) { st.frames[st.cur].getContext("2d").putImageData(prev, 0, 0); paintStage(); renderStrip(); }
        } }, "↶ 復原"),
        h("button", { type: "button", class: "mk-chip", onclick: () => {
          st.undo[st.cur].push(st.frames[st.cur].getContext("2d").getImageData(0, 0, SIZE, SIZE));
          st.frames[st.cur].getContext("2d").clearRect(0, 0, SIZE, SIZE); paintStage(); renderStrip();
        } }, "清除"));
      const strip = h("div", { class: "mk-frames", id: "mk-strip" });
      const frameTools = h("div", { class: "mk-row" },
        h("button", { type: "button", class: "mk-chip", onclick: () => addFrame(true) }, "＋ 複製這格"),
        h("button", { type: "button", class: "mk-chip", onclick: () => addFrame(false) }, "＋ 空白格"),
        h("button", { type: "button", class: "mk-chip", onclick: deleteFrame }, "刪除這格"),
        h("label", { class: "mk-label", style: "display:flex;gap:4px;align-items:center" },
          h("input", { type: "checkbox", checked: st.onion, onchange: e => { st.onion = e.target.checked; paintStage(); } }), "淡影"),
        h("button", { type: "button", class: "mk-chip", id: "mk-play", onclick: () => (st.playing ? (stop(), paintStage()) : play()) },
          st.playing ? "■ 停止" : "▶ 播放"));
      setTimeout(renderStrip);
      return [stage, colors, tools, h("div", { class: "mk-label" }, "影格(點一下切換,一格一格畫,上一格會淡淡地顯示當參考)"), strip, frameTools];
    }
    function addFrame(copy) {
      const c = newCanvas();
      if (copy) c.getContext("2d").drawImage(st.frames[st.cur], 0, 0);
      st.frames.splice(st.cur + 1, 0, c);
      st.undo.splice(st.cur + 1, 0, []);
      st.cur++;
      if (st.frames.length > 24) toast2("最多 24 格");
      st.frames = st.frames.slice(0, 24); st.undo = st.undo.slice(0, 24); st.cur = Math.min(st.cur, 23);
      paintStage(); renderStrip();
    }
    function deleteFrame() {
      if (st.frames.length === 1) { st.frames[0].getContext("2d").clearRect(0, 0, SIZE, SIZE); st.undo[0] = []; }
      else { st.frames.splice(st.cur, 1); st.undo.splice(st.cur, 1); st.cur = Math.max(0, st.cur - 1); }
      paintStage(); renderStrip();
    }
    function renderStrip() {
      const strip = dlg && dlg.querySelector("#mk-strip");
      if (!strip) return;
      strip.replaceChildren(...st.frames.map((f, i) => {
        const t = h("canvas", { width: SIZE, height: SIZE, class: i === st.cur ? "on" : "", title: `第 ${i + 1} 格`,
          onclick: () => { stop(); st.cur = i; paintStage(); renderStrip(); } });
        t.getContext("2d").drawImage(f, 0, 0);
        return t;
      }));
    }
    function paintStage(frameIndex = st.cur, withOnion = true) {
      const view = dlg.querySelector("#mk-view"), onion = dlg.querySelector("#mk-onion");
      if (!view) return;
      const g = view.getContext("2d");
      g.clearRect(0, 0, SIZE, SIZE);
      g.drawImage(st.frames[frameIndex], 0, 0);
      const og = onion.getContext("2d");
      og.clearRect(0, 0, SIZE, SIZE);
      if (withOnion && st.onion && frameIndex > 0) og.drawImage(st.frames[frameIndex - 1], 0, 0);
      const btn = dlg.querySelector("#mk-play");
      if (btn) btn.textContent = st.playing ? "■ 停止" : "▶ 播放";
    }
    function play(restart) {
      stop();
      st.playing = true;
      let i = 0;
      const step = () => paintStage(i++ % st.frames.length, false);
      step();
      st.timer = setInterval(step, 1000 / st.fps);
      if (!restart) paintStage(0, false);
      const btn = dlg.querySelector("#mk-play");
      if (btn) btn.textContent = "■ 停止";
    }

    // ---------- 照片動起來
    function photoUI() {
      const stage = h("div", { class: "mk-stage" }, h("canvas", { width: SIZE, height: SIZE, id: "mk-view" }));
      const pick = h("input", { type: "file", accept: "image/*", hidden: true, onchange: async e => {
        const file = e.target.files[0];
        if (!file) return;
        try { st.img = await createImageBitmap(file, { imageOrientation: "from-image" }); }
        catch (_) {
          st.img = await new Promise((res, rej) => { const im = new Image(); im.onload = () => res(im); im.onerror = rej; im.src = URL.createObjectURL(file); });
        }
        render();
      } });
      const choose = h("div", { class: "mk-row" },
        h("button", { type: "button", class: "btn secondary", onclick: () => pick.click() }, st.img ? "換一張照片" : "選一張照片"),
        h("label", { class: "mk-label", style: "display:flex;gap:4px;align-items:center" },
          h("input", { type: "checkbox", checked: st.round, onchange: e => { st.round = e.target.checked; refresh(); } }), "裁成圓形"), pick);
      const effects = h("div", { class: "mk-row" }, ...Object.entries(PHOTO_EFFECTS).map(([k, [label]]) =>
        h("button", { type: "button", class: "mk-chip" + (st.effect === k ? " on" : ""), onclick: () => { st.effect = k; render(); } }, label)));
      return [stage, choose, st.img ? effects : h("div", { class: "mk-label", style: "text-align:center" }, "選一張照片(最好是去背的人物或寵物),再選一個效果")];
    }

    // ---------- 文字動畫
    function textUI() {
      const stage = h("div", { class: "mk-stage" }, h("canvas", { width: SIZE, height: SIZE, id: "mk-view" }));
      const input = h("textarea", { class: "field", maxlength: 40, placeholder: "輸入文字,例如:謝謝！ / 晚安 / 讚啦", oninput: e => { st.text = e.target.value; refresh(); } });
      input.value = st.text;
      const colors = h("div", { class: "mk-row" }, ...COLORS.map(c => h("button", { type: "button",
        class: "mk-sw" + (st.textColor === c ? " on" : ""), style: `background:${c}`, onclick: () => { st.textColor = c; render(); } })));
      const effects = h("div", { class: "mk-row" }, ...Object.entries(TEXT_EFFECTS).map(([k, label]) =>
        h("button", { type: "button", class: "mk-chip" + (st.textEffect === k ? " on" : ""), onclick: () => { st.textEffect = k; render(); } }, label)));
      setTimeout(() => input.focus());
      return [stage, input, colors, effects];
    }

    function buildFrames() {
      if (st.mode === "draw") return st.frames;
      if (st.mode === "photo") return st.img ? photoFrames(st.img, st.effect, st.round) : [newCanvas()];
      return textFrames(st.text.trim() || "Hello", st.textEffect, st.textColor);
    }

    function toast2(text) { const s = dlg.querySelector("#mk-status"); if (s) s.textContent = text; }

    async function finish(action) {
      if (st.mode === "photo" && !st.img) return toast2("請先選一張照片");
      if (st.mode === "text" && !st.text.trim()) return toast2("請先輸入文字");
      const frames = buildFrames();
      if (st.mode === "draw" && frames.every(f => !f.getContext("2d").getImageData(0, 0, SIZE, SIZE).data.some((v, i) => i % 4 === 3 && v))) {
        return toast2("還沒有畫任何東西");
      }
      toast2("產生 GIF 中…");
      await new Promise(r => setTimeout(r, 30));  // 讓畫面先更新
      const data = frames.map(f => f.getContext("2d").getImageData(0, 0, SIZE, SIZE));
      const gif = GifEncoder.encode({ frames: data, delayMs: 1000 / st.fps, transparent: true });
      const blob = new Blob([gif], { type: "image/gif" });
      if (blob.size > 2 * 1024 * 1024) return toast2("GIF 太大了(超過 2 MB),請減少格數");
      toast2("");
      dlg.close();
      st.onDone(blob, action);
    }

    return { open };
  })();

  if (typeof window !== "undefined") window.StickerMaker = StickerMaker;
  if (typeof module !== "undefined") module.exports = { GifEncoder };  // 給測試用
})();

#!/usr/bin/env python3
"""
城堡副本的 8 方向 2D 敵人圖（日式 Q 版）。

做法：每隻敵人用立體零件（球、橢球、圓柱、凸多邊形）組成，從 8 個方向畫成 2D：
  每個零件各自上「底色＋一層賽璐璐陰影（只落在零件裡）＋高光」，零件之間畫細內線，最後整隻描一圈粗外框；
  眼睛、嘴巴、腮紅等五官貼在頭的表面，轉到看不到的方向就不畫；發光的部分另外一層柔光暈。
  所以 8 個方向一定是同一隻、動作也一致。

格式（必須和 Runtime/SpriteEnemyView.cs 一致）：
  Resources/SpriteEnemies/{sheet}.png，2048×2048，每格 128×128（16×16 格）。
  第 slot 格＝(動作的第一張 + 第幾張) × 8 + 方向；slot 從左上往右、再往下數。
  動作（張數）：idle 4、walk 6、windup 2、attack 3、hurt 2、die 4。
  方向 0～7：面向相對鏡頭的角度，每 45°；0＝面向鏡頭，2＝面向畫面右邊，4＝背對，6＝面向畫面左邊。
  角色腳底在格子底邊往上 6% 的地方、水平置中；一格的高度＝SpriteMonsterDef.height 公尺。

用法：python3 gen_castle_sprites.py [輸出資料夾] [預覽圖資料夾] [只做哪幾隻,逗號分隔]
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

CELL, GRID, DIRS = 128, 16, 8
SS = 4
S = CELL * SS
FOOT = 0.06
PITCH = math.radians(14)
ACTIONS = [("idle", 4), ("walk", 6), ("windup", 2), ("attack", 3), ("hurt", 2), ("die", 4)]
INK = (52, 30, 52)
OUTER = 2.4 * SS      # 外框粗細（超取樣後的像素）
INNER = 1.0 * SS      # 內線

def clamp(v, a, b): return a if v < a else b if v > b else v
def mix(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
def shade_of(c):  # 陰影色：偏冷偏紫，比較像動畫
    return (c[0] * 0.70 + 18, c[1] * 0.66 + 8, c[2] * 0.74 + 30)
def hi_of(c): return mix(c, (255, 255, 250), 0.55)

# ---------------------------------------------------------------- 3D → 2D
class View:
    def __init__(self, yaw_deg, height_m):
        psi = math.radians(yaw_deg)
        self.ppm = S / height_m
        self.cx, self.foot = S / 2, S * (1 - FOOT)
        self.cp, self.sp = math.cos(PITCH), math.sin(PITCH)
        self.cy, self.sy = math.cos(psi), math.sin(psi)
    def cam(self, p):
        x, y, z = p
        return x * self.cy + z * self.sy, y, -x * self.sy + z * self.cy
    def proj(self, p):
        X, Y, Z = self.cam(p)
        return self.cx + X * self.ppm, self.foot - (Y * self.cp - Z * self.sp) * self.ppm, Z * self.cp + Y * self.sp
    def facing(self, n):
        X, Y, Z = self.cam(n)
        return Z * self.cp + Y * self.sp

class Pose:
    def __init__(self, lean=0.0, pivot=0.0, off=(0, 0, 0), squash=1.0, roll=0.0, yaw=0.0):
        self.lean, self.pivot, self.off, self.squash = math.radians(lean), pivot, off, squash
        self.roll, self.yaw = math.radians(roll), math.radians(yaw)
    def rot(self, v):
        x, y, z = v
        c, s = math.cos(self.yaw), math.sin(self.yaw); x, z = x * c + z * s, -x * s + z * c
        c, s = math.cos(self.lean), math.sin(self.lean); y, z = y * c - z * s, z * c + y * s
        c, s = math.cos(self.roll), math.sin(self.roll); x, y = x * c - y * s, y * c + x * s
        return (x, y, z)
    def apply(self, p):
        x, y, z = p
        y = self.pivot + (y - self.pivot) * self.squash
        sx = 1 + (1 - self.squash) * 0.6   # 壓扁時變寬（Q 彈）
        x, y2, z = self.rot((x * sx, y - self.pivot, z * sx))
        return (x + self.off[0], y2 + self.pivot + self.off[1], z + self.off[2])

class Model:
    def __init__(self, pose): self.pose, self.items = pose, []
    def ell(self, c, rx, ry, rz, col, shade=True, line=True, glow=0.0, bias=0.0, alpha=1.0):
        self.items.append(dict(k="ell", p=self.pose.apply(c), r=(rx, ry, rz), col=col, shade=shade, line=line, glow=glow, bias=bias, a=alpha))
    def ball(self, c, r, col, **kw): self.ell(c, r, r, r, col, **kw)
    def limb(self, a, b, r, col, line=True, bias=0.0, shade=False, r2=None):
        self.items.append(dict(k="limb", p=(self.pose.apply(a), self.pose.apply(b)), r=r, r2=r2 if r2 is not None else r, col=col, shade=shade, line=line, glow=0.0, bias=bias, a=1.0))
    def chain(self, pts, r, col, **kw):
        for i in range(len(pts) - 1): self.limb(pts[i], pts[i + 1], r, col, **kw)
    def hull(self, pts, col, line=True, glow=0.0, bias=0.0, shade=True, alpha=1.0):
        self.items.append(dict(k="hull", p=[self.pose.apply(q) for q in pts], col=col, shade=shade, line=line, glow=glow, bias=bias, a=alpha))
    def feat(self, head, r, yaw, pitch, kind, col=INK, size=0.1, bias=0.03, **kw):
        a, e = math.radians(yaw), math.radians(pitch)
        n = (math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e))
        p = (head[0] + n[0] * r, head[1] + n[1] * r, head[2] + n[2] * r)
        self.items.append(dict(k="feat", p=self.pose.apply(p), n=self.pose.rot(n), kind=kind, col=col, size=size, kw=kw, bias=bias, line=False, shade=False, glow=0.0, a=1.0))

def ring(c, rx, rz, n=16, y=0.0):
    return [(c[0] + math.cos(i * 2 * math.pi / n) * rx, c[1] + y, c[2] + math.sin(i * 2 * math.pi / n) * rz) for i in range(n)]

def convex(points):
    pts = sorted(set((round(p[0], 2), round(p[1], 2)) for p in points))
    if len(pts) < 3: return pts
    def cr(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cr(hi[-2], hi[-1], p) <= 0: hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]

# ---------------------------------------------------------------- 畫圖
def bbox_of(kind, g, pad):
    if kind == "ell":
        u, v, rh, rv = g; xs, ys = (u - rh, u + rh), (v - rv, v + rv)
    elif kind == "limb":
        ua, va, ub, vb, ra, rb = g; r = max(ra, rb); xs, ys = (min(ua, ub) - r, max(ua, ub) + r), (min(va, vb) - r, max(va, vb) + r)
    elif kind == "hull":
        xs, ys = (min(q[0] for q in g), max(q[0] for q in g)), (min(q[1] for q in g), max(q[1] for q in g))
    else:
        u, v, r = g; xs, ys = (u - r, u + r), (v - r, v + r)
    x0, y0 = max(0, int(xs[0] - pad)), max(0, int(ys[0] - pad))
    x1, y1 = min(S, int(xs[1] + pad) + 1), min(S, int(ys[1] + pad) + 1)
    return x0, y0, max(x0 + 1, x1), max(y0 + 1, y1)

def mask_of(kind, g, box, grow=0.0):
    x0, y0, x1, y1 = box
    im = Image.new("L", (x1 - x0, y1 - y0), 0); d = ImageDraw.Draw(im)
    if kind == "ell":
        u, v, rh, rv = g; u -= x0; v -= y0
        d.ellipse((u - rh - grow, v - rv - grow, u + rh + grow, v + rv + grow), fill=255)
    elif kind == "limb":
        ua, va, ub, vb, ra, rb = g; ua -= x0; ub -= x0; va -= y0; vb -= y0
        ra += grow; rb += grow
        dx, dy = ub - ua, vb - va; L = math.hypot(dx, dy) or 1e-3; nx, ny = -dy / L, dx / L
        d.polygon([(ua + nx * ra, va + ny * ra), (ub + nx * rb, vb + ny * rb), (ub - nx * rb, vb - ny * rb), (ua - nx * ra, va - ny * ra)], fill=255)
        d.ellipse((ua - ra, va - ra, ua + ra, va + ra), fill=255); d.ellipse((ub - rb, vb - rb, ub + rb, vb + rb), fill=255)
    elif kind == "hull":
        pts = [(q[0] - x0, q[1] - y0) for q in g]
        d.polygon(pts, fill=255)
        if grow > 0: d.line(pts + [pts[0]], fill=255, width=int(grow * 2), joint="curve")
    return np.asarray(im, dtype=np.float32) / 255.0

def shift(a, dx, dy):
    out = np.zeros_like(a)
    h, w = a.shape
    if abs(dx) >= w or abs(dy) >= h: return out
    xs, xd = (slice(dx, w), slice(0, w - dx)) if dx >= 0 else (slice(0, w + dx), slice(-dx, w))
    ys, yd = (slice(dy, h), slice(0, h - dy)) if dy >= 0 else (slice(0, h + dy), slice(-dy, h))
    out[yd, xd] = a[ys, xs]
    return out

def paint(canvas, m, col, alpha=1.0):
    a = (m * alpha)[..., None]
    canvas[..., :3] = canvas[..., :3] * (1 - a) + np.array(col, dtype=np.float32) * a
    canvas[..., 3:] = canvas[..., 3:] * (1 - a) + a

def render(model, view):
    canvas = np.zeros((S, S, 4), np.float32)
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
    jobs = []
    for it in model.items:
        k = it["k"]
        if k == "ell":
            u, v, d = view.proj(it["p"]); rx, ry, rz = it["r"]
            rh = math.hypot(rx * view.cy, rz * view.sy) * view.ppm
            rd = math.hypot(rx * view.sy, rz * view.cy)
            rv = math.hypot(ry * view.cp, rd * view.sp) * view.ppm
            jobs.append((d + it["bias"], k, (u, v, rh, rv), it))
        elif k == "limb":
            (ua, va, da), (ub, vb, db) = view.proj(it["p"][0]), view.proj(it["p"][1])
            jobs.append(((da + db) / 2 + it["bias"], k, (ua, va, ub, vb, it["r"] * view.ppm, it["r2"] * view.ppm), it))
        elif k == "hull":
            pp = [view.proj(q) for q in it["p"]]
            jobs.append((sum(q[2] for q in pp) / len(pp) + it["bias"], k, convex([(q[0], q[1]) for q in pp]), it))
        elif k == "feat":
            f = view.facing(it["n"])
            if f < 0.25: continue
            u, v, d = view.proj(it["p"])
            jobs.append((d + it["bias"], k, (u, v, f), it))
    jobs.sort(key=lambda j: j[0])
    for d, k, g, it in jobs:
        if k == "feat":
            u, v, f = g; r = it["size"] * view.ppm * 1.3 + 8
            x0, y0, x1, y1 = bbox_of("feat", (u, v, r), 2)
            patch = Image.new("RGBA", (x1 - x0, y1 - y0), (0, 0, 0, 0))
            draw_feature(ImageDraw.Draw(patch), Off(gd, x0, y0), (u - x0, v - y0, f), it, view)
            fa = np.asarray(patch, dtype=np.float32) / 255.0
            a = fa[..., 3:4]; c = canvas[y0:y1, x0:x1]
            c[..., :3] = c[..., :3] * (1 - a) + fa[..., :3] * 255 * a
            c[..., 3:] = c[..., 3:] * (1 - a) + a
            continue
        if k == "hull" and len(g) < 3: continue
        box = bbox_of(k, g, INNER + 4)
        x0, y0, x1, y1 = box
        c = canvas[y0:y1, x0:x1]
        m = mask_of(k, g, box)
        col = it["col"]; al = it["a"]
        if it["line"]:
            paint(c, np.clip(mask_of(k, g, box, INNER) - m, 0, 1), INK, al)
        if it["shade"]:
            size = (g[2] + g[3]) / 2 if k == "ell" else (g[4] if k == "limb" else 22)
            o = max(3, int(size * 0.22))
            lit = m * shift(m, o, o)
            paint(c, m, shade_of(col), al)
            paint(c, lit, col, al)
            if k == "ell":
                u, v, rh, rv = g
                hm = Image.new("L", (x1 - x0, y1 - y0), 0)
                ImageDraw.Draw(hm).ellipse((u - x0 - rh * 0.62, v - y0 - rv * 0.72, u - x0 - rh * 0.12, v - y0 - rv * 0.3), fill=255)
                paint(c, np.asarray(hm, np.float32) / 255 * m, hi_of(col), 0.75 * al)
        else:
            paint(c, m, col, al)
        if it["glow"] > 0:
            if k == "ell":
                u, v, rh, rv = g; s2 = 2.4
                gd.ellipse((u - rh * s2, v - rv * s2, u + rh * s2, v + rv * s2), fill=(int(col[0]), int(col[1]), int(col[2]), int(150 * it["glow"])))
            elif k == "hull":
                gd.polygon(g, fill=(int(col[0]), int(col[1]), int(col[2]), int(150 * it["glow"])))
    sil = Image.fromarray((np.clip(canvas[..., 3], 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(int(OUTER) * 2 + 1))
    sila = np.asarray(sil, np.float32) / 255.0
    a = canvas[..., 3:4]
    rgb = np.array(INK, np.float32) * (1 - a) + canvas[..., :3] * a
    alpha = np.maximum(sila, canvas[..., 3])
    img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(alpha[..., None] * 255, 0, 255)]).astype(np.uint8), "RGBA")
    g = glow.filter(ImageFilter.GaussianBlur(6 * SS))
    img = Image.alpha_composite(g, img)
    return img.resize((CELL, CELL), Image.LANCZOS)

class Off:
    """發光層用整張圖的座標；五官畫在小塊上，這裡幫忙加回位移"""
    def __init__(self, d, x0, y0): self.d, self.x0, self.y0 = d, x0, y0
    def ellipse(self, b, **kw): self.d.ellipse((b[0] + self.x0, b[1] + self.y0, b[2] + self.x0, b[3] + self.y0), **kw)
    def polygon(self, pts, **kw): self.d.polygon([(x + self.x0, y + self.y0) for x, y in pts], **kw)

def draw_feature(dr, gd, g, it, view):
    u, v, f = g
    size, kind, col, kw = it["size"], it["kind"], it["col"], it["kw"]
    w = size * view.ppm * clamp(0.25 + f, 0.45, 1.0); h = size * view.ppm
    lw = max(2, int(0.045 * size * view.ppm))
    if kind == "eye":              # 動畫大眼：上眼線＋漸層虹膜＋瞳孔＋兩個高光
        iris = kw.get("iris", (90, 60, 140))
        dr.ellipse((u - w * 0.5, v - h * 0.62, u + w * 0.5, v + h * 0.62), fill=INK)
        for i, t in enumerate((0.0, 0.35, 0.7)):
            c = tuple(int(x) for x in mix(mix(iris, (20, 10, 30), 0.55), mix(iris, (255, 255, 255), 0.25), t))
            k = 1 - i * 0.12
            dr.ellipse((u - w * 0.42 * k, v - h * 0.5 + h * 0.18 * i, u + w * 0.42 * k, v + h * 0.54), fill=c)
        dr.ellipse((u - w * 0.18, v - h * 0.1, u + w * 0.18, v + h * 0.3), fill=tuple(int(x) for x in mix(iris, (10, 5, 20), 0.75)))
        dr.ellipse((u - w * 0.36, v - h * 0.48, u + w * 0.02, v - h * 0.08), fill=(255, 255, 255))
        dr.ellipse((u + w * 0.1, v + h * 0.16, u + w * 0.3, v + h * 0.36), fill=(255, 255, 255))
        dr.arc((u - w * 0.62, v - h * 0.72, u + w * 0.62, v + h * 0.5), 200, 340, fill=INK, width=lw * 2)   # 上眼線
    elif kind == "socket":         # 骷髏的大眼窩：深色＋閃亮的光點
        dr.ellipse((u - w * 0.52, v - h * 0.6, u + w * 0.52, v + h * 0.6), fill=(30, 22, 48))
        dr.ellipse((u - w * 0.36, v - h * 0.44, u + w * 0.0, v - h * 0.06), fill=(255, 255, 255))
        dr.ellipse((u + w * 0.08, v + h * 0.12, u + w * 0.26, v + h * 0.3), fill=(200, 220, 255))
    elif kind == "glow":           # 發光的眼睛
        gd.ellipse((u - w * 1.6, v - h * 1.6, u + w * 1.6, v + h * 1.6), fill=(col[0], col[1], col[2], 170))
        dr.ellipse((u - w * 0.5, v - h * 0.55, u + w * 0.5, v + h * 0.55), fill=col)
        dr.ellipse((u - w * 0.26, v - h * 0.36, u + w * 0.06, v - h * 0.04), fill=(255, 255, 240))
    elif kind == "shut":           # > <  或 × ×
        t = lw * 2
        if kw.get("x"):
            dr.line((u - w * 0.4, v - h * 0.4, u + w * 0.4, v + h * 0.4), fill=INK, width=t)
            dr.line((u - w * 0.4, v + h * 0.4, u + w * 0.4, v - h * 0.4), fill=INK, width=t)
        else:
            s = -1 if kw.get("left") else 1
            dr.line((u - w * 0.42 * s, v - h * 0.38, u + w * 0.36 * s, v, u - w * 0.42 * s, v + h * 0.38), fill=INK, width=t, joint="curve")
    elif kind == "happy":          # ^ ^
        dr.arc((u - w * 0.45, v - h * 0.2, u + w * 0.45, v + h * 0.6), 200, 340, fill=INK, width=lw * 2)
    elif kind == "mouth":
        o = kw.get("open", 0.0)
        if o > 0.08:
            dr.ellipse((u - w * 0.42, v - h * 0.25 * o, u + w * 0.42, v + h * 0.55 * o + h * 0.12), fill=(120, 40, 60), outline=INK, width=lw)
            dr.ellipse((u - w * 0.22, v + h * 0.2 * o, u + w * 0.22, v + h * 0.5 * o + h * 0.1), fill=(240, 120, 140))
        else:   # ω 貓嘴
            dr.arc((u - w * 0.5, v - h * 0.35, u, v + h * 0.25), 0, 180, fill=INK, width=lw)
            dr.arc((u, v - h * 0.35, u + w * 0.5, v + h * 0.25), 0, 180, fill=INK, width=lw)
    elif kind == "teeth":          # 骷髏的一排小牙
        dr.rounded_rectangle((u - w * 0.5, v - h * 0.16, u + w * 0.5, v + h * 0.2), radius=h * 0.1, fill=(250, 246, 232), outline=INK, width=lw)
        for i in range(1, 4): x = u - w * 0.5 + w * i / 4; dr.line((x, v - h * 0.16, x, v + h * 0.2), fill=INK, width=max(1, lw // 2))
    elif kind == "nose":           # 骷髏的愛心鼻孔
        dr.polygon([(u - w * 0.3, v - h * 0.15), (u + w * 0.3, v - h * 0.15), (u, v + h * 0.3)], fill=(60, 40, 70))
    elif kind == "blush":
        dr.ellipse((u - w * 0.6, v - h * 0.28, u + w * 0.6, v + h * 0.28), fill=(255, 140, 165, 150))
        for i in (-1, 0, 1): dr.line((u + i * w * 0.25 - w * 0.08, v + h * 0.1, u + i * w * 0.25 + w * 0.08, v - h * 0.1), fill=(240, 100, 130, 200), width=max(1, lw // 2))
    elif kind == "fang":
        dr.polygon([(u - w * 0.35, v), (u + w * 0.35, v), (u, v + h)], fill=(255, 255, 255), outline=INK)
    elif kind == "tri":            # 南瓜刻痕（會發光）
        pts = [(u - w * 0.55, v + h * 0.35), (u + w * 0.55, v + h * 0.35), (u + w * 0.05, v - h * 0.5)]
        gd.polygon([(u + (x - u) * 2, v + (y - v) * 2) for x, y in pts], fill=(col[0], col[1], col[2], 160))
        dr.polygon(pts, fill=col, outline=INK, width=lw)
        dr.ellipse((u - w * 0.1, v - h * 0.02, u + w * 0.12, v + h * 0.2), fill=(255, 255, 230))
    elif kind == "grin":
        pts = [(u - w * 0.6, v - h * 0.15)]
        for i in range(1, 6): pts.append((u - w * 0.6 + w * 1.2 * i / 6, v + (h * 0.12 if i % 2 else -h * 0.08)))
        pts += [(u + w * 0.6, v - h * 0.15), (u + w * 0.35, v + h * 0.4), (u - w * 0.35, v + h * 0.4)]
        gd.polygon([(u + (x - u) * 1.5, v + (y - v) * 1.5) for x, y in pts], fill=(col[0], col[1], col[2], 160))
        dr.polygon(pts, fill=col, outline=INK, width=lw)
    elif kind == "slit":           # 頭盔的 T 形縫
        dr.rounded_rectangle((u - w * 0.9, v - h * 0.16, u + w * 0.9, v + h * 0.16), radius=h * 0.12, fill=(16, 18, 36))
        dr.rounded_rectangle((u - w * 0.14, v, u + w * 0.14, v + h * 0.8), radius=h * 0.1, fill=(16, 18, 36))
    elif kind == "star":
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5; r = (w if i % 2 == 0 else w * 0.42) * 0.6
            pts.append((u + math.cos(a) * r, v + math.sin(a) * r))
        dr.polygon(pts, fill=col, outline=INK, width=max(1, lw // 2))
    elif kind == "crack":
        dr.line((u, v - h * 0.5, u + w * 0.15, v - h * 0.1, u - w * 0.1, v + h * 0.2, u + w * 0.08, v + h * 0.5), fill=(120, 110, 100), width=lw, joint="curve")
    elif kind == "emblem":         # 盾牌、罩袍的紋章
        dr.ellipse((u - w * 0.5, v - h * 0.5, u + w * 0.5, v + h * 0.5), fill=col, outline=INK, width=lw)
        dr.polygon([(u, v - h * 0.32), (u + w * 0.25, v), (u, v + h * 0.32), (u - w * 0.25, v)], fill=kw.get("inner", (255, 240, 200)))

# ---------------------------------------------------------------- 動作
def motion(action, f, n):
    t = f / max(1, n)
    m = dict(bob=0.0, lean=0.0, leg=0.0, legamp=0.0, arm=0.0, swing=-0.1, hurt=False, dead=0.0, squash=1.0,
             flap=math.sin(t * 2 * math.pi), mouth=0.0, glow=0.0, off=0.0, act=action, f=f, t=t, yaw=0.0)
    s = math.sin(t * 2 * math.pi)
    if action == "idle":
        m.update(bob=0.012 * s, squash=1 + 0.03 * s, arm=0.12 * s)
    elif action == "walk":
        ph = t * 2 * math.pi
        m.update(leg=ph, legamp=0.6, bob=0.045 * abs(math.sin(ph)), squash=1 - 0.04 * math.cos(2 * ph), lean=8, arm=0.55 * math.sin(ph), flap=math.sin(ph * 2), yaw=4 * math.sin(ph))
    elif action == "windup":
        m.update(lean=-10 - 8 * f, swing=-0.7 - 0.3 * f, squash=0.94 - 0.04 * f, glow=0.5 + 0.5 * f, mouth=0.4, yaw=-8 * (f + 1))
    elif action == "attack":
        m.update(lean=[18, 22, 8][f], swing=[0.3, 1.0, 0.5][f], squash=[1.08, 1.0, 0.98][f], glow=[1, 0.6, 0.2][f], mouth=[1.0, 0.7, 0.2][f], off=[0.07, 0.12, 0.05][f], yaw=[10, 14, 4][f])
    elif action == "hurt":
        m.update(lean=[-20, -10][f], hurt=True, swing=-0.3, bob=0.03, squash=[0.9, 0.97][f], mouth=0.6)
    elif action == "die":
        m.update(dead=(f + 1) / n, hurt=True, mouth=0.3)
    return m

def pose(m, pivot=0.2, floating=False):
    if m["dead"] > 0:
        k = m["dead"]
        if floating: return Pose(lean=-25 * k, pivot=0.5, off=(0, 0.2 * k, 0), squash=1 - 0.3 * k, roll=30 * k)
        return Pose(lean=-80 * min(1, k * 1.3), pivot=0.0, off=(0, 0.24 * min(1, k * 1.3), 0.4 * min(1, k * 1.3)), squash=1 - 0.12 * k)   # 往後倒、躺下時身體抬高，頭不會沉到地板下
    return Pose(lean=m["lean"], pivot=pivot, off=(0, m["bob"], m["off"]), squash=m["squash"], yaw=m["yaw"])

def legs(md, m, hip_y, spread, length, r, col, boot=None, foot=0.07):
    for side in (-1, 1):
        ph = m["leg"] + (0 if side < 0 else math.pi)
        a = math.sin(ph) * m["legamp"]
        lift = 0.04 * max(0, -math.cos(ph)) * (m["legamp"] > 0)
        hip = (side * spread, hip_y, 0)
        ft = (side * spread * 1.15, max(0.02, hip_y - length * math.cos(a)) + lift, length * math.sin(a))
        md.limb(hip, ft, r, col, shade=True)
        md.ell((ft[0], ft[1] - r * 0.2, ft[2] + foot * 0.35), r * 1.5, r * 1.05, foot, boot or shade_of(col))

def hand_at(sh, length, swing):
    a = math.radians(-120 + 105 * (swing + 1))
    return (sh[0], sh[1] - math.sin(a) * length, sh[2] + math.cos(a) * length)

def eyes(md, m, head, r, iris, spread=24, y=-6, size=None, kind="eye"):
    size = size or r * 0.5
    for s in (-1, 1):
        if m["hurt"]: md.feat(head, r, s * spread, y, "shut", size=size * 0.8, x=m["dead"] > 0, left=s < 0)
        elif m["act"] == "idle" and m["f"] == 3 and kind == "eye": md.feat(head, r, s * spread, y, "happy", size=size * 0.8)   # 眨眼
        else: md.feat(head, r, s * spread, y, kind, col=iris, size=size, iris=iris)

def cheeks(md, m, head, r, spread=44, y=-22, size=None):
    if m["dead"] > 0: return
    for s in (-1, 1): md.feat(head, r, s * spread, y, "blush", size=size or r * 0.32, bias=0.01)

def sword(md, hand, swing, length, width, steel=(205, 215, 230), guard=(235, 195, 80), hilt=(130, 80, 50)):
    a = math.radians(-120 + 105 * (swing + 1)) - math.radians(30)
    d = (0, -math.sin(a), math.cos(a))
    tip = (hand[0], hand[1] + d[1] * length, hand[2] + d[2] * length)
    base = (hand[0], hand[1] + d[1] * 0.07, hand[2] + d[2] * 0.07)
    md.hull([(base[0] - width, base[1], base[2]), (base[0] + width, base[1], base[2]), (tip[0] + width * 0.55, tip[1] - d[1] * width, tip[2] - d[2] * width),
             (tip[0], tip[1], tip[2]), (tip[0] - width * 0.55, tip[1] - d[1] * width, tip[2] - d[2] * width)], steel, bias=0.05)
    md.limb((base[0], base[1], base[2]), (tip[0], tip[1] - d[1] * width, tip[2] - d[2] * width), width * 0.18, (245, 250, 255), line=False, bias=0.06)
    md.limb((base[0] - width * 2.3, base[1], base[2]), (base[0] + width * 2.3, base[1], base[2]), width * 0.6, guard, bias=0.07, shade=True)
    md.limb(hand, (hand[0], hand[1] - d[1] * 0.09, hand[2] - d[2] * 0.09), width * 0.6, hilt, bias=0.04)
    md.ball((hand[0], hand[1] - d[1] * 0.11, hand[2] - d[2] * 0.11), width * 0.9, guard, bias=0.05)

# ---------------------------------------------------------------- 8 種敵人
IVORY = (250, 244, 226)

def skeleton(m, archer=False):
    md = Model(pose(m, 0.25))
    cloth = (122, 92, 170) if not archer else (92, 150, 88)
    legs(md, m, 0.27, 0.075, 0.2, 0.026, IVORY, boot=(110, 80, 60) if not archer else (90, 70, 50))
    md.ell((0, 0.38, 0), 0.12, 0.12, 0.09, IVORY)                              # 胸廓
    for i in range(3): md.limb((-0.075, 0.33 + i * 0.045, 0.085), (0.075, 0.33 + i * 0.045, 0.085), 0.008, (205, 195, 180), line=False, bias=0.04)
    md.ell((0, 0.27, 0), 0.11, 0.05, 0.08, cloth)                              # 腰布
    md.hull([(-0.1, 0.27, 0.05), (0.1, 0.27, 0.05), (0.07, 0.17, 0.07), (0, 0.15, 0.08), (-0.07, 0.17, 0.07)], cloth, bias=0.01)
    head = (0, 0.74, 0.02); R = 0.3
    if archer:
        md.ell((0, 0.78, -0.03), 0.34, 0.33, 0.33, (92, 150, 88))                # 兜帽
        for s in (-1, 1): md.hull([(s * 0.18, 0.98, -0.05), (s * 0.3, 0.98, -0.08), (s * 0.27, 1.17, -0.12)], (92, 150, 88))   # 兜帽上的耳朵
        md.ell((0, 0.52, 0), 0.2, 0.06, 0.16, (80, 130, 75))
    md.ball(head, R, IVORY)
    md.ell((0, 0.52, 0.09), 0.13, 0.065, 0.1, (238, 230, 210))                 # 下巴
    eyes(md, m, head, R, (40, 30, 70), spread=27, y=-4, size=0.15, kind="socket")
    md.feat(head, R, 0, -20, "nose", size=0.05)
    md.feat(head, R, 0, -36, "teeth", size=0.1, bias=0.02)
    if not archer:
        md.feat(head, R, -40, 32, "crack", size=0.1)
        # 紅頭巾＋飄帶
        md.ell((0, 0.92, -0.05), 0.27, 0.13, 0.27, (225, 70, 80))
        md.ball((0.0, 0.93, -0.27), 0.05, (225, 70, 80))
        wave = 0.04 * math.sin(m["t"] * 2 * math.pi * 2)
        for s in (-1, 1): md.hull([(s * 0.02, 0.93, -0.28), (s * 0.12, 0.8 + wave * s, -0.4), (s * 0.04, 0.78 + wave * s, -0.38)], (205, 55, 70))
        md.ell((0, 0.52, 0.02), 0.17, 0.045, 0.13, (225, 70, 80))              # 圍巾
    sh_r, sh_l = (-0.13, 0.46, 0), (0.13, 0.46, 0)
    if archer:
        md.limb((0.08, 0.35, -0.12), (-0.12, 0.7, -0.14), 0.05, (130, 90, 55), shade=True, bias=-0.04)   # 箭筒
        for i in range(3): md.limb((-0.1 + i * 0.03, 0.66, -0.14), (-0.13 + i * 0.03, 0.78, -0.15), 0.01, (230, 70, 80), line=False, bias=-0.03)
        pull = clamp(-m["swing"], 0, 1)
        bow_hand = (0.11, 0.45, 0.27)
        md.limb(sh_l, bow_hand, 0.024, IVORY); md.ball(bow_hand, 0.035, IVORY)
        arc = [(0.11, 0.45 + math.sin(a) * 0.27, 0.27 - (1 - math.cos(a)) * 0.22 + 0.1) for a in [math.radians(x) for x in range(-75, 76, 15)]]
        md.chain(arc, 0.016, (150, 95, 50), bias=0.04)
        nock = (0.11, 0.45, 0.17 - pull * 0.2)
        md.limb(arc[0], nock, 0.005, (250, 250, 240), line=False, bias=0.05); md.limb(arc[-1], nock, 0.005, (250, 250, 240), line=False, bias=0.05)
        draw = (0.0, 0.46, 0.15 - pull * 0.2)
        md.limb(sh_r, draw, 0.024, IVORY); md.ball(draw, 0.035, IVORY)
        if m["swing"] < 0.5:
            md.limb(nock, (0.11, 0.45, 0.5), 0.011, (170, 130, 80), bias=0.06)
            md.hull([(0.11, 0.47, 0.5), (0.11, 0.43, 0.5), (0.11, 0.45, 0.56)], (210, 220, 235), bias=0.07)
    else:
        hand = hand_at(sh_r, 0.19, m["swing"])
        md.limb(sh_r, hand, 0.024, IVORY); md.ball(hand, 0.036, IVORY)
        sword(md, hand, m["swing"], 0.33, 0.03)
        sh = (0.17, 0.38 + 0.02 * m["arm"], 0.13)
        md.limb(sh_l, sh, 0.024, IVORY)
        md.ell(sh, 0.13, 0.13, 0.03, (170, 105, 60), bias=0.03)
        md.ell((sh[0], sh[1], sh[2] + 0.015), 0.11, 0.11, 0.02, (200, 130, 70), bias=0.035, line=False)
        md.feat((sh[0], sh[1], sh[2]), 0.03, 0, 0, "emblem", col=(235, 195, 80), size=0.09, inner=(225, 70, 80), bias=0.05)
    return md

def knight(m):
    steel, steel2, gold, blue, cape = (196, 206, 226), (150, 160, 190), (240, 196, 80), (110, 210, 255), (70, 90, 180)
    md = Model(pose(m, 0.3))
    legs(md, m, 0.32, 0.12, 0.24, 0.062, steel2, boot=(120, 128, 155), foot=0.1)
    sway = 0.05 * math.sin(m["t"] * 2 * math.pi + 1)
    md.hull([(-0.22, 0.75, -0.12), (0.22, 0.75, -0.12), (0.3 + sway, 0.2, -0.25), (-0.3 + sway, 0.2, -0.25)], cape, bias=-0.06)   # 披風
    md.ell((0, 0.52, 0), 0.27, 0.25, 0.21, steel)                               # 圓滾滾的胸甲
    md.hull([(-0.13, 0.6, 0.18), (0.13, 0.6, 0.18), (0.12, 0.3, 0.2), (0, 0.24, 0.2), (-0.12, 0.3, 0.2)], cape, bias=0.02)   # 罩袍
    md.feat((0, 0.48, 0), 0.21, 0, 0, "emblem", col=gold, size=0.12, inner=(255, 250, 220), bias=0.04)
    md.limb((-0.2, 0.3, 0.12), (0.2, 0.3, 0.12), 0.03, gold, shade=True, bias=0.01)
    head = (0, 0.98, 0.02); R = 0.34
    md.ball(head, R, steel)
    md.feat(head, R, 0, -6, "slit", size=0.24, bias=0.01)
    for s in (-1, 1):
        if m["hurt"]: md.feat(head, R, s * 17, -4, "shut", col=blue, size=0.08, x=m["dead"] > 0, left=s < 0, bias=0.03)
        else: md.feat(head, R, s * 17, -4, "glow", col=blue, size=0.075, bias=0.03)
    # 捲捲的紅羽飾
    for i in range(7):
        a = i / 6.0
        md.ball((0, 1.33 + 0.08 * math.sin(a * 2.4) + 0.01 * m["flap"], -0.02 - a * 0.36), 0.08 - a * 0.03, (240, 80, 95))
    sh_r, sh_l = (-0.29, 0.66, 0), (0.29, 0.66, 0)
    for sh in (sh_r, sh_l): md.ball(sh, 0.12, steel)
    hand = hand_at(sh_r, 0.25, m["swing"])
    hand2 = (hand[0] + 0.13, hand[1] - 0.02, hand[2] - 0.02)
    md.limb(sh_r, hand, 0.055, steel2, shade=True); md.ball(hand, 0.07, steel)
    md.limb(sh_l, hand2, 0.055, steel2, shade=True); md.ball(hand2, 0.07, steel)
    sword(md, hand, m["swing"], 0.66, 0.06, guard=gold, hilt=(80, 60, 110))
    return md

def ghost(m):
    white, lav, cheek = (248, 246, 255), (205, 190, 250), (255, 150, 180)
    fy = 0.3 + 0.05 * math.sin(m["t"] * 2 * math.pi) + m["bob"]
    md = Model(pose(m, 0.6, floating=True))
    c = (0, fy + 0.5, 0)
    sway = 0.06 * math.sin(m["t"] * 2 * math.pi)
    # 水滴形的身體：上面圓圓的頭，下面收成一條往後捲的尾巴
    md.hull(ring(c, 0.4, 0.38, 16) + ring((0, fy + 0.3, -0.02), 0.32, 0.3, 14) + ring((sway * 0.5, fy + 0.12, -0.1), 0.18, 0.16, 10) + [(sway, fy - 0.05, -0.28)], white, bias=-0.02)
    md.hull(ring((sway * 0.5, fy + 0.12, -0.1), 0.17, 0.15, 10) + [(sway, fy - 0.04, -0.27), (sway * 1.5, fy - 0.02, -0.36)], lav, bias=-0.05)
    md.ball(c, 0.4, white)
    eyes(md, m, c, 0.4, (70, 60, 130), spread=24, y=0, size=0.17)
    cheeks(md, m, c, 0.4, y=-18, spread=42)
    md.feat(c, 0.4, 0, -18, "mouth", size=0.07, open=m["mouth"])
    for s in (-1, 1): md.ell((s * 0.36, fy + 0.36 + 0.03 * s * m["arm"], 0.12), 0.08, 0.065, 0.07, white)
    # 提燈：舉高、發光
    up = 0.18 if m["swing"] < -0.3 else 0.08 if m["act"] == "attack" else 0.0
    lan = (-0.46, fy + 0.25 + up + 0.03 * math.sin(m["t"] * 6.28 + 1), 0.16)
    md.limb((-0.36, fy + 0.36, 0.12), (lan[0], lan[1] + 0.15, lan[2]), 0.007, (90, 70, 60), line=False, bias=0.02)
    md.ell((lan[0], lan[1] + 0.12, lan[2]), 0.07, 0.025, 0.07, (110, 85, 70), bias=0.03)
    md.ell(lan, 0.07 + 0.025 * m["glow"], 0.09 + 0.025 * m["glow"], 0.07, (215, 150, 255), shade=False, glow=0.6 + 0.4 * m["glow"], bias=0.03)
    md.ball((lan[0], lan[1] - 0.01, lan[2]), 0.035, (255, 240, 255), shade=False, line=False, bias=0.05)
    md.ell((lan[0], lan[1] - 0.1, lan[2]), 0.06, 0.02, 0.06, (110, 85, 70), bias=0.03)
    return md

def bat(m):
    fur, belly, wing, inner = (150, 100, 205), (225, 200, 245), (110, 70, 160), (255, 170, 200)
    fy = 0.42 + 0.05 * math.sin(m["t"] * 2 * math.pi * 2)
    md = Model(pose(m, fy, floating=True))
    b = (0, fy + 0.2, 0)
    flap = math.sin(m["t"] * 2 * math.pi * (2 if m["act"] in ("walk", "idle") else 1)) if m["dead"] == 0 else -0.7
    if m["act"] == "windup": flap = 0.9
    if m["act"] == "attack": flap = -0.8
    for s in (-1, 1):
        up = 0.24 * flap
        root = (s * 0.17, b[1] + 0.05, -0.04)
        f1 = (s * 0.42, b[1] + 0.3 + up, -0.07); f2 = (s * 0.66, b[1] + 0.16 + up * 1.5, -0.1)
        e1 = (s * 0.56, b[1] - 0.04 + up, -0.08); e2 = (s * 0.42, b[1] + 0.02 + up * 0.6, -0.07); e3 = (s * 0.3, b[1] - 0.08 + up * 0.4, -0.05)
        md.hull([root, f1, f2, e1, e2, e3], wing, bias=-0.05)
        md.chain([root, f1, f2], 0.014, (80, 50, 120), line=False, bias=-0.04)
        md.limb(f1, e2, 0.008, (80, 50, 120), line=False, bias=-0.04)
    for s in (-1, 1):
        md.hull([(s * 0.06, b[1] + 0.18, 0), (s * 0.22, b[1] + 0.16, -0.02), (s * 0.18, b[1] + 0.44, -0.03)], fur, bias=-0.01)
        md.hull([(s * 0.1, b[1] + 0.2, 0.02), (s * 0.18, b[1] + 0.19, 0.01), (s * 0.165, b[1] + 0.36, 0.0)], inner, bias=0.0, line=False, shade=False)
    md.ball(b, 0.27, fur)
    md.ell((0, b[1] - 0.07, 0.13), 0.16, 0.13, 0.12, belly, shade=False, line=False, bias=0.02)
    md.ball((0, b[1] + 0.27, 0.02), 0.06, fur)                                  # 頭頂的毛
    eyes(md, m, b, 0.27, (235, 50, 90), spread=27, y=10, size=0.12)
    cheeks(md, m, b, 0.27, y=-8, spread=46)
    md.feat(b, 0.27, 0, -10, "mouth", size=0.05, open=m["mouth"])
    if not m["hurt"]:
        for s in (-1, 1): md.feat(b, 0.27, s * 7, -17, "fang", size=0.04, bias=0.04)
    for s in (-1, 1): md.limb((s * 0.07, b[1] - 0.24, 0), (s * 0.08, b[1] - 0.33, 0.03), 0.018, (90, 60, 120))
    return md

def pumpkin(m):
    orange, rib, stem, cloak, gl = (250, 150, 50), (225, 120, 35), (100, 160, 70), (90, 60, 130), (255, 225, 110)
    md = Model(pose(m, 0.18))
    legs(md, m, 0.2, 0.08, 0.14, 0.035, (60, 45, 80), boot=(70, 45, 40))
    md.hull(ring((0, 0.12, 0), 0.19, 0.17, 12) + ring((0, 0.32, 0), 0.12, 0.1, 10), cloak)     # 小斗篷
    md.limb((-0.08, 0.3, 0.1), (0.08, 0.3, 0.1), 0.02, (240, 200, 90), shade=True, bias=0.02)
    sw = 1 + 0.14 * m["glow"]
    head = (0, 0.56, 0); R = 0.33 * sw
    for a in (-70, -35, 35, 70, 180):
        x = math.sin(math.radians(a)) * 0.17 * sw; z = math.cos(math.radians(a)) * 0.17 * sw
        md.ell((x, 0.56, z), 0.2 * sw, 0.25 * sw, 0.2 * sw, rib, bias=-0.03)
    md.ell(head, 0.33 * sw, 0.27 * sw, 0.32 * sw, orange)
    md.limb((0, 0.8 * sw, -0.01), (0.04, 0.95, -0.03), 0.04, stem, shade=True)
    md.hull([(0.04, 0.96, -0.03), (0.2, 1.02, 0.02), (0.13, 0.92, 0.04)], (120, 190, 80), bias=0.01)   # 葉子
    md.chain([(0.04, 0.99, -0.03), (-0.05, 1.07, 0.0), (-0.13, 1.1, 0.05)], 0.012, (90, 70, 55), line=False)   # 引信
    md.ball((-0.14, 1.11 + 0.01 * m["flap"], 0.06), 0.03 + 0.02 * (m["glow"] + 0.5 * abs(m["flap"])), (255, 235, 140), shade=False, line=False, glow=1.0)
    if m["hurt"]:
        for s in (-1, 1): md.feat(head, R, s * 26, 8, "shut", size=0.13, x=m["dead"] > 0, left=s < 0)
    else:
        for s in (-1, 1): md.feat(head, R, s * 26, 10, "tri", col=gl if m["glow"] < 0.8 else (255, 252, 220), size=0.15)
    md.feat(head, R, 0, -18, "grin", col=gl, size=0.22)
    cheeks(md, m, head, R, spread=48, y=-8)
    for s in (-1, 1): md.limb((s * 0.12, 0.28, 0.03), (s * 0.21, 0.2 + 0.04 * m["arm"] * s, 0.08), 0.026, cloak, shade=True)
    return md

def gargoyle(m):
    stone, stone2, wing, horn = (170, 165, 190), (125, 120, 150), (135, 125, 165), (245, 235, 210)
    md = Model(pose(m, 0.24))
    legs(md, m, 0.24, 0.09, 0.18, 0.045, stone2, boot=stone2)
    for s in (-1, 1):
        md.hull([(s * 0.09, 0.52, -0.12), (s * 0.38, 0.76 + 0.06 * m["flap"], -0.2), (s * 0.48, 0.6 + 0.04 * m["flap"], -0.22), (s * 0.36, 0.46, -0.2), (s * 0.18, 0.42, -0.15)], wing, bias=-0.05)
    md.chain([(0, 0.26, -0.1), (0, 0.16, -0.26), (0.05 * math.sin(m["t"] * 6.28), 0.18, -0.4)], 0.025, stone2, bias=-0.04)
    md.hull([(0.05 * math.sin(m["t"] * 6.28), 0.22, -0.42), (0.05 * math.sin(m["t"] * 6.28) + 0.07, 0.15, -0.46), (0.05 * math.sin(m["t"] * 6.28) - 0.07, 0.15, -0.46), (0.05 * math.sin(m["t"] * 6.28), 0.09, -0.44)], stone2, bias=-0.04)
    md.ell((0, 0.4, 0), 0.16, 0.15, 0.13, stone)
    md.ell((0, 0.36, 0.08), 0.1, 0.09, 0.07, (195, 190, 210), shade=False, line=False, bias=0.02)
    head = (0, 0.73, 0.02); R = 0.28
    md.ball(head, R, stone)
    for s in (-1, 1):
        md.chain([(s * 0.12, 0.93, -0.02), (s * 0.22, 1.03, -0.06), (s * 0.3, 1.04, -0.02), (s * 0.31, 0.98, 0.03)], 0.035, horn, bias=-0.01, shade=True)
        md.hull([(s * 0.22, 0.8, 0), (s * 0.25, 0.7, 0), (s * 0.44, 0.88, -0.05)], stone, bias=-0.01)
    eyes(md, m, head, R, (255, 140, 40), spread=26, y=-2, size=0.13)
    cheeks(md, m, head, R)
    md.feat(head, R, 0, -26, "mouth", size=0.05, open=m["mouth"])
    if not m["hurt"]: md.feat(head, R, 9, -32, "fang", size=0.035, bias=0.04)
    md.feat(head, R, 38, 28, "crack", size=0.09)
    sh_r, sh_l = (-0.15, 0.5, 0), (0.15, 0.5, 0)
    rockc = (150, 130, 115)
    if m["swing"] < -0.4:
        rock = (0, 1.15, -0.02); h1, h2 = (-0.13, 1.05, 0), (0.13, 1.05, 0)
    elif m["act"] == "attack" and m["swing"] < 0.8:
        rock = (0, 0.72, 0.34); h1, h2 = (-0.13, 0.7, 0.28), (0.13, 0.7, 0.28)
    elif m["act"] != "attack":
        rock = (0, 0.36, 0.24); h1, h2 = (-0.14, 0.38, 0.2), (0.14, 0.38, 0.2)   # 平常抱在肚子前
    else:
        rock = None; h1, h2 = (-0.18, 0.52, 0.24), (0.18, 0.52, 0.24)
    if rock: md.ell(rock, 0.17, 0.15, 0.16, rockc, bias=0.05); md.feat(rock, 0.16, 20, 20, "crack", size=0.08, bias=0.06)
    for sh, h in ((sh_r, h1), (sh_l, h2)): md.limb(sh, h, 0.036, stone2, shade=True); md.ball(h, 0.05, stone)
    return md

def mage(m):
    robe, robe2, hat, gold, dark = (70, 85, 175), (55, 65, 140), (60, 55, 130), (245, 200, 80), (28, 22, 40)
    md = Model(pose(m, 0.22))
    legs(md, m, 0.14, 0.06, 0.1, 0.03, (90, 60, 45), boot=(110, 70, 45))
    md.hull(ring((0, 0.07, 0), 0.22, 0.19, 14) + ring((0, 0.5, 0), 0.12, 0.1, 12), robe)
    md.hull([(-0.2, 0.08, 0.1), (0.2, 0.08, 0.1), (0.21, 0.13, 0.11), (-0.21, 0.13, 0.11)], gold, bias=0.03, shade=False)
    for i in range(3): md.ball((0, 0.42 - i * 0.1, 0.14 - 0.02 * i), 0.018, gold, bias=0.04)
    md.ell((0, 0.53, 0), 0.17, 0.06, 0.15, robe2)                                # 高領
    head = (0, 0.7, 0.02); R = 0.23
    md.ball(head, R, dark, shade=False)
    for s in (-1, 1):
        if m["hurt"]: md.feat(head, R, s * 22, -2, "shut", col=(255, 225, 70), size=0.08, x=m["dead"] > 0, left=s < 0, bias=0.03)
        else: md.feat(head, R, s * 22, -2, "glow", col=(255, 225, 70), size=0.08, bias=0.03)
    md.ell((0, 0.88, 0), 0.4, 0.04, 0.4, hat)                                    # 帽簷
    bend = 0.18 + 0.05 * math.sin(m["t"] * 6.28)
    md.hull(ring((0, 0.9, 0), 0.23, 0.23) + [(0, 1.18, -0.06), (0.0, 1.3, -bend)], hat, bias=0.01)
    md.hull([(0, 1.24, -bend * 0.8), (0.02, 1.36, -bend - 0.05), (-0.03, 1.38, -bend - 0.12)], hat, bias=0.012)
    band = ring((0, 0.94, 0), 0.228, 0.228, 18); band.append(band[0])
    md.chain(band, 0.032, gold, line=False, bias=0.005)
    md.feat((0, 0.95, 0), 0.23, 0, 0, "star", col=(255, 240, 150), size=0.09, bias=0.03)
    sh_r, sh_l = (-0.12, 0.47, 0), (0.12, 0.47, 0)
    hand = hand_at(sh_r, 0.16, m["swing"] * 0.6)
    md.limb(sh_r, hand, 0.04, robe2, shade=True); md.ball(hand, 0.04, (250, 235, 215))
    top = (hand[0], hand[1] + 0.44, hand[2] + 0.02)
    md.limb((hand[0], hand[1] - 0.25, hand[2]), top, 0.018, (140, 95, 55), shade=True, bias=0.02)
    md.chain([(top[0], top[1], top[2]), (top[0] - 0.07, top[1] + 0.06, top[2]), (top[0] - 0.02, top[1] + 0.14, top[2])], 0.014, gold, bias=0.02)
    md.ball((top[0] - 0.03, top[1] + 0.07, top[2] + 0.01), 0.055 + 0.04 * m["glow"], (255, 80, 110), shade=False, glow=0.5 + 0.5 * m["glow"], bias=0.03)
    md.limb(sh_l, (0.16, 0.34, 0.1), 0.04, robe2, shade=True); md.ball((0.16, 0.34, 0.1), 0.04, (250, 235, 215))
    return md

ENEMIES = [
    ("castle_skeleton", skeleton, 1.6),
    ("castle_knight", knight, 2.0),
    ("castle_archer", lambda m: skeleton(m, archer=True), 1.6),
    ("castle_ghost", ghost, 1.6),
    ("castle_bat", bat, 1.4),
    ("castle_pumpkin", pumpkin, 1.4),
    ("castle_gargoyle", gargoyle, 1.6),
    ("castle_mage", mage, 1.6),
]

def build_sheet(fn, height):
    sheet = Image.new("RGBA", (CELL * GRID, CELL * GRID), (0, 0, 0, 0))
    first = 0
    for action, n in ACTIONS:
        for f in range(n):
            mo = motion(action, f, n)
            for d in range(DIRS):
                img = render(fn(mo), View(d * 45, height))
                slot = (first + f) * DIRS + d
                sheet.paste(img, ((slot % GRID) * CELL, (slot // GRID) * CELL))
        first += n
    return sheet

def preview(sheet):
    """預覽：每一列是一個動作的第一張（8 方向）"""
    out = Image.new("RGBA", (CELL * DIRS, CELL * len(ACTIONS)), (62, 54, 70, 255))
    first = 0
    for r, (action, n) in enumerate(ACTIONS):
        for d in range(DIRS):
            slot = first * DIRS + d
            out.alpha_composite(sheet.crop(((slot % GRID) * CELL, (slot // GRID) * CELL, (slot % GRID + 1) * CELL, (slot // GRID + 1) * CELL)), (d * CELL, r * CELL))
        first += n
    return out

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "..", "Assets", "SpellDuel", "Resources", "SpriteEnemies")
    prev_dir = sys.argv[2] if len(sys.argv) > 2 else None
    only = sys.argv[3].split(",") if len(sys.argv) > 3 else None
    os.makedirs(out_dir, exist_ok=True)
    for name, fn, h in ENEMIES:
        if only and name not in only: continue
        sheet = build_sheet(fn, h)
        sheet.save(os.path.join(out_dir, name + ".png"), optimize=True)
        if prev_dir:
            os.makedirs(prev_dir, exist_ok=True)
            preview(sheet).save(os.path.join(prev_dir, name + "_preview.png"))
        print("ok", name, flush=True)

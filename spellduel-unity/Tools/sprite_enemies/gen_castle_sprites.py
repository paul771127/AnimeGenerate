#!/usr/bin/env python3
"""
城堡副本的 8 方向 2D 敵人圖（日式 Q 版，測試版）。

做法：每隻敵人用簡單的立體零件（球、橢球、圓柱、凸多邊形）組成，再從 8 個方向「描粗線＋平塗＋高光」畫成 2D，
所以 8 個方向一定是同一隻角色、動作也一致。之後若換成 AI 畫的圖，照下面的格式輸出同名檔案即可，遊戲端不用改。

格式（必須和 Runtime/SpriteEnemyView.cs 一致）：
  Resources/SpriteEnemies/{sheet}.png，2048×2048，每格 128×128（16×16 格）。
  第 slot 格＝(動作的第一張 + 第幾張) × 8 + 方向；slot 從左上往右、再往下數。
  動作（張數）：idle 4、walk 6、windup 2、attack 3、hurt 2、die 4。
  方向 0～7：面向相對鏡頭的角度，每 45°；0＝面向鏡頭，2＝面向畫面右邊，4＝背對，6＝面向畫面左邊。
  角色腳底在格子底邊往上 6% 的地方、水平置中；一格的高度＝SpriteMonsterDef.height 公尺。

用法：python3 gen_castle_sprites.py [輸出資料夾] [預覽圖資料夾]
"""
import math, os, sys
from PIL import Image, ImageDraw

CELL, GRID, DIRS = 128, 16, 8
SS = 4                      # 超取樣倍數（畫 512 再縮成 128）
S = CELL * SS
FOOT = 0.06
PITCH = math.radians(16)    # 鏡頭略微俯視
ACTIONS = [("idle", 4), ("walk", 6), ("windup", 2), ("attack", 3), ("hurt", 2), ("die", 4)]
OUTLINE = (34, 22, 40, 255)

def clamp(v, a, b): return a if v < a else b if v > b else v
def mix(c1, c2, t): return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3)) + ((c1[3] if len(c1) > 3 else 255),)
def rgba(c): return c if len(c) == 4 else (c[0], c[1], c[2], 255)
def dark(c, k=0.68): return (int(c[0] * k), int(c[1] * k), int(c[2] * k), 255)
def light(c, k=0.45): return mix(rgba(c), (255, 255, 255, 255), k)

# ---------------------------------------------------------------- 3D → 2D
class View:
    def __init__(self, yaw_deg, height_m):
        self.psi = math.radians(yaw_deg)
        self.ppm = S / height_m                      # 每公尺幾像素
        self.cx, self.foot = S / 2, S * (1 - FOOT)
        self.cp, self.sp = math.cos(PITCH), math.sin(PITCH)
        self.cy, self.sy = math.cos(self.psi), math.sin(self.psi)
    def cam(self, p):
        x, y, z = p
        X = x * self.cy + z * self.sy
        Z = -x * self.sy + z * self.cy
        return X, y, Z
    def proj(self, p):
        X, Y, Z = self.cam(p)
        u = self.cx + X * self.ppm
        v = self.foot - (Y * self.cp - Z * self.sp) * self.ppm
        d = Z * self.cp + Y * self.sp
        return u, v, d
    def facing(self, n):
        """法線朝鏡頭的程度（1＝正對）"""
        X, Y, Z = self.cam(n)
        return Z * self.cp + Y * self.sp

class Pose:
    """整個身體的姿勢：前傾（以 pivot 為軸）、平移、整體縮放"""
    def __init__(self, lean=0.0, pivot=0.0, off=(0, 0, 0), squash=1.0, roll=0.0):
        self.lean, self.pivot, self.off, self.squash, self.roll = math.radians(lean), pivot, off, squash, math.radians(roll)
    def rot(self, v):
        x, y, z = v
        c, s = math.cos(self.lean), math.sin(self.lean)
        y2 = y * c - z * s; z2 = z * c + y * s
        c2, s2 = math.cos(self.roll), math.sin(self.roll)
        x3 = x * c2 - y2 * s2; y3 = y2 * c2 + x * s2
        return (x3, y3, z2)
    def apply(self, p):
        x, y, z = p
        y = self.pivot + (y - self.pivot) * self.squash
        x, y2, z = self.rot((x, y - self.pivot, z))
        return (x + self.off[0], y2 + self.pivot + self.off[1], z + self.off[2])

class Model:
    def __init__(self, pose):
        self.pose, self.items = pose, []
    # 每個零件記下：類型、資料、顏色
    def ball(self, c, r, col, shade=True, outline=True, glow=False):
        self.items.append(("ell", self.pose.apply(c), (r, r, r), rgba(col), shade, outline, glow, 0.0))
    def ell(self, c, rx, ry, rz, col, shade=True, outline=True, glow=False, bias=0.0):
        self.items.append(("ell", self.pose.apply(c), (rx, ry, rz), rgba(col), shade, outline, glow, bias))
    def limb(self, a, b, r, col, outline=True, bias=0.0):
        self.items.append(("limb", (self.pose.apply(a), self.pose.apply(b)), r, rgba(col), False, outline, False, bias))
    def hull(self, pts, col, outline=True, glow=False, bias=0.0):
        self.items.append(("hull", [self.pose.apply(p) for p in pts], None, rgba(col), False, outline, glow, bias))
    def feature(self, head, r, yaw, pitch, kind, col, size, bias=0.02, **kw):
        """貼在頭（球）表面的五官：yaw/pitch＝在頭上的位置（度），size＝大小（公尺）"""
        a, e = math.radians(yaw), math.radians(pitch)
        n = (math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e))
        p = (head[0] + n[0] * r, head[1] + n[1] * r, head[2] + n[2] * r)
        self.items.append(("feat", (self.pose.apply(p), self.pose.rot(n)), (kind, size, kw), rgba(col), False, True, False, bias))

def ring_pts(c, rx, rz, y=0.0, n=14):
    return [(c[0] + math.cos(i * 2 * math.pi / n) * rx, c[1] + y, c[2] + math.sin(i * 2 * math.pi / n) * rz) for i in range(n)]

def convex(points):
    pts = sorted(set(points))
    if len(pts) < 3: return pts
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0: hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]

def render(model, view):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dr, dg = ImageDraw.Draw(img), ImageDraw.Draw(glow)
    ow = 2.6 * SS
    jobs = []
    for it in model.items:
        kind = it[0]
        if kind == "ell":
            u, v, d = view.proj(it[1]); rx, ry, rz = it[2]
            rh = math.hypot(rx * view.cy, rz * view.sy) * view.ppm
            rd = math.hypot(rx * view.sy, rz * view.cy)   # 朝鏡頭方向的半徑
            rv = math.hypot(ry * view.cp, rd * view.sp) * view.ppm
            jobs.append((d + it[7], "ell", (u, v, rh, rv), it))
        elif kind == "limb":
            a, b = it[1]; ua, va, da = view.proj(a); ub, vb, db = view.proj(b)
            jobs.append(((da + db) / 2 + it[7], "limb", (ua, va, ub, vb, it[2] * view.ppm), it))
        elif kind == "hull":
            pp = [view.proj(p) for p in it[1]]
            d = sum(p[2] for p in pp) / len(pp)
            jobs.append((d + it[7], "hull", convex([(p[0], p[1]) for p in pp]), it))
        elif kind == "feat":
            p, n = it[1]; f = view.facing(n)
            if f < 0.2: continue
            u, v, d = view.proj(p)
            jobs.append((d + it[7], "feat", (u, v, f, n), it))
    jobs.sort(key=lambda j: j[0])
    for d, kind, g, it in jobs:
        col, shade, outline, isglow = it[3], it[4], it[5], it[6]
        if kind == "ell":
            u, v, rh, rv = g
            if outline: dr.ellipse((u - rh - ow, v - rv - ow, u + rh + ow, v + rv + ow), fill=OUTLINE)
            if shade:
                dr.ellipse((u - rh, v - rv, u + rh, v + rv), fill=dark(col))
                k = 0.88
                dr.ellipse((u - rh * k - rh * 0.1, v - rv * k - rv * 0.12, u + rh * k - rh * 0.1, v + rv * k - rv * 0.12), fill=col)
                hr = 0.28
                hx, hy = u - rh * 0.38, v - rv * 0.42
                dr.ellipse((hx - rh * hr, hy - rv * hr * 0.8, hx + rh * hr, hy + rv * hr * 0.8), fill=light(col, 0.5))
            else:
                dr.ellipse((u - rh, v - rv, u + rh, v + rv), fill=col)
            if isglow:
                for k in (2.2, 1.6):
                    a = 70 if k > 2 else 110
                    dg.ellipse((u - rh * k, v - rv * k, u + rh * k, v + rv * k), fill=(col[0], col[1], col[2], a))
        elif kind == "limb":
            ua, va, ub, vb, r = g
            if outline:
                dr.line((ua, va, ub, vb), fill=OUTLINE, width=int(2 * (r + ow)))
                for (x, y) in ((ua, va), (ub, vb)): dr.ellipse((x - r - ow, y - r - ow, x + r + ow, y + r + ow), fill=OUTLINE)
            dr.line((ua, va, ub, vb), fill=col, width=int(2 * r))
            for (x, y) in ((ua, va), (ub, vb)): dr.ellipse((x - r, y - r, x + r, y + r), fill=col)
        elif kind == "hull":
            if len(g) < 3: continue
            if outline: dr.line(g + [g[0]], fill=OUTLINE, width=int(ow * 2), joint="curve")
            dr.polygon(g, fill=col)
            if isglow:
                cx = sum(p[0] for p in g) / len(g); cy = sum(p[1] for p in g) / len(g)
                big = [(cx + (p[0] - cx) * 1.6, cy + (p[1] - cy) * 1.6) for p in g]
                dg.polygon(big, fill=(col[0], col[1], col[2], 90))
        elif kind == "feat":
            u, v, f, n = g
            fk, size, kw = it[2]
            w = size * view.ppm * clamp(f, 0.35, 1.0); h = size * view.ppm
            if fk == "eye":            # 動畫大眼：深色橢圓＋白色高光
                iris = kw.get("iris", col)
                dr.ellipse((u - w * 0.55 - ow * 0.6, v - h * 0.75 - ow * 0.6, u + w * 0.55 + ow * 0.6, v + h * 0.75 + ow * 0.6), fill=OUTLINE)
                dr.ellipse((u - w * 0.55, v - h * 0.75, u + w * 0.55, v + h * 0.75), fill=rgba(iris))
                dr.ellipse((u - w * 0.4, v - h * 0.1, u + w * 0.4, v + h * 0.7), fill=dark(rgba(iris), 0.6))
                dr.ellipse((u - w * 0.38, v - h * 0.6, u + w * 0.02, v - h * 0.15), fill=(255, 255, 255, 255))
                dr.ellipse((u + w * 0.1, v + h * 0.15, u + w * 0.32, v + h * 0.4), fill=(255, 255, 255, 220))
            elif fk == "glow":         # 發光的眼睛（騎士、魔導士）
                dg.ellipse((u - w * 1.4, v - h * 1.4, u + w * 1.4, v + h * 1.4), fill=(col[0], col[1], col[2], 120))
                dr.ellipse((u - w * 0.5, v - h * 0.5, u + w * 0.5, v + h * 0.5), fill=col)
                dr.ellipse((u - w * 0.22, v - h * 0.3, u + w * 0.1, v - h * 0.02), fill=(255, 255, 255, 255))
            elif fk == "socket":       # 骷髏的眼窩（大黑眼＋小光點）
                dr.ellipse((u - w * 0.55, v - h * 0.62, u + w * 0.55, v + h * 0.62), fill=(20, 14, 28, 255))
                dr.ellipse((u - w * 0.26, v - h * 0.38, u + w * 0.06, v - h * 0.06), fill=(255, 255, 255, 255))
            elif fk == "shut":         # 受擊／倒下：「> <」或「× ×」
                t = 0.06 * view.ppm
                if kw.get("x"):
                    dr.line((u - w * 0.45, v - h * 0.45, u + w * 0.45, v + h * 0.45), fill=OUTLINE, width=int(t))
                    dr.line((u - w * 0.45, v + h * 0.45, u + w * 0.45, v - h * 0.45), fill=OUTLINE, width=int(t))
                else:
                    s = -1 if kw.get("left") else 1
                    dr.line((u - w * 0.45 * s, v - h * 0.45, u + w * 0.4 * s, v, u - w * 0.45 * s, v + h * 0.45), fill=OUTLINE, width=int(t), joint="curve")
            elif fk == "mouth":
                o = kw.get("open", 0.0)
                if o > 0.05:
                    dr.ellipse((u - w * 0.5, v - h * 0.3 * o, u + w * 0.5, v + h * 0.6 * o + h * 0.1), fill=(70, 20, 30, 255), outline=OUTLINE, width=int(ow * 0.6))
                else:
                    dr.arc((u - w * 0.45, v - h * 0.5, u + w * 0.45, v + h * 0.3), 20, 160, fill=OUTLINE, width=int(0.035 * view.ppm))
            elif fk == "blush":
                dr.ellipse((u - w * 0.6, v - h * 0.3, u + w * 0.6, v + h * 0.3), fill=(255, 130, 150, 150))
            elif fk == "fang":
                dr.polygon([(u - w * 0.3, v), (u + w * 0.3, v), (u, v + h)], fill=(255, 255, 255, 255), outline=OUTLINE)
            elif fk == "tri":          # 南瓜的刻痕（三角形，會發光）
                pts = [(u, v - h * 0.5), (u + w * 0.55, v + h * 0.4), (u - w * 0.55, v + h * 0.4)]
                dg.polygon([(u + (x - u) * 1.8, v + (y - v) * 1.8) for x, y in pts], fill=(col[0], col[1], col[2], 110))
                dr.polygon(pts, fill=col, outline=OUTLINE)
            elif fk == "grin":
                pts = []
                for i in range(9):
                    x = u - w * 0.6 + w * 1.2 * i / 8; y = v + (h * 0.18 if i % 2 else -h * 0.05) + abs(i - 4) * -h * 0.05
                    pts.append((x, y))
                pts += [(u + w * 0.5, v + h * 0.35), (u, v + h * 0.5), (u - w * 0.5, v + h * 0.35)]
                dg.polygon([(u + (x - u) * 1.4, v + (y - v) * 1.4) for x, y in pts], fill=(col[0], col[1], col[2], 110))
                dr.polygon(pts, fill=col, outline=OUTLINE)
            elif fk == "slit":         # 頭盔的縫
                dr.rounded_rectangle((u - w * 0.9, v - h * 0.18, u + w * 0.9, v + h * 0.18), radius=h * 0.15, fill=(14, 16, 30, 255))
    out = Image.alpha_composite(glow, img)
    out = Image.alpha_composite(out, Image.new("RGBA", (S, S), (0, 0, 0, 0)))
    return out.resize((CELL, CELL), Image.LANCZOS)

# ---------------------------------------------------------------- 動作的參數
def motion(action, f, n):
    """回傳這一張的參數：bob 上下、lean 前傾（度）、leg 走路相位、swing 武器（−1 舉起 … +1 揮下）、hurt、dead"""
    t = f / max(1, n)
    m = dict(bob=0.0, lean=0.0, leg=0.0, legamp=0.0, arm=0.0, swing=-0.15, hurt=False, dead=0.0, squash=1.0, flap=math.sin(f * 2 * math.pi / n), mouth=0.0, glow=0.0)
    if action == "idle":
        m["bob"] = 0.018 * math.sin(t * 2 * math.pi); m["arm"] = 0.15 * math.sin(t * 2 * math.pi); m["squash"] = 1 + 0.02 * math.sin(t * 2 * math.pi)
    elif action == "walk":
        ph = t * 2 * math.pi
        m["leg"] = ph; m["legamp"] = 0.55; m["bob"] = 0.035 * abs(math.sin(ph)); m["lean"] = 7; m["arm"] = 0.5 * math.sin(ph); m["flap"] = math.sin(ph * 2)
    elif action == "windup":
        m["lean"] = -9 - 7 * f; m["swing"] = -0.65 - 0.35 * f; m["squash"] = 0.97 - 0.03 * f; m["glow"] = 0.5 + 0.5 * f; m["mouth"] = 0.3
    elif action == "attack":
        m["lean"] = [16, 20, 6][f]; m["swing"] = [0.35, 1.0, 0.45][f]; m["squash"] = [1.04, 1.0, 1.0][f]; m["glow"] = [1, 0.6, 0.2][f]; m["mouth"] = [0.8, 0.6, 0.2][f]
        m["off"] = [0.06, 0.1, 0.04][f]
    elif action == "hurt":
        m["lean"] = [-18, -9][f]; m["hurt"] = True; m["swing"] = -0.3; m["bob"] = 0.02
    elif action == "die":
        m["dead"] = (f + 1) / n; m["hurt"] = True
    return m

def body_pose(m, height_pivot=0.0, floating=False):
    if m["dead"] > 0:
        k = m["dead"]
        if floating: return Pose(lean=-20 * k, pivot=0.5, off=(0, 0.25 * k, 0), squash=1 - 0.25 * k, roll=25 * k)
        return Pose(lean=-88 * min(1, k * 1.25), pivot=0.0, off=(0, -0.02 * k, -0.05 * k), squash=1.0)
    return Pose(lean=m["lean"], pivot=height_pivot, off=(0, m["bob"], m.get("off", 0.0)), squash=m["squash"])

def legs(md, m, hip_y, spread, length, r, col, foot_col=None, foot=0.07):
    """兩隻腳：走路時前後擺"""
    for side in (-1, 1):
        a = math.sin(m["leg"] + (0 if side < 0 else math.pi)) * m["legamp"]
        hip = (side * spread, hip_y, 0)
        ft = (side * spread * 1.1, max(0.03, hip_y - length * math.cos(a) + 0.03 * max(0, -math.sin(m["leg"] + (0 if side < 0 else math.pi)))), length * math.sin(a))
        md.limb(hip, ft, r, col)
        md.ell((ft[0], ft[1] - r * 0.3, ft[2] + foot * 0.4), r * 1.35, r * 0.85, foot, foot_col or dark(col, 0.8))

def face(md, m, head, r, kind="eye", iris=(70, 40, 110), y=-8, spread=28, size=None, blush=True, mouth=True):
    size = size or r * 0.42
    if m["hurt"]:
        for s in (-1, 1): md.feature(head, r, s * spread, y, "shut", OUTLINE, size, x=m["dead"] > 0, left=s < 0)
    else:
        for s in (-1, 1): md.feature(head, r, s * spread, y, kind, iris, size, iris=iris)
    if blush and not m["dead"]:
        for s in (-1, 1): md.feature(head, r, s * (spread + 16), y - 18, "blush", (255, 140, 160), size * 0.75, bias=0.01)
    if mouth: md.feature(head, r, 0, y - 30, "mouth", OUTLINE, size * 0.5, open=m["mouth"] + (0.5 if m["hurt"] else 0))

def arm_to(md, shoulder, hand, r, col, hand_col=None):
    md.limb(shoulder, hand, r, col)
    md.ball(hand, r * 1.45, hand_col or col)

def hand_at(shoulder, length, swing):
    """手的位置：swing −1 在頭上偏後、0 在前方齊肩、+1 在前下方"""
    a = math.radians(-120 + 105 * (swing + 1))   # −120°（頭上後方）… +90°（往下）
    # a＝從「正前方」往下轉的角度；負的＝往上
    return (shoulder[0], shoulder[1] - math.sin(a) * length, shoulder[2] + math.cos(a) * length)

def blade(md, hand, swing, length, width, col, hilt=(120, 80, 40), guard=(180, 150, 60)):
    a = math.radians(-120 + 105 * (swing + 1)) - math.radians(35)
    d = (0, -math.sin(a), math.cos(a))
    tip = (hand[0], hand[1] + d[1] * length, hand[2] + d[2] * length)
    base = (hand[0], hand[1] + d[1] * 0.06, hand[2] + d[2] * 0.06)
    perp = (width, 0, 0)
    md.hull([(base[0] - width, base[1], base[2]), (base[0] + width, base[1], base[2]), (tip[0] + width * 0.4, tip[1], tip[2]), (tip[0] - width * 0.4, tip[1], tip[2]),
             (base[0], base[1] + d[2] * width * 0.6, base[2] - d[1] * width * 0.6), (tip[0], tip[1] - d[2] * width * 0.3, tip[2] + d[1] * width * 0.3)], col, bias=0.05)
    md.limb((base[0] - width * 2.2, base[1], base[2]), (base[0] + width * 2.2, base[1], base[2]), width * 0.55, guard, bias=0.06)
    md.limb(hand, (hand[0], hand[1] - d[1] * 0.08, hand[2] - d[2] * 0.08), width * 0.6, hilt, bias=0.04)

# ---------------------------------------------------------------- 8 種敵人
BONE, BONE_D = (238, 232, 214), (190, 180, 160)

def skeleton(m, archer=False):
    md = Model(body_pose(m, 0.25))
    hood = (70, 130, 70) if archer else None
    # 腳、身體
    legs(md, m, 0.3, 0.07, 0.24, 0.028, BONE)
    md.ell((0, 0.4, 0), 0.12, 0.13, 0.09, (92, 70, 110) if not archer else (60, 90, 60))           # 破爛的上衣
    for i in range(3): md.limb((-0.08, 0.36 + i * 0.05, 0.075), (0.08, 0.36 + i * 0.05, 0.075), 0.012, BONE, bias=0.03)   # 肋骨
    md.ell((0, 0.29, 0), 0.1, 0.05, 0.07, (80, 60, 50))                     # 腰帶
    head = (0, 0.74, 0.02)
    sh_r, sh_l = (-0.13, 0.5, 0), (0.13, 0.5, 0)
    if archer:
        md.ell((head[0], head[1] + 0.03, head[2] - 0.05), 0.31, 0.3, 0.3, hood)                       # 兜帽（後面）
    md.ball(head, 0.26, BONE)
    md.ell((0, 0.56, 0.08), 0.12, 0.06, 0.1, BONE_D)                        # 下巴
    if archer:
        md.hull([(head[0] + math.cos(a) * 0.29, head[1] + 0.08 + math.sin(a) * 0.25, head[2] - 0.02) for a in [i * math.pi / 8 for i in range(9)]] + [(0, 1.1, -0.12)], hood, bias=-0.02)
    else:
        md.ell((0, 0.88, -0.01), 0.24, 0.12, 0.24, (120, 110, 105))         # 生鏽的小頭盔
        md.limb((0, 0.98, 0), (0, 1.04, -0.04), 0.03, (200, 60, 60))
    md.ell((0, 0.53, 0.02), 0.15, 0.04, 0.12, (200, 50, 60), shade=False)  # 紅圍巾
    face(md, m, head, 0.26, kind="socket", spread=26, y=-2, size=0.13, blush=False, mouth=False)
    md.feature(head, 0.26, 0, -32, "mouth", OUTLINE, 0.06, open=m["mouth"])
    if archer:
        # 弓（左手）、箭（右手拉弦）
        pull = clamp(-m["swing"], 0, 1)
        bow_hand = (0.12, 0.5, 0.28)
        arm_to(md, sh_l, bow_hand, 0.025, BONE)
        pts = [(0.12, 0.5 + math.sin(a) * 0.28, 0.28 - math.cos(a) * 0.08 + 0.06) for a in [math.radians(x) for x in range(-80, 81, 20)]]
        for i in range(len(pts) - 1): md.limb(pts[i], pts[i + 1], 0.016, (130, 85, 45), bias=0.04)
        string_x = (0.12, 0.5, 0.28 - 0.05 - pull * 0.22)
        md.limb(pts[0], string_x, 0.005, (240, 240, 230), outline=False, bias=0.05); md.limb(pts[-1], string_x, 0.005, (240, 240, 230), outline=False, bias=0.05)
        draw = (-0.02, 0.5, 0.2 - pull * 0.2)
        arm_to(md, sh_r, draw, 0.025, BONE)
        if m["swing"] < 0.5: md.limb(draw, (0.12, 0.5, 0.5), 0.01, (160, 120, 70), bias=0.06)
    else:
        hand = hand_at(sh_r, 0.2, m["swing"])
        arm_to(md, sh_r, hand, 0.025, BONE)
        blade(md, hand, m["swing"], 0.36, 0.03, (190, 200, 210))
        shield = (0.17, 0.43, 0.12)
        arm_to(md, sh_l, shield, 0.025, BONE)
        md.ell(shield, 0.13, 0.13, 0.025, (150, 90, 50), bias=0.03)
        md.ell((shield[0], shield[1], shield[2] + 0.02), 0.04, 0.04, 0.02, (200, 180, 90), bias=0.05)
    return md

def knight(m):
    steel, steel_d, blue = (170, 180, 200), (110, 118, 140), (90, 200, 255)
    md = Model(body_pose(m, 0.3))
    legs(md, m, 0.36, 0.11, 0.28, 0.06, steel_d, foot=0.1)
    md.ell((0, 0.55, 0), 0.24, 0.24, 0.19, steel)                     # 胸甲
    md.ell((0, 0.38, 0), 0.22, 0.08, 0.17, steel_d)                   # 裙甲
    md.limb((-0.1, 0.62, 0.17), (0.1, 0.62, 0.17), 0.02, (220, 190, 80), bias=0.03)   # 金邊
    head = (0, 0.98, 0.02)
    md.ball(head, 0.3, steel)
    md.feature(head, 0.3, 0, -5, "slit", OUTLINE, 0.22, bias=0.01)
    for s in (-1, 1): md.feature(head, 0.3, s * 18, -5, "glow" if not m["hurt"] else "shut", blue if not m["hurt"] else OUTLINE, 0.07, bias=0.03, x=m["dead"] > 0, left=s < 0)
    for i in range(5): md.ball((0, 1.27 + i * 0.02, -0.04 - i * 0.07), 0.07 - i * 0.006, (220, 50, 60))   # 紅羽飾
    md.limb((0, 1.25, 0.05), (0, 0.8, 0.29), 0.018, steel_d, bias=0.02)
    sh_r, sh_l = (-0.27, 0.68, 0), (0.27, 0.68, 0)
    for s, sh in ((-1, sh_r), (1, sh_l)): md.ball(sh, 0.11, steel)       # 肩甲
    hand = hand_at(sh_r, 0.26, m["swing"])
    hand2 = (hand[0] + 0.12, hand[1] - 0.03, hand[2] - 0.02)
    arm_to(md, sh_r, hand, 0.055, steel_d, steel)
    arm_to(md, sh_l, hand2, 0.055, steel_d, steel)
    blade(md, hand, m["swing"], 0.62, 0.055, (200, 210, 225), hilt=(70, 50, 90), guard=(220, 190, 80))
    return md

def ghost(m):
    white, lav = (240, 236, 255), (180, 160, 240)
    float_y = 0.32 + 0.04 * math.sin(m["flap"] * 1.2) + m["bob"]
    md = Model(body_pose(m, 0.6, floating=True))
    body = (0, float_y + 0.42, 0)
    # 尾巴（往後下方）
    tail = [(math.cos(a) * 0.22, float_y + 0.3 + math.sin(a) * 0.1, -0.02) for a in [i * math.pi / 6 for i in range(12)]]
    md.hull(tail + [(0.05 * math.sin(m["leg"] + 1), float_y - 0.12, -0.22)], lav, bias=-0.05)
    md.ball(body, 0.33, white)
    face(md, m, body, 0.33, kind="eye", iris=(50, 30, 80), spread=24, y=4, size=0.12)
    # 小手＋燈籠
    lantern_y = float_y + 0.3 + (0.15 if m["swing"] < -0.3 else 0) + 0.05 * math.sin(m["flap"])
    md.ball((0.3, float_y + 0.38, 0.08), 0.06, white)
    md.ball((-0.3, float_y + 0.38, 0.08), 0.06, white)
    lan = (-0.36, lantern_y - 0.05, 0.16)
    md.limb((-0.3, float_y + 0.38, 0.08), (lan[0], lan[1] + 0.13, lan[2]), 0.008, (80, 60, 50), outline=False, bias=0.02)
    md.ball(lan, 0.07 + 0.03 * m["glow"], (200, 130, 255), shade=False, glow=True)
    md.limb((lan[0] - 0.06, lan[1] + 0.08, lan[2]), (lan[0] + 0.06, lan[1] + 0.08, lan[2]), 0.015, (90, 70, 60), bias=0.05)
    md.limb((lan[0] - 0.06, lan[1] - 0.08, lan[2]), (lan[0] + 0.06, lan[1] - 0.08, lan[2]), 0.015, (90, 70, 60), bias=0.05)
    return md

def bat(m):
    purple, wing = (130, 80, 170), (90, 50, 120)
    fly = 0.42 + 0.05 * math.sin(m["flap"] * 2)
    md = Model(body_pose(m, fly, floating=True))
    body = (0, fly + 0.18, 0)
    flap = m["flap"] if m["dead"] == 0 else -0.6
    for s in (-1, 1):
        up = 0.22 * flap
        root = (s * 0.18, body[1] + 0.05, -0.02)
        tip = (s * 0.62, body[1] + 0.12 + up * 1.6, -0.08)
        mid = (s * 0.42, body[1] + 0.25 + up, -0.06)
        low1 = (s * 0.5, body[1] - 0.08 + up * 0.8, -0.06); low2 = (s * 0.3, body[1] - 0.06 + up * 0.4, -0.04)
        md.hull([root, mid, tip, low1, low2], wing, bias=-0.04)
        md.limb(root, mid, 0.012, dark(wing), outline=False, bias=-0.03); md.limb(mid, tip, 0.012, dark(wing), outline=False, bias=-0.03)
    for s in (-1, 1):
        md.hull([(s * 0.08, body[1] + 0.2, 0), (s * 0.2, body[1] + 0.2, -0.02), (s * 0.17, body[1] + 0.42, -0.02)], purple, bias=-0.02)
    md.ball(body, 0.25, purple)
    md.ell((0, body[1] - 0.06, 0.1), 0.13, 0.1, 0.12, (200, 160, 220), shade=False, outline=False, bias=0.02)
    face(md, m, body, 0.25, kind="eye", iris=(220, 40, 70), spread=26, y=8, size=0.1, mouth=False)
    if not m["hurt"]:
        for s in (-1, 1): md.feature(body, 0.25, s * 8, -16, "fang", (255, 255, 255), 0.05, bias=0.04)
    for s in (-1, 1): md.limb((s * 0.07, body[1] - 0.22, 0), (s * 0.08, body[1] - 0.3, 0.02), 0.018, dark(purple))
    return md

def pumpkin(m):
    orange, stem, cloak = (240, 140, 40), (90, 140, 60), (60, 40, 80)
    md = Model(body_pose(m, 0.2))
    legs(md, m, 0.2, 0.08, 0.16, 0.035, cloak)
    md.ell((0, 0.26, 0), 0.15, 0.1, 0.12, cloak)
    swell = 1 + 0.12 * m["glow"]
    head = (0, 0.58, 0)
    r = 0.3 * swell
    for s in (-1, 1): md.ell((s * 0.12 * swell, 0.58, -0.01), 0.2 * swell, 0.27 * swell, 0.22 * swell, dark(orange, 0.85), bias=-0.01)   # 南瓜的瓣
    md.ell(head, 0.22 * swell, r * 0.98, 0.27 * swell, orange)
    md.limb((0, 0.84 * swell + 0.02, 0), (0.03, 0.96, -0.02), 0.035, stem)
    md.limb((0.03, 0.96, -0.02), (0.12, 1.02, 0.03), 0.012, (90, 70, 50), outline=False)              # 引信
    md.ball((0.13, 1.03 + 0.01 * m["flap"], 0.04), 0.025 + 0.02 * (m["glow"] + 0.3 * abs(m["flap"])), (255, 230, 120), shade=False, outline=False, glow=True)
    gl = (255, 220, 90) if m["glow"] < 0.7 else (255, 250, 200)
    if m["hurt"]:
        for s in (-1, 1): md.feature(head, r, s * 25, 2, "shut", OUTLINE, 0.1, x=m["dead"] > 0, left=s < 0)
    else:
        for s in (-1, 1): md.feature(head, r, s * 25, 4, "tri", gl, 0.1)
    md.feature(head, r, 0, -22, "grin", gl, 0.16)
    for s in (-1, 1): md.limb((s * 0.12, 0.3, 0.02), (s * 0.2, 0.22 + 0.04 * m["arm"] * s, 0.06), 0.025, cloak)
    return md

def gargoyle(m):
    stone, stone_d, wing = (150, 150, 165), (105, 105, 122), (115, 110, 130)
    md = Model(body_pose(m, 0.25))
    legs(md, m, 0.26, 0.09, 0.2, 0.045, stone_d)
    for s in (-1, 1):
        md.hull([(s * 0.1, 0.55, -0.12), (s * 0.45, 0.78 + 0.05 * m["flap"], -0.2), (s * 0.4, 0.5, -0.2), (s * 0.2, 0.42, -0.15)], wing, bias=-0.05)
    md.limb((0, 0.3, -0.1), (0, 0.18, -0.35), 0.025, stone_d, bias=-0.03); md.hull([(0, 0.2, -0.33), (0.06, 0.13, -0.42), (-0.06, 0.13, -0.42)], stone_d, bias=-0.03)
    md.ell((0, 0.42, 0), 0.16, 0.16, 0.13, stone)
    head = (0, 0.75, 0.02)
    md.ball(head, 0.25, stone)
    for s in (-1, 1):
        md.hull([(s * 0.1, 0.92, -0.02), (s * 0.16, 0.93, -0.02), (s * 0.24, 1.1, -0.08)], (230, 220, 200), bias=0.0)   # 角
        md.hull([(s * 0.2, 0.8, 0), (s * 0.24, 0.72, 0), (s * 0.4, 0.86, -0.04)], stone, bias=-0.01)              # 尖耳
    face(md, m, head, 0.25, kind="eye", iris=(255, 120, 40), spread=26, y=-4, size=0.1)
    # 石塊：預備時舉過頭，出手往前丟
    sh_r, sh_l = (-0.15, 0.52, 0), (0.15, 0.52, 0)
    if m["swing"] < -0.4:
        rock = (0, 1.15, -0.02); h1, h2 = (-0.12, 1.05, 0), (0.12, 1.05, 0)
        md.ball(rock, 0.17, (130, 115, 100))
    elif m["swing"] < 0.8:
        rock = (0, 0.75, 0.32); h1, h2 = (-0.12, 0.72, 0.25), (0.12, 0.72, 0.25)
        md.ball(rock, 0.15, (130, 115, 100))
    else:
        h1, h2 = (-0.18, 0.55, 0.25), (0.18, 0.55, 0.25)
    arm_to(md, sh_r, h1, 0.035, stone_d, stone); arm_to(md, sh_l, h2, 0.035, stone_d, stone)
    return md

def mage(m):
    robe, robe_d, hat, gold, face_c = (60, 70, 150), (40, 45, 100), (50, 45, 110), (230, 190, 70), (25, 20, 35)
    md = Model(body_pose(m, 0.25))
    legs(md, m, 0.18, 0.06, 0.14, 0.03, (70, 50, 40))
    md.hull(ring_pts((0, 0.1, 0), 0.2, 0.17) + ring_pts((0, 0.55, 0), 0.1, 0.09), robe)       # 長袍
    md.limb((-0.12, 0.2, 0.15), (0.12, 0.2, 0.15), 0.015, gold, bias=0.02)
    head = (0, 0.7, 0.02)
    md.ball(head, 0.22, face_c, shade=False)
    for s in (-1, 1):
        md.feature(head, 0.22, s * 22, -4, "glow" if not m["hurt"] else "shut", (255, 220, 60) if not m["hurt"] else (255, 220, 60), 0.075, bias=0.03, x=m["dead"] > 0, left=s < 0)
    md.ell((0, 0.86, 0), 0.36, 0.035, 0.36, hat, bias=0.0)                                   # 帽簷
    bend = 0.12 + 0.05 * m["flap"]
    md.hull(ring_pts((0, 0.88, 0), 0.21, 0.21) + [(0, 1.3, -bend), (0.02, 1.32, -bend - 0.02)], hat, bias=0.01)
    md.hull(ring_pts((0, 0.9, 0), 0.215, 0.215) + ring_pts((0, 0.96, 0), 0.19, 0.19), gold, bias=0.015)
    sh_r, sh_l = (-0.12, 0.5, 0), (0.12, 0.5, 0)
    hand = hand_at(sh_r, 0.17, m["swing"] * 0.7)
    arm_to(md, sh_r, hand, 0.035, robe_d, (240, 220, 200))
    top = (hand[0], hand[1] + 0.42, hand[2] + 0.02)
    md.limb((hand[0], hand[1] - 0.25, hand[2]), top, 0.018, (120, 80, 40), bias=0.02)
    md.ball(top, 0.06 + 0.04 * m["glow"], (255, 70, 90), shade=False, glow=True)
    arm_to(md, sh_l, (0.15, 0.36, 0.1), 0.035, robe_d, (240, 220, 200))
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
            m = motion(action, f, n)
            for d in range(DIRS):
                img = render(fn(m), View(d * 45, height))
                slot = (first + f) * DIRS + d
                sheet.paste(img, ((slot % GRID) * CELL, (slot // GRID) * CELL))
        first += n
    return sheet

def preview(sheet, name):
    """預覽：每一列是一個動作的第一張（8 方向），加上走路的 6 張（正面）"""
    rows = []
    first = 0
    for action, n in ACTIONS:
        rows.append([sheet.crop((((first) * DIRS + d) % GRID * CELL, ((first) * DIRS + d) // GRID * CELL, ((first) * DIRS + d) % GRID * CELL + CELL, ((first) * DIRS + d) // GRID * CELL + CELL)) for d in range(DIRS)])
        first += n
    out = Image.new("RGBA", (CELL * DIRS, CELL * len(rows)), (54, 48, 60, 255))
    for r, row in enumerate(rows):
        for c, img in enumerate(row): out.alpha_composite(img, (c * CELL, r * CELL))
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
            preview(sheet, name).save(os.path.join(prev_dir, name + "_preview.png"))
        print("ok", name)

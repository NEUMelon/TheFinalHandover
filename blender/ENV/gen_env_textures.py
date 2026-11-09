# 《最后一次交班》Stage 3 · 车间环境贴图（numpy 程序生成，由 Claude 编写，已登记 AI 使用日志）
# 运行：python3 gen_env_textures.py [输出目录=同目录 Textures/]
# 输出 URP 用三张：<名字>_Atlas_BaseColor（sRGB）/ _Normal（OpenGL）/ _MS（R=金属度, A=光滑度）。材质名 M_<名字>_Atlas 由 Unity 端自动接。
# 颜色依据 01_圣经/02_美术圣经_v0.1.md §2（墙裙绿 #5E7D6A、水磨石灰 #B9B4A8、安全黄 #F2C200、警示黑 #1A1A1A）、§5（墙裙上沿 30 mm 深色线、分格条 1 m、安全线 100 mm）。
# 平铺贴图（四边无缝，周期频谱噪声）：
#   LSB_ENV_BLD_Terrazzo     2048² = 2 m × 2 m（1024 px/m），Hero 地面：石子、分格条、抛光差异、细划痕、发丝裂、小油点、崩粒小坑
#   LSB_ENV_BLD_Wainscot     2048×1024 = 2 m × 1.2 m：1983 墙裙绿手刷漆、崩漆露白灰、踢脚脏、上沿 30 mm 深色线
#   LSB_ENV_BLD_Plaster      1024² = 2 m：白灰墙泛黄、水渍
#   LSB_ENV_BLD_Concrete     1024² = 2 m：柱、梁、屋面板
#   LSB_ENV_BLD_SteelPaint   1024² = 1 m：屋架、窗框、大门（灰绿钢漆 + 锈点）
#   LSB_ENV_BLD_SafetyLine   1024×128 = 1 m × 0.1 m：磨旧的黄线
#   LSB_ENV_BLD_Zebra        1024² = 1 m：45° 黄黑斑马线
# 非平铺：LSB_ENV_BLD_FloorStain 2048² 贴图集：机床脚下分格（见 env_common.STAIN_PADS），内容 = 水磨石下采样 + 按机床脚位置算的油污 / 铁屑 / 操作位磨亮
import sys, os
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import env_common as C

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "Textures")
os.makedirs(OUT, exist_ok=True)

def hexrgb(h): return np.array([int(h[i:i+2], 16) / 255 for i in (1, 3, 5)], np.float32)
def lin(h): return hexrgb(h) ** 2.2
def norm01(a): a = a - a.min(); return a / (a.max() + 1e-9)
def smooth(e0, e1, x): t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def fbm(shape, beta, seed, fmin=1.0, fmax=None, aniso=(1.0, 1.0)):
    """周期分形噪声（H, W），频率以“每张贴图周期数”计。"""
    H, W = shape
    fx = np.fft.fftfreq(W)[None, :] * W; fy = np.fft.fftfreq(H)[:, None] * H
    f = np.sqrt((fx * aniso[0]) ** 2 + (fy * aniso[1]) ** 2)
    amp = np.where(f >= fmin, 1.0 / np.maximum(f, 1e-9) ** (beta / 2), 0.0)
    if fmax: amp = amp * np.exp(-(f / fmax) ** 2)
    rng = np.random.default_rng(seed)
    return norm01(np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((H, W))) * amp))).astype(np.float32)

def blur(a, r):
    H, W = a.shape
    fx = np.fft.fftfreq(W)[None, :]; fy = np.fft.fftfreq(H)[:, None]
    k = np.exp(-(fx ** 2 + fy ** 2) * (np.pi * r) ** 2 * 2)
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k)).astype(np.float32)

def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    n = np.stack([-dx, dy, np.ones_like(h)], -1)        # 图像行 0 在上；OpenGL G 朝上
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5

def save(name, base_lin, smooth_, metal, height, nstr, normal=None):
    pre = os.path.join(OUT, f"{name}_Atlas")
    Image.fromarray((np.clip(base_lin, 0, 1) ** (1 / 2.2) * 255 + 0.5).astype(np.uint8)).save(pre + "_BaseColor.png")
    nm = normal if normal is not None else normal_from_height(height, nstr)
    Image.fromarray((np.clip(nm, 0, 1) * 255 + 0.5).astype(np.uint8)).save(pre + "_Normal.png")
    z = np.zeros_like(smooth_)
    Image.fromarray((np.clip(np.stack([metal, z, z, smooth_], -1), 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(pre + "_MS.png")
    m = (np.clip(base_lin, 0, 1) ** (1 / 2.2)).reshape(-1, 3).mean(0)
    print(f"wrote {pre}  mean sRGB #{''.join(f'{int(c*255):02X}' for c in m)}", flush=True)

def stamp_disc(acc, cy, cx, r, val, H, W):
    """把一个略不规则的圆粒（周期包裹）写进 acc（取最大）。"""
    R = int(np.ceil(r)) + 1
    yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
    ang = np.arctan2(yy, xx)
    rr = r * (1 + 0.18 * np.sin(3 * ang + val * 7) + 0.1 * np.cos(5 * ang + val * 3))
    m = (xx ** 2 + yy ** 2) <= rr ** 2
    iy = (cy + yy[m]) % H; ix = (cx + xx[m]) % W
    acc[iy, ix] = val

# =====================================================================================
# 1. 水磨石 2048² = 2 m
# =====================================================================================
def terrazzo(N=2048):
    S = (N, N); rng = np.random.default_rng(7)
    pxm = N / C.TERRAZZO_TILE_M                            # 1024 px/m
    # 石子：三档粒径（mm），按顺序盖（小粒后盖，填缝）
    chip_id = np.full(S, -1, np.int32); chip_val = np.zeros(S, np.float32)
    palette = [("#E4DFD4", 0.30), ("#CFCAC0", 0.22), ("#9D9A92", 0.16), ("#5B5955", 0.12), ("#CDB99C", 0.10), ("#8F9A8C", 0.03), ("#A88677", 0.02)]
    cols = np.array([lin(h) for h, _ in palette]); probs = np.array([p for _, p in palette]); probs /= probs.sum()
    tone = np.zeros(S, np.float32)
    for (rmin, rmax, count) in ((3.0, 6.0, 22000), (1.5, 3.0, 70000), (0.6, 1.5, 160000)):
        for _ in range(count):
            cy, cx = rng.integers(0, N, 2); r = rng.uniform(rmin, rmax) * pxm / 1000
            k = rng.choice(len(palette), p=probs); v = rng.random()
            R = int(np.ceil(r * 1.4)) + 1
            yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
            th = rng.uniform(0, np.pi); asp = rng.uniform(0.55, 1.0)       # 碎石：扁、带棱角（超椭圆 + 随机切角）
            xr = xx * np.cos(th) + yy * np.sin(th); yr = (-xx * np.sin(th) + yy * np.cos(th)) / asp
            pw = rng.uniform(1.2, 2.2)
            m = (np.abs(xr) / r) ** pw + (np.abs(yr) / r) ** pw <= 1.0
            a2 = rng.uniform(0, 2 * np.pi); m &= (xx * np.cos(a2) + yy * np.sin(a2)) <= r * rng.uniform(0.5, 1.0)
            iy = (cy + yy[m]) % N; ix = (cx + xx[m]) % N
            chip_id[iy, ix] = k; tone[iy, ix] = v
    is_chip = chip_id >= 0
    print(f"terrazzo chips cover {is_chip.mean():.0%}", flush=True)
    sand = fbm(S, 0.4, 70, fmin=300)
    matrix = lin("#B2AD9F") * (0.9 + 0.2 * sand[..., None])
    base = matrix.copy()
    cc = cols[np.clip(chip_id, 0, None)] * (0.9 + 0.2 * tone[..., None])
    base[is_chip] = cc[is_chip]
    # 分格条：1 m 一道（贴图 0 与 0.5 处），3 mm 宽，黄铜旧色
    u = (np.arange(N) / N * C.TERRAZZO_TILE_M) % 1.0
    du = np.minimum(u, 1 - u) * 1000                       # 到分格线的距离 mm
    strip_1d = (du <= 1.6).astype(np.float32)
    groove_1d = smooth(4.0, 1.6, du)                       # 分格条两侧略凹（磨石机磨不到）
    strip = np.maximum(strip_1d[None, :], strip_1d[:, None])
    groove = np.maximum(groove_1d[None, :], groove_1d[:, None])
    base = base * (1 - strip[..., None]) + (lin("#6E6048") * (0.8 + 0.3 * sand[..., None])) * strip[..., None]
    # 年代：大块污渍（低频）、抛光差异、细划痕、发丝裂、小油点、崩粒小坑
    grime = fbm(S, 2.6, 71, fmin=1)
    g2 = smooth(0.45, 0.9, grime)
    dull = fbm(S, 2.2, 72, fmin=2)
    base = base * (1 - 0.18 * g2[..., None])
    base = base * (1 - 0.25 * groove[..., None] * (1 - strip[..., None]))   # 缝边积灰
    smooth_ = 0.40 + 0.10 * (dull - 0.5) - 0.18 * g2 - 0.15 * groove
    smooth_ = np.where(is_chip, smooth_ + 0.05, smooth_)
    height = np.zeros(S, np.float32) - 0.6 * groove - 0.3 * strip * 0
    # 细划痕：随机线段（推车、拖铁件）
    scratch = np.zeros(S, np.float32)
    for _ in range(260):
        y0, x0 = rng.uniform(0, N, 2); ang = rng.normal(0.0, 0.5) + (np.pi / 2 if rng.random() < 0.5 else 0)
        L = rng.uniform(20, 260); t = np.linspace(0, L, int(L * 1.5))
        ys = ((y0 + np.sin(ang) * t + rng.normal(0, 0.4, t.size).cumsum() * 0.1) % N).astype(int)
        xs = ((x0 + np.cos(ang) * t) % N).astype(int)
        scratch[ys, xs] = np.maximum(scratch[ys, xs], rng.uniform(0.3, 1.0))
    scratch = np.clip(blur(scratch, 0.6) * 2.5, 0, 1)
    base = base * (1 + 0.10 * scratch[..., None]); smooth_ -= 0.15 * scratch; height -= 0.4 * scratch
    # 发丝裂：随机游走，多从分格条出发
    crack = np.zeros(S, np.float32)
    for _ in range(9):
        y, x = rng.uniform(0, N, 2)
        if rng.random() < 0.6: x = rng.choice([0, N / 2]) + rng.normal(0, 3)
        ang = rng.uniform(0, 2 * np.pi)
        for _s in range(int(rng.uniform(120, 420))):
            ang += rng.normal(0, 0.25); y += np.sin(ang); x += np.cos(ang)
            crack[int(y) % N, int(x) % N] = 1
    crack = np.clip(blur(crack, 0.5) * 3, 0, 1)
    base = base * (1 - 0.45 * crack[..., None]); height -= 1.2 * crack; smooth_ -= 0.2 * crack
    # 崩粒小坑（石子掉了）：深色 + 凹
    pits = np.zeros(S, np.float32)
    for _ in range(140):
        cy, cx = rng.integers(0, N, 2); r = rng.uniform(1.0, 3.5)
        R = int(r) + 1; yy, xx = np.mgrid[-R:R + 1, -R:R + 1]; m = xx ** 2 + yy ** 2 <= r * r
        pits[(cy + yy[m]) % N, (cx + xx[m]) % N] = 1
    base = base * (1 - 0.55 * pits[..., None]); height -= 1.5 * pits; smooth_ -= 0.3 * pits
    # 小油点：圆斑 1–5 cm，压暗、更光滑（油膜）
    oil = np.zeros(S, np.float32)
    for _ in range(14):
        cy, cx = rng.integers(0, N, 2); r = rng.uniform(8, 45)
        R = int(r * 1.6) + 1; yy, xx = np.mgrid[-R:R + 1, -R:R + 1]
        d = np.sqrt(xx ** 2 + yy ** 2) / r
        w = np.clip(1.25 - d, 0, 1) ** 0.7 * rng.uniform(0.3, 0.8)
        iy = (cy + yy) % N; ix = (cx + xx) % N
        oil[iy, ix] = np.maximum(oil[iy, ix], w)
    oil = oil * (0.7 + 0.3 * fbm(S, 1.5, 73, fmin=20))
    base = base * (1 - 0.35 * oil[..., None]) * (1 - np.array([0.0, 0.02, 0.06]) * oil[..., None])
    smooth_ += 0.25 * oil
    height += 0.15 * fbm(S, 1.0, 74, fmin=150) * (1 - strip)
    smooth_ = np.clip(smooth_, 0.08, 0.9)
    metal = 0.5 * strip
    return base, smooth_, metal, height

# =====================================================================================
# 2. 墙裙 2048×1024 = 2 m × 1.2 m（V 0 = 地面，V 1 = 1.2 m）
# =====================================================================================
def wainscot(W=2048, H=1024):
    S = (H, W)
    vy = (np.arange(H)[::-1] + 0.5) / H * 1.2               # 行 0 = 顶（1.2 m）
    Y = np.broadcast_to(vy[:, None], S)
    brush = fbm(S, 1.6, 81, fmin=3, aniso=(1.0, 0.15))      # 横向刷痕
    brush2 = fbm(S, 1.8, 82, fmin=6, aniso=(0.2, 1.0))
    grime = fbm(S, 2.5, 83, fmin=1)
    top = lin("#5E7D6A") * (0.9 + 0.16 * brush[..., None]) * (0.95 + 0.08 * brush2[..., None])
    under = lin("#D3CCB8") * (0.92 + 0.1 * brush2[..., None])   # 底层白灰
    # 崩漆：多在下部（踢碰）和局部
    h = fbm(S, 2.7, 84, fmin=3) * 0.85 + fbm(S, 1.8, 85, fmin=15, fmax=300) * 0.15
    bias = 0.12 * smooth(0.35, 0.0, Y) + 0.05 * smooth(1.0, 1.15, Y)
    chip = smooth(0.80, 0.805, h + bias)
    base = top * (1 - chip[..., None]) + under * chip[..., None]
    # 踢脚：离地 15 cm 发黑、拖把水渍线、鞋底黑印
    kick = smooth(0.18, 0.0, Y)
    base = base * (1 - 0.35 * kick[..., None] * (0.6 + 0.4 * grime[..., None]))
    rng = np.random.default_rng(86)
    marks = np.zeros(S, np.float32)
    for _ in range(18):
        y = H - 1 - int(rng.uniform(0.03, 0.4) / 1.2 * H); x = rng.integers(0, W); L = int(rng.uniform(15, 90))
        t = np.arange(L); yy = (y + (t * rng.normal(0, 0.15)).astype(int)) % H; xx = (x + t) % W
        marks[yy, xx] = rng.uniform(0.4, 1)
    marks = np.clip(blur(marks, 2.5) * 6, 0, 1)
    base = base * (1 - 0.4 * marks[..., None])
    base = base * (1 - 0.15 * smooth(0.5, 0.95, grime)[..., None])
    # 上沿 30 mm 深色线（手刷，边缘轻微起伏）
    wob = (fbm((1, W), 1.8, 87, fmin=2)[0] - 0.5) * 0.004
    line = smooth(1.168, 1.171, Y + wob[None, :])
    base = base * (1 - line[..., None]) + (lin("#2B332D") * (0.9 + 0.15 * brush[..., None])) * line[..., None]
    smooth_ = 0.30 + 0.08 * brush - 0.12 * chip - 0.1 * kick + 0.05 * line
    height = 0.3 * brush2 + 0.6 * (1 - chip) + 0.2 * line
    return base, np.clip(smooth_, 0.05, 1), np.zeros(S, np.float32), height

# =====================================================================================
# 3. 白灰墙 / 混凝土 / 钢漆
# =====================================================================================
def plaster(N=1024):
    S = (N, N)
    b = fbm(S, 1.7, 91, fmin=3); g = fbm(S, 2.6, 92, fmin=1)
    streak = fbm(S, 1.5, 93, fmin=2, aniso=(1.0, 0.05))       # 竖向水渍
    patch = smooth(0.72, 0.76, fbm(S, 2.8, 94, fmin=2))       # 补过的灰（更白）
    base = lin("#D2C8AF") * (0.9 + 0.12 * b[..., None])
    base = base * (1 - 0.18 * smooth(0.55, 0.95, streak)[..., None]) * (1 - 0.12 * smooth(0.5, 1, g)[..., None])
    base = base * (1 - patch[..., None]) + lin("#D8D0BC") * (0.95 + 0.05 * b[..., None]) * patch[..., None]
    return base, np.full(S, 0.1, np.float32), np.zeros(S, np.float32), 0.6 * b + 0.3 * patch

def concrete(N=1024):
    S = (N, N); rng = np.random.default_rng(101)
    b = fbm(S, 1.9, 102, fmin=2); g = fbm(S, 2.6, 103, fmin=1); fine = fbm(S, 0.6, 104, fmin=100)
    base = lin("#A8A59C") * (0.88 + 0.18 * b[..., None]) * (0.95 + 0.08 * fine[..., None]) * (1 - 0.2 * smooth(0.5, 1, g)[..., None])
    holes = np.zeros(S, np.float32)
    for _ in range(220):
        cy, cx = rng.integers(0, N, 2); r = rng.uniform(0.8, 2.5)
        R = int(r) + 1; yy, xx = np.mgrid[-R:R + 1, -R:R + 1]; m = xx ** 2 + yy ** 2 <= r * r
        holes[(cy + yy[m]) % N, (cx + xx[m]) % N] = 1
    base = base * (1 - 0.5 * holes[..., None])
    return base, np.full(S, 0.14, np.float32) + 0.05 * fine, np.zeros(S, np.float32), 0.4 * b + 0.3 * fine - 1.0 * holes

def steelpaint(N=1024):
    S = (N, N)
    b = fbm(S, 1.6, 111, fmin=3); g = fbm(S, 2.6, 112, fmin=1)
    h = fbm(S, 2.6, 113, fmin=3) * 0.85 + fbm(S, 1.6, 114, fmin=20, fmax=250) * 0.15
    rust = smooth(0.84, 0.86, h)
    base = lin("#56615A") * (0.9 + 0.15 * b[..., None]) * (1 - 0.2 * smooth(0.5, 1, g)[..., None])
    base = base * (1 - rust[..., None]) + lin("#6B4A35") * (0.8 + 0.3 * b[..., None]) * rust[..., None]
    sm = 0.38 + 0.08 * b - 0.25 * rust
    return base, sm, 0.25 * rust, 0.3 * b + 0.8 * (1 - rust)

# =====================================================================================
# 4. 安全线（1 m × 0.1 m）、斑马线（1 m，条纹沿 x+y 方向周期 0.25 m）
# =====================================================================================
def safety(W=1024, H=128):
    S = (H, W)
    b = fbm(S, 1.5, 121, fmin=2, aniso=(0.3, 1.0)); h = fbm(S, 2.4, 122, fmin=2)
    wear = smooth(0.6, 0.66, h * 0.8 + 0.2 * fbm(S, 1.2, 123, fmin=30))
    v = (np.arange(H) + 0.5) / H; edge = np.minimum(v, 1 - v)[:, None] * 100        # mm
    ragged = smooth(0.0, 6.0, edge + (fbm(S, 1.4, 124, fmin=8) - 0.5) * 6)       # 边缘不齐
    wear = np.clip(wear + (1 - ragged) * 0.7, 0, 1)
    base = lin("#F2C200") * (0.85 + 0.15 * b[..., None]) * 0.92
    floor_col = lin("#A9A497") * (0.9 + 0.15 * b[..., None])
    base = base * (1 - wear[..., None]) + floor_col * wear[..., None]
    base = base * (1 - 0.18 * smooth(0.5, 1, fbm(S, 2.5, 125, fmin=1))[..., None])
    return base, 0.35 - 0.1 * wear, np.zeros(S, np.float32), 1.0 - 0.6 * wear

def zebra(N=1024):
    S = (N, N)
    y, x = np.mgrid[0:N, 0:N] / N
    p = ((x + (1 - y)) / 0.25) % 1.0                         # 4 个周期 / 米，四边无缝
    yellow = smooth(0.02, 0.05, p) * smooth(0.52, 0.49, p)
    b = fbm(S, 1.5, 131, fmin=2); h = fbm(S, 2.4, 132, fmin=2)
    wear = smooth(0.66, 0.72, h * 0.8 + 0.2 * fbm(S, 1.2, 133, fmin=30))
    base = lin("#F2C200") * 0.9 * yellow[..., None] + lin("#1A1A1A") * (1 - yellow[..., None])
    base = base * (0.88 + 0.15 * b[..., None])
    base = base * (1 - wear[..., None]) + lin("#A9A497") * wear[..., None]
    return base, 0.35 - 0.1 * wear, np.zeros(S, np.float32), 1.0 - 0.6 * wear

# =====================================================================================
# 5. 机床脚下油污贴图集（2048²）
# =====================================================================================
def rect_dist(X, Y, x0, x1, y0, y1):
    dx = np.maximum(np.maximum(x0 - X, X - x1), 0); dy = np.maximum(np.maximum(y0 - Y, Y - y1), 0)
    return np.sqrt(dx ** 2 + dy ** 2)

def stain_field(X, Y, name, rng_seed, wob):
    """返回（油污 0..1，铁屑亮点 0..1，磨亮 0..1）。X, Y = 世界米；wob = 0..1 噪声（让油浸边缘不规则）。"""
    oil = np.zeros_like(X); swarf = np.zeros_like(X); polish = np.zeros_like(X)
    rng = np.random.default_rng(rng_seed)
    def blob(cx, cy, r, k):
        d = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2) / r
        return k * np.clip(1.2 - d, 0, 1) ** 0.8
    if name.startswith("LT"):
        x0 = C.LATHES[name]; y = C.LATHE_Y
        legs = [(x0 - 0.01, x0 + 0.71, y - 0.36, y + 0.36), (x0 + 2.09, x0 + 2.66, y - 0.34, y + 0.34)]
        for r in legs:
            d = rect_dist(X, Y, *r)
            oil = np.maximum(oil, 0.85 * np.exp(-np.clip(d + 0.07 * (wob - 0.5), 0, None) / 0.10))
        d = rect_dist(X, Y, x0 + 0.7, x0 + 2.1, y - 0.35, y + 0.35)      # 接屑盘下滴油
        oil = np.maximum(oil, 0.7 * np.exp(-np.clip(d + 0.07 * (wob - 0.5), 0, None) / 0.08))
        for _ in range(7):                                                # 操作侧（南）滴油点
            oil = np.maximum(oil, blob(x0 + rng.uniform(0.9, 2.2), y - rng.uniform(0.40, 0.75), rng.uniform(0.04, 0.12), rng.uniform(0.4, 0.8)))
        oil = np.maximum(oil, blob(x0 + rng.uniform(1.3, 1.9), y - 0.55, 0.30, 0.45))   # 一摊旧油
        d = rect_dist(X, Y, x0 + 0.7, x0 + 2.1, y - 0.65, y - 0.35)       # 铁屑：接屑盘前沿掉下来的
        swarf = np.exp(-d / 0.12)
        if name == "LT01":                                                # 操作位：几十年的站位磨亮（规格 §4：操作者站 Z 10.1）
            d = rect_dist(X, Y, x0 + 1.1, x0 + 2.1, y - 1.05, y - 0.7)
            polish = np.exp(-(d / 0.25) ** 2)
    elif name == "Drill":
        d = rect_dist(X, Y, 15.6, 16.4, 2.7, 3.3); oil = 0.8 * np.exp(-d / 0.12)
        oil = np.maximum(oil, blob(16.0, 2.45, 0.25, 0.5)); swarf = np.exp(-rect_dist(X, Y, 15.7, 16.3, 2.4, 2.7) / 0.1)
    elif name == "Grinder":
        d = rect_dist(X, Y, 18.85, 19.15, 2.35, 2.65); oil = 0.5 * np.exp(-d / 0.1)
        for wx in (18.75, 19.25):                                          # 砂轮下方往南的磨屑扇面（铁灰）
            dx = X - wx; dy = 2.4 - Y
            fan = np.exp(-(dx / (0.05 + 0.25 * np.clip(dy, 0, None))) ** 2) * np.exp(-np.clip(dy, 0, None) / 0.35) * (dy > -0.05)
            swarf = np.maximum(swarf, fan)
    return np.clip(oil, 0, 1), np.clip(swarf, 0, 1), np.clip(polish, 0, 1)

def stain_atlas(tz):
    base_t, sm_t, mt_t, h_t = tz
    N = base_t.shape[0]; A = C.STAIN_ATLAS
    # 贴图行 0 = V 1（图像顶）；这里按 V 向上建，存的时候翻转
    base = np.zeros((A, A, 3), np.float32); sm = np.zeros((A, A), np.float32); mt = np.zeros((A, A), np.float32); hh = np.zeros((A, A), np.float32)
    for k, pad in enumerate(C.STAIN_PADS):
        name, X0, Y0, Wm, Hm, px, py, rw, rh = pad
        g = C.STAIN_GUTTER
        ii, jj = np.mgrid[0:rh, 0:rw]
        X = X0 + np.clip((jj + 0.5 - g) / (rw - 2 * g), 0, 1 - 1e-6) * Wm
        Y = Y0 + np.clip((ii + 0.5 - g) / (rh - 2 * g), 0, 1 - 1e-6) * Hm
        dens = (rw - 2 * g) / Wm                                         # px/m
        ci = np.floor(X).astype(int); cj = np.floor(Y).astype(int)
        s = X - ci; t = Y - cj
        tu = np.zeros_like(X); tv = np.zeros_like(X)
        for (a, b) in set(zip(ci.ravel().tolist(), cj.ravel().tolist())):
            m = (ci == a) & (cj == b)
            qx, qy, o = C.cell_variant(a, b)
            s2, t2 = s[m], t[m]
            if o & 1: s2, t2 = t2, s2
            if o & 2: s2 = 1 - s2
            if o & 4: t2 = 1 - t2
            tu[m] = (qx + s2) / 2; tv[m] = (qy + t2) / 2
        # 盒式下采样：按密度比例取平均（1024 → dens px/m）
        f = max(1, int(round(1024 / dens)))
        def ds(a):
            if f == 1: return a
            sh = (N // f, f, N // f, f) + a.shape[2:]
            return a.reshape(sh).mean(axis=(1, 3))
        bt, st, mtt, ht = ds(base_t), ds(sm_t), ds(mt_t), ds(h_t)
        n2 = N // f
        # 平铺图行 0 = V 1 → 行号 = (1 − v)
        iy = np.clip(((1 - tv) * n2).astype(int), 0, n2 - 1); ix = np.clip((tu * n2).astype(int), 0, n2 - 1)
        B = bt[iy, ix]; Sm = st[iy, ix]; Mt = mtt[iy, ix]; Hh = ht[iy, ix] * (dens / 1024)   # 高度按像素尺度修正，法线强度一致
        nz = fbm((rh, rw), 2.0, 600 + k, fmin=2) * 0.6 + fbm((rh, rw), 1.2, 700 + k, fmin=20) * 0.4
        oil, swarf, pol = stain_field(X, Y, name, 500 + k, fbm((rh, rw), 1.8, 650 + k, fmin=3))
        oil = np.clip(oil * (0.55 + 0.7 * nz), 0, 1)
        B = B * (1 - 0.62 * oil[..., None]) * (1 - np.array([0.0, 0.03, 0.08]) * oil[..., None])     # 油浸：深、略偏暖
        Sm = Sm + 0.3 * oil * (1 - oil) * 2 + 0.1 * oil                                          # 新油亮，老油污发闷
        sw_spk = (fbm((rh, rw), 0.3, 800 + k, fmin=rw / 4) > 0.62) * swarf * (nz > 0.35)
        B = B * (1 - 0.25 * swarf[..., None]) + lin("#8C8E8E") * 0.6 * sw_spk[..., None]
        Mt = np.maximum(Mt, 0.8 * sw_spk); Sm = Sm + 0.2 * sw_spk
        B = B * (1 + 0.08 * pol[..., None]); Sm = Sm + 0.15 * pol
        base[py:py + rh, px:px + rw] = B; sm[py:py + rh, px:px + rw] = np.clip(Sm, 0.05, 0.92)
        mt[py:py + rh, px:px + rw] = Mt; hh[py:py + rh, px:px + rw] = Hh * (1 - 0.6 * oil)
        print(f"stain pad {name}: {dens:.0f} px/m", flush=True)
    flip = lambda a: a[::-1].copy()
    return flip(base), flip(sm), flip(mt), flip(hh)

if __name__ == "__main__":
    tz = terrazzo()
    save("LSB_ENV_BLD_Terrazzo", *tz, 1.5)
    save("LSB_ENV_BLD_FloorStain", *stain_atlas(tz), 1.5)
    save("LSB_ENV_BLD_Wainscot", *wainscot(), 2.0)
    save("LSB_ENV_BLD_Plaster", *plaster(), 2.0)
    save("LSB_ENV_BLD_Concrete", *concrete(), 2.0)
    save("LSB_ENV_BLD_SteelPaint", *steelpaint(), 2.0)
    save("LSB_ENV_BLD_SafetyLine", *safety(), 2.0)
    save("LSB_ENV_BLD_Zebra", *zebra(), 2.0)

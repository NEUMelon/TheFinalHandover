# 《最后一次交班》Stage 3 · 可平铺年代叠层贴图生成（numpy 程序生成，由 Claude 编写，已登记 AI 使用日志）
# 运行：python3 gen_textures.py <输出目录> [尺寸=1024]
# 每张贴图 1 张 = 1 m × 1 m（与模型的 1 m 立方体投影 UV 对应，≥1024 px/m）。所有噪声都用周期频谱生成，四边无缝。
# 每个材质输出（漆面另有 _Mask 供烘焙按磨损动线控制剥落）：_BaseColor（sRGB）、_Normal（OpenGL，G 朝上）、_ORM（R=AO, G=粗糙度, B=金属度）、_MS（URP Lit：R=金属度, A=光滑度）。
# 叠层依据：美术圣经 §4 / 场景主产规格 §2.4：L0 铸铁 → L1 1956 苏式灰绿 → L2 1983 墙裙绿（降饱和）→ 磨亮 / 包浆。
import sys, os
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
os.makedirs(OUT, exist_ok=True)

fx = np.fft.fftfreq(N)[None, :] * N
fy = np.fft.fftfreq(N)[:, None] * N

def norm01(a):
    a = a - a.min(); return a / (a.max() + 1e-9)

def fbm(beta, seed, fmin=1.0, fmax=None, aniso=(1.0, 1.0)):
    """周期分形噪声：功率谱 ∝ 1/f^beta；aniso 拉伸频率 → 笔触 / 车削纹。"""
    rng = np.random.default_rng(seed)
    f = np.sqrt((fx * aniso[0]) ** 2 + (fy * aniso[1]) ** 2)
    amp = np.where(f >= fmin, 1.0 / np.maximum(f, 1e-9) ** (beta / 2), 0.0)
    if fmax: amp *= np.exp(-(f / fmax) ** 2)
    ph = np.fft.fft2(rng.standard_normal((N, N)))
    return norm01(np.real(np.fft.ifft2(ph * amp)))

def blur(a, r):
    k = np.exp(-(fx ** 2 + fy ** 2) * (np.pi * r / N) ** 2 * 2)
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def hexrgb(h): return np.array([int(h[i:i+2], 16) / 255 for i in (1, 3, 5)])

def lin(c): return c ** 2.2
def srgb(c): return np.clip(c, 0, 1) ** (1 / 2.2)

def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    n = np.stack([-dx, dy, np.ones_like(h)], -1)          # OpenGL：图像向上 = +V，行号增大是向下，所以 dy 取正号
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5

def save(name, base_lin, rough, metal, ao, height, nstr):
    pre = os.path.join(OUT, f"T_{name}")
    Image.fromarray((srgb(base_lin) * 255 + 0.5).astype(np.uint8)).save(pre + "_BaseColor.png")
    Image.fromarray((normal_from_height(height, nstr) * 255 + 0.5).astype(np.uint8)).save(pre + "_Normal.png")
    orm = np.stack([ao, rough, metal], -1)
    Image.fromarray((np.clip(orm, 0, 1) * 255 + 0.5).astype(np.uint8)).save(pre + "_ORM.png")
    ms = np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), 1 - rough], -1)
    Image.fromarray((np.clip(ms, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(pre + "_MS.png")
    print("wrote", pre)

# ---------------------------------------------------------------- 共用层
grime = fbm(2.6, 11, fmin=1)                         # 大块污渍 / 包浆
speck = fbm(0.6, 12, fmin=40)                        # 细颗粒
brush = fbm(1.6, 13, fmin=3, aniso=(0.12, 1.0))      # 竖向刷痕（手刷漆、手绘笔触）
pit = fbm(1.2, 14, fmin=60)                          # 铸铁砂眼

def iron_layer(seed=21):
    rust = smooth(0.62, 0.8, fbm(2.2, seed, fmin=2) * 0.7 + speck * 0.3)
    base = lin(hexrgb("#3A3D3B")) * (0.85 + 0.3 * pit[..., None])
    base = base * (1 - rust[..., None]) + lin(hexrgb("#5A4232")) * rust[..., None]
    rough = 0.62 + 0.25 * pit + 0.15 * rust
    metal = 0.75 * (1 - rust)
    return base, rough, metal, rust

def painted(name, top_hex, mid_hex, chip_amt, seed, wear_bias=0.0, rough_top=0.55):
    """三层漆：top（最外层）/ mid（下层旧漆）/ 铸铁。chip_amt 越大剥落越多。"""
    grime = fbm(2.6, seed + 7, fmin=1)
    brush = fbm(1.6, seed + 8, fmin=3, aniso=(0.12, 1.0))
    h = fbm(2.8, seed, fmin=3) * 0.86 + fbm(1.9, seed + 1, fmin=12, fmax=220) * 0.14 + wear_bias   # 成片剥落，边缘略碎
    t1 = np.quantile(h, 1 - chip_amt)            # 外层剥落面积 = chip_amt
    t2 = np.quantile(h, 1 - chip_amt * 0.3)      # 露铁面积 = chip_amt × 0.3
    c1 = smooth(t1 - 0.004, t1 + 0.004, h)
    c2 = smooth(t2 - 0.004, t2 + 0.004, h)
    ib, ir, im, rust = iron_layer(seed + 5)
    top = lin(hexrgb(top_hex)) * (0.88 + 0.22 * brush[..., None]) * (0.92 + 0.08 * speck[..., None])
    mid = lin(hexrgb(mid_hex)) * (0.95 + 0.08 * brush.T[..., None])          # 下层刷痕方向不同
    base = top * (1 - c1[..., None]) + mid * c1[..., None]
    base = base * (1 - c2[..., None]) + ib * c2[..., None]
    g = smooth(0.45, 0.95, grime)[..., None]
    base = base * (1 - 0.22 * g)                                              # 污渍 / 包浆压暗
    rough = rough_top + 0.12 * brush - 0.1 * smooth(0.5, 1, grime)            # 包浆处更亮
    rough = rough * (1 - c1) + 0.6 * c1
    rough = rough * (1 - c2) + ir * c2
    metal = im * c2
    height = 1.0 - 0.35 * c1 - 0.5 * c2 + 0.05 * brush
    edge = np.abs(blur(c1, 1.5) - c1) * 2                                     # 漆皮崩边一圈压暗
    ao = np.clip(1 - 0.5 * blur(c2, 3) * (1 - c2) - 0.6 * edge, 0, 1)
    base *= ao[..., None] ** 0.5
    save(name, base, np.clip(rough, 0.05, 1), metal, ao, height, 9.0)
    # 烘焙用遮罩（R = 剥落序位：值 > 1−面积 即剥落，面积可在着色器里按磨损动线调；G = 刷痕；B = 污渍）
    rank = np.empty(N * N); rank[np.argsort(h, axis=None)] = np.linspace(0, 1, N * N)
    mask = np.stack([rank.reshape(N, N), brush, grime], -1)
    Image.fromarray((mask * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, f"T_{name}_Mask.png"))

# ---------------------------------------------------------------- 材质
# LT01 机身漆：1983 墙裙绿（降饱和 15%）盖在 1956 灰绿上，局部露铁
painted("LT01_Paint_1983Green", "#5F7F72", "#8A968C", 0.08, 100)
# 护罩：1956 灰绿原漆（1983 技改没重漆护罩），剥落更多，露铁
painted("LT01_Guard_1956Grey", "#6E7A72", "#4A564F", 0.14, 200)
# 继电器柜体：1956 灰绿，门边磨亮由模型分材质表达，这里少量剥落
painted("CAB_Body_1956GreyGreen", "#6E7A72", "#59645D", 0.06, 300, rough_top=0.5)

# 铸铁（床脚底座、手轮、挂轮罩螺钉等）
ib, ir, im, rust = iron_layer(400)
save("LT01_CastIron", ib * (1 - 0.3 * smooth(0.5, 1, grime))[..., None], ir, im, np.clip(1 - 0.3 * pit, 0, 1), pit * 0.6 + 0.4 * grime, 4.0)

# 机加工钢（导轨面、卡盘、丝杠）：细车削纹 + 油膜
turn = fbm(0.8, 500, fmin=80, aniso=(1.0, 0.02))      # 横向细纹
oil = smooth(0.4, 0.9, fbm(2.4, 501, fmin=1))
base = lin(hexrgb("#9EA3A3")) * (0.9 + 0.12 * turn[..., None]) * (1 - 0.25 * oil[..., None])
save("LT01_MachinedSteel", base, np.clip(0.28 + 0.12 * turn - 0.12 * oil + 0.1 * speck, 0.05, 1), np.full((N, N), 1.0), np.ones((N, N)), turn * 0.3, 2.0)

# 磨亮金属（手轮把手、手柄、L1 周围）：几十年手掌磨出的镜面 + 细划痕
scr = fbm(0.5, 600, fmin=120, aniso=(1.0, 0.05)) * 0.6 + fbm(0.5, 601, fmin=120, aniso=(0.05, 1.0)) * 0.4
base = lin(hexrgb("#C9C6BC")) * (0.92 + 0.1 * scr[..., None])
save("LT01_WornBright", base, np.clip(0.14 + 0.18 * scr, 0.05, 1), np.full((N, N), 1.0), np.ones((N, N)), scr * 0.2, 1.5)

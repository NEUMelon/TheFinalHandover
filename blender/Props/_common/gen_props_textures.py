# 《最后一次交班》Stage 3 · 道具组可平铺贴图（numpy 程序生成，由 Claude 编写，已登记 AI 使用日志）
# 运行：python3 gen_props_textures.py <输出目录> [尺寸=1024]
# 约定与 Textures/gen_textures.py 相同：1 张 = 1 m × 1 m，四边无缝；输出 _BaseColor / _Normal / _ORM / _MS，漆面另出 _Mask（R 剥落序位 / G 刷痕 / B 污渍）。
# 这里只补道具需要、而车床贴图里没有的材质：清漆木、书皮布、旧纸页、深色塑料、喇叭网、漆面遮罩。
import sys, os
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
os.makedirs(OUT, exist_ok=True)
fx = np.fft.fftfreq(N)[None, :] * N
fy = np.fft.fftfreq(N)[:, None] * N

def norm01(a): a = a - a.min(); return a / (a.max() + 1e-9)
def fbm(beta, seed, fmin=1.0, fmax=None, aniso=(1.0, 1.0)):
    rng = np.random.default_rng(seed)
    f = np.sqrt((fx * aniso[0]) ** 2 + (fy * aniso[1]) ** 2)
    amp = np.where(f >= fmin, 1.0 / np.maximum(f, 1e-9) ** (beta / 2), 0.0)
    if fmax: amp *= np.exp(-(f / fmax) ** 2)
    return norm01(np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((N, N))) * amp)))
def smooth(e0, e1, x): t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def hexrgb(h): return np.array([int(h[i:i+2], 16) / 255 for i in (1, 3, 5)])
def lin(c): return c ** 2.2
def srgb(c): return np.clip(c, 0, 1) ** (1 / 2.2)
def nrm(h, s):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * s; dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * s
    n = np.stack([-dx, dy, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True); return n * 0.5 + 0.5
def save(name, base, rough, metal, ao, height, nstr):
    pre = os.path.join(OUT, f"T_{name}")
    Image.fromarray((srgb(base) * 255 + 0.5).astype(np.uint8)).save(pre + "_BaseColor.png")
    Image.fromarray((nrm(height, nstr) * 255 + 0.5).astype(np.uint8)).save(pre + "_Normal.png")
    Image.fromarray((np.clip(np.stack([ao, rough, metal], -1), 0, 1) * 255 + 0.5).astype(np.uint8)).save(pre + "_ORM.png")
    ms = np.stack([metal, 0 * metal, 0 * metal, 1 - rough], -1)
    Image.fromarray((np.clip(ms, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(pre + "_MS.png")
    print("wrote", pre, flush=True)
one = np.ones((N, N)); zero = np.zeros((N, N))
grime = fbm(2.6, 11, fmin=1); speck = fbm(0.6, 12, fmin=40)

# 1) 清漆木（交班桌）：木纹沿 V（三向投影里：桌面 = 南北向，桌腿 = 竖向）。年轮用扭曲的竖条，清漆膜上有包浆与细划痕
warp = fbm(2.4, 31, fmin=1) * 0.06
u = np.linspace(0, 1, N, endpoint=False)[None, :] + warp
ring = 0.5 + 0.5 * np.sin(2 * np.pi * (u * 38 + fbm(2.0, 32, fmin=2, aniso=(0.05, 1.0)) * 3.0))
ring = ring ** 3
pores = fbm(0.8, 33, fmin=60, aniso=(0.04, 1.0))
c0, c1 = lin(hexrgb("#4E2E1C")), lin(hexrgb("#6E4428"))   # 深栗色清漆（第一版偏浅偏橙，10-15 预览后压暗）
base = c0 * (1 - ring[..., None] * 0.55) + c1 * (ring[..., None] * 0.55)
base = base * (0.92 + 0.12 * pores[..., None]) * (1 - 0.18 * smooth(0.5, 1, grime)[..., None])
scr = fbm(0.5, 34, fmin=150, aniso=(1.0, 0.08))
save("PRP_WoodVarnish", base, np.clip(0.38 + 0.12 * pores + 0.12 * scr - 0.1 * smooth(0.5, 1, grime), 0.1, 1), zero, one, ring * 0.3 + pores * 0.4, 3.0)


# 1b) 手磨亮的木（听针木柄左手握处、交班桌东沿手肘处）：颜色浅一档、粗糙度低（包浆）
base = (c0 * (1 - ring[..., None] * 0.4) + c1 * (ring[..., None] * 0.4)) * 1.25 * (0.95 + 0.08 * pores[..., None])
save("PRP_WoodPolished", base, np.clip(0.22 + 0.08 * pores, 0.05, 1), zero, one, ring * 0.15, 1.5)

# 2) 书皮布（交班本硬壳布面，墨绿）：平纹编织 + 边角磨白由几何分材质，这里只做布纹与手汗污
k = np.arange(N); wx = (np.sin(2 * np.pi * k * 340 / N)[None, :] * 0.5 + 0.5); wy = (np.sin(2 * np.pi * k * 340 / N)[:, None] * 0.5 + 0.5)
weave = np.maximum(wx * (1 - wy) * 1.0, wy * (1 - wx) * 0.8)
base = lin(hexrgb("#2F4A3C")) * (0.82 + 0.25 * weave[..., None]) * (0.9 + 0.15 * fbm(1.4, 41, fmin=4)[..., None])
base = base * (1 - 0.25 * smooth(0.55, 1, grime)[..., None]) + lin(hexrgb("#6E7465")) * 0.15 * smooth(0.7, 1, fbm(2.2, 42, fmin=2))[..., None]
save("PRP_BookCloth_Green", base, np.clip(0.75 + 0.1 * weave - 0.15 * smooth(0.6, 1, grime), 0, 1), zero, one, weave, 2.0)

# 3) 旧纸（页块侧边、贴页）：发黄、纤维、水平细纹（页块侧面看到一层层纸）
lines = 0.5 + 0.5 * np.sin(2 * np.pi * k * 260 / N)[:, None] * one
fib = fbm(0.9, 51, fmin=30)
yel = fbm(2.4, 52, fmin=1)
base = lin(hexrgb("#D9CFB4")) * (0.9 + 0.08 * fib[..., None]) * (1 - 0.12 * lines[..., None])
base = base * (1 - yel[..., None] * 0.25) + lin(hexrgb("#B79D6E")) * yel[..., None] * 0.25
save("PRP_PaperAged", base, 0.85 + 0.1 * fib, zero, one, lines * 0.5 + fib * 0.3, 2.0)

# 4) 深色塑料（磁带壳、录音机机身、旋钮）：细橡皮纹 + 手摸处略亮
tex = fbm(0.7, 61, fmin=80)
base = lin(hexrgb("#26262A")) * (0.92 + 0.12 * tex[..., None]) * (1 + 0.12 * smooth(0.6, 1, grime)[..., None])
save("PRP_PlasticDark", base, np.clip(0.48 + 0.15 * tex - 0.12 * smooth(0.6, 1, grime), 0, 1), zero, one, tex * 0.3, 1.5)

# 5) 录音机面板拉丝铝 + 喇叭网孔（孔距 4 mm，与 1 m 投影对应）
brushed = fbm(0.6, 71, fmin=80, aniso=(1.0, 0.03))
yy, xx = np.mgrid[0:N, 0:N] / N
pitch = 0.004
gx = (xx % pitch) / pitch - 0.5; gy = ((yy + (np.floor(xx / pitch) % 2) * pitch / 2) % pitch) / pitch - 0.5
hole = smooth(0.30, 0.22, np.sqrt(gx ** 2 + gy ** 2))
base = lin(hexrgb("#A7A9A6")) * (0.9 + 0.1 * brushed[..., None]) * (1 - 0.85 * hole[..., None])
save("PRP_SpeakerGrille", base, np.clip(0.35 + 0.1 * brushed + 0.4 * hole, 0, 1), 1 - hole, 1 - 0.6 * hole, 1 - hole, 4.0)
base = lin(hexrgb("#A7A9A6")) * (0.9 + 0.1 * brushed[..., None])
save("PRP_BrushedAlu", base, np.clip(0.3 + 0.12 * brushed, 0, 1), one, one, brushed * 0.2, 1.0)

# 6) 漆面遮罩（更衣柜、工具箱、灯罩、台灯）：只出 _Mask，颜色由 lsb_bake.Painted 的 top / mid 决定
def mask(name, seed):
    brush = fbm(1.6, seed + 8, fmin=3, aniso=(0.12, 1.0)); gr = fbm(2.6, seed + 7, fmin=1)
    h = fbm(2.8, seed, fmin=3) * 0.86 + fbm(1.9, seed + 1, fmin=12, fmax=220) * 0.14
    rank = np.empty(N * N); rank[np.argsort(h, axis=None)] = np.linspace(0, 1, N * N)
    Image.fromarray((np.stack([rank.reshape(N, N), brush, gr], -1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, f"T_{name}_Mask.png"))
    print("wrote mask", name, flush=True)
mask("PRP_Paint_A", 700)
mask("PRP_Paint_B", 800)

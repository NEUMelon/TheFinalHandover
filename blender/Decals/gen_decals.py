# 《最后一次交班》Stage 3 · 贴花贴图（LT01 铭牌、联锁继电器红 / 白胶布手写字）。PIL + numpy 程序生成，由 Claude 编写，记入 AI 使用日志。
# 字体：工程已用的开源字体（SIL OFL 1.1）——铭牌用 Noto Serif SC Bold（印刷体 S 的顶替），胶布手写用 Long Cang（手写体 H 的顶替，待老周体 / 建国体手写采集后替换）。
# 文案：铭牌 = 虚构“HC-620 型 普通车床 1956”（工程里的字体是子集，没有“卧”字，所以用“普通车床”）（规格 §铭牌：无真实厂名）；红胶布“护罩 87”（1987 周铁生），白胶布“保留 08”（2008）。“护罩 87”字样待用户确认 U-08。
# 运行：python3 gen_decals.py <输出目录>
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
FONTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../02_母项目VS/demo_alpha/UnityProject/Assets/LSB/Fonts")
rng = np.random.default_rng(7)

def noise(h, w, scale):
    small = rng.random((max(2, h // scale), max(2, w // scale)))
    return np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255

def font(name, size): return ImageFont.truetype(os.path.join(FONTS, name), size)

# ---------------- LT01 铭牌：铝板蚀刻，黑底凸字 + 边框，角上四个铆钉孔，旧化：油污、划痕、边缘磨亮
W, H = 1024, 420
img = Image.new("L", (W, H), 0)          # 蚀刻遮罩：255 = 凸起铝面（字、边框）
d = ImageDraw.Draw(img)
d.rounded_rectangle([14, 14, W - 15, H - 15], 18, outline=255, width=10)
d.rounded_rectangle([40, 40, W - 41, H - 41], 10, outline=255, width=3)
d.text((W // 2, 150), "HC-620 型", font=font("NotoSerifSC-Bold.ttf", 120), fill=255, anchor="mm")
d.text((W // 2, 270), "普 通 车 床", font=font("NotoSerifSC-Bold.ttf", 64), fill=255, anchor="mm")
d.line([(170, 330), (W - 170, 330)], fill=255, width=3)
d.text((W // 2, 362), "1956", font=font("NotoSerifSC-Bold.ttf", 40), fill=255, anchor="mm")
m = np.asarray(img, dtype=np.float32) / 255
alu = np.array([0.66, 0.66, 0.63]); black = np.array([0.08, 0.08, 0.075])
n1, n2 = noise(H, W, 24), noise(H, W, 4)
grime = np.clip((noise(H, W, 90) - 0.45) * 2.2, 0, 1)
base = black * (1 - m[..., None]) + alu * m[..., None] * (0.85 + 0.25 * n2[..., None])
base = base * (1 - 0.45 * grime[..., None]) + np.array([0.18, 0.15, 0.10]) * 0.45 * grime[..., None]
for _ in range(25):                       # 细划痕
    x0, y0 = rng.integers(0, W), rng.integers(0, H); ang = rng.normal(0.2, 0.3); L = rng.integers(30, 220)
    xs = np.clip((x0 + np.cos(ang) * np.arange(L)).astype(int), 0, W - 1); ys = np.clip((y0 + np.sin(ang) * np.arange(L)).astype(int), 0, H - 1)
    base[ys, xs] = np.minimum(base[ys, xs] + 0.05, 1)
for cx, cy in ((28, 28), (W - 29, 28), (28, H - 29), (W - 29, H - 29)):
    yy, xx = np.ogrid[:H, :W]; base[(xx - cx) ** 2 + (yy - cy) ** 2 < 10 ** 2] = 0.05
Image.fromarray((np.clip(base, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).save(os.path.join(OUT, "LT01_Nameplate_BaseColor.png"))

# ---------------- 胶布：布基胶带，手写字，边缘毛、发黄、按指纹的污
def tape(fn, text, col, ink, yellow, size=512):
    S = size; HH = size * 7 // 16      # 胶布正面约 68 × 30 mm
    t = Image.new("RGB", (S, HH), tuple(int(c * 255) for c in col))
    arr = np.asarray(t, dtype=np.float32) / 255
    weave = 0.5 * noise(HH, S, 2) + 0.5 * (np.sin(np.arange(S) * 1.7)[None, :] * 0.5 + 0.5) * 0.3
    age = np.clip(noise(HH, S, 60) * yellow, 0, 1)
    arr = arr * (0.92 + 0.1 * weave[..., None])
    arr = arr * (1 - age[..., None] * 0.35) + np.array([0.55, 0.42, 0.18]) * age[..., None] * 0.35
    txt = Image.new("L", (S, HH), 0); dd = ImageDraw.Draw(txt)
    dd.text((S // 2 + rng.integers(-6, 6), HH // 2), text, font=font("LongCang-Regular.ttf", int(S * 0.2)), fill=255, anchor="mm")
    txt = txt.rotate(rng.normal(0, 3), resample=Image.BICUBIC).filter(ImageFilter.GaussianBlur(1.2))
    k = np.asarray(txt, dtype=np.float32) / 255 * (0.75 + 0.25 * noise(HH, S, 6))
    arr = arr * (1 - k[..., None]) + np.array(ink) * k[..., None]
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, fn))

tape("CAB_RedTape_1987_BaseColor.png", "护罩 87", (0.62, 0.13, 0.10), (0.08, 0.06, 0.05), 0.9)
tape("CAB_WhiteTape_2008_BaseColor.png", "保留 08", (0.86, 0.84, 0.78), (0.10, 0.12, 0.25), 0.6)
print("ok")

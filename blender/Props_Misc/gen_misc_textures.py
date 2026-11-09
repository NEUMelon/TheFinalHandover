# 《最后一次交班》Stage 3 · 杂项道具贴图（由 Claude 编写，已登记 AI 使用日志）。运行：python3 gen_misc_textures.py [输出目录=同目录 Textures/]
# 1. 复用 LT01 已有的可平铺贴图（../Textures/gen_textures.py 生成，1024 px = 1 m），改名成 Unity 能自动接的 <名字>_Atlas_* 三张：
#    LT01_Paint_1983Green → LSB_ENV_PRP_MachinePaint（立钻、砂轮机）、LT01_CastIron → LSB_ENV_PRP_CastIron、
#    LT01_MachinedSteel → LSB_ENV_PRP_MachinedSteel、LT01_WornBright → LSB_ENV_PRP_WornBright（砂轮机托架、手柄球）
#    墙 / 钢架另用 ENV 的 LSB_ENV_BLD_SteelPaint（不复制，Unity 里已有）。
# 2. 新生成：LSB_ENV_PRP_Wood（清漆木 1024 = 1 m，桌凳椅）、LSB_ENV_PRP_Slogan（标语牌正面 2048×512，整张对应 2.4 m × 0.6 m 牌面）。
#    标语字：Noto Serif SC Bold（SIL OFL，工程已有字体 Assets/LSB/Fonts/），字色晒图蓝 #1F3A5F（美术圣经 §2：红色只给红胶布和柜灯），
#    内容按美术圣经 §5“只用通用安全生产标语”：安全生产　人人有责。
import sys, os, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__))
STAGE3 = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(STAGE3, "ENV"))
from gen_env_textures import fbm, smooth, lin, blur, save as _save
import gen_env_textures as G
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "Textures")
os.makedirs(OUT, exist_ok=True); G.OUT = OUT
SRC = os.path.join(STAGE3, "Textures")
for src, dst in (("LT01_Paint_1983Green", "LSB_ENV_PRP_MachinePaint"), ("LT01_CastIron", "LSB_ENV_PRP_CastIron"),
                 ("LT01_MachinedSteel", "LSB_ENV_PRP_MachinedSteel"), ("LT01_WornBright", "LSB_ENV_PRP_WornBright")):
    for suf in ("BaseColor", "Normal", "MS"):
        shutil.copyfile(os.path.join(SRC, f"T_{src}_{suf}.png"), os.path.join(OUT, f"{dst}_Atlas_{suf}.png"))
    print("copied", dst)

def wood(N=1024):
    S = (N, N)
    grain = fbm(S, 1.4, 201, fmin=2, aniso=(0.04, 1.0))           # 沿 U（长度方向）拉长的木纹
    ring = np.sin(grain * 40.0) * 0.5 + 0.5
    g = fbm(S, 2.6, 202, fmin=1); fine = fbm(S, 0.8, 203, fmin=60, aniso=(0.1, 1.0))
    base = lin("#8E6A42") * (0.78 + 0.25 * ring[..., None]) * (0.92 + 0.12 * fine[..., None])
    wear = smooth(0.62, 0.8, fbm(S, 2.2, 204, fmin=2))              # 清漆磨掉、木头发白发灰
    base = base * (1 - 0.35 * wear[..., None]) + lin("#A79478") * 0.35 * wear[..., None]
    base = base * (1 - 0.2 * smooth(0.5, 1, g)[..., None])
    return base, 0.45 - 0.25 * wear + 0.05 * fine, np.zeros(S, np.float32), 0.5 * ring + 0.3 * fine

def slogan(W=2048, H=512):
    S = (H, W)
    g = fbm(S, 2.6, 301, fmin=1); b = fbm(S, 1.6, 302, fmin=3, aniso=(1.0, 0.2))
    base = lin("#E2DBC8") * (0.92 + 0.1 * b[..., None])
    # 文字
    font = ImageFont.truetype(os.path.join(STAGE3, "..", "..", "02_母项目VS", "demo_alpha", "UnityProject", "Assets", "LSB", "Fonts", "NotoSerifSC-Bold.ttf"), 300)
    im = Image.new("L", (W, H), 0); d = ImageDraw.Draw(im)
    text = "安全生产　人人有责"
    bb = d.textbbox((0, 0), text, font=font); tw, th = bb[2] - bb[0], bb[3] - bb[1]
    sx = (W * 0.9) / tw
    big = Image.new("L", (tw + 20, th + 20), 0); ImageDraw.Draw(big).text((10 - bb[0], 10 - bb[1]), text, font=font, fill=255)
    big = big.resize((int((tw + 20) * sx), int((th + 20) * 1.0)), Image.LANCZOS)
    im.paste(big, ((W - big.width) // 2, (H - big.height) // 2))
    t = np.asarray(im, np.float32)[::-1] / 255.0                  # 行 0 = V 0（下）
    chip = smooth(0.72, 0.74, fbm(S, 2.4, 303, fmin=6))           # 字漆崩掉的小块
    t = t * (1 - chip)
    base = base * (1 - t[..., None]) + lin("#1F3A5F") * (0.9 + 0.15 * b[..., None]) * t[..., None]
    # 年代：四角螺钉锈水往下流、边缘发黄、整体污渍
    yy, xx = np.mgrid[0:H, 0:W]
    rust = np.zeros(S, np.float32)
    for cx, cy in ((60, H - 60), (W - 60, H - 60), (60, 60), (W - 60, 60)):
        streak = np.exp(-((xx - cx) / 10.0) ** 2) * (yy < cy) * np.exp(-(cy - yy) / 120.0)
        dot = np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / 200.0))
        rust = np.maximum(rust, np.maximum(streak * (0.5 + 0.5 * g), dot))
    edge = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy)).astype(np.float32)
    yellow = smooth(80, 0, edge)
    base = base * (1 - 0.15 * yellow[..., None]) * (1 - np.array([0, 0.03, 0.12]) * yellow[..., None])
    base = base * (1 - 0.5 * rust[..., None]) + lin("#7A4A2A") * 0.5 * rust[..., None]
    base = base * (1 - 0.18 * smooth(0.5, 1, g)[..., None])
    base = base[::-1]                                               # 存图：行 0 = 顶
    return base, (0.3 - 0.1 * rust)[::-1], np.zeros(S, np.float32), (0.3 * b + 0.2 * t)[::-1]

if __name__ == "__main__":
    _save("LSB_ENV_PRP_Wood", *wood(), 2.0)
    _save("LSB_ENV_PRP_Slogan", *slogan(), 1.5)

# 《最后一次交班》Stage 3 · 手写 → Rule ID 点阵重组：离线取点
# 用 Demo α 工程里的同一批字体（老周体 = 志莽行书 ZhiMangXing，建国体 = 龙藏 LongCang，等宽 = JetBrains Mono Regular，均 OFL 1.1，
# 见 demo_alpha/UnityProject/Assets/LSB/Fonts/FONTS.md）把四条规则的手写原句和 Rule ID 栅格化，按网格取点。
# 输出：
#   1. stage3_addon/Assets/LSB_Stage3/Scripts/Runtime/RuleIdDotsData.cs（Unity 运行时直接用，免 Resources 导入设置）
#   2. 40_特效/stage3_RuleID重组/ruleid_dots.json，并写进网页样片 LSB_VFX_RuleIdReassemble_LookDev_v01.html 的 /*DATA*/…/*END*/ 处
# 坐标单位：1/1000 em（em = cine-rule 标签字号，Demo α 为 34px）；y 以“行框”顶为 0，行框高 = JetBrains Mono 的 ascent + descent。
# 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）。
import json, os, sys
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FONTS = os.path.join(ROOT, "02_母项目VS/demo_alpha/UnityProject/Assets/LSB/Fonts")
OUT_CS = os.path.join(ROOT, "02_母项目VS/stage3_addon/Assets/LSB_Stage3/Scripts/Runtime/RuleIdDotsData.cs")
OUT_JSON = os.path.join(ROOT, "40_特效/stage3_RuleID重组/ruleid_dots.json")
OUT_HTML = os.path.join(ROOT, "40_特效/stage3_RuleID重组/LSB_VFX_RuleIdReassemble_LookDev_v01.html")

EM = 400                 # 栅格化字号（px），只影响取点精度
HAND_SCALE = 28 / 34     # 手写原句字号 = UI-DS T-Hand-L 28px，相对 cine-rule 34px
HAND_PITCH = 0.040       # 手写点阵间距（em）：34px 下约 1.4px，看起来仍是连续笔画
ID_PITCH = 1 / 12        # Rule ID 点阵间距（em）：34px 下约 2.8px，大写字母约 9 行点
HAND_TH, ID_TH = 0.38, 0.42

# 四条规则的手写原句：全部取自 Demo α 现有文本（ContentDB.cs PageText / ReaderText / TapeView），字体与 Demo α 用法一致。
RULES = [
    ("ZHOU_LEFT",     "zhou", "L1，听这里。",          "sketch1983 末行（老周体，1983 测点示意）"),
    ("ZHOU_CLICK",    "zhou", "咔哒在左，是轴承；",     "note1983 第 4 行（老周体，1983 设备记录）"),
    ("JIANGUO_GUARD", "jian", "护罩，开车前看一眼。",   "prev 第 4 行（建国体，上一班交代）"),
    ("SUN_HALFBEAT",  "jian", "广播站　孙桂兰",         "tapeLabel（建国体，磁带标签；孙桂兰的规则来自采访录音，没有她本人的手写）"),
]
FONT_FILE = {"zhou": "ZhiMangXing-Regular.ttf", "jian": "LongCang-Regular.ttf", "mono": "JetBrainsMono-Regular.ttf"}

mono = ImageFont.truetype(os.path.join(FONTS, FONT_FILE["mono"]), EM)
asc, desc = mono.getmetrics()
LINE_H = (asc + desc) / EM          # 行框高（em）
BASE = asc / EM                     # 基线距行框顶（em）
ADV = mono.getlength("M") / EM      # 等宽字宽（em）

def raster(text, font, size):
    f = ImageFont.truetype(os.path.join(FONTS, font), size)
    w = int(f.getlength(text)) + EM
    h = int(EM * LINE_H * 1.6) + EM
    im = Image.new("L", (w, h), 0)
    ImageDraw.Draw(im).text((0, int(BASE * EM)), text, font=f, fill=255, anchor="ls")
    return np.asarray(im, dtype=np.float32) / 255.0, f.getlength(text) / EM

def sample(a, pitch_em, th):
    p = pitch_em * EM
    pts = []
    H, W = a.shape
    ny, nx = int(H / p), int(W / p)
    for j in range(ny):
        for i in range(nx):
            y0, x0 = int(j * p), int(i * p)
            cell = a[y0:int(y0 + p), x0:int(x0 + p)]
            if cell.size and cell.mean() > th:
                pts.append(((i + 0.5) * pitch_em, (j + 0.5) * pitch_em))
    return pts

def q(v): return int(round(v * 1000))

data = {"lineH": q(LINE_H), "base": q(BASE), "adv": q(ADV), "handPitch": q(HAND_PITCH), "idPitch": q(ID_PITCH), "rules": []}
for rid, who, hand, note in RULES:
    ha, hw = raster(hand, FONT_FILE[who], int(EM * HAND_SCALE))
    hp = sample(ha, HAND_PITCH, HAND_TH)
    ia, iw = raster(rid, FONT_FILE["mono"], EM)
    ip = sample(ia, ID_PITCH, ID_TH)
    ic = [min(len(rid) - 1, int(x / ADV)) for x, _ in ip]
    missing = [ch for ch in hand if ImageFont.truetype(os.path.join(FONTS, FONT_FILE[who]), 40).getmask(ch).getbbox() is None and ch.strip() and ch != "　"]
    if missing: print("WARN 缺字", rid, missing, file=sys.stderr)
    data["rules"].append({"id": rid, "who": who, "hand": hand, "note": note, "handW": q(hw), "idW": q(iw),
                          "handDots": [c for x, y in hp for c in (q(x), q(y))],
                          "idDots": [c for x, y in ip for c in (q(x), q(y))], "idChar": ic})
    print(f"{rid}: hand {len(hp)} dots ({hw:.2f} em), id {len(ip)} dots ({iw:.2f} em)")

with open(OUT_JSON, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, separators=(",", ":"))

if os.path.exists(OUT_HTML):
    import re
    h = open(OUT_HTML, encoding="utf-8").read()
    h2 = re.sub(r"const DATA=/\*DATA\*/.*?/\*END\*/;", lambda m: "const DATA=/*DATA*/" + json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "/*END*/;", h, count=1, flags=re.S)
    if h2 != h:
        open(OUT_HTML, "w", encoding="utf-8").write(h2)
        print("updated", OUT_HTML)

def arr(name, v, t="short"):
    lines, cur = [], []
    for i, x in enumerate(v):
        cur.append(str(x))
        if len(cur) == 24: lines.append(", ".join(cur)); cur = []
    if cur: lines.append(", ".join(cur))
    body = ",\n                ".join(lines)
    return f"new {t}[] {{\n                {body}\n            }}"

cs = ["// 《最后一次交班》Stage 3 · 手写 → Rule ID 点阵数据（自动生成，勿手改）",
      "// 生成脚本：40_特效/stage3_RuleID重组/tools/gen_ruleid_dots.py；字体：Demo α Assets/LSB/Fonts（OFL 1.1）。",
      "// 坐标单位 1/1000 em，y 从行框顶算起。由 AI 辅助工具编写脚本生成（AI 辅助代码，记入 AI 使用日志）。",
      "namespace LSB.Stage3", "{", "    public static class RuleIdDotsData", "    {",
      f"        public const int LineH = {data['lineH']}, Base = {data['base']}, Adv = {data['adv']}, HandPitch = {data['handPitch']}, IdPitch = {data['idPitch']};",
      "        public sealed class Entry { public string Id, Who, Hand; public short[] HandDots, IdDots; public byte[] IdChar; }",
      "        public static readonly Entry[] Rules =", "        {"]
for r in data["rules"]:
    cs.append("            new Entry")
    cs.append("            {")
    cs.append(f"                Id = \"{r['id']}\", Who = \"{r['who']}\", Hand = \"{r['hand']}\",   // {r['note']}")
    cs.append(f"                HandDots = {arr('h', r['handDots'])},")
    cs.append(f"                IdDots = {arr('i', r['idDots'])},")
    cs.append(f"                IdChar = {arr('c', r['idChar'], 'byte')},")
    cs.append("            },")
cs += ["        };", "    }", "}", ""]
with open(OUT_CS, "w", encoding="utf-8") as f:
    f.write("\n".join(cs).replace("\n                ", "\n                "))
print("wrote", OUT_CS, os.path.getsize(OUT_CS), "bytes;", OUT_JSON, os.path.getsize(OUT_JSON), "bytes")

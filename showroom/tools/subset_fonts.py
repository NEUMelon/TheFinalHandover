# 按页面实际用到的字生成字体子集，以 data: URL 写进 fonts.css。
# 匿名仓库的网页模式不允许跨域载入字体文件，所以字体直接内嵌。
#   python3 tools/subset_fonts.py <字体目录：含 NotoSerifSC-700/900.ttf、NotoSansSC-400/500.ttf、JetBrainsMono-700.ttf>
import base64, glob, io, os, sys
from fontTools import subset
from fontTools.ttLib import TTFont

FD = sys.argv[1]
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..")
texts = ""
for p in glob.glob(os.path.join(ROOT, "*.html")) + [os.path.join(ROOT, "..", "index.html")]:
    if os.path.exists(p):
        texts += open(p, encoding="utf-8").read()
chars = "".join(sorted(set(texts) | set("0123456789·—–“”‘’（）《》，。：；！？、×²³ ")))

FACES = [
    ("SRSerif", 700, "NotoSerifSC-700.ttf"),
    ("SRSerif", 900, "NotoSerifSC-900.ttf"),
    ("SRSans", 400, "NotoSansSC-400.ttf"),
    ("SRSans", 500, "NotoSansSC-500.ttf"),
    ("SRMono", 700, "JetBrainsMono-700.ttf"),
]
css = ["/* 由 tools/subset_fonts.py 生成，勿手改。字体为 SIL OFL 1.1 开源授权（思源宋体 / 思源黑体 / JetBrains Mono）。 */"]
for fam, w, fn in FACES:
    opts = subset.Options(); opts.flavor = "woff2"; opts.layout_features = ["*"]; opts.name_IDs = ["*"]; opts.notdef_outline = True
    f = TTFont(os.path.join(FD, fn))
    s = subset.Subsetter(opts); s.populate(text=chars); s.subset(f)
    buf = io.BytesIO(); f.flavor = "woff2"; f.save(buf)
    data = base64.b64encode(buf.getvalue()).decode()
    css.append(f"@font-face{{font-family:'{fam}';font-weight:{w};font-style:normal;font-display:swap;src:url(data:font/woff2;base64,{data}) format('woff2');}}")
    print(fam, w, len(buf.getvalue()))
open(os.path.join(ROOT, "fonts.css"), "w", encoding="utf-8").write("\n".join(css) + "\n")

# 生成仓库根目录的 index.html（在线演示首页）。
# 截图以 data: URL 内嵌：匿名仓库的网页模式下，页面越少依赖外部文件越稳。
#   python3 showroom/tools/build_landing.py
import base64, glob, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
SHOW = os.path.normpath(os.path.join(HERE, ".."))
ROOT = os.path.normpath(os.path.join(SHOW, ".."))

src = open(os.path.join(HERE, "landing.src.html"), encoding="utf-8").read()

def img(name):
    p = os.path.join(SHOW, "shots", name + ".jpg")
    return "data:image/jpeg;base64," + base64.b64encode(open(p, "rb").read()).decode()

def size(key):
    files = glob.glob(os.path.join(SHOW, "models", key + ".*.js"))
    files += [os.path.join(SHOW, "lib", "three-bundle.js"), os.path.join(SHOW, "fonts.css")]
    mb = sum(os.path.getsize(f) for f in files) / 1e6
    return "约 %d MB" % round(mb)

out = re.sub(r"\{\{img:(\w+)\}\}", lambda m: img(m.group(1)), src)
out = re.sub(r"\{\{size:(\w+)\}\}", lambda m: size(m.group(1)), out)
open(os.path.join(ROOT, "index.html"), "w", encoding="utf-8").write(out)
print("index.html", len(out))

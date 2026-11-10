# 把 GLB 切成若干个普通 <script> 文件（base64），写入 models/。
# 这样页面不用 fetch：匿名仓库的网页模式禁止跨域请求，双击本地打开（file://）也一样能看。
#   python3 tools/pack_models.py <glb 目录>
import base64, glob, json, os, sys

SRC = sys.argv[1]
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "models")
CHUNK = 900 * 1024  # 每个文件约 0.9 MB 的 base64
os.makedirs(OUT, exist_ok=True)
for f in glob.glob(os.path.join(OUT, "*.js")):
    os.remove(f)

manifest = {}
for path in sorted(glob.glob(os.path.join(SRC, "*.glb"))):
    key = os.path.splitext(os.path.basename(path))[0]
    b64 = base64.b64encode(open(path, "rb").read()).decode("ascii")
    parts = [b64[i:i + CHUNK] for i in range(0, len(b64), CHUNK)]
    for i, p in enumerate(parts):
        with open(os.path.join(OUT, f"{key}.{i}.js"), "w") as fh:
            fh.write(f'(window.__GLB=window.__GLB||{{}});(window.__GLB["{key}"]=window.__GLB["{key}"]||[])[{i}]="{p}";\n')
    manifest[key] = {"parts": len(parts), "bytes": os.path.getsize(path)}
    print(key, len(parts), os.path.getsize(path))

with open(os.path.join(OUT, "manifest.js"), "w") as fh:
    fh.write("window.__GLB_MANIFEST=" + json.dumps(manifest) + ";\n")

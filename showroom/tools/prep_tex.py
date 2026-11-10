# 网页版贴图：缩小尺寸；Unity 的 MS（R=金属度, A=光滑度）转成 glTF 的 ORM（G=粗糙度, B=金属度）
import glob, os, sys
from PIL import Image
import numpy as np
S = os.environ.get("LSB_STAGE3", "stage3")  # Stage 3 美术源目录
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
def cap(asset): return 2048 if asset.startswith("LT01") else 1024
n = 0
for bc in sorted(glob.glob(S + "/**/*_Atlas_BaseColor.png", recursive=True)):
    d = os.path.dirname(bc); asset = os.path.basename(bc)[:-len("_Atlas_BaseColor.png")]
    c = cap(asset)
    for suf in ("BaseColor", "Normal"):
        p = os.path.join(d, f"{asset}_Atlas_{suf}.png")
        if not os.path.exists(p): continue
        im = Image.open(p).convert("RGB")
        if max(im.size) > c:
            r = c / max(im.size); im = im.resize((max(1, int(im.width * r)), max(1, int(im.height * r))), Image.LANCZOS)
        im.save(os.path.join(OUT, f"{asset}_Atlas_{suf}.png"))
    ms = os.path.join(d, f"{asset}_Atlas_MS.png")
    if os.path.exists(ms):
        im = Image.open(ms).convert("RGBA")
        if max(im.size) > c:
            r = c / max(im.size); im = im.resize((max(1, int(im.width * r)), max(1, int(im.height * r))), Image.LANCZOS)
        a = np.asarray(im).astype(np.uint8)
        orm = np.stack([np.full(a.shape[:2], 255, np.uint8), 255 - a[..., 3], a[..., 0]], -1)
        Image.fromarray(orm, "RGB").save(os.path.join(OUT, f"{asset}_Atlas_ORM.png"))
    n += 1
print("assets", n)

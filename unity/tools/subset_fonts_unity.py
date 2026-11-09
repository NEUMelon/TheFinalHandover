#!/usr/bin/env python3
"""把 Fontsource 分片字体按 Demo α 实际用到的字符裁成 TTF 子集，输出到 UnityProject/Assets/LSB/Fonts/。
改编自 UI 原型 tools/subset_fonts.py（原型输出 woff2；Unity 不认 woff2，这里输出 TTF）。

用法：python3 tools/subset_fonts_unity.py <fontsource 解包目录> [UI 原型目录]
  解包目录下应有 fontsource-<name>-5.3.0/package/ 结构（npm pack @fontsource/<name>@5.3.0 后 tar 解开）。
  给了原型目录时，把原型用到的字也一起收进来（原型界面迁到 Unity 时不缺字）。
依赖：pip install fonttools brotli
每次改了 C# / USS 里的文案都要重跑，否则新字会显示成方框。
"""
import re, sys, os, io
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools import subset
from fontTools.merge import Merger

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent / 'UnityProject' / 'Assets' / 'LSB'
OUT = ASSETS / 'Fonts'
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else None
PROTO = Path(sys.argv[2]) if len(sys.argv) > 2 else None

FONTS = [
    ('noto-serif-sc', 400, 'NotoSerifSC-Regular'),
    ('noto-serif-sc', 700, 'NotoSerifSC-Bold'),
    ('noto-serif-sc', 900, 'NotoSerifSC-Black'),
    ('noto-sans-sc', 400, 'NotoSansSC-Regular'),
    ('noto-sans-sc', 500, 'NotoSansSC-Medium'),
    ('jetbrains-mono', 400, 'JetBrainsMono-Regular'),
    ('jetbrains-mono', 700, 'JetBrainsMono-Bold'),
    ('long-cang', 400, 'LongCang-Regular'),
    ('zhi-mang-xing', 400, 'ZhiMangXing-Regular'),
    ('lxgw-wenkai', 500, 'LXGWWenKai-Medium'),
]


def used_chars():
    s = set(chr(c) for c in range(0x20, 0x7f))
    s |= set('　，。、：；？！“”‘’（）《》〔〕【】—…·°×→←↑↓±≈≤≥▶■□○●◆◇★☆／％～·①②③④⑤⑥〇')
    files = list(ASSETS.rglob('*.cs')) + list(ASSETS.rglob('*.uss'))
    if PROTO:
        files += list(PROTO.glob('*.html')) + list(PROTO.glob('js/*.js')) + list(PROTO.glob('css/*.css'))
    for p in files:
        s |= set(p.read_text(encoding='utf-8'))
    return {c for c in s if ord(c) >= 0x20}


def chunks(pkg, weight):
    d = next(SRC.glob(f'fontsource-{pkg}-*/package'))
    css = (d / f'{weight}.css').read_text()
    files = re.findall(r"url\(\./files/([^)]+?\.woff2)\)", css)
    return [d / 'files' / f for f in files]


def main():
    if not SRC:
        sys.exit(__doc__)
    OUT.mkdir(parents=True, exist_ok=True)
    chars = used_chars()
    cps = {ord(c) for c in chars}
    total = 0
    for pkg, w, name in FONTS:
        parts = []
        covered = set()
        for f in chunks(pkg, w):
            ft = TTFont(f)
            have = set(ft.getBestCmap().keys()) & cps
            if not have:
                continue
            covered |= have
            opts = subset.Options(); opts.flavor = None; opts.layout_features = []; opts.drop_tables += ['GSUB', 'GPOS', 'GDEF']; opts.name_IDs = ['*']; opts.notdef_outline = True
            sub = subset.Subsetter(opts); sub.populate(unicodes=have); sub.subset(ft)
            ft.flavor = None
            buf = io.BytesIO(); ft.save(buf); buf.seek(0)
            parts.append(buf)
        out = OUT / f'{name}.ttf'
        if len(parts) == 1:
            ft = TTFont(parts[0])
        else:
            tmp = []
            for i, b in enumerate(parts):
                p = OUT / f'.tmp_{name}_{i}.ttf'; p.write_bytes(b.getvalue()); tmp.append(str(p))
            ft = Merger().merge(tmp)
            for p in tmp: os.remove(p)
        ft.flavor = None
        ft.save(out)
        total += out.stat().st_size
        han = [c for c in cps if 0x4E00 <= c <= 0x9FFF or c == 0x3007]
        miss = len([c for c in han if c not in covered])
        print(f'{name}: {len(parts)} 片 → {out.stat().st_size // 1024} KB（汉字缺 {miss} 个）')
    print(f'字符数 {len(chars)}，合计 {total // 1024} KB')


if __name__ == '__main__':
    main()

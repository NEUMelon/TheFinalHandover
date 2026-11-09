#!/usr/bin/env python3
"""把 Fontsource 分片字体按原型实际用到的字符裁成小文件，输出到 ../fonts/，并生成 fonts.css。

用法：python3 tools/subset_fonts.py <fontsource 解包目录>
  解包目录下应有 fontsource-<name>-<ver>/package/ 结构（npm pack @fontsource/<name> 后 tar 解开）。
依赖：pip install fonttools brotli
每次改了文案（html/js/css）都要重跑，否则新字会回退到系统字体。
"""
import re, sys, glob, os, io
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools import subset
from fontTools.merge import Merger

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'fonts'
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else None

# (CSS 字体名, fontsource 包名, 字重, 输出名)
FONTS = [
    ('NotoSerifSC', 'noto-serif-sc', 400, 'NotoSerifSC-Regular'),
    ('NotoSerifSC', 'noto-serif-sc', 700, 'NotoSerifSC-Bold'),
    ('NotoSerifSC', 'noto-serif-sc', 900, 'NotoSerifSC-Black'),
    ('NotoSansSC', 'noto-sans-sc', 400, 'NotoSansSC-Regular'),
    ('NotoSansSC', 'noto-sans-sc', 500, 'NotoSansSC-Medium'),
    ('JetBrainsMono', 'jetbrains-mono', 400, 'JetBrainsMono-Regular'),
    ('JetBrainsMono', 'jetbrains-mono', 700, 'JetBrainsMono-Bold'),
    ('LongCang', 'long-cang', 400, 'LongCang-Regular'),
    ('ZhiMangXing', 'zhi-mang-xing', 400, 'ZhiMangXing-Regular'),
    ('LXGWWenKai', 'lxgw-wenkai', 500, 'LXGWWenKai-Medium'),
]


def used_chars():
    s = set(chr(c) for c in range(0x20, 0x7f))
    s |= set('　，。、：；？！“”‘’（）《》〔〕【】—…·°×→←↑↓±≈≤≥▶■□○●◆◇★☆／％～·①②③④⑤⑥')
    for p in list(ROOT.glob('*.html')) + list(ROOT.glob('js/*.js')) + list(ROOT.glob('css/*.css')):
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
    OUT.mkdir(exist_ok=True)
    chars = used_chars()
    cps = {ord(c) for c in chars}
    css = ['/* 由 tools/subset_fonts.py 生成，勿手改。字体均为 SIL OFL 1.1，见 fonts/FONTS.md */']
    for fam, pkg, w, name in FONTS:
        parts = []
        for f in chunks(pkg, w):
            ft = TTFont(f)
            have = set(ft.getBestCmap().keys()) & cps
            if not have:
                continue
            opts = subset.Options(); opts.flavor = None; opts.layout_features = []; opts.drop_tables += ['GSUB', 'GPOS', 'GDEF']; opts.name_IDs = ['*']; opts.notdef_outline = True
            sub = subset.Subsetter(opts); sub.populate(unicodes=have); sub.subset(ft)
            ft.flavor = None
            buf = io.BytesIO(); ft.save(buf); buf.seek(0)
            parts.append(buf)
        out = OUT / f'{name}.woff2'
        if len(parts) == 1:
            ft = TTFont(parts[0])
        else:
            tmp = []
            for i, b in enumerate(parts):
                p = OUT / f'.tmp_{name}_{i}.ttf'; p.write_bytes(b.getvalue()); tmp.append(str(p))
            ft = Merger().merge(tmp)
            for p in tmp: os.remove(p)
        ft.flavor = 'woff2'; ft.save(out)
        css.append(f"@font-face{{font-family:'{fam}';font-weight:{w};font-style:normal;font-display:block;src:url('{name}.woff2') format('woff2');}}")
        print(f'{name}: {len(parts)} 片 → {out.stat().st_size // 1024} KB')
    (OUT / 'fonts.css').write_text('\n'.join(css) + '\n', encoding='utf-8')
    print(f'字符数 {len(chars)}')


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""把原型打包成单个 HTML 文件：字体（base64 woff2）、CSS、JS、SVG 全部内联，音频仍由程序实时合成。

用法：python3 tools/build_single.py [片段输出路径]
  输出 dist/最后一次交班_UI原型.html：完整网页，双击即可离线打开，也可以直接发给别人。
  给了片段输出路径时，另外写一份不带 <!doctype>/<html>/<head>/<body> 外壳的片段，
  用于嵌入其他网页（由宿主页面补外壳）。
源文件（index.html、css/、js/、fonts/）不改；改了源文件后重跑本脚本即可。
"""
import base64, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / 'dist'
TITLE = '最后一次交班 · UI 原型'


def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')


def fonts_css():
    def inline(m):
        data = base64.b64encode((ROOT / 'fonts' / m.group(1)).read_bytes()).decode('ascii')
        return f"url(data:font/woff2;base64,{data})"
    return re.sub(r"url\('([^']+\.woff2)'\)", inline, read('fonts/fonts.css'))


def build():
    html = read('index.html')
    css_links = re.findall(r'<link rel="stylesheet" href="([^"]+)">', html)
    css = '\n'.join(fonts_css() if h == 'fonts/fonts.css' else read(h) for h in css_links)
    # 网页版没有 Esc 时靠右上角“目录”按钮；color-scheme 让浏览器自带控件也走深色
    css += '\n:root { color-scheme: dark; }\n'
    body = re.search(r'<body>\n?(.*?)</body>', html, re.S).group(1)
    scripts = re.findall(r'<script src="([^"]+)"></script>\n?', body)
    body = re.sub(r'<script src="[^"]+"></script>\n?', '', body).rstrip() + '\n'
    for s in scripts + css_links:
        assert '</script' not in read(s).lower() and '</style' not in read(s).lower(), s
    js = '\n'.join(f'<script>\n/* {s} */\n{read(s)}\n</script>' for s in scripts)
    flag = "<script>document.documentElement.classList.add('web');</script>"
    head = f'<title>{TITLE}</title>\n<style>\n{css}</style>\n{flag}\n'
    return head, body, js


def main():
    head, body, js = build()
    DIST.mkdir(exist_ok=True)
    full = ('<!doctype html>\n<html lang="zh-CN">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
            f'{head}</head>\n<body>\n{body}{js}\n</body>\n</html>\n')
    out = DIST / '最后一次交班_UI原型.html'
    out.write_text(full, encoding='utf-8')
    print(f'{out.relative_to(ROOT)}  {out.stat().st_size / 1024 / 1024:.2f} MB')
    if len(sys.argv) > 1:
        frag = Path(sys.argv[1])
        frag.parent.mkdir(parents=True, exist_ok=True)
        frag.write_text(f'{head}{body}{js}\n', encoding='utf-8')
        print(f'{frag}  {frag.stat().st_size / 1024 / 1024:.2f} MB（Artifact 片段）')


if __name__ == '__main__':
    main()

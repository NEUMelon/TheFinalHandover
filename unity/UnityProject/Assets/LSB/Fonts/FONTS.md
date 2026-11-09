# 字体（Demo α · Unity）

与 UI 原型 `50_游戏交互/prototype/fonts/FONTS.md` 同一套开源字体（全部 SIL OFL 1.1，许可证原文见 `licenses/`），来源是 npm 上的 Fontsource 包 5.3.0。原型输出 woff2，Unity 不认 woff2，所以这里用 `demo_alpha/tools/subset_fonts_unity.py` 重新裁成 TTF 子集。

| 文件 | 字体 | UI-DS 角色 |
|---|---|---|
| NotoSerifSC-Regular / Bold / Black.ttf | 思源宋体 Noto Serif SC | S 印刷宋：表格、表头、页眉、过场卡 |
| NotoSansSC-Regular / Medium.ttf | 思源黑体 Noto Sans SC | U 界面黑：字幕、HUD、设置；衡日志的中文回退 |
| JetBrainsMono-Regular / Bold.ttf | JetBrains Mono | M 等宽：衡的时间戳、规则 ID（只有拉丁字符） |
| LongCang-Regular.ttf | 龙藏体 | H 手写：建国体占位 |
| ZhiMangXing-Regular.ttf | 志莽行书 | H 手写：老周体占位 |
| LXGWWenKai-Medium.ttf | 霞鹜文楷 | H 手写：林小满（玩家）笔迹占位 |

- 三款手写字体都是**占位**，正式稿换团队手写矢量化的字（UI-DS §2）。
- 子集按 `Assets/LSB` 下全部 `.cs` / `.uss` 和 UI 原型的 html/js/css 里出现过的字裁切（2A 时 1202 个字符）。**改了文案要重跑**，否则新字显示成方框：
  `python3 tools/subset_fonts_unity.py <fontsource 解包目录> <UI 原型目录>`
- 龙藏体、志莽行书缺“〇”（与原型相同）。
- 合规：这些字体仍需登记到 `80_合规/授权清单`（本线程不改 80_合规）。

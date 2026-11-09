# 字体使用说明（UI Prototype v0.1）

本原型只用开源字体，全部为 SIL Open Font License 1.1（OFL），允许免费商用、修改与随作品再分发，唯一要求是随字体附上许可证原文。许可证原文见 `fonts/licenses/`。六个字体的版权声明里都没有指定“保留字体名”（Reserved Font Name），所以裁切后的子集沿用原字体名不违反 OFL。

> 合规登记：这六款字体还需要登记到 `80_合规/授权清单`（主项目文档 §8.3 要求字体全部登记）。本线程不改 80_合规，留给负责合规的同学登记。

## 1. 字体与来源

| CSS 字体名 | 字体 | 原型里的用途（UI-DS §2 角色） | 字重 | 来源（npm 包，版本） | 上游项目 | 许可 |
|---|---|---|---|---|---|---|
| `NotoSerifSC` | 思源宋体 / Noto Serif SC | S 印刷宋：表格、表头、通知、档案卡、正文 | 400 / 700 / 900 | `@fontsource/noto-serif-sc` 5.3.0 | Google Noto CJK | OFL 1.1 |
| `NotoSansSC` | 思源黑体 / Noto Sans SC | U 界面黑：字幕、设置、暂停菜单；等宽字体的中文回退 | 400 / 500 | `@fontsource/noto-sans-sc` 5.3.0 | Google Noto CJK | OFL 1.1 |
| `JetBrainsMono` | JetBrains Mono | M 等宽：衡的全部界面、规则编号、变量名、时间戳 | 400 / 700 | `@fontsource/jetbrains-mono` 5.3.0 | github.com/JetBrains/JetBrainsMono | OFL 1.1 |
| `LongCang` | 龙藏体 Long Cang | H 手写：**建国体占位**（交班本上一班、打卡告示、签名） | 400 | `@fontsource/long-cang` 5.3.0 | github.com/googlefonts/longcang | OFL 1.1 |
| `ZhiMangXing` | 志莽行书 Zhi Mang Xing | H 手写：**老周体占位**（1983 设备记录、照片背注） | 400 | `@fontsource/zhi-mang-xing` 5.3.0 | github.com/googlefonts/zhimangxing | OFL 1.1 |
| `LXGWWenKai` | 霞鹜文楷 LXGW WenKai | H 手写：**林小满（玩家）笔迹占位**（本班点检、批注、评定签名） | 500 | `@fontsource/lxgw-wenkai` 5.3.0 | github.com/lxgw/LxgwWenKai | OFL 1.1 |

三种手写体刻意用了三款不同的字体，让“老周 / 建国 / 林小满”三代人的笔迹在同一页上一眼可分。

## 2. 与 UI-DS §2 的差异（都要在正式稿换回）

| UI-DS 规定 | 原型实际 | 原因 / 正式稿怎么做 |
|---|---|---|
| 手写体 H：老师傅手写后矢量化，老周体、建国体两套，团队原创 | 志莽行书顶替老周体，龙藏体顶替建国体 | 原型阶段还没有自制字。正式稿换成团队手写矢量化的两套字 |
| （UI-DS 未规定玩家笔迹） | 林小满的字用霞鹜文楷 | 本班点检、评定签名需要和两位老师傅区分。**待定**：玩家笔迹是否要第三套自制字，还是用建国体的“年轻版”；需圣经层确认 |
| 等宽体 M 中文：更纱黑体等宽 Sarasa Mono SC | 中文回退到思源黑体（NotoSansSC） | Fontsource 上没有 Sarasa Mono SC；本环境拿不到 GitHub 官方发布包，npm 上只有来源不明的第三方转包（约 260MB），出于授权可追溯的考虑没有采用。思源黑体的中文字宽与 JetBrains Mono 不是严格 2:1，长日志的中英混排列不齐。字体栈里已把 Sarasa Mono SC 排在思源黑体前面，本机装了更纱等宽会自动用上；正式稿换回 Sarasa Mono SC |
| 印刷体 S：思源宋体 Source Han Serif | Noto Serif SC | 两者是同一套字形的两个发行名，不算差异 |

三款手写字库都只用来让原型先有手写的观感，正式稿必须替换，详见 README 的“必须人工替换”清单。

已知的小问题：龙藏体和志莽行书没有“〇”（U+3007），手写日期里的“〇”回退到霞鹜文楷，原型用 `.o0` 类把它缩小了一号。自制字库时记得收这个字。

## 3. 字号阶梯（与 UI-DS §2.1 同名同值，1080p 像素）

| Token | 字号 / 行高 | 用在 |
|---|---|---|
| `T-Display` | 48 / 60 | 大标题（原型中按版面放大到 66，见打卡告示） |
| `T-H1` | 32 / 44 | 页眉：上一班 / 本班 / 三联对应单 / 职工档案卡 |
| `T-H2` | 24 / 34 | 栏目标题：一联 异常现象 等 |
| `T-Body` | 20 / 32 | 通知、档案字段 |
| `T-Hand-L` | 28 / 36 | 手写正文 |
| `T-Hand-M` | 22 / 30 | 页边批注 |
| `T-Mono-L` | 22 / 30 | 衡：规则编号 |
| `T-Mono` | 18 / 26 | 衡：日志正文 |
| `T-Mono-S` | 14 / 20 | 衡：标签、坐标轴 |
| `T-Caption` | 22 / 32 | 字幕 |
| `T-Label` | 16 / 22 | 表格标签、小字说明 |

## 4. 裁切方式

原版 CJK 字体每款 5–20MB，原型按实际用到的字（约 900 个）裁成子集，十个字重合计约 1.4MB，双击 `index.html` 离线也能正确显示。

- 脚本：`tools/subset_fonts.py`（需要 `pip install fonttools brotli`）。
- 做法：`npm pack @fontsource/<名字>@5.3.0` 下载后解包，脚本扫描 `index.html`、`js/`、`css/` 里出现的全部字符，从 Fontsource 的分片 woff2 中取出并合并，输出 `fonts/*.woff2` 和 `fonts/fonts.css`。
- **改了任何界面文字都要重跑一次**，否则新字会回退到系统字体：

```bash
python3 tools/subset_fonts.py <fontsource 解包目录>
```

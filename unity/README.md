# 《最后一次交班》Demo α（Vertical Slice）· Unity 工程

阶段 2 主产出。目标是从头跑到尾的垂直切片：开场 → 谜题一“左手” → 谜题二“咔哒在左” → 谜题三“一九八七年的凹痕” → 谜题四“提前半拍” → 签字 → 停机 → 衡接管 → 玩家签名。允许灰盒、占位资源、临时音频、简化特效。

- 引擎：**Unity 2022.3.62f3c1 LTS（中国版，版本号带 c1）+ URP 14.0.12**。2026-10-11 mirinda 指定改用这个版本（原先按主项目文档 v1.1 写的是 Unity 6 LTS，改动登记在 `口径与待回写项_DemoAlpha.md` D-08）。`ProjectSettings/ProjectVersion.txt` 写的是 2022.3.62f3c1；本机是别的 2022.3.x 补丁版也能打开，Hub 提示版本不同时选用本机版本继续。
- UI：**UI Toolkit**（UI-DS §8：USS 变量与 Token 同名，原型 CSS Token 一一搬成 USS 变量）。
- 输入：旧版 Input Manager（不依赖 Input System 包）。
- 机位：节点式固定机位 + 设备特写，不是第一人称自由移动。
- 本工程**没有 Web 版代码**：引擎改成 Unity 之前还没写任何 Web 代码，所以没有需要迁移或删除的东西。`50_游戏交互/prototype/` 是 UI 设计基线，本工程只复制了其中的数据和做法（见“来源”），没有改动原型目录。

## 打开和运行

1. Unity Hub →“添加 / Add project from disk”→ 选 `demo_alpha/UnityProject` 文件夹。第一次打开会导入 URP、UI、Test Framework 等包。
2. 等脚本编译完。工程会自动把 `Assets/LSB/Fonts/*.ttf` 转成 UI 用的字体资源（`Fonts/SDF/*.asset`），Console 出现“[LSB] 已生成 UI 字体资源”即可。第一次导入时 USS 可能先报几条“找不到字体资源”的警告，字体资源生成后会自动重新导入，不用管；如果界面里中文还是方框，点 **LSB → 6 重建字体资源**。
3. 菜单栏 **LSB → 1 一键生成 Demo 场景**：设置 URP（已装就跳过）→ 生成 UI 面板设置 → 按白盒规格生成三号车间灰盒 → 存为 `Assets/LSB/Scenes/DemoAlpha.unity` 并加进 Build Settings。
4. 按 Play。标题页点“打卡上班（新游戏）”。

**请解压到一个新文件夹**，不要覆盖旧版本的文件夹：旧文件夹里的 `Library/`、`Packages/packages-lock.json` 会留着旧的包记录和旧的字体缓存，可能让已经修掉的报错又冒出来。

其它菜单：**LSB → 3 节点截图**（10 个节点 + 5 个特写 + 总闸机位各截一张 1920×1080，存到工程根目录 `Screenshots_DemoAlpha/`，用于白盒规格 §9 的截图测试）；**LSB → 4 逻辑层自检**（在编辑器里从新游戏自动走到结局，结果打到 Console）。

操作：鼠标点击交互；左下角按钮走节点 / 进特写；特写里按住左键拖动旋转，右键或 Esc 返回；1–4 选工具（听针 / 手电 / 卡尺 / 螺丝刀，选中手电时鼠标指哪儿照哪儿）；Tab 或右下角按钮开 / 合交班本（在交班桌也可以按 L），交班本里 A / D 翻页；按住空格高亮当前能点的东西；记忆显影时点一下或按 Esc 跳过。天车平板上拖“二”或点“早一点 / 晚一点”挪第二段收车，按“试吊”；结局里按住总闸手柄往下拖（或点“拉下总闸”），在签名板上按住鼠标写名字（或点“代签”），在 ID 栏打一个英文名字。**F1（或 ~）开发菜单**只在编辑器和 Development Build 里有，和玩家界面分开；正式试玩用非开发构建，或加启动参数 `-lsbNoDev`。

## 工程结构

```
UnityProject/
  Packages/manifest.json             URP 14.0.12、UGUI 1.0.0、Test Framework 1.1.33、内置模块
  ProjectSettings/ProjectVersion.txt Unity 2022.3.62f3c1 LTS
  Assets/LSB/
    Scripts/Core/       LSB.Core 程序集：纯 C#，不引用 UnityEngine（noEngineReferences），云端可直接编译和测试
      Data/             ContentDB：节点、特写、交互物、证据、页面、台词、三联、规则卡、推断、提示、剧情时间（数据驱动，系统代码不写死文案）
      Systems/          各系统分开写（见下表）
      Sim/              SpectroModel（时频图 / 边频 / 信号条）、CraneZvModel（单摆 + 两段整形）、GuardTwinModel（护罩启动许可）
    Scripts/Runtime/    LSB.Runtime：GameBootstrap、灰盒生成、节点机位、交互拾取、场景表现、HUD、面板、占位音频、存档
    Scripts/DevTools/   LSB.DevTools：开发菜单（IMGUI，独立程序集，不进玩家界面）
    Scripts/Editor/     LSB.Editor：编辑器菜单（生成场景、只重建灰盒、节点截图、逻辑自检、设置 URP）
    Tests/EditMode/     LSB.Tests.EditMode：数据完整性、主流程、各谜题、提示、存档、文本预算（Unity Test Runner 和 dotnet test 共用同一份源文件）
    Resources/LSB/UI/   lsb_tokens.uss（UI-DS Token）、lsb_hud.uss、lsb_views.uss（2B 面板）、LSB_Theme.tss
    Fonts/              10 个开源字体的 TTF 子集 + 许可证（FONTS.md）
tools/
  CoreCheck/            dotnet：单独编译 LSB.Core
  CoreTests/            dotnet test：跑 Tests/EditMode 下的测试
  UnityCompileCheck/    dotnet：把 Runtime / DevTools / Editor 对着 Unity 参考库编译
  subset_fonts_unity.py 字体子集脚本（改了文案要重跑）
验证记录/               每个子阶段的测试与编译输出
口径与待回写项_DemoAlpha.md  口径决定、和阶段 1 文档对不上的地方、〔派生〕内容
```

### 系统模块（用户阶段 2 消息里列的系统，一个系统一个类）

| 系统 | 类 | 文件 |
|---|---|---|
| Game Flow 主流程 | `FlowSystem` | Core/Systems/FlowAndNav.cs |
| Node Navigation 节点导航 | `NavigationSystem`（逻辑）+ `NodeCameraRig`（相机） | FlowAndNav.cs / Runtime/World/NodeCameraRig.cs |
| Interaction 交互 | `InteractionSystem`（规则匹配）+ `InteractionController`（鼠标拾取） | InteractionEvidenceLedger.cs / Runtime/World/InteractionController.cs |
| Evidence 证据 | `EvidenceSystem` | InteractionEvidenceLedger.cs |
| Ledger 交班本 | `LedgerSystem` | InteractionEvidenceLedger.cs |
| Triad 三联板 | `TriadSystem` | TriadRulesDialogue.cs |
| Knowledge Rule 经验规则 | `KnowledgeRuleSystem`（含交接灯、衡等级） | TriadRulesDialogue.cs |
| Dialogue / Subtitle 对白字幕 | `DialogueSystem` + `HudView.Caption` | TriadRulesDialogue.cs / Runtime/UI/HudView.cs |
| 面板（2B–2D） | `LedgerView` `ReaderView` `DiagView`（+ `SpectroTexture`）`TriadView` `RuleCardView` `MesView` `DeductionView` `TwinView` `CraneView` `TapeView` `SwitchView` `EndingView`（基类 `ModalView`，`ViewHost` 管开合与“返回上一个面板”）；记忆显影 `MemoryOverlay`、结局过场 `EndingOverlay`（覆盖层） | Runtime/UI/ |
| Hint 提示 | `HintSystem` + `PuzzleProgress` | HintTimelinePlaytest.cs |
| Timeline 剧情时间 | `TimelineSystem` | HintTimelinePlaytest.cs |
| 谜题小玩法 | `PuzzleSystem` | PuzzleSystem.cs |
| 结局 | `EndingSystem` | EndingSystem.cs |
| 试玩记录 | `PlaytestLog` | HintTimelinePlaytest.cs |
| Dev Debug Menu 开发菜单 | `DevMenu`（与玩家 UI 分开） | DevTools/DevMenu.cs |
| 自动通关（开发跳关 / 测试） | `AutoPlayer`（只调用玩家能做的操作） | AutoPlayer.cs |

## 验证状态（如实）

| 内容 | 状态 |
|---|---|
| 逻辑层 LSB.Core | **已实际编译**（dotnet 8，C# 9 = Unity 2022.3 的语言版本） |
| 逻辑层测试 62 项 | **已实际运行**（2F）：62 项全过。文本预算按 mirinda 2026-10-11 选定的口径算（不含提示与填错反馈）：2313 / 2500；全部文字 3386 也照样打印。2B 新增谜题一面板路径、规则卡中途存读档、主层无专业词、最少操作统计；2C 新增谜题二面板路径、时频图示意数据、衡屏幕给维修记录；2D 新增谜题三面板路径（凹痕显影 → 继电器柜 → 三张旧纸 → 推断单错一处 / 错多处 / 全对 → 三联“可以拆掉”的孪生反馈 → 补回前后各试 → JIANGUO_GUARD → 第四项）、三联前只能试不能补、孪生试到一半存读档、谜题三主层无 PLC / 安全回路等专业词、最少操作统计；2E 新增谜题四面板路径（自动试吊水洒 → 没听磁带调稳不算 → 磁带 + 铃声显影 → 半拍水稳 → 三联“固定 2 秒”6 m 复算失效 → SUN_HALFBEAT → 结局）、第二段时刻扫一遍残余摆动真的跟着变、“晃不洒”听过磁带可确认、时刻一格 0.05 拍和上下限、试吊调到一半存读档、谜题四主层无专业词、时机表、最少操作统计；结局只靠每帧 Tick + 拉闸 / 命名 / 签名走完（不用开发菜单，静默 5 秒、四条规则按顺序出现、12 步顺序）、先命名后签名、命名时存读档。2F 新增从新游戏只走正式玩家入口跑到本局完成（不调跳关、不调结局 Advance，结局只靠每帧推进）、Runtime 代码不引用开发菜单 / 自动玩家、试玩编号规整。输出见 `验证记录/2F_逻辑层单元测试.txt` |
| 无头通关 | **已实际运行**（2F 起有两条：开发菜单同款的自动通关，和只走正式玩家入口的全程走查 `FullRunTests`）：从新游戏不跳关走到结局（四盏交接灯按 1→4 熄灭、衡升到四级、结局 12 步顺序正确、全部必要证据拿到、时钟停在 23:58）。这是自动化功能验证，**不是真人试玩数据** |
| Runtime / DevTools / Editor | **已对照 Unity 参考库编译通过**（nuget UnityEngine.Modules 2021.3.33 + UnityEditor 2021.1；网上没有 2022.3 的参考库包，2021.3 是最接近的）。参考库比 2022.3 旧一个大版本，能编过说明没用到更新的 API；**还没有在真正的 Unity 2022.3 编辑器里编译和运行过** |
| 在 Unity 2022.3 里实际打开 | mirinda 打开 2A 包后报了两类错，都已修改，**修改后还没在 Unity 里复测**：① `NodeCameraRig.SnapToNode` 空引用（启动时 GameBootstrap 先于相机组件初始化，相机引用改成用到时再取）；② “m_AtlasTextures of FontAsset doesn't exist anymore”（USS 直接引用 .ttf，运行时临时转换的字体资源丢了图集；改成工程里先生成字体资源，USS 引用 .asset）；③ 打开工程时弹“Unity Package Manager Error：com.unity.test-framework.tests@1.1.33 cannot be found”（manifest 里多写了一行 `testables`，让包管理器去下测试框架自己的测试包，中国版的包源里没有这个包；已删掉这行，本工程的测试不需要它）。**三处修改后都还没在 Unity 里复测** |
| 灰盒场景 | **只写了生成脚本**，还没有在 Unity 里生成、看过画面 |
| URP 设置 | 只写了反射脚本；没装 URP 时自动退回 Built-in + Standard 材质 |
| UI 面板 | 已写源文件并通过参考库编译：2B 交班本（页签、翻页、贴页、本班点检表、便签挡页、1983 测点示意）、细看（工作照 / 合影 / 点检单）、衡的平板（谜题一左右对比）、三联板、经验规则卡（纸面 + 屏幕）+ 技术等级考核合格通知、手电聚光灯；2C 衡的平板谜题二（时频图框选、播放头和“咔哒”同步、齿轮指示判读、A/B 对比声音）、衡的屏幕（设备 / 日志 / 知识库 / 诊断 / 交接 / 等级 / 点检，规则确认后改口）。2D 记忆显影（覆盖层：静音 2 秒、声音先于画面、网点聚合 / 停留 / 溶解，可跳过）、推断单（填空 + 旧纸栏 + 对一下）、离线孪生（启动条件示意、补回、两种情况各试、提交待验证）、衡的屏幕第三项比对单、交班本贴页 9 点开比对单。2E 天车试吊平板（时间轴、铃点、第二段拖动、上一次 / 这一次曲线、搪瓷缸）、录音机（转带、逐句字幕）、总闸 + 交接单（拖手柄）、交班本最后一页（周建国签字的灰盒手、签名板、规则命名）、结局过场（上下黑边、衡的日志逐行出现、黑屏献词、本局完成卡）。**都还没在 Unity 里看过** |
| 音频 | 程序合成的占位声音（移植原型 audio.js 并补车间底噪、汽笛、天车铃）；已写源文件，未在 Unity 里听过 |

## 占位清单（都要在正式版替换）

- 全部几何是灰盒体块 + 纯色材质；场景里有“灰盒 / 占位”标签（开发菜单可关）。
- LT01 Hero、配电柜、天车中模，4 个角色，均未制作（只有体块）。
- 音频全部是程序合成的示意声音，不是实地录音；诊断数据（时频图、边频、信号条）是**模拟 / 占位数据**，不是任何真实设备的实测数据。
- 三款手写字体（老周体、建国体、林小满笔迹）是开源字体占位。
- 窗户光柱、体积光、纸张肌理未做。记忆显影是程序画的网点图 + 几何剪影（灰盒），正式版换特效小组的网点 Shader。
- 记忆显影里的金属撞击、一声喊、颗粒声、低音也是程序合成的占位声音。
- 磁带只有字幕和合成的磁带底噪，没有录人声；天车摆动曲线是二维单摆示意（模拟 / 占位），不是起重机控制模型。
- 结局里周建国的手、远景背影是灰盒体块。

## 来源（从 UI 原型复制或移植的部分）

`50_游戏交互/prototype/` 保持原样未改，以下内容复制或移植进本工程：

| 原型文件 | 本工程 |
|---|---|
| js/data.js（时频图参数、冲击时刻、边频指标、谜题二三联与反馈文案） | Core/Sim/SpectroModel.cs、Core/Data/ContentDB.Puzzles.cs |
| js/spectro.js `judge()`（框选判定规则） | SpectroModel.Judge |
| js/audio.js（四段诊断示意声音与界面音效的合成方法） | Runtime/Audio/ProceduralAudio.cs |
| js/app.js（结局日志顺序、命名规则、判偏 3 次给提示） | Core/Systems/EndingSystem.cs、PuzzleSystem.cs |
| css/tokens.css（UI-DS Token） | Resources/LSB/UI/lsb_tokens.uss（同名同值） |
| fonts/FONTS.md、tools/subset_fonts.py、fonts/licenses/ | Fonts/FONTS.md、tools/subset_fonts_unity.py（改输出 TTF）、Fonts/licenses/ |

## 2F 可用性测试

- 先在 Unity 里按 `验证记录/2F_Unity全程检查单.md` 从新游戏跑一遍（不按 F1）。
- 菜单 **LSB → 7 打试玩包（非开发构建）**：打到工程根目录 `Builds/DemoAlpha_试玩/`，没有开发菜单。
- 测试材料在 `试玩测试包/`，从 `00_测试包说明.md` 看起。**包里只有空模板，没有任何数据。**
- 游戏导出的试玩记录用 `python3 tools/merge_playtest.py <记录文件夹> [输出文件夹]` 汇总。

## 跑测试

- 云端 / 命令行：`cd tools/CoreTests && dotnet test`（需要 .NET 8 SDK）。
- Unity 里：Window → General → Test Runner → EditMode → Run All（同一份测试源文件）。

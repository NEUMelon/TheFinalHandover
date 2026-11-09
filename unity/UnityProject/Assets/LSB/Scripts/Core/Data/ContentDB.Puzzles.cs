// 《最后一次交班》Demo α —— 四个谜题的交互规则、三联板、经验规则卡、推断、提示
// 三联正确答案与干扰项：01_四谜题事实链_v0.1 各题 §6 / §9（不改答案，只把主层文字改成玩家语言，专业说法放进 Pro）。
// UX 原则（用户阶段 2 消息第五节）：主交互层只说左边 / 右边、咔哒 / 嗡、正常 / 异常、老师傅怎么判断；
// L1 / R1、2–4kHz、GMF、边频、ZV、安全回路等专业信息放第二层（规则卡、衡日志、“专业说法”开关）。
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed partial class ContentDB
    {
        public readonly List<InteractionRule> Rules = new List<InteractionRule>();
        public readonly List<TriadDef> Triads = new List<TriadDef>();
        public readonly Dictionary<string, RuleCardDef> RuleCards = new Dictionary<string, RuleCardDef>();
        public readonly List<HintSet> Hints = new List<HintSet>();
        public DeductionDef Deduction;

        public TriadDef Triad(Phase p) => Triads.FirstOrDefault(t => t.Phase == p);
        public HintSet Hint(Phase p, string stage) => Hints.FirstOrDefault(h => h.Phase == p && h.Stage == stage);

        void BuildPuzzles()
        {
            BuildInteractionRules();
            BuildTriads();
            BuildRuleCards();
            BuildDeduction();
            BuildHints();
        }

        // =====================================================================
        // 交互规则（同一物件按顺序匹配第一条）
        // =====================================================================
        void BuildInteractionRules()
        {
            // ---- 通用 ----
            R("ledger", new[] { Effect.View("ledger") }, anyTool: true, min: Phase.Intro);
            R("mes", new[] { Effect.View("mes") }, anyTool: true, min: Phase.Intro);
            R("walkie", new[] { Effect.Say("@last") }, anyTool: true, min: Phase.Intro, valid: false);
            R("toolbox_desk", new[] { Effect.Cap("工具箱。按 1–4 换工具。") }, anyTool: true, min: Phase.Intro, valid: false);
            R("handover_board", new[] { Effect.Cap("交接板：灯亮归旧办法，灯灭归衡。") }, anyTool: true, min: Phase.Intro, valid: false);
            R("desk_lamp", new[] { Effect.Sfx("switch"), Effect.Flag("lamp_toggled") }, anyTool: true, min: Phase.Intro, valid: false);

            // ---- 谜题一“左手” ----
            R("locker_zhou", new[] { Effect.Flag("locker_open"), Effect.Sfx("locker"), Effect.Cap("三副手套。门内侧夹着工牌套。") }, anyTool: true, forbid: new[] { "locker_open" });
            R("locker_zhou", new[] { Effect.Cap("老周的柜子。") }, anyTool: true, valid: false);
            R("gloves", new[] { Effect.Ev("E1_1"), Effect.Cap("三副手套，只有左手食指磨穿了。") }, anyTool: true, flags: new[] { "locker_open" });
            R("badge_photo", new[] { Effect.Ev("E1_2"), Effect.View("reader", "photo") }, anyTool: true, flags: new[] { "locker_open" });
            R("photo1956", new[] { Effect.Ev("E1_6"), Effect.View("reader", "photo1956") }, anyTool: true, valid: false);
            R("lunchbox", new[] { Effect.Ev("E1_7"), Effect.Cap("搪瓷饭盒，盖子上刻着“周”。") }, anyTool: true, flags: new[] { "locker_open" }, valid: false);

            R("l1_mark", new[] { Effect.Ev("E1_3"), Effect.Sfx("torch"), Effect.Cap("磨亮的样冲眼，一圈褪色的漆。"), Effect.Log("MON", "左端有旧测点标记。") }, tool: ToolId.Torch, forbidEv: new[] { "E1_3" });
            R("l1_mark", new[] { Effect.Cap("老周的点。") }, tool: ToolId.Torch, valid: false);
            R("l1_mark", new[] { Effect.CloseUp("CU1"), Effect.View("diag", "L1") }, tool: ToolId.Probe, ev: new[] { "E1_3" });
            R("l1_mark", new[] { Effect.Cap("太暗了，拿手电照照。") }, tool: ToolId.Probe, valid: false);
            R("l1_mark", new[] { Effect.Cap("太暗了，拿手电照照。") }, anyTool: true, valid: false);
            R("r1_sensor", new[] { Effect.View("diag", "R1") }, tool: ToolId.Probe);
            R("r1_sensor", new[] { Effect.Cap("衡装的传感器，在右端。") }, anyTool: true, valid: false);

            // ---- 谜题三“一九八七年的凹痕” ----
            R("guard", new[] { Effect.Memory("m1987"), Effect.Ev("E3_2") }, tool: ToolId.Torch, min: Phase.P3, forbidEv: new[] { "E3_2" });
            R("guard", new[] { Effect.Cap("手电下：护罩外面一块凹痕，边上的漆裂了。") }, tool: ToolId.Torch, valid: false);
            R("guard", new[] { Effect.CloseUp("CU2") }, anyTool: true, min: Phase.P3, valid: false);
            R("guard", new[] { Effect.Cap("卡盘护罩。外面有一块凹痕。") }, anyTool: true, valid: false);
            R("cabinet_door", new[] { Effect.Flag("cabinet_open"), Effect.Sfx("cabinet"), Effect.CloseUp("CU3"), Effect.Cap("柜门开了。一排排继电器在吸合。") }, tool: ToolId.Driver, min: Phase.P3, forbid: new[] { "cabinet_open" });
            R("cabinet_door", new[] { Effect.CloseUp("CU3") }, anyTool: true, min: Phase.P3, flags: new[] { "cabinet_open" }, valid: false);
            R("cabinet_door", new[] { Effect.Cap("柜门锁着，要用螺丝刀开。") }, anyTool: true, min: Phase.P3, valid: false);
            R("cabinet_door", new[] { Effect.Cap("旧继电器柜。旧回路还在工作。") }, anyTool: true, valid: false);
            R("relay_guard", new[] { Effect.Ev("E3_3"), Effect.Ev("E3_4"), Effect.Cap("图纸上没有这颗继电器。红胶布“护罩 87”，白胶布“保留 08”。"), Effect.Log("TWIN", "柜内确认：未登记继电器 1 颗（护罩相关）。") }, anyTool: true, flags: new[] { "cabinet_open" }, forbidEv: new[] { "E3_3" });
            R("relay_guard", new[] { Effect.Cap("红胶布“护罩 87”。白胶布“保留 08”。") }, anyTool: true, flags: new[] { "cabinet_open" }, valid: false);
            R("checklist", new[] { Effect.Ev("E3_5"), Effect.View("reader", "checklist") }, anyTool: true, min: Phase.P3);
            R("checklist", new[] { Effect.Cap("玻璃板下压着点检单。") }, anyTool: true, valid: false);

            // ---- 谜题四“提前半拍” ----
            R("crane_tablet", new[] { Effect.CloseUp("CU4") }, anyTool: true, min: Phase.P4);
            R("crane_tablet", new[] { Effect.Cap("衡的调试平板。天车是今晚最后一项。") }, anyTool: true, valid: false);
            R("drawer", new[] { Effect.Flag("drawer_open"), Effect.Sfx("drawer"), Effect.CloseUp("CU5") }, anyTool: true, min: Phase.P4, forbid: new[] { "drawer_open" });
            R("drawer", new[] { Effect.CloseUp("CU5") }, anyTool: true, min: Phase.P4, valid: false);
            R("drawer", new[] { Effect.Cap("抽屉里有些旧东西。现在还用不着。") }, anyTool: true, valid: false);
            R("tape", new[] { Effect.Flag("tape_taken"), Effect.Cap("盒式磁带，标签手写：“广播站　孙桂兰”。") }, anyTool: true, min: Phase.P4, flags: new[] { "drawer_open" }, forbid: new[] { "tape_taken" });
            R("tape", new[] { Effect.Cap("磁带已经拿出来了。放进录音机。") }, anyTool: true, valid: false);
            R("recorder", new[] { Effect.View("tape") }, anyTool: true, min: Phase.P4, flags: new[] { "tape_taken" });
            R("recorder", new[] { Effect.Cap("单卡录音机。里面是空的。") }, anyTool: true, valid: false);
            R("cab_mug", new[] { Effect.Ev("E4_5"), Effect.Cap("司机室里一只旧搪瓷缸，口沿磕掉一块瓷。") }, anyTool: true, valid: false);

            // ---- 结局 ----
            R("main_switch", new[] { Effect.View("switch") }, anyTool: true, min: Phase.Ending, flags: new[] { "switch_authorized" });
            R("main_switch", new[] { Effect.Cap("总闸。只有交接时才拉。") }, anyTool: true, valid: false);

            // ---- 氛围 ----
            R("cab_mug", new[] { Effect.Cap("司机室里一只旧搪瓷缸。") }, anyTool: true, valid: false);
            R("award", new[] { Effect.Cap("“先进生产者”奖状，纸边发黄。") }, anyTool: true, valid: false);
            R("thermos", new[] { Effect.Cap("暖水瓶还是温的。") }, anyTool: true, valid: false);
            R("shaft_rack", new[] { Effect.Cap("工件架上一排半成品轴，明天还要接着车。") }, anyTool: true, valid: false);
            R("speaker", new[] { Effect.Sfx("speaker"), Effect.Cap("〔广播喇叭：一阵底噪〕") }, anyTool: true, valid: false);
            R("grinder", new[] { Effect.Cap("砂轮机的托架磨得发亮。") }, anyTool: true, valid: false);
            R("slogan", new[] { Effect.Cap("“安全生产　人人有责”") }, anyTool: true, valid: false);
        }

        void R(string target, Effect[] fx, ToolId tool = ToolId.None, bool anyTool = false, Phase min = Phase.P1, Phase max = Phase.Ending,
               string[] flags = null, string[] ev = null, string[] forbid = null, string[] forbidEv = null, bool valid = true)
        {
            var rule = new InteractionRule
            {
                Target = target, Tool = tool, AnyTool = anyTool, MinPhase = min, MaxPhase = max, Valid = valid, Effects = fx,
                RequireFlags = flags ?? new string[0], RequireEvidence = ev ?? new string[0], ForbidFlags = forbid ?? new string[0],
            };
            if (forbidEv != null) rule.ForbidEvidence = forbidEv;
            Rules.Add(rule);
        }

        // =====================================================================
        // 三联板
        // =====================================================================
        void BuildTriads()
        {
            Triads.Add(new TriadDef
            {
                Phase = Phase.P1, RuleId = RuleIds.ZhouLeft, Title = "点检第一项　LT01 主轴后轴承：在哪儿听",
                Columns = new[] {
                    Col("phen", "一联", "异常现象",
                        Opt("p1", "右边只听得到一片“嗡”", "右端测点 R1 只听得到一片“嗡”", true, "E1_5"),
                        OptBad("p2", "左手食指的手套磨穿了", "RULE", "规则冲突：这是证据，不是设备异常。", null, null, "E1_1"),
                        OptBad("p3", "主轴转速不稳", "MON", "转速波动 < 0.5%，与现象不符。", "steady", "〔仿真：转速平稳的声音〕")),
                    Col("exp", "二联", "经验判断",
                        OptBad("e2", "老周是左撇子", "RULE", "规则冲突：照片中老周右手扶刀架手柄。", null, null, "E1_1"),
                        Opt("e1", "老周固定在主轴箱左端那个点听", "老周固定在主轴箱左端的 L1 测点听", true, "E1_2", "E1_3", "E1_4"),
                        OptBad("e3", "两头各装一个，越多越准", "TWIN", "两端信号平均后，轴承信号被嗡声淹没。", "humMask", "〔仿真：嗡声盖住了一切〕")),
                    Col("data", "三联", "数据特征",
                        OptBad("d2", "留在右边，把音量调大", "TWIN", "增益提高后嗡声同样放大，轴承仍不可读。", "humLoud", "〔仿真：嗡声跟着变大〕", "E1_5"),
                        Opt("d1", "以后在左边听：轴承的声音更清楚", "L1 对主轴后轴承信噪比更高，监测位置定为 L1", true, "E1_5"),
                        OptBad("d3", "两边取平均", "TWIN", "平均后轴承特征被稀释，不可读。", "humMask", "〔仿真：轴承的声音听不见了〕")),
                },
            });

            Triads.Add(new TriadDef
            {
                Phase = Phase.P2, RuleId = RuleIds.ZhouClick, Title = "点检第二项　LT01 主轴异响",
                Columns = new[] {
                    Col("phen", "一联", "异常现象",
                        OptBad("p2", "嗡声变大了", "TWIN", "按“嗡变大”建模仿真：没有咔哒。", "humLoud", "〔仿真：只有嗡声，没有咔哒〕", "E2_2"),
                        Opt("p1", "“嗡”里夹着“咔哒”", "“嗡”里夹着“咔哒”", true, "E2_1"),
                        OptBad("p3", "主轴发热", "MON", "温度 41°C，正常。", null, null)),
                    Col("exp", "二联", "经验判断",
                        Opt("e1", "咔哒在左，是轴承", "咔哒在左，是轴承", true, "E2_3"),
                        OptBad("e2", "在右，是齿轮", "TWIN", "按“更换齿轮”仿真：咔哒仍在。", "gearFixed", "〔仿真：换过齿轮，咔哒还在〕", "E2_3"),
                        OptBad("e3", "老机器都这样响", "TWIN", "按“忽略”仿真：咔哒随时间变密。", "denser", "〔仿真：咔哒越来越密〕")),
                    Col("data", "三联", "数据特征",
                        OptBad("d2", "右边嗡得响，就是齿轮坏了", "RULE", "规则冲突：右边齿轮指示在绿区。", null, null, "E2_2"),
                        OptBad("d3", "哪儿都更响，就是轴承坏了", "RULE", "规则冲突：分不出轴承和齿轮。", null, null),
                        Opt("d1", "左边的咔哒＝后轴承有毛病；齿轮看右边指示", "L1 测点、本机 2–4kHz 共振带内的周期冲击，作为后轴承缺陷特征；齿轮状态以啮合频率边频带为准", true, "E2_1", "E2_2")),
                },
            });

            Triads.Add(new TriadDef
            {
                Phase = Phase.P3, RuleId = RuleIds.JianguoGuard, Title = "点检第三项　LT01 护罩 / 启动条件",
                Prerequisites = new[] { "E3_INF" },
                Columns = new[] {
                    Col("phen", "一联", "异常现象",
                        OptBad("p2", "继电器老了，该换", "MON", "继电器动作正常，无老化记录。", null, null, "E3_3"),
                        Opt("p1", "护罩上有凹痕，柜里有一颗图纸上没有的继电器", "护罩凹痕 + 现行图纸上没有的联锁继电器", true, "E3_2", "E3_3"),
                        OptBad("p3", "护罩变形，合不严", "MON", "护罩闭合反馈正常。", null, null, "E3_2")),
                    Col("exp", "二联", "经验判断",
                        OptBad("e2", "旧设备的多余接线，可以拆掉", "RULE", "规则冲突：安全条件缺失。", "guardOpenStart", "〔仿真：护罩开着，主轴照样转了起来〕", "E3_3"),
                        OptBad("e3", "一九九八年改造时临时加的", "RULE", "规则冲突：继电器标记“87”，与变更单日期一致。", null, null, "E3_3"),
                        Opt("e1", "一九八七年出事以后正式整改加的，记录后来漏了", "一九八七年险情后的正式安全整改，变更记录漏迁", true, "E3_INF")),
                    Col("data", "三联", "数据特征",
                        Opt("d1", "护罩合上，主轴才许启动", "护罩闭合反馈接入安全相关控制回路，作为主轴启动许可，补入新控制系统并在仿真中验证", true, "E3_INF"),
                        OptBad("d2", "改成每班有人看一眼护罩", "RULE", "规则冲突：明日起无人工点检。", null, null),
                        OptBad("d3", "拆掉旧继电器，全交给新程序", "RULE", "规则冲突：安全条件缺失。", "guardOpenStart", "〔仿真：护罩开着，主轴照样转了起来〕")),
                },
            });

            Triads.Add(new TriadDef
            {
                Phase = Phase.P4, RuleId = RuleIds.SunHalfbeat, Title = "点检第四项　CR01 天车试吊",
                Prerequisites = new[] { "E4_TUNE" },
                Columns = new[] {
                    Col("phen", "一联", "异常现象",
                        OptBad("p2", "天车走得太快", "CR01", "运行速度 0.6 m/s，在额定范围内。", null, null),
                        OptBad("p3", "钢丝绳放得太长", "CR01", "绳长 4.0 m，是本次作业要求。", null, null),
                        Opt("p1", "停车时吊钩晃，缸里的水洒了", "停车时吊钩晃、缸里水洒", true, "E4_1")),
                    Col("exp", "二联", "经验判断",
                        Opt("e1", "比钩子早半拍收车", "“比钩子早半拍收车”", true, "E4_2"),
                        OptBad("e2", "等钩子停稳了再走", "CR01", "可以不洒，但作业时间 +40 秒。规则冲突：不满足节拍要求。", "slowCrane", "〔仿真：天车慢慢挪了很久〕"),
                        OptBad("e3", "收车越慢越稳", "CR01", "作业时间 +40 秒。规则冲突：不满足节拍要求。", "slowCrane", "〔仿真：天车慢慢挪了很久〕")),
                    Col("data", "三联", "数据特征",
                        OptBad("d2", "第二段收车固定在 2 秒", "TWIN", "绳长改为 6 m 复算：2.0 s = 0.41 拍，残余摆动 29%。规则冲突：绳长变化后失效。", null, null, "E4_TUNE"),
                        Opt("d1", "第二段收车放在钩子摆动的半拍处", "第二段整形指令相位 ≈ 当前摆动周期的一半（周期由衡实时估计）", true, "E4_4", "E4_TUNE"),
                        OptBad("d3", "第二段收车放在一整拍处", "TWIN", "与初始设置相同：残余摆动 100%，水洒出。", "spill", "〔仿真：水洒了〕")),
                },
            });
        }

        static TriadColumn Col(string key, string no, string title, params TriadOption[] o) => new TriadColumn { Key = key, No = no, Title = title, Options = o };
        static TriadOption Opt(string id, string text, string pro, bool ok, params string[] needs) => new TriadOption { Id = id, Text = text, Pro = pro, Ok = ok, Needs = needs };
        static TriadOption OptBad(string id, string text, string module, string log, string sim, string cap, params string[] needs)
            => new TriadOption { Id = id, Text = text, Pro = text, Ok = false, Needs = needs, FbModule = module, FbLog = log, FbSim = sim, FbCaption = cap };

        // =====================================================================
        // 经验规则卡（UI-DS §5.4 六字段）
        // =====================================================================
        void BuildRuleCards()
        {
            RuleCards[RuleIds.ZhouLeft] = new RuleCardDef
            {
                Id = RuleIds.ZhouLeft, Type = "监测位置", Owner = "周铁生",
                Source = "周铁生，1983 年测点示意页；2026 年衡对比复核", Point = "LT01 主轴箱左端 L1",
                Feature = "对主轴后轴承信噪比高于 R1", Binds = new[] { "LT01_SPDL_BRG_RR_VIB" },
                PlainSummary = "以后在左边 L1 听后轴承。",
            };
            RuleCards[RuleIds.ZhouClick] = new RuleCardDef
            {
                Id = RuleIds.ZhouClick, Type = "诊断", Owner = "周铁生",
                Source = "周铁生，1983 年设备记录批注；2026 年林小满复测", Point = "LT01 主轴箱左端 L1",
                Feature = "本机约 2–4kHz 结构共振带内的周期冲击", Binds = new[] { "LT01_SPDL_BRG_RR_IMP_ALM", "LT01_GB_GMF_SB_IDX" },
                PlainSummary = "左边一下一下的咔哒，是这台车后轴承的毛病；齿轮看右边。",
            };
            RuleCards[RuleIds.JianguoGuard] = new RuleCardDef
            {
                Id = RuleIds.JianguoGuard, Type = "安全联锁", Owner = "周建国",
                Source = "周建国；1987 年安全整改变更单（周铁生提出）", Point = "LT01 卡盘护罩 / 主轴启动",
                Feature = "护罩闭合反馈接入安全相关控制回路，作为启动许可", Binds = new[] { "LT01_GUARD_CLSD", "→ LT01_SPDL_START_PERM" },
                PlainSummary = "护罩合上，主轴才许启动。",
            };
            RuleCards[RuleIds.SunHalfbeat] = new RuleCardDef
            {
                Id = RuleIds.SunHalfbeat, Type = "控制时序", Owner = "孙桂兰",
                Source = "孙桂兰，1990 年代厂广播站采访；2026 年试吊复核", Point = "CR01 吊钩 / 防摇整形",
                Feature = "第二段整形指令在当前摆动周期的一半处", Binds = new[] { "CR01_SWAY_PERIOD_EST", "CR01_AS_ZV_T2" },
                PlainSummary = "第二段收车，比钩子早半拍。",
            };
        }

        // =====================================================================
        // 谜题三 Golden Idol 式推断（事实链 §4.5）
        // =====================================================================
        void BuildDeduction()
        {
            Deduction = new DeductionDef
            {
                Phase = Phase.P3,
                Segments = new[] { "", "在", "因为", "险些受伤；", "提出整改，", "正式加装了", "。变更单后来在", "中被漏掉。" },
                Blanks = new[] {
                    new DeductionBlank { Key = "who", Answer = "周建国", FromEvidence = new[] { "E3_6", "E3_8" } },
                    new DeductionBlank { Key = "year", Answer = "一九八七年", FromEvidence = new[] { "E3_6", "E3_7" } },
                    new DeductionBlank { Key = "cause", Answer = "护罩未合即开车", FromEvidence = new[] { "E3_6" } },
                    new DeductionBlank { Key = "proposer", Answer = "老周", FromEvidence = new[] { "E3_7" } },
                    new DeductionBlank { Key = "installer", Answer = "车间电工", FromEvidence = new[] { "E3_7" } },
                    new DeductionBlank { Key = "device", Answer = "护罩联锁", FromEvidence = new[] { "E3_3", "E3_7" } },
                    new DeductionBlank { Key = "lost", Answer = "图纸数字化", FromEvidence = new[] { "E3_1" } },
                },
                WordBank = new[] { "周建国", "孙桂兰", "一九八七年", "一九九八年", "二〇〇八年", "护罩未合即开车", "设备老化", "刀具崩刃", "老周", "设备科", "车间电工", "设备厂家", "护罩联锁", "急停按钮", "图纸数字化", "一九九八年电气改造" },
                GivesEvidence = "E3_INF",
            };
            var d = Deduction.WordNeeds;
            d["周建国"] = new[] { "E3_8" }; d["一九八七年"] = new[] { "E3_6" }; d["护罩未合即开车"] = new[] { "E3_6" };
            d["老周"] = new[] { "E3_7" }; d["设备科"] = new[] { "E3_7" }; d["车间电工"] = new[] { "E3_7" };
            d["护罩联锁"] = new[] { "E3_3" }; d["图纸数字化"] = new[] { "E3_1" }; d["二〇〇八年"] = new[] { "E3_4" };
        }

        // =====================================================================
        // 三级提示（UI-DS §5.8；事实链各题 §10 的原句优先，其余〔派生〕）
        // =====================================================================
        void BuildHints()
        {
            H(Phase.Intro, "ledger", "N1", "交班本在桌上。", "点开交班本，看本班第一项。", "点桌上的交班本。");

            H(Phase.P1, "locker", "locker_zhou", "老周的东西在更衣室。", "更衣室，老周的柜子。", "打开柜子，看门内侧的工牌套。");
            H(Phase.P1, "mark", "l1_mark", "手套为什么只坏左手？", "照片里，他左手伸向哪？", "主轴箱左端，用手电照。");
            H(Phase.P1, "sketch", "S1", "交班本里有一页老图。", "翻到“一 测点”。", "看 L1 指着哪儿。");
            H(Phase.P1, "compare", "l1_mark", "两边哪边听得清？", "拿听针贴左端那个点。", "平板上两边都听，选听得清的。");
            H(Phase.P1, "triad", "triad", "现象是机器的，不是人的。", "老周固定在哪儿听？", "以后在哪边听，就选哪边。");

            H(Phase.P2, "spectro", "l1_mark", "咔哒在哪边最清楚？", "左边的图上，找竖着的亮条。", "拖动框住竖条，别框到横线。");
            H(Phase.P2, "gear", "r1_sensor", "衡说是齿轮。听听右边。", "平板上换到右边，看齿轮指示。", "指针在绿区：点“正常”。");
            H(Phase.P2, "note", "S2", "老周当年听过这台车。", "交班本“二 异响”那页。", "看老周写的那句。");
            H(Phase.P2, "triad", "triad", "老周怎么说咔哒的？", "齿轮好不好，看哪边？", "嗡里夹着咔哒；咔哒在左是轴承。");

            H(Phase.P3, "dent", "guard", "衡说少了一个护罩的条件。去看看护罩。", "护罩上那块凹痕，拿手电照照。", "LT01 正面，点护罩，用手电照凹痕。");
            H(Phase.P3, "cabinet", "cabinet_door", "图纸上没有的东西，柜子里有。", "控制间的继电器柜，柜门用螺丝刀开。", "看贴红胶布的那颗继电器。");
            H(Phase.P3, "papers", "S3", "建国在交班本里贴了几张旧纸。", "简报上的日期，翻翻那年的工票。", "变更单上写着谁提的、谁装的。");
            H(Phase.P3, "deduce", "deduction", "把那年的事连成一句话。", "简报说“学徒一名”，工票是谁的？", "漏掉的那一步，衡的比对单上写着。");
            H(Phase.P3, "triad", "triad", "这不是多余的线，是一次整改。", "明天起，没人再看护罩了。", "护罩合上，主轴才许启动。");
            H(Phase.P3, "verify", "twin", "在孪生里试一试。", "护罩开着按启动，应当不转。", "两种情况都试一遍。");

            H(Phase.P4, "watch", "crane_tablet", "衡在天车下试吊。去看看。", "天车试吊区，点衡的调试平板。", "到天车下，点地上的调试平板。");
            H(Phase.P4, "tape", "drawer", "抽屉里有盘磁带。", "交班桌的抽屉。", "拿出磁带，放进录音机。");
            H(Phase.P4, "tune", "crane_tablet", "听听磁带里的铃。", "衡跟着钩子走。她比钩子早。", "往前挪，挪半拍。");
            H(Phase.P4, "triad", "triad", "稳不稳，看第二段收车。", "“半拍”跟着钩子走，不是固定秒数。", "第二段收车放在半拍处。");

            H(Phase.Ending, "switch", "main_switch", "按交接单，拉下总闸。", "总闸在控制间东墙。", "按住总闸手柄往下拖。");
            H(Phase.Ending, "name", "ending", "衡在等这个新信号的名字。", "在规则卡的 ID 栏写一个名字。", "签名、命名，都在最后一页。");
        }

        void H(Phase p, string stage, string pointsAt, string l1, string l2, string l3) => Hints.Add(new HintSet { Phase = p, Stage = stage, PointsAt = pointsAt, Levels = new[] { l1, l2, l3 } });
    }
}

// 《最后一次交班》Demo α —— 叙事数据：证据、交班本页面、台词、剧情时间、衡的脚本日志
// 依据：01_四谜题事实链_v0.1（证据 ID 与等级）、世界观圣经 §1.3（剧情时间压缩）/ §3.3（周建国台词表）、
//       UI 原型 js/data.js（谜题二文案原样沿用）。〔派生〕的新补文案集中登记在 demo_alpha/口径与待回写项_DemoAlpha.md。
// 用字：叙事文本年份用汉字，系统 UI / 日志 / 规则卡用阿拉伯数字（世界观圣经 §8）。
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed partial class ContentDB
    {
        public readonly List<EvidenceDef> Evidence = new List<EvidenceDef>();
        public readonly List<PageDef> Pages = new List<PageDef>();
        public readonly List<SpreadDef> Spreads = new List<SpreadDef>();
        public readonly Dictionary<string, LineDef> Lines = new Dictionary<string, LineDef>();
        public readonly List<TimeAnchor> Time = new List<TimeAnchor>();
        public readonly Dictionary<Phase, List<HengLine>> PhaseStartLogs = new Dictionary<Phase, List<HengLine>>();

        public EvidenceDef Ev(string id) => Evidence.FirstOrDefault(e => e.Id == id);
        public PageDef Page(string id) => Pages.FirstOrDefault(p => p.Id == id);
        public TimeAnchor Anchor(Phase p) => Time.FirstOrDefault(t => t.Phase == p);

        void BuildStory()
        {
            // ---------- 证据（事实链 §2–§5；E?_INF / E?_TUNE / E4_4 为玩法新增的“推断 / 操作结果”记录） ----------
            E("E1_1", Phase.P1, "劳保手套", "手套", "贴页 1", "只有左手食指磨穿了。", EvidenceGrade.Guide);
            E("E1_2", Phase.P1, "工作照（工牌套中）", "工作照", "贴页 2", "左手拿听针，伸向主轴箱左端。", EvidenceGrade.Necessary);
            E("E1_3", Phase.P1, "左端的老测点", "样冲眼", "贴页 3", "磨亮的样冲眼，一圈褪色的漆。", EvidenceGrade.Necessary);
            E("E1_4", Phase.P1, "一九八三年测点示意", "测点示意", "贴页 4", "L1 正对后轴承座。", EvidenceGrade.Necessary);
            E("E1_5", Phase.P1, "左右两边的信号", "信号对比", "贴页 5", "左边听得清轴承；右边全是嗡。", EvidenceGrade.Necessary);
            E("E1_6", Phase.P1, "一九五六年建线合影", "合影", "", "建线那年。", EvidenceGrade.Ambient);
            E("E1_7", Phase.P1, "搪瓷饭盒", "饭盒", "", "盖子上刻着“周”。", EvidenceGrade.Ambient);

            E("E2_1", Phase.P2, "左边的时频图", "左边的图", "贴页 6", "间隔均匀的竖条：一下一下的咔哒。", EvidenceGrade.Necessary);
            E("E2_2", Phase.P2, "右边的齿轮指示", "齿轮指示", "贴页 7", "指针在绿区：齿轮正常。", EvidenceGrade.Necessary);
            E("E2_3", Phase.P2, "一九八三年设备记录", "老周的记录", "贴页 8", "老周批注：“咔哒在左，是轴承；在右，是齿轮。”", EvidenceGrade.Necessary);
            E("E2_4", Phase.P2, "齿轮维修记录", "维修记录", "", "变速齿轮 2022 年换过。", EvidenceGrade.Ambient);

            E("E3_1", Phase.P3, "衡的比对单", "比对单", "贴页 9", "新程序少一个启动条件。图纸来源：2025 年数字化图纸 Rev.A。", EvidenceGrade.Necessary);
            E("E3_2", Phase.P3, "护罩上的凹痕", "凹痕", "贴页 10", "手电照见：一声撞击，一声喊，一个年轻人被拽开。", EvidenceGrade.Necessary);
            E("E3_3", Phase.P3, "红胶布继电器", "红胶布", "贴页 11", "图纸上没有这颗继电器。红胶布上写着“护罩 87”。", EvidenceGrade.Necessary);
            E("E3_4", Phase.P3, "白胶布", "白胶布", "", "同一颗继电器上，二〇〇八年补的“保留 08”。", EvidenceGrade.Ambient);
            E("E3_5", Phase.P3, "手写点检项", "点检单", "", "点检单上手写：“开车先看护罩”。", EvidenceGrade.Ambient);
            E("E3_6", Phase.P3, "一九八七年险情简报", "险情简报", "贴页 12", "LT01，学徒一名，护罩未合即开车，险情，无人员受伤。", EvidenceGrade.Necessary);
            E("E3_7", Phase.P3, "安全整改变更单（副本）", "变更单", "贴页 13", "提出：周铁生。施工：车间电工。内容：加装护罩联锁。", EvidenceGrade.Necessary);
            E("E3_8", Phase.P3, "学徒工票", "工票", "贴页 14", "周建国，同一天，工位 LT01。", EvidenceGrade.Necessary);
            E("E3_INF", Phase.P3, "推断：那一年发生了什么", "推断", "贴页 15", "一九八七年险情之后的正式安全整改，记录在数字化时漏掉了。", EvidenceGrade.Necessary);

            E("E4_1", Phase.P4, "衡的试吊记录", "试吊记录", "贴页 16", "停车时吊钩晃，缸里水洒。第二段收车在 1.00 拍。", EvidenceGrade.Necessary);
            E("E4_2", Phase.P4, "广播站采访磁带", "磁带", "贴页 17", "孙桂兰：“我这手，就比钩子早半拍。”", EvidenceGrade.Necessary);
            E("E4_3", Phase.P4, "一九九八年最后一次吊运", "铃声", "", "铃跟着吊钩的摆一下一下地响。", EvidenceGrade.Necessary);
            E("E4_4", Phase.P4, "时间轴上的铃点", "铃点", "贴页 18", "衡把铃声标在时间轴上：每半拍一响。", EvidenceGrade.Necessary);
            E("E4_5", Phase.P4, "司机室里的旧搪瓷缸", "搪瓷缸", "", "一九七六年的缸，口沿磕掉了一块瓷。", EvidenceGrade.Ambient);
            E("E4_TUNE", Phase.P4, "试吊：水稳住了", "试吊结果", "贴页 19", "第二段收车挪到半拍上下，停车后缸里的水没洒。", EvidenceGrade.Necessary);

            // ---------- 交班本页面（UI-DS §5.1 / §5.3）。主层用玩家语言，专业说法放在规则卡与衡日志 ----------
            P("prev", Phase.Intro, "上一班", "shift_prev");
            P("this", Phase.Intro, "本班", "shift_this");
            P("sketch1983", Phase.P1, "测点示意　一九八三", "sketch1983", "E1_4");
            P("found1", Phase.P1, "第一项　贴页", "found");
            P("note1983", Phase.P2, "设备记录　一九八三", "note1983", "E2_3");
            P("printout2026", Phase.P2, "振动复测单　2026", "printout2026");
            P("brief1987", Phase.P3, "险情简报　一九八七", "brief1987", "E3_6");
            P("change1987", Phase.P3, "安全整改变更单", "change1987", "E3_7");
            P("ticket1987", Phase.P3, "学徒工票　一九八七", "ticket1987", "E3_8");
            P("found3", Phase.P3, "第三项　贴页", "found");
            P("tapeLabel", Phase.P4, "磁带", "tape");
            P("found4", Phase.P4, "第四项　贴页", "found");
            P("rules", Phase.P1, "经验规则卡", "rules");
            P("handover", Phase.Ending, "交班", "handover_last");

            Spreads.Add(new SpreadDef { Id = "S0", Tab = "本班", MinPhase = Phase.Intro, Left = "prev", Right = "this" });
            Spreads.Add(new SpreadDef { Id = "S1", Tab = "一 测点", MinPhase = Phase.P1, Left = "sketch1983", Right = "found1" });
            Spreads.Add(new SpreadDef { Id = "S2", Tab = "二 异响", MinPhase = Phase.P2, Left = "note1983", Right = "printout2026" });
            Spreads.Add(new SpreadDef { Id = "S3", Tab = "三 护罩", MinPhase = Phase.P3, Left = "brief1987", Right = "change1987" });
            Spreads.Add(new SpreadDef { Id = "S3b", Tab = "三 工票", MinPhase = Phase.P3, Left = "ticket1987", Right = "found3" });
            Spreads.Add(new SpreadDef { Id = "S4", Tab = "四 天车", MinPhase = Phase.P4, Left = "tapeLabel", Right = "found4" });
            Spreads.Add(new SpreadDef { Id = "S5", Tab = "规则", MinPhase = Phase.P1, Left = "rules", Right = "rules" });

            // ---------- 台词（世界观圣经 §3.3：周建国 ≤10 句，本 Demo 用 8 句；孙桂兰只在磁带里） ----------
            L("J1", "周建国", "对讲机", "本子在桌上，按单子走。");
            L("J2", "周建国", "对讲机", "柜子我一直没清。");
            L("J3", "周建国", "对讲机", "嗯。");
            L("J4", "周建国", "对讲机", "那个不用管。");
            L("J5", "周建国", "对讲机", "那年我二十一。");
            L("J6", "周建国", "对讲机", "抽屉里有盘磁带。");
            L("J7", "周建国", "入画", "我签。");
            L("J8", "周建国", "入画", "左边那个咔哒，你听见了吗？");
            L("S1", "孙桂兰", "磁带", "开天车啊，稳不稳看手。");
            L("S2", "孙桂兰", "磁带", "我这手，就比钩子早半拍。");
            L("S3", "广播站", "磁带", "这里是厂广播站……");

            // ---------- 剧情时间（世界观圣经 §1.3：13–15 分钟游玩对应 16:00–24:00 中班） ----------
            T(Phase.Intro, 16 * 60, 16 * 60, "", "day1600");
            T(Phase.P1, 16 * 60, 16 * 60 + 40, "十六点　点检第一项", "day1600");
            T(Phase.P2, 17 * 60 + 30, 18 * 60 + 20, "十七点三十分　点检第二项", "dusk1730");
            T(Phase.P3, 20 * 60, 20 * 60 + 50, "二十点　点检第三项", "night");
            T(Phase.P4, 22 * 60, 22 * 60 + 50, "二十二点　点检第四项", "night");
            T(Phase.Ending, 23 * 60 + 48, 23 * 60 + 58, "二十三点五十分　交班", "night");

            // ---------- 每个阶段开始时衡自动写的日志（事实链各题“衡最初的问题”） ----------
            PhaseStartLogs[Phase.P1] = new List<HengLine> {
                new HengLine("SYS", "中班开始。班长 周建国，调试 林小满。"),
                new HengLine("MON", "LT01 后轴承：R1 几乎全是齿轮声，轴承听不清。", "warn"),
                new HengLine("MON", "请确认正式测点。"),
            };
            PhaseStartLogs[Phase.P2] = new List<HengLine> {
                new HengLine("DIAG", "通用模型判定：主轴变速齿轮磨损（置信度 0.71）。", "warn"),
                new HengLine("DIAG", "建议停机更换齿轮。", "warn"),
            };
            PhaseStartLogs[Phase.P3] = new List<HengLine> {
                new HengLine("TWIN", "新程序主轴启动条件比对（离线）：旧回路有 1 项未登记条件（护罩相关）。", "warn"),
                new HengLine("TWIN", "图纸来源：2025 年数字化图纸 Rev.A，未见该继电器。"),
                new HengLine("SYS", "旧回路照常工作。LT01 仍受旧继电器保护。"),
            };
            PhaseStartLogs[Phase.P4] = new List<HengLine> {
                new HengLine("CR01", "自动试吊准备：绳长 4.0m，摆动周期估计 4.0s。"),
                new HengLine("CR01", "防摇整形：两段幅值已计算。第二段指令时刻 1.00 拍。"),
            };
            PhaseStartLogs[Phase.Ending] = new List<HengLine> {
                new HengLine("SYS", "四项点检交接完成。请原班长确认交接。"),
            };
        }

        void E(string id, Phase ph, string name, string shortName, string paper, string desc, EvidenceGrade g)
            => Evidence.Add(new EvidenceDef { Id = id, Phase = ph, Name = name, Short = shortName, Paper = paper, Desc = desc, Grade = g });

        void P(string id, Phase min, string title, string kind, string gives = null)
            => Pages.Add(new PageDef { Id = id, MinPhase = min, Title = title, Kind = kind, GivesEvidence = gives });

        void L(string id, string who, string via, string text) => Lines[id] = new LineDef { Id = id, Speaker = who, Via = via, Text = text };

        void T(Phase p, int start, int end, string card, string light) => Time.Add(new TimeAnchor { Phase = p, StartMinute = start, EndMinute = end, Card = card, Light = light });
    }
}

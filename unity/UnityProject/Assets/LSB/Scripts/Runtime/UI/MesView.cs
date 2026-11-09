// 《最后一次交班》Demo α —— 衡的屏幕（MES / 预测维护，控制间 N7）
// 冷白数字 UI（UI-DS §6 纸张 UI 与冷白 UI 对应表）：设备 / 日志 / 知识库 / 诊断 / 交接 / 等级 / 点检。
// 衡不提示答案，只报冲突。谜题二里，这里能看到衡最初的误判“齿轮磨损，停机换齿轮”，规则确认后改口。
using System.Linq;
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class MesView : ModalView
    {
        public override string Id => "mes";
        static readonly string[] LampNames = { "测点", "诊断", "联锁", "天车" };

        public MesView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "sys") { Panel.AddToClassList("mes"); }

        protected override void Build(string arg) => Render();

        protected override void OnEvent(GameEvent e)
        {
            if (e.Type == Ev.HengLog || e.Type == Ev.RuleStatusChanged || e.Type == Ev.EvidenceAcquired || e.Type == Ev.FlagChanged) Panel.schedule.Execute(Render);
        }

        bool Conf(string id) => g.State.Rule(id) == RuleStatus.Confirmed;

        void Render()
        {
            var s = g.State;
            Panel.Clear();
            var top = Box("mes-top");
            top.Add(L("衡", "mes-logo"));
            top.Add(L("设备监测与维护　三号车间", "sys-dim"));
            top.Add(L($"中班　{s.Clock}", "sys-dim"));
            Panel.Add(top);

            var cols = Box("mes-cols");
            // ---- 左：设备 + 日志 ----
            var c1 = Box("mes-col");
            var asset = Sec(c1, "设备", "三号车间");
            Row(asset, "LT01", "车床主轴", Conf(RuleIds.ZhouClick) ? "运行　后轴承列入计划检修" : s.Phase >= Phase.P2 ? "判定：变速齿轮磨损　建议停机" : "运行", !Conf(RuleIds.ZhouClick) && s.Phase >= Phase.P2 ? "warn" : "");
            Row(asset, "LT01", "护罩 / 启动条件", Conf(RuleIds.JianguoGuard) ? "启动许可已补回" : "旧回路", "");
            Row(asset, "CR01", "天车", Conf(RuleIds.SunHalfbeat) ? "防摇：第二段在半拍" : "待试吊", "");
            var log = Sec(c1, "日志", s.Log.Count + " 条");
            log.AddToClassList("mes-log");
            foreach (var l in s.Log.Skip(System.Math.Max(0, s.Log.Count - 12)))
            {
                var line = L($"[{l.Time}] {l.Module} > {(l.Kind == "warn" ? "! " : "")}{l.Text}", "mes-line");
                if (!string.IsNullOrEmpty(l.Kind)) line.AddToClassList(l.Kind);
                log.Add(line);
            }
            cols.Add(c1);

            // ---- 中：知识库 + 诊断 ----
            var c2 = Box("mes-col");
            var kb = Sec(c2, "知识库", $"{RuleIds.All.Count(Conf)}/4");
            foreach (var id in RuleIds.All)
            {
                var card = g.Rules.Card(id);
                var st = s.Rule(id);
                var r = Box("mes-rule");
                r.EnableInClassList("none", st == RuleStatus.None);
                r.Add(L(id, "f-mono", "mes-rule-id"));
                r.Add(L($"{card.Type}　来源 {card.Owner}", "sys-dim"));
                r.Add(L(st == RuleStatus.None ? "—" : st.ToString().ToUpperInvariant(), "f-mono", "mes-rule-st"));
                kb.Add(r);
            }
            var diag = Sec(c2, "诊断", s.Phase == Phase.P3 ? "LT01 启动条件" : "LT01 主轴");
            if (s.Phase == Phase.P3)
            {
                // 事实链 E3-1：衡的比对日志在衡的屏幕上
                Row(diag, "比对", "", "新程序主轴启动条件，对照旧回路（离线）", "");
                Row(diag, "", "按启动", "旧回路 有　新程序 有", "");
                Row(diag, "", "护罩合上", g.Puzzles.TwinRestored ? "旧回路 有　孪生里已补回" : "旧回路 有　新程序 缺", g.Puzzles.TwinRestored ? "ok" : "warn");
                Row(diag, "图纸", "", "2025 年数字化图纸 Rev.A，没有这颗继电器", "");
                Row(diag, "现在", "", "旧继电器还在工作，车床仍受它保护", "");
                var tw = B("离线孪生 ▶", () => host.Push("twin"), "sys-btn");
                diag.Add(tw);
            }
            else if (s.Phase >= Phase.P2)
            {
                Row(diag, "17:30", "", "通用模型：变速齿轮磨损（0.71）→ 停机换齿轮", Conf(RuleIds.ZhouClick) ? "struck" : "warn");
                Row(diag, Pro ? "L1" : "左边", "", s.Has("E2_1") ? (Pro ? "周期冲击，间隔 0.29 秒，本机共振带内" : "一下一下的咔哒，间隔 0.29 秒") : "未标记", "");
                Row(diag, Pro ? "R1" : "右边", "", s.Has("E2_2") ? (Pro ? "边频指标 0.18 < 0.35，齿轮正常" : "齿轮指示在绿区：正常") : "未判断", "");
                if (s.Has("E2_4")) Row(diag, "维修", "", "变速齿轮 2022 年换过", "");
                int conflicts = s.Log.Count(l => l.Kind == "conflict");
                Row(diag, "冲突", "", conflicts > 0 ? $"规则冲突 ×{conflicts}" : "无", "");
                Row(diag, "结论", "", Conf(RuleIds.ZhouClick) ? "后轴承早期缺陷，列入计划检修" : "待经验规则", Conf(RuleIds.ZhouClick) ? "ok" : "");
            }
            else diag.Add(L("暂无诊断任务。", "sys-dim"));
            cols.Add(c2);

            // ---- 右：交接 + 等级 + 点检 ----
            var c3 = Box("mes-col");
            var ho = Sec(c3, "交接", $"{s.LampsOff}/4");
            for (int i = 0; i < 4; i++) Row(ho, $"{i + 1}", LampNames[i], s.Lamps[i] ? (i == 2 ? "旧回路" : "人工") : "衡", s.Lamps[i] ? "" : "ok");
            var lv = Sec(c3, "等级", g.Rules.LevelName);
            var ladder = Box("promo-ladder", "sys");
            for (int i = 0; i < KnowledgeRuleSystem.Levels.Length; i++)
            {
                var step = L(KnowledgeRuleSystem.Levels[i], "promo-step");
                step.EnableInClassList("reached", i <= s.Level);
                step.EnableInClassList("new", i == s.Level);
                ladder.Add(step);
            }
            lv.Add(ladder);
            var ck = Sec(c3, "点检", "");
            foreach (var row in g.Content.CheckRows)
            {
                int i = PhaseUtil.PuzzleIndex(row.Phase);
                Row(ck, "", row.Item, !s.Lamps[i] ? "完成" : row.Phase == s.Phase ? "进行中" : "—", !s.Lamps[i] ? "ok" : "");
            }
            cols.Add(c3);
            Panel.Add(cols);

            var foot = Box("mes-foot");
            foot.Add(B("返回（Esc）", CloseSelf, "sys-btn"));
            foot.Add(L("衡不提示答案，只报冲突。", "sys-dim"));
            Panel.Add(foot);
        }

        static VisualElement Sec(VisualElement col, string title, string right)
        {
            var sec = Box("mes-sec");
            var h = Box("mes-h");
            h.Add(L(title, "mes-h-t")); h.Add(L(right, "sys-dim"));
            sec.Add(h);
            col.Add(sec);
            return sec;
        }

        static void Row(VisualElement sec, string id, string name, string value, string kind)
        {
            var r = Box("mes-row");
            if (!string.IsNullOrEmpty(id)) r.Add(L(id, "mes-id"));
            if (!string.IsNullOrEmpty(name)) r.Add(L(name, "mes-name"));
            var v = L(value, "mes-val");
            if (!string.IsNullOrEmpty(kind)) v.AddToClassList(kind);
            r.Add(v);
            sec.Add(r);
        }
    }
}

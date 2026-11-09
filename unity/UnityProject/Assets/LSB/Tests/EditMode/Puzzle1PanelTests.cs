// 《最后一次交班》Demo α —— 2B：谜题一按 Unity 面板的操作路径走一遍
// 面板（DiagView / LedgerView / TriadView / RuleCardView）只调用下面这些逻辑层方法；这里按玩家在面板里的点击顺序调用，
// 证明正式界面接上以后流程能从“更衣柜”一直走到“第一盏灯熄灭、进入第二项”。这是自动化功能验证，不是试玩数据。
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class Puzzle1PanelTests
    {
        static GameSession NewP1()
        {
            var g = new GameSession();
            g.Flow.NewGame();
            g.Flow.FinishIntro();
            Assert.AreEqual(Phase.P1, g.State.Phase);
            return g;
        }

        [Test]
        public void PanelPath_LockerToFirstLampOff()
        {
            var g = NewP1();
            var a = new AutoPlayer(g);
            int captionsBefore(string text) => g.Bus.History.Count(e => e.Type == Ev.Caption && e.A == text);
            const string ready = "证据齐了。翻开交班本，填三联板。";

            // 更衣柜 → 手套 → 工作照（阅读器）
            a.Goto("N2");
            a.Use("locker_zhou"); a.Use("gloves"); a.Use("badge_photo");
            Assert.IsTrue(g.State.Has("E1_1") && g.State.Has("E1_2"));
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "reader" && e.B == "photo"), "工作照应在阅读器里打开");

            // 历史 L1 测点：手电照 → 听针进特写，平板打开并开始听左边
            a.Goto("N4");
            a.Use("l1_mark", ToolId.Torch);
            Assert.IsTrue(g.State.Has("E1_3"));
            a.Use("l1_mark", ToolId.Probe);
            Assert.AreEqual("CU1", g.State.CloseUp);
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "diag" && e.B == "L1"));
            Assert.IsTrue(g.State.Flag("heard_L1"));

            // 平板上：只听了左边就选 → 提示两边都听；点“听右边” → 再选左边
            Assert.IsFalse(g.Puzzles.CompareChoose("L1").Ok);
            g.Puzzles.Listen("R1");                       // DiagView 的“听右边”按钮
            Assert.IsTrue(g.State.Flag("heard_R1"));
            Assert.IsTrue(g.Puzzles.CompareChoose("L1").Ok);
            Assert.IsTrue(g.State.Has("E1_5"));
            Assert.AreEqual(0, captionsBefore(ready), "测点示意还没读，三联“经验判断”还锁着，不该提示去填三联板");

            // 交班本“一 测点”：点页面细看 → 贴页 4；这时三格正确项都解锁，提示一次
            Assert.IsTrue(g.Ledger.HasNew("sketch1983"));
            Assert.IsTrue(g.Ledger.ReadPage("sketch1983"));
            Assert.IsTrue(g.State.Has("E1_4"));
            Assert.IsTrue(g.Triad.Ready);
            Assert.AreEqual(1, captionsBefore(ready));
            g.Ledger.ReadPage("sketch1983");
            Assert.AreEqual(1, captionsBefore(ready), "只提示一次");

            // 三联板：一格填错 → 冲突、不扣分；改对 → 草拟
            Assert.IsTrue(g.Triad.CanOpen(out _));
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e3"); g.Triad.Pick("data", "d1");
            var bad = g.Triad.Confirm();
            Assert.AreEqual(TriadOutcome.Conflict, bad.Outcome);
            Assert.IsNotNull(bad.Feedback.FbLog, "冲突时面板要显示衡的那句反馈");
            g.Triad.Pick("exp", "e1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            Assert.AreEqual(RuleStatus.Draft, g.State.Rule(RuleIds.ZhouLeft));

            // 规则卡：600ms 待验证 → 1400ms 确认（同一次调用里灭灯、升级）
            Assert.IsTrue(g.Rules.CanSetPending(RuleIds.ZhouLeft));
            Assert.IsTrue(g.Rules.SetPending(RuleIds.ZhouLeft));
            Assert.IsTrue(g.State.Lamps[0], "待验证时灯还亮着");
            int lampEvents = g.Bus.History.Count(e => e.Type == Ev.LampOff);
            Assert.IsTrue(g.Rules.Confirm(RuleIds.ZhouLeft));
            Assert.IsFalse(g.State.Lamps[0]);
            Assert.AreEqual(lampEvents + 1, g.Bus.History.Count(e => e.Type == Ev.LampOff));
            Assert.AreEqual("一级", g.Rules.LevelName);
            Assert.IsTrue(g.State.PuzzleSolved);

            // 等级通知收起 → 第二项
            g.Flow.CompletePuzzle();
            Assert.AreEqual(Phase.P2, g.State.Phase);
            Assert.AreEqual("17:30", g.State.Clock);
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.TransitionCard && e.A.StartsWith("十七点三十分")));
        }

        /// <summary>规则卡动画中途存档 / 读档：规则停在草拟或待验证，读档后规则卡能接着走完（GameBootstrap.ResumeAfterLoad）。</summary>
        [Test]
        public void SaveDuringRuleCardThenResume()
        {
            var g = NewP1();
            var a = new AutoPlayer(g);
            a.Goto("N2"); a.Use("locker_zhou"); a.Use("badge_photo");
            a.Goto("N4"); a.Use("l1_mark", ToolId.Torch); a.Use("l1_mark", ToolId.Probe); g.Puzzles.Listen("R1");
            g.Puzzles.CompareChoose("L1"); g.Ledger.ReadPage("sketch1983");
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e1"); g.Triad.Pick("data", "d1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);

            string saved = null; g.Save(s => saved = s);
            var g2 = new GameSession();
            g2.ReplaceState(SaveCodec.Decode(saved));
            Assert.AreEqual(RuleStatus.Draft, g2.State.Rule(RuleIds.ZhouLeft));
            Assert.IsFalse(g2.Triad.CanOpen(out _), "三联已一致，不该再填一次");
            Assert.IsTrue(g2.Rules.SetPending(RuleIds.ZhouLeft) && g2.Rules.Confirm(RuleIds.ZhouLeft));

            g2.Save(s => saved = s);
            var g3 = new GameSession();
            g3.ReplaceState(SaveCodec.Decode(saved));
            Assert.IsTrue(g3.State.PuzzleSolved, "确认后、收起等级通知前存档：读档后应回到规则卡并能进入下一项");
            g3.Flow.CompletePuzzle();
            Assert.AreEqual(Phase.P2, g3.State.Phase);
        }

        /// <summary>谜题一、二主层文字（三联选项、一二级提示、证据名和说明）里不出现专业词（UX 原则：kHz / 边频 / R1 等放第二层）。</summary>
        [Test]
        public void PuzzleOneMainLayerIsPlainLanguage()
        {
            var c = new ContentDB();
            var phases = new[] { Phase.P1, Phase.P2 };   // 2C 起把谜题二也纳入
            var main = phases.SelectMany(ph => c.Triad(ph).Columns.SelectMany(col => col.Options.Select(o => o.Text)))
                .Concat(c.Hints.Where(h => phases.Contains(h.Phase)).SelectMany(h => h.Levels.Take(2)))
                .Concat(c.Evidence.Where(e => phases.Contains(e.Phase)).SelectMany(e => new[] { e.Name, e.Desc }));
            foreach (var s in main)
                foreach (var w in new[] { "kHz", "Hz", "GMF", "R1", "信噪比", "边频", "啮合" })
                    Assert.IsFalse(s.Contains(w), $"主层文字“{s}”里出现了专业词 {w}");
        }

        /// <summary>谜题一最少要做几步（自动玩家的路线）：给设计估时用，不是试玩数据。</summary>
        [Test]
        public void ReportMinimalStepsP1()
        {
            var g = NewP1();
            var a = new AutoPlayer(g);
            a.SolveP1();
            g.Flow.CompletePuzzle();
            var moves = g.Playtest.Entries.Count(e => e.Type == "move");
            var uses = g.Playtest.Entries.Count(e => e.Type == "interact");
            TestContext.WriteLine($"谜题一最少操作：走动 {moves} 次，点物件 {uses} 次，读页 {g.State.ReadPages.Count} 页，自动玩家记录 {a.Trace.Count} 条");
            Assert.AreEqual(Phase.P2, g.State.Phase);
        }
    }
}

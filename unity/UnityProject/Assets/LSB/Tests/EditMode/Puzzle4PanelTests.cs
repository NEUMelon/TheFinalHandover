// 《最后一次交班》Demo α —— 2E：谜题四按 Unity 面板的操作路径走一遍（自动化功能验证，不是试玩数据）
// 天车试吊（水洒）→ 抽屉里的磁带 → 孙桂兰采访 + 一九九八年铃声显影 → 衡标铃点 → 调第二段收车时刻 → 水稳 → SUN_HALFBEAT。
// 核心要求（用户阶段 2 消息）：玩家改变控制时机，摆动结果真的改变——结果全由 CraneZvModel 积分得出，不是写死的分支。
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class Puzzle4PanelTests
    {
        static GameSession NewP4() { var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P4); return g; }

        static void Tape(GameSession g, AutoPlayer a)
        {
            a.Goto("N1");
            a.Use("drawer");
            if (g.State.CloseUp != "CU5") a.CloseUp("CU5");
            a.Use("tape"); a.Use("recorder");
            Assert.IsTrue(g.Puzzles.PlayTape().Count > 0);
            g.Puzzles.TapeFinished();
        }

        [Test]
        public void PanelPath_SpillTapeTuneHalfbeat()
        {
            var g = NewP4();
            var a = new AutoPlayer(g);

            // 天车：点平板 → 特写 + 衡自动试吊一次：第二段在一整拍，水洒
            a.Goto("N8");
            a.Use("crane_tablet");
            Assert.AreEqual("CU4", g.State.CloseUp);
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "crane"));
            Assert.IsTrue(g.State.Has("E4_1"));
            Assert.AreEqual(CraneOutcome.Spill, g.Puzzles.LastTrial.Outcome);
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J6"));

            // 没听磁带就挪到半拍：水是稳了，但没有依据，不给 E4_TUNE
            g.Puzzles.SetT2(0.5f);
            Assert.AreEqual(CraneOutcome.Still, g.Puzzles.Trial().Outcome);
            Assert.IsFalse(g.State.Has("E4_TUNE"));
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("没有依据")));
            Assert.IsFalse(g.Triad.CanOpen(out _), "没调好不能填三联");

            // 磁带：放完 → 一九九八年铃声显影 + 衡标铃点（间隔 = 周期的一半）
            Tape(g, a);
            Assert.IsTrue(g.State.Has("E4_2") && g.State.Has("E4_3") && g.State.Has("E4_4"));
            Assert.AreEqual("m1998", g.Bus.History.Last(e => e.Type == Ev.MemoryDevelop).A);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("等于当前摆动周期的一半")));
            foreach (var id in g.Content.TapeScript) Assert.IsTrue(g.Dialogue.Spoken.Contains(id), "磁带台词都播了：" + id);

            // 回到天车：先挪回一整拍试一次（还是洒），再挪到半拍（稳）
            a.Goto("N8"); a.Use("crane_tablet");
            g.Puzzles.SetT2(1f);
            Assert.AreEqual(CraneOutcome.Spill, g.Puzzles.Trial().Outcome);
            Assert.IsFalse(g.State.Has("E4_TUNE"));
            g.Puzzles.SetT2(0.5f);
            Assert.AreEqual(CraneOutcome.Still, g.Puzzles.Trial().Outcome);
            Assert.IsTrue(g.State.Has("E4_TUNE"));

            // 三联：固定 2 秒 → 衡用 6 m 绳长复算，失效；一整拍 → 洒；半拍 → 成立
            Assert.IsTrue(g.Triad.CanOpen(out _));
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e1"); g.Triad.Pick("data", "d2");
            var bad = g.Triad.Confirm();
            Assert.AreEqual(TriadOutcome.Conflict, bad.Outcome);
            StringAssert.Contains("6 m", bad.Feedback.FbLog);
            g.Triad.Pick("data", "d3");
            Assert.AreEqual(TriadOutcome.Conflict, g.Triad.Confirm().Outcome);
            g.Triad.Pick("data", "d1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            Assert.AreEqual(RuleStatus.Draft, g.State.Rule(RuleIds.SunHalfbeat));

            Assert.IsTrue(g.Rules.SetPending(RuleIds.SunHalfbeat));
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("绳长改为 6.0m 复算")), "衡换绳长复核");
            Assert.IsTrue(g.Rules.Confirm(RuleIds.SunHalfbeat));
            Assert.IsFalse(g.State.Lamps[3], "第四盏灯熄灭");
            Assert.AreEqual(4, g.State.LampsOff);
            g.Flow.CompletePuzzle();
            Assert.AreEqual(Phase.Ending, g.State.Phase);
            Assert.AreEqual(EndingStep.HandoverRequest, g.State.Ending);
        }

        /// <summary>玩家改变控制时机，结果真的改变：同一绳长下扫一遍第二段时刻，残余摆动随离半拍的距离变大。</summary>
        [Test]
        public void ControlTimingChangesOutcome()
        {
            float R(float b) => CraneZvModel.Simulate(CraneZvModel.DefaultRope, b).Residual;
            Assert.AreEqual(CraneOutcome.Spill, CraneZvModel.Simulate(4f, 1.00f).Outcome);
            Assert.AreEqual(CraneOutcome.Shake, CraneZvModel.Simulate(4f, 0.60f).Outcome);
            Assert.AreEqual(CraneOutcome.Shake, CraneZvModel.Simulate(4f, 0.40f).Outcome);
            Assert.AreEqual(CraneOutcome.Still, CraneZvModel.Simulate(4f, 0.50f).Outcome);
            Assert.Less(R(0.50f), R(0.55f));
            Assert.Less(R(0.55f), R(0.60f));
            Assert.Less(R(0.60f), R(0.70f));
            Assert.Less(R(0.70f), R(1.00f) + 1e-3f);
            // 曲线本身不同：半拍和一整拍停车后吊钩摆幅差一个量级
            var still = CraneZvModel.Simulate(4f, 0.5f); var spill = CraneZvModel.Simulate(4f, 1f);
            Assert.Less(still.ResidualAmp * 5, spill.ResidualAmp);

            // 规则换绳长也成立：6 m 时“半拍”仍静止；“固定 2 秒”失效，三联反馈里的数字与仿真一致
            Assert.AreEqual(CraneOutcome.Still, CraneZvModel.Simulate(6f, 0.5f).Outcome);
            float b2 = CraneZvModel.SecondsToBeats(2f, 6f);
            var fixed2 = CraneZvModel.Simulate(6f, b2);
            Assert.AreNotEqual(CraneOutcome.Still, fixed2.Outcome);
            var d2 = new ContentDB().Triad(Phase.P4).Columns.First(c => c.Key == "data").Options.First(o => o.Id == "d2").FbLog;
            StringAssert.Contains($"{b2:0.00} 拍", d2);
            StringAssert.Contains($"{fixed2.Residual * 100:0}%", d2);
        }

        /// <summary>事实链 §5：偏 0.05–0.10 拍 = 水面轻晃、未洒，可以确认（衡提示可再精调）。</summary>
        [Test]
        public void ShakeIsConfirmableAfterTape()
        {
            var g = NewP4();
            var a = new AutoPlayer(g);
            a.Goto("N8"); a.Use("crane_tablet");
            Tape(g, a);
            a.Goto("N8"); a.Use("crane_tablet");
            g.Puzzles.SetT2(0.6f);
            Assert.AreEqual(CraneOutcome.Shake, g.Puzzles.Trial().Outcome);
            Assert.IsTrue(g.State.Has("E4_TUNE"));
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("也可以再精调")));
        }

        /// <summary>只能按 0.05 拍一格挪，范围 0.20–1.20 拍；第四项以外挪不动。</summary>
        [Test]
        public void T2SnapsAndClamps()
        {
            var g = NewP4();
            Assert.AreEqual(1f, g.State.CraneT2, 1e-4f, "初始在一整拍（和衡自动试吊一样）");
            Assert.AreEqual(0.55f, g.Puzzles.SetT2(0.56f), 1e-4f);
            Assert.AreEqual(0.20f, g.Puzzles.SetT2(-3f), 1e-4f);
            Assert.AreEqual(1.20f, g.Puzzles.SetT2(9f), 1e-4f);
            var g1 = new GameSession(); AutoPlayer.JumpTo(g1, Phase.P1);
            Assert.IsNull(g1.Puzzles.Trial());
        }

        /// <summary>试吊调到一半存档，读档后时刻、磁带、铃点都在。</summary>
        [Test]
        public void SaveDuringP4ThenResume()
        {
            var g = NewP4();
            var a = new AutoPlayer(g);
            a.Goto("N8"); a.Use("crane_tablet");
            Tape(g, a);
            g.Puzzles.SetT2(0.65f);
            string saved = null; g.Save(s => saved = s);
            var g2 = new GameSession();
            g2.ReplaceState(SaveCodec.Decode(saved));
            Assert.AreEqual(0.65f, g2.State.CraneT2, 1e-4f);
            Assert.IsTrue(g2.State.Has("E4_2") && g2.State.Has("E4_4"));
            g2.Puzzles.SetT2(0.5f);
            Assert.AreEqual(CraneOutcome.Still, g2.Puzzles.Trial().Outcome);
            Assert.IsTrue(g2.State.Has("E4_TUNE"));
        }

        /// <summary>谜题四主层文字不出现 ZV / 整形 / 相位之类的专业词（专业说法在第二层）。</summary>
        [Test]
        public void PuzzleFourMainLayerIsPlainLanguage()
        {
            var c = new ContentDB();
            var main = c.Triad(Phase.P4).Columns.SelectMany(col => col.Options.Select(o => o.Text))
                .Concat(c.Hints.Where(h => h.Phase == Phase.P4).SelectMany(h => h.Levels.Take(2)))
                .Concat(c.Evidence.Where(e => e.Phase == Phase.P4).SelectMany(e => new[] { e.Name, e.Desc }));
            foreach (var s in main)
                foreach (var w in new[] { "ZV", "整形", "相位", "CR01_", "t2" })
                    Assert.IsFalse(s.Contains(w), $"主层文字“{s}”里出现了专业词 {w}");
        }

        /// <summary>谜题四最少要做几步（自动玩家的路线）：给设计估时用，不是试玩数据。</summary>
        [Test]
        public void ReportMinimalStepsP4()
        {
            var g = NewP4();
            int moves0 = g.Playtest.Entries.Count(e => e.Type == "move"), uses0 = g.Playtest.Entries.Count(e => e.Type == "interact");
            new AutoPlayer(g).SolveP4();
            g.Flow.CompletePuzzle();
            int moves = g.Playtest.Entries.Count(e => e.Type == "move") - moves0;
            int uses = g.Playtest.Entries.Count(e => e.Type == "interact") - uses0;
            int trials = g.Playtest.Entries.Count(e => e.Type == "crane_trial");
            TestContext.WriteLine($"谜题四最少操作：走动 {moves} 次，点物件 {uses} 次，玩家试吊 {trials} 次，磁带 {g.Content.TapeScript.Count} 句");
            Assert.AreEqual(Phase.Ending, g.State.Phase);
        }

        /// <summary>第二段时刻 → 残余摆动 / 水的结果（数值积分，给走查文档用）。</summary>
        [Test]
        public void ReportTimingTable()
        {
            var sb = new System.Text.StringBuilder("谜题四时机表（绳长 4 m）：");
            foreach (var b in new[] { 0.20f, 0.30f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.80f, 1.00f, 1.20f })
            {
                var t = CraneZvModel.Simulate(4f, b);
                sb.Append($" {b:0.00}→{t.Residual * 100:0}% {t.Outcome};");
            }
            var six = CraneZvModel.Simulate(6f, 0.5f);
            sb.Append($"　6 m 半拍 {six.Residual * 100:0}% {six.Outcome}");
            TestContext.WriteLine(sb.ToString());
            Assert.Pass();
        }
    }
}

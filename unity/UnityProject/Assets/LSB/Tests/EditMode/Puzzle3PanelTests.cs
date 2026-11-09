// 《最后一次交班》Demo α —— 2D：谜题三按 Unity 面板的操作路径走一遍（自动化功能验证，不是试玩数据）
// 衡的比对单 → 手电照护罩凹痕（记忆显影）→ 螺丝刀开继电器柜 → 红胶布继电器 + 白胶布 → 交班本三张旧纸 → 推断单
// → 三联 → 孪生里补回启动条件、两种情况都试 → JIANGUO_GUARD → 第四项。
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class Puzzle3PanelTests
    {
        static GameSession NewP3() { var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P3); return g; }

        static void FillAll(GameSession g, params (string key, string word)[] fills)
        {
            foreach (var (k, w) in fills) Assert.IsTrue(g.Puzzles.Fill(k, w), $"填不进 {k}={w}");
        }

        static readonly (string, string)[] Correct =
        {
            ("who", "周建国"), ("year", "一九八七年"), ("cause", "护罩未合即开车"), ("proposer", "老周"),
            ("installer", "车间电工"), ("device", "护罩联锁"), ("lost", "图纸数字化"),
        };

        [Test]
        public void PanelPath_DentToJianguoGuard()
        {
            var g = NewP3();
            var a = new AutoPlayer(g);
            Assert.IsTrue(g.State.Has("E3_1"), "第三项一开始就有衡的比对单");
            Assert.IsTrue(g.State.Log.Any(l => l.Module == "TWIN" && l.Text.Contains("未登记条件") && l.Kind == "warn"));
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J4"), "周建国：“那个不用管。”");

            // 护罩：空手只给字幕；手电 → 记忆显影（显影前要静音）+ 贴页 10
            a.Goto("N3");
            a.Use("guard");
            Assert.IsFalse(g.State.Has("E3_2"));
            a.Use("guard", ToolId.Torch);
            Assert.IsTrue(g.State.Has("E3_2"));
            var mem = g.Bus.History.Last(e => e.Type == Ev.MemoryDevelop);
            Assert.AreEqual("m1987", mem.A);
            Assert.IsTrue(g.Content.Memories["m1987"].SilenceBefore, "凹痕显影前 2 秒全静音");
            Assert.AreEqual(3, g.Content.Memories["m1987"].Captions.Length);
            int memCount = g.Bus.History.Count(e => e.Type == Ev.MemoryDevelop);
            a.Use("guard", ToolId.Torch);
            Assert.AreEqual(memCount, g.Bus.History.Count(e => e.Type == Ev.MemoryDevelop), "显影只放一次");

            // 继电器柜：没螺丝刀打不开；开了以后看红胶布继电器 → 贴页 11 + 白胶布（二〇〇八年保留）
            a.Goto("N6");
            a.Tool(ToolId.None);
            a.Use("cabinet_door");
            Assert.IsFalse(g.State.Flag("cabinet_open"));
            a.Use("cabinet_door", ToolId.Driver);
            Assert.IsTrue(g.State.Flag("cabinet_open"));
            a.Tool(ToolId.None);
            a.Use("relay_guard");
            Assert.IsTrue(g.State.Has("E3_3") && g.State.Has("E3_4"));

            // 三联板这时打不开：要先填推断单
            Assert.IsFalse(g.Triad.CanOpen(out var why));
            StringAssert.Contains("推断单", why);

            // 推断单：工票还没读，“周建国”不在词库里
            Assert.IsFalse(g.Puzzles.AvailableWords().Contains("周建国"));
            Assert.IsFalse(g.Puzzles.Fill("who", "周建国"));
            Assert.IsTrue(g.Ledger.ReadPage("brief1987"));
            Assert.IsTrue(g.Ledger.ReadPage("change1987"));
            Assert.IsTrue(g.Ledger.ReadPage("ticket1987"));
            Assert.IsTrue(g.State.Has("E3_6") && g.State.Has("E3_7") && g.State.Has("E3_8"));
            foreach (var (_, w) in Correct) Assert.IsTrue(g.Puzzles.AvailableWords().Contains(w), "证据齐了，正确词都该出现：" + w);

            // 错一处（诱导项：一九九八年电气改造）→ 只说错几处
            FillAll(g, Correct);
            FillAll(g, ("lost", "一九九八年电气改造"));
            var r1 = g.Puzzles.CheckDeduction();
            Assert.IsFalse(r1.Correct);
            Assert.AreEqual("有 1 处不对。", r1.Message);
            Assert.IsFalse(g.State.Has("E3_INF"));
            Assert.IsFalse(g.Dialogue.Spoken.Contains("J5"));
            // 错三处以上 → 不报数
            FillAll(g, ("who", "孙桂兰"), ("year", "一九九八年"), ("proposer", "设备科"));
            Assert.AreEqual("还对不上。再看看那几张旧纸。", g.Puzzles.CheckDeduction().Message);
            // 一个词只能放一个空：把“周建国”放到 who，再放到 proposer，who 就空了
            FillAll(g, ("who", "周建国"), ("proposer", "周建国"));
            Assert.IsNull(g.Puzzles.Filled("who"));
            // 全对
            FillAll(g, Correct);
            var ok = g.Puzzles.CheckDeduction();
            Assert.IsTrue(ok.Correct, ok.Message);
            Assert.IsTrue(g.State.Has("E3_INF"));
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J5"), "推断填对：周建国“那年我二十一。”");
            Assert.IsFalse(g.Puzzles.Fill("who", "孙桂兰"), "填对以后推断单不能再改");

            // 三联：“可以拆掉” → 孪生仿真：护罩开着主轴照样转
            Assert.IsTrue(g.Triad.CanOpen(out _));
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e2"); g.Triad.Pick("data", "d1");
            var bad = g.Triad.Confirm();
            Assert.AreEqual(TriadOutcome.Conflict, bad.Outcome);
            Assert.AreEqual("guardOpenStart", bad.Feedback.FbSim);
            g.Triad.Pick("exp", "e1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            Assert.AreEqual(RuleStatus.Draft, g.State.Rule(RuleIds.JianguoGuard));
            Assert.IsFalse(g.Rules.CanSetPending(RuleIds.JianguoGuard), "孪生里没试过，不能进待验证");

            // 孪生：补回前，护罩开着按启动 → 主轴照样转（不符合要求）
            var before = g.Puzzles.TwinTest("open_start");
            Assert.IsTrue(before.SpindleRuns);
            Assert.IsFalse(before.Pass);
            Assert.IsTrue(g.Puzzles.TwinRestore().Ok);
            var t1 = g.Puzzles.TwinTest("open_start");
            Assert.IsFalse(t1.SpindleRuns, "补回后护罩开着不转");
            Assert.IsTrue(t1.Pass);
            Assert.IsFalse(g.Puzzles.TwinComplete, "只试了一种");
            var t2 = g.Puzzles.TwinTest("closed_start");
            Assert.IsTrue(t2.SpindleRuns && t2.Pass, "补回后护罩合上能转");
            Assert.IsTrue(g.Puzzles.TwinComplete);

            Assert.IsTrue(g.Rules.SetPending(RuleIds.JianguoGuard));
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("离线验证通过")));
            Assert.IsTrue(g.Rules.Confirm(RuleIds.JianguoGuard));
            Assert.IsFalse(g.State.Lamps[2], "第三盏灯熄灭");
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("旧联锁继电器继续在回路中工作")));
            g.Flow.CompletePuzzle();
            Assert.AreEqual(Phase.P4, g.State.Phase);
        }

        /// <summary>三联之前也能在孪生里试现行新程序（看见缺了什么），但不能补。</summary>
        [Test]
        public void TwinBeforeTriadShowsMissingConditionButCannotRestore()
        {
            var g = NewP3();
            var run = g.Puzzles.TwinTest("open_start");
            Assert.IsTrue(run.SpindleRuns);
            Assert.IsFalse(g.Puzzles.TwinRestore().Ok);
            Assert.IsFalse(g.Puzzles.TwinRestored);
            Assert.IsFalse(g.Puzzles.TwinComplete);
        }

        /// <summary>孪生试到一半存档，读档后补回状态和已经试过的情况都在。</summary>
        [Test]
        public void SaveDuringTwinThenResume()
        {
            var g = NewP3();
            var a = new AutoPlayer(g);
            a.Goto("N3"); a.Use("guard", ToolId.Torch);
            a.Goto("N6"); a.Use("cabinet_door", ToolId.Driver); a.Tool(ToolId.None); a.Use("relay_guard");
            a.ReadPage("brief1987"); a.ReadPage("change1987"); a.ReadPage("ticket1987");
            FillAll(g, Correct);
            Assert.IsTrue(g.Puzzles.CheckDeduction().Correct);
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e1"); g.Triad.Pick("data", "d1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            g.Puzzles.TwinRestore();
            g.Puzzles.TwinTest("open_start");

            string saved = null; g.Save(s => saved = s);
            var g2 = new GameSession();
            g2.ReplaceState(SaveCodec.Decode(saved));
            Assert.IsTrue(g2.Puzzles.TwinRestored);
            Assert.IsTrue(g2.State.TwinPassed.Contains("open_start"));
            Assert.IsFalse(g2.Puzzles.TwinComplete);
            g2.Puzzles.TwinTest("closed_start");
            Assert.IsTrue(g2.Puzzles.TwinComplete);
            Assert.IsTrue(g2.Rules.SetPending(RuleIds.JianguoGuard) && g2.Rules.Confirm(RuleIds.JianguoGuard));
        }

        /// <summary>
        /// 谜题三主层文字（三联选项、一二级提示、证据名和说明、推断单）里不出现 PLC / 安全回路之类的专业词，
        /// 也不把谜题写成“玩家写一段 PLC 的 if”（用户阶段 2 消息）。
        /// </summary>
        [Test]
        public void PuzzleThreeMainLayerIsPlainLanguage()
        {
            var c = new ContentDB();
            var main = c.Triad(Phase.P3).Columns.SelectMany(col => col.Options.Select(o => o.Text))
                .Concat(c.Hints.Where(h => h.Phase == Phase.P3).SelectMany(h => h.Levels.Take(2)))
                .Concat(c.Evidence.Where(e => e.Phase == Phase.P3).SelectMany(e => new[] { e.Name, e.Desc }))
                .Concat(c.Deduction.Segments).Concat(c.Deduction.WordBank)
                .Concat(GuardTwinModel.Cases.Select(x => x.Name));
            foreach (var s in main)
                foreach (var w in new[] { "PLC", "plc", "if", "安全相关控制回路", "启动许可", "LT01_", "SIL", "联锁条件" })
                    Assert.IsFalse(s.Contains(w), $"主层文字“{s}”里出现了专业词 {w}");
            // 专业说法放在第二层（三联“专业说法”开关）
            Assert.IsTrue(c.Triad(Phase.P3).Columns.Any(col => col.Options.Any(o => o.Pro.Contains("安全相关控制回路"))));
        }

        /// <summary>谜题三最少要做几步（自动玩家的路线）：给设计估时用，不是试玩数据。</summary>
        [Test]
        public void ReportMinimalStepsP3()
        {
            var g = NewP3();
            int moves0 = g.Playtest.Entries.Count(e => e.Type == "move"), uses0 = g.Playtest.Entries.Count(e => e.Type == "interact"), read0 = g.State.ReadPages.Count;
            var a = new AutoPlayer(g);
            a.SolveP3();
            g.Flow.CompletePuzzle();
            int moves = g.Playtest.Entries.Count(e => e.Type == "move") - moves0;
            int uses = g.Playtest.Entries.Count(e => e.Type == "interact") - uses0;
            int reads = g.State.ReadPages.Count - read0;
            TestContext.WriteLine($"谜题三最少操作：走动 {moves} 次，点物件 {uses} 次，读页 {reads} 页，推断单 7 个空，孪生 1 次补回 + 2 次试");
            Assert.AreEqual(Phase.P4, g.State.Phase);
        }
    }
}

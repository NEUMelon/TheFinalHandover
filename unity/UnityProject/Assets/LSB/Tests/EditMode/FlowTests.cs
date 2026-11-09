// 《最后一次交班》Demo α —— 主流程 / 各谜题逻辑测试
// ⚠ 这些是自动化功能验证（流程能不能走通、判定对不对），不是真人试玩数据。
using System;
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class FlowTests
    {
        static GameSession NewSession() { var g = new GameSession(); g.Flow.NewGame(); return g; }

        // =====================================================================
        // 从新游戏一路走到结局（不跳关）
        // =====================================================================
        [Test]
        public void NewGameToFinishedWithoutJumps()
        {
            var g = new GameSession();
            var a = new AutoPlayer(g);
            var phases = new List<string>();
            var lampOrder = new List<int>();
            var endingSteps = new List<EndingStep>();
            var lines = new List<string>();
            g.Bus.Raised += e =>
            {
                if (e.Type == Ev.PhaseChanged) phases.Add(e.A);
                if (e.Type == Ev.LampOff) lampOrder.Add(e.N);
                if (e.Type == Ev.EndingStepChanged) endingSteps.Add((EndingStep)e.N);
                if (e.Type == Ev.Line) lines.Add(e.A);
            };

            g.Flow.NewGame();
            int guard = 0;
            while (g.State.Phase != Phase.Done && guard++ < 10)
            {
                g.Tick(30f);              // 每题模拟 30 秒游玩时间（推进提示计时，不影响判定）
                a.SolveCurrent();
            }

            Assert.AreEqual(Phase.Done, g.State.Phase, string.Join("\n", a.Trace));
            CollectionAssert.IsSubsetOf(new[] { "Intro", "P1", "P2", "P3", "P4", "Ending", "Done" }, phases);
            // 顺序：开场 → 一 → 二 → 三 → 四 → 结局
            var order = phases.Where(p => p != "Title").Distinct().ToList();   // NewGame 会先发一次 Title（替换状态）
            CollectionAssert.AreEqual(new[] { "Intro", "P1", "P2", "P3", "P4", "Ending", "Done" }, order);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, lampOrder, "交接状态灯应按 1→4 依次熄灭");
            Assert.AreEqual(4, g.State.Level);
            Assert.IsTrue(RuleIds.All.All(r => g.State.Rule(r) == RuleStatus.Confirmed));

            // 结局顺序（用户阶段 2 消息 2E）
            CollectionAssert.AreEqual(new[] {
                EndingStep.HandoverRequest, EndingStep.JianguoSign, EndingStep.PullSwitch, EndingStep.LightsOut, EndingStep.Silence,
                EndingStep.Siren, EndingStep.HengBoot, EndingStep.RulesRoll, EndingStep.UnknownSignal, EndingStep.NameAndSign,
                EndingStep.Credits, EndingStep.Finished }, endingSteps);
            Assert.AreEqual("LIN_TAIL_HUM", g.State.PlayerRuleName);
            Assert.IsTrue(g.State.PlayerSigned);

            // 周建国台词：J1 开场，J3 二收尾，J4 三开头，J5 三推断填对，J6 四中段，J7/J8 结局
            foreach (var id in new[] { "J1", "J3", "J4", "J5", "J6", "J7", "J8" }) CollectionAssert.Contains(lines, id);
            Assert.Less(lines.IndexOf("J7"), lines.IndexOf("J8"));
            Assert.LessOrEqual(lines.Distinct().Count(l => g.Content.Lines[l].Speaker == "周建国"), 10);

            // 所有必要证据都拿到了（B-1～B-7 证据细节都进了流程）
            foreach (var e in g.Content.Evidence.Where(e => e.Grade == EvidenceGrade.Necessary))
                Assert.IsTrue(g.State.Has(e.Id), "缺必要证据 " + e.Id);

            // 剧情时间停在 23:58
            Assert.AreEqual("23:58", g.State.Clock);
            StringAssert.Contains("game_finished", g.Playtest.ToCsv());
        }

        [Test]
        public void ClockStaysInsideEachPhaseWindow()
        {
            var g = new GameSession();
            var a = new AutoPlayer(g);
            var bad = new List<string>();
            g.Bus.Raised += e =>
            {
                if (e.Type != Ev.ClockChanged) return;
                var an = g.Content.Anchor(g.State.Phase);
                if (an != null && (e.N < an.StartMinute || e.N > an.EndMinute)) bad.Add($"{g.State.Phase} {e.A}");
            };
            g.Flow.NewGame();
            for (int i = 0; i < 7 && g.State.Phase != Phase.Done; i++) a.SolveCurrent();
            CollectionAssert.IsEmpty(bad);
        }

        [Test]
        public void DevJumpReachesEveryPhaseStart()
        {
            foreach (var p in new[] { Phase.P1, Phase.P2, Phase.P3, Phase.P4, Phase.Ending })
            {
                var g = new GameSession();
                AutoPlayer.JumpTo(g, p);
                Assert.AreEqual(p, g.State.Phase);
                Assert.AreEqual(PhaseUtil.IsPuzzle(p) ? PhaseUtil.PuzzleIndex(p) : 4, g.State.LampsOff);
            }
        }

        [Test]
        public void SaveRoundTripMidPuzzleThree()
        {
            var g = new GameSession();
            AutoPlayer.JumpTo(g, Phase.P3);
            var a = new AutoPlayer(g);
            a.Goto("N3"); a.Use("guard", ToolId.Torch);
            g.Ledger.ReadPage("ticket1987");
            Assert.IsTrue(g.Puzzles.Fill("who", "周建国"));
            string text = null; g.Save(t => text = t);
            var s2 = SaveCodec.Decode(text);
            Assert.IsNotNull(s2);
            Assert.AreEqual(g.State.Phase, s2.Phase);
            CollectionAssert.AreEquivalent(g.State.Evidence, s2.Evidence);
            CollectionAssert.AreEquivalent(g.State.Flags, s2.Flags);
            CollectionAssert.AreEqual(g.State.Lamps, s2.Lamps);
            Assert.AreEqual(g.State.Level, s2.Level);
            Assert.AreEqual(g.State.ClockMinute, s2.ClockMinute);
            Assert.AreEqual("周建国", s2.DeductionFill["who"]);
            Assert.AreEqual(SaveCodec.Encode(g.State), SaveCodec.Encode(s2));
            // 读档后接着玩到通关
            var g2 = new GameSession(); g2.ReplaceState(s2);
            var a2 = new AutoPlayer(g2);
            for (int i = 0; i < 5 && g2.State.Phase != Phase.Done; i++) a2.SolveCurrent();
            Assert.AreEqual(Phase.Done, g2.State.Phase);
        }

        // =====================================================================
        // 谜题一
        // =====================================================================
        [Test]
        public void P1_LockerBeforeGloves_AndCompareNeedsBothSides()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P1);
            var a = new AutoPlayer(g);
            a.Goto("N2");
            a.Use("gloves");
            Assert.IsFalse(g.State.Has("E1_1"), "柜门没开不该拿到手套");
            a.Use("locker_zhou"); a.Use("gloves"); a.Use("badge_photo");
            Assert.IsTrue(g.State.Has("E1_1") && g.State.Has("E1_2"));

            a.Goto("N4");
            a.Use("l1_mark", ToolId.Probe);
            Assert.IsNull(g.State.CloseUp, "没用手电照出记号前，听针不该直接进特写");
            a.Use("l1_mark", ToolId.Torch);
            Assert.IsTrue(g.State.Has("E1_3"));
            a.Use("l1_mark", ToolId.Probe);
            Assert.AreEqual("CU1", g.State.CloseUp);
            Assert.IsFalse(g.Puzzles.CompareChoose("L1").Ok, "只听了左边，不该能比");
            a.Use("r1_sensor", ToolId.Probe);
            Assert.IsFalse(g.Puzzles.CompareChoose("R1").Ok);
            Assert.IsFalse(g.State.Has("E1_5"));
            Assert.IsTrue(g.Puzzles.CompareChoose("L1").Ok);
            Assert.IsTrue(g.State.Has("E1_5"));
        }

        [Test]
        public void P1_TriadLockedOptionsAndConflictDoNotPunish()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P1);
            var t = g.Triad.Current;
            var ok = t.Columns.First(c => c.Key == "exp").Options.First(o => o.Ok);
            Assert.IsTrue(g.Triad.IsLocked(ok), "没证据时正确项应显示“待取证”");
            Assert.IsFalse(g.Triad.Pick("exp", ok.Id));

            var a = new AutoPlayer(g);
            a.Goto("N2"); a.Use("locker_zhou"); a.Use("gloves"); a.Use("badge_photo");
            a.Goto("N4"); a.Use("l1_mark", ToolId.Torch); a.Use("l1_mark", ToolId.Probe); a.Use("r1_sensor", ToolId.Probe);
            a.ReadPage("sketch1983"); g.Puzzles.CompareChoose("L1");

            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e2"); g.Triad.Pick("data", "d1");
            var r = g.Triad.Confirm();
            Assert.AreEqual(TriadOutcome.Conflict, r.Outcome);
            CollectionAssert.AreEqual(new[] { "exp" }, r.WrongColumns);
            Assert.AreEqual(RuleStatus.None, g.State.Rule(RuleIds.ZhouLeft));
            Assert.IsTrue(g.State.Lamps[0]);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("左撇子") || l.Text.Contains("右手")));
            g.Triad.Pick("exp", "e1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            Assert.AreEqual(RuleStatus.Draft, g.State.Rule(RuleIds.ZhouLeft));
            Assert.IsTrue(g.State.Lamps[0], "草案不灭灯，确认才灭");
            g.Rules.SetPending(RuleIds.ZhouLeft); g.Rules.Confirm(RuleIds.ZhouLeft);
            Assert.IsFalse(g.State.Lamps[0]);
            Assert.AreEqual(1, g.State.Level);
        }

        // =====================================================================
        // 谜题二
        // =====================================================================
        [Test]
        public void P2_SpectroJudgeCases()
        {
            Assert.AreEqual("gmf", SpectroModel.Judge(new SpecBox(1, 3, 500, 3800)).Reason);
            Assert.AreEqual("band", SpectroModel.Judge(new SpecBox(1, 3, 4500, 7000)).Reason);
            Assert.AreEqual("few", SpectroModel.Judge(new SpecBox(1.0f, 1.3f, 2200, 3800)).Reason);
            var ok = SpectroModel.Judge(new SpecBox(1, 3, 2200, 3800));
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(7, ok.Impacts);
        }

        [Test]
        public void P2_ThreeDeviationsRaiseHintAndGearIsNormal()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P2);
            var a = new AutoPlayer(g);
            a.Goto("N4"); a.Use("l1_mark", ToolId.Probe);
            Assert.AreEqual(0, g.Hints.Level);
            g.Puzzles.JudgeBox(new SpecBox(1, 3, 500, 3800));
            g.Puzzles.JudgeBox(new SpecBox(1, 3, 5000, 7000));
            Assert.AreEqual(0, g.Hints.Level);
            g.Puzzles.JudgeBox(new SpecBox(1, 1.2f, 2200, 3800));
            Assert.AreEqual(1, g.Hints.Level, "连续 3 次判偏，页边应出一级提示");
            Assert.IsFalse(g.State.Has("E2_1"));
            g.Puzzles.JudgeBox(new SpecBox(0.5f, 4, 2100, 3900));
            Assert.IsTrue(g.State.Has("E2_1"));

            a.Use("r1_sensor", ToolId.Probe);
            Assert.IsFalse(g.Puzzles.Sideband("bad").Ok);
            Assert.IsFalse(g.State.Has("E2_2"));
            Assert.IsTrue(g.Puzzles.Sideband("ok").Ok);
            Assert.IsTrue(g.State.Has("E2_2"));
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("建议停机更换齿轮")), "开头应有衡的齿轮误判");
        }

        [Test]
        public void P2_ConfirmWithdrawsGearAdvice()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P3);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("撤回")));
        }

        // =====================================================================
        // 谜题三
        // =====================================================================
        [Test]
        public void P3_TwinShowsMissingRequirementThenVerifies()
        {
            Assert.IsTrue(GuardTwinModel.Run("open_start", false).SpindleRuns, "现行新程序：护罩开着也能启动（缺失的安全要求）");
            Assert.IsFalse(GuardTwinModel.Run("open_start", true).SpindleRuns);
            Assert.IsTrue(GuardTwinModel.Run("closed_start", true).SpindleRuns);

            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P3);
            Assert.IsTrue(g.State.Has("E3_1"));
            var a = new AutoPlayer(g);
            Assert.IsFalse(g.Triad.CanOpen(out _), "推断之前三联板不开");
            a.Goto("N3"); a.Use("guard", ToolId.Torch);
            a.Goto("N6"); a.Use("cabinet_door", ToolId.Driver); a.Tool(ToolId.None); a.Use("relay_guard");
            a.ReadPage("brief1987"); a.ReadPage("change1987"); a.ReadPage("ticket1987");
            var d = g.Content.Deduction;
            foreach (var b in d.Blanks) g.Puzzles.Fill(b.Key, b.Answer);
            g.Puzzles.Fill("year", "一九九八年");
            var chk = g.Puzzles.CheckDeduction();
            Assert.IsFalse(chk.Correct); Assert.AreEqual(1, chk.Wrong);
            g.Puzzles.Fill("year", "一九八七年");
            Assert.IsTrue(g.Puzzles.CheckDeduction().Correct);
            Assert.IsTrue(g.State.Has("E3_INF"));

            new AutoPlayer(g).SolveTriad();
            Assert.AreEqual(RuleStatus.Draft, g.State.Rule(RuleIds.JianguoGuard));
            Assert.IsFalse(g.Rules.SetPending(RuleIds.JianguoGuard), "没做离线验证不能进待验证");
            var before = g.Puzzles.TwinTest("open_start");
            Assert.IsTrue(before.SpindleRuns); Assert.IsFalse(before.Pass);
            Assert.IsFalse(g.Puzzles.TwinComplete);
            g.Puzzles.TwinRestore();
            g.Puzzles.TwinTest("open_start");
            Assert.IsFalse(g.Puzzles.TwinComplete, "两种情况都要试");
            g.Puzzles.TwinTest("closed_start");
            Assert.IsTrue(g.Puzzles.TwinComplete);
            Assert.IsTrue(g.Rules.SetPending(RuleIds.JianguoGuard));
        }

        [Test]
        public void P3_WordsAppearOnlyAfterEvidence()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P3);
            CollectionAssert.DoesNotContain(g.Puzzles.AvailableWords(), "周建国");
            CollectionAssert.Contains(g.Puzzles.AvailableWords(), "图纸数字化");   // E3_1 开局就有
            g.Ledger.ReadPage("ticket1987");
            CollectionAssert.Contains(g.Puzzles.AvailableWords(), "周建国");
        }

        // =====================================================================
        // 谜题四
        // =====================================================================
        [Test]
        public void P4_ZvPhysics()
        {
            var full = CraneZvModel.Simulate(4f, 1.0f);
            var half = CraneZvModel.Simulate(4f, 0.5f);
            Assert.AreEqual(CraneOutcome.Spill, full.Outcome);
            Assert.That(full.Residual, Is.InRange(0.9f, 1.1f));
            Assert.AreEqual(CraneOutcome.Still, half.Outcome);
            Assert.Less(half.Residual, 0.05f);
            Assert.AreEqual(4.0f, CraneZvModel.Period(4f), 0.05f);

            // 错误三联 d2：固定 2.0 秒，绳长改 6 m → 0.41 拍，残余约 28%（不静止）
            float b6 = CraneZvModel.SecondsToBeats(2.0f, 6f);
            Assert.AreEqual(0.41f, b6, 0.01f);
            var six = CraneZvModel.Simulate(6f, b6);
            Assert.AreEqual(0.28f, six.Residual, 0.04f);
            Assert.AreNotEqual(CraneOutcome.Still, six.Outcome);

            // 残余随 t2 单调：越接近半拍越小
            float prev = 2;
            foreach (var t in new[] { 1.0f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f })
            {
                var r = CraneZvModel.Simulate(4f, t).Residual;
                Assert.Less(r, prev, "t2=" + t); prev = r;
            }
            // 容差：±0.05 拍静止，±0.10 拍晃
            Assert.AreEqual(CraneOutcome.Still, CraneZvModel.Simulate(4f, 0.55f).Outcome);
            Assert.AreEqual(CraneOutcome.Still, CraneZvModel.Simulate(4f, 0.45f).Outcome);
            Assert.AreEqual(CraneOutcome.Shake, CraneZvModel.Simulate(4f, 0.6f).Outcome);
            Assert.AreEqual(CraneOutcome.Spill, CraneZvModel.Simulate(4f, 0.75f).Outcome);
        }

        [Test]
        public void P4_TuningWithoutTapeHasNoBasis()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P4);
            var a = new AutoPlayer(g);
            a.Goto("N8"); a.Use("crane_tablet");
            Assert.IsTrue(g.State.Has("E4_1"));
            Assert.AreEqual(CraneOutcome.Spill, g.Puzzles.LastTrial.Outcome);
            g.Puzzles.SetT2(0.5f);
            Assert.AreEqual(CraneOutcome.Still, g.Puzzles.Trial().Outcome);
            Assert.IsFalse(g.State.Has("E4_TUNE"), "没听磁带就调对了，也不算有依据");
            Assert.IsFalse(g.Triad.CanOpen(out _));

            a.Goto("N1"); a.Use("drawer"); a.Use("tape"); a.Use("recorder");
            CollectionAssert.AreEqual(new[] { "S3", "S1", "S2" }, g.Puzzles.PlayTape());
            g.Puzzles.TapeFinished();
            Assert.IsTrue(g.State.Has("E4_2") && g.State.Has("E4_3") && g.State.Has("E4_4"));
            a.Goto("N8"); a.Use("crane_tablet");
            g.Puzzles.SetT2(0.8f);
            Assert.AreNotEqual(CraneOutcome.Still, g.Puzzles.Trial().Outcome, "改时刻必须真的改变结果");
            Assert.IsFalse(g.State.Has("E4_TUNE"));
            g.Puzzles.SetT2(0.5f);
            Assert.AreEqual(CraneOutcome.Still, g.Puzzles.Trial().Outcome);
            Assert.IsTrue(g.State.Has("E4_TUNE"));
            Assert.IsTrue(g.Triad.CanOpen(out _));
        }

        [Test]
        public void P4_BellsEveryHalfBeat()
        {
            var b = CraneZvModel.BellTimes(4f, 10f);
            Assert.AreEqual(CraneZvModel.Period(4f) / 2f, b[1] - b[0], 1e-3f);
        }

        // =====================================================================
        // 结局
        // =====================================================================
        [Test]
        public void Ending_SwitchNeedsJianguoSignature()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.Ending);
            Assert.AreEqual(EndingStep.HandoverRequest, g.State.Ending);
            Assert.IsFalse(g.Ending.PullSwitch());
            var a = new AutoPlayer(g);
            a.Goto("N9"); a.Use("main_switch");
            Assert.IsFalse(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "switch"), "原班长签字前总闸打不开");
            g.Ending.Advance(); g.Ending.Advance();
            Assert.AreEqual(EndingStep.PullSwitch, g.State.Ending);
            Assert.IsFalse(g.Ending.Advance(), "拉闸要玩家自己来");
            a.Use("main_switch");
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "switch"));
            Assert.IsTrue(g.Ending.PullSwitch());
            while (g.State.Ending != EndingStep.NameAndSign) Assert.IsTrue(g.Ending.Advance());
            Assert.IsFalse(g.Ending.Advance(), "命名和签名要玩家自己来");
            Assert.IsFalse(g.Ending.NameRule("周"), "只收 A–Z 0–9 _");
            Assert.IsFalse(g.Ending.NameRule("ZHOU_CLICK"), "不能和已有规则重名");
            Assert.IsTrue(g.Ending.Sign());
            Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending);
            Assert.IsTrue(g.Ending.NameRule("tail hum-2"));
            Assert.AreEqual("TAILHUM2", g.State.PlayerRuleName);
            Assert.AreEqual(EndingStep.Credits, g.State.Ending);
        }

        [Test]
        public void RuleNameNormalization()
        {
            Assert.AreEqual("ABC_123", EndingSystem.NormalizeRuleName("abc_123"));
            Assert.AreEqual(20, EndingSystem.NormalizeRuleName(new string('A', 40)).Length);
            Assert.AreEqual("", EndingSystem.NormalizeRuleName("咔哒"));
        }

        // =====================================================================
        // 提示 / 时间
        // =====================================================================
        [Test]
        public void HintsAt90_180_300AndResetOnValidAction()
        {
            var g = NewSession(); g.Flow.FinishIntro();
            Assert.AreEqual("locker", g.Hints.Stage);
            g.Tick(89); Assert.AreEqual(0, g.Hints.Level);
            g.Tick(2); Assert.AreEqual(1, g.Hints.Level);
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J2"), "卡在更衣柜时周建国补一句“柜子我一直没清。”");
            g.Tick(90); Assert.AreEqual(2, g.Hints.Level);
            g.Tick(120); Assert.AreEqual(3, g.Hints.Level);
            var a = new AutoPlayer(g); a.Goto("N2"); a.Use("locker_zhou");
            Assert.AreEqual(0, g.Hints.Timer, 1e-3);
            a.Use("gloves"); a.Use("badge_photo");
            Assert.AreEqual("mark", g.Hints.Stage);
            Assert.AreEqual(0, g.Hints.Level, "卡点变了提示清零");
        }

        [Test]
        public void HintsOffMeansManualOnly()
        {
            var g = NewSession(); g.Flow.FinishIntro();
            g.State.Settings.HintsOn = false;
            g.Tick(400); Assert.AreEqual(0, g.Hints.Level);
            g.Hints.RequestManual(); Assert.AreEqual(1, g.Hints.Level);
        }

        [Test]
        public void EveryStageSeenInPlayHasHints()
        {
            var g = new GameSession(); var a = new AutoPlayer(g);
            var stages = new HashSet<string>();
            g.Bus.Raised += e => { if (e.Type == Ev.HintChanged) stages.Add(g.State.Phase + "." + e.A); };
            g.Flow.NewGame();
            for (int i = 0; i < 7 && g.State.Phase != Phase.Done; i++) a.SolveCurrent();
            foreach (var s in stages.Where(s => !s.EndsWith(".done") && !s.EndsWith(".show") && !s.EndsWith(".none")))
            {
                var parts = s.Split('.');
                var p = (Phase)Enum.Parse(typeof(Phase), parts[0]);
                Assert.IsNotNull(g.Content.Hint(p, parts[1]), "缺提示：" + s);
            }
            Assert.Greater(stages.Count, 15);
        }

        [Test]
        public void ChineseClock()
        {
            Assert.AreEqual("十七点三十分", TimelineSystem.ChineseTime(17 * 60 + 30));
            Assert.AreEqual("二十点整", TimelineSystem.ChineseTime(20 * 60));
            Assert.AreEqual("二十三点五十分", TimelineSystem.ChineseTime(23 * 60 + 50));
        }

        [Test]
        public void SwitchPoseFacesTheSwitch()
        {
            var c = new ContentDB();
            Assert.AreEqual(159f, c.SwitchPose.Yaw, 2f);
        }
    }
}

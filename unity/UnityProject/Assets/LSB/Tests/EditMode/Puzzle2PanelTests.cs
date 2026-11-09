// 《最后一次交班》Demo α —— 2C：谜题二按 Unity 面板的操作路径走一遍（自动化功能验证，不是试玩数据）
// 衡误判“齿轮磨损” → 听针贴左边 → 时频图框选一下一下的竖条 → 平板换到右边看齿轮指示 → 1983 年设备记录 → 三联 → ZHOU_CLICK → 衡改口 → 第三项。
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class Puzzle2PanelTests
    {
        // DiagView 的坐标换算（时频图显示 680×280）
        const float SW = 680, SH = 280;
        static SpecBox Drag(float x0, float y0, float x1, float y1) =>
            new SpecBox(x0 / SW * SpectroModel.Duration, x1 / SW * SpectroModel.Duration, (1 - y0 / SH) * SpectroModel.FMax, (1 - y1 / SH) * SpectroModel.FMax);

        [Test]
        public void PanelPath_GearMisdiagnosisToZhouClick()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P2);
            var a = new AutoPlayer(g);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("齿轮磨损") && l.Kind == "warn"), "第二项一开始，衡先误判齿轮磨损");

            a.Goto("N4");
            a.Use("l1_mark", ToolId.Probe);
            Assert.AreEqual("L1", g.Puzzles.DiagPoint);

            // 框到下面那条横线（齿轮声）→ 不算；框对 → 贴页 6
            Assert.IsFalse(g.Puzzles.JudgeBox(Drag(100, 120, 400, 275)).Ok);
            Assert.IsFalse(g.Puzzles.JudgeBox(Drag(100, 150, 140, 200)).Ok, "只框到一两条竖条不够");
            var ok = g.Puzzles.JudgeBox(Drag(100, 145, 400, 205));
            Assert.IsTrue(ok.Ok, ok.Message);
            Assert.IsTrue(g.State.Has("E2_1"));

            // 平板换到右边：点“异常” → 放对比声音、不给证据；点“正常” → 贴页 7
            g.Puzzles.Listen("R1");
            Assert.IsFalse(g.Puzzles.Sideband("bad").Ok);
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.Sfx && e.A == "ab_gearWear"));
            Assert.IsFalse(g.State.Has("E2_2"));
            Assert.IsTrue(g.Puzzles.Sideband("ok").Ok);
            Assert.IsTrue(g.State.Has("E2_2"));

            // 交班本“二 异响”：1983 年设备记录
            Assert.IsFalse(g.Triad.Ready, "老周的记录还没读，“经验判断”应锁着");
            Assert.IsTrue(g.Ledger.ReadPage("note1983"));
            Assert.IsTrue(g.State.Has("E2_3"));
            Assert.IsTrue(g.Triad.Ready);
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.Caption && e.A.StartsWith("证据齐了")));

            // 三联：选“在右，是齿轮” → 仿真“换过齿轮，咔哒还在”
            g.Triad.Pick("phen", "p1"); g.Triad.Pick("exp", "e2"); g.Triad.Pick("data", "d1");
            var bad = g.Triad.Confirm();
            Assert.AreEqual(TriadOutcome.Conflict, bad.Outcome);
            Assert.AreEqual("gearFixed", bad.Feedback.FbSim);
            g.Triad.Pick("exp", "e1");
            Assert.AreEqual(TriadOutcome.Solved, g.Triad.Confirm().Outcome);
            Assert.IsTrue(g.Rules.SetPending(RuleIds.ZhouClick) && g.Rules.Confirm(RuleIds.ZhouClick));
            Assert.IsFalse(g.State.Lamps[1]);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("撤回")), "确认后衡改口：撤回停机换齿轮");

            g.Flow.CompletePuzzle();
            Assert.AreEqual(Phase.P3, g.State.Phase);
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J3"), "第二项收尾：周建国“嗯。”");
        }

        /// <summary>时频图示意数据：左边冲击在共振带里亮、右边几乎看不到；右边齿轮横线亮。同一测点画两次结果一样。</summary>
        [Test]
        public void SpectroImageShowsClicksLeftAndHumRight()
        {
            float At(string p, float t, float f) => SpectroModel.Energy(p, t, f, () => 0.5f);
            float tImp = SpectroModel.Impacts[3] + 0.002f;
            Assert.Greater(At("L1", tImp, 3000), 0.7f);
            Assert.Less(At("R1", tImp, 3000), 0.4f);
            Assert.Greater(At("R1", 1.5f, SpectroModel.Gmf), At("L1", 1.5f, SpectroModel.Gmf));
            var a = SpectroModel.Seeded("L1"); var b = SpectroModel.Seeded("L1");
            for (int i = 0; i < 100; i++) Assert.AreEqual(a(), b());
            Assert.AreEqual(3, SpectroModel.LastImpact(SpectroModel.Impacts[3] + 0.01f));
            Assert.AreEqual(-1, SpectroModel.LastImpact(0f));
        }

        [Test]
        public void MesOpenGivesGearRepairRecordOnlyInPuzzleTwo()
        {
            var g = new GameSession(); AutoPlayer.JumpTo(g, Phase.P1);
            new AutoPlayer(g).Goto("N7");
            g.Interaction.Use("mes");
            Assert.IsFalse(g.State.Has("E2_4"));
            AutoPlayer.JumpTo(g, Phase.P2);
            new AutoPlayer(g).Goto("N7");
            g.Interaction.Use("mes");
            Assert.IsTrue(g.State.Has("E2_4"));
        }
    }
}

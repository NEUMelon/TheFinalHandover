// 《最后一次交班》Demo α —— 2E：结局只靠每帧 Tick + 玩家的三个动作（拉闸、命名、签名）就能走完（2F 要求不用开发菜单）
// 顺序（用户阶段 2 消息 2E）：周建国确认交接 → 签字 → 拉闸停机 → 逐排熄灯 → 静默 → 汽笛 → 衡冷白启动
//   → 四条规则依次出现 → 新的未登记信号 → 玩家命名 / 签名 → 献词 → 本局完成。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class EndingTests
    {
        static GameSession NewEnding()
        {
            var g = new GameSession();
            AutoPlayer.JumpTo(g, Phase.Ending);
            Assert.AreEqual(Phase.Ending, g.State.Phase);
            return g;
        }

        /// <summary>按 0.1 秒一帧跑 seconds 秒；返回每一步停留的时长。</summary>
        static void Run(GameSession g, float seconds, Dictionary<EndingStep, float> dwell)
        {
            for (float t = 0; t < seconds; t += 0.1f)
            {
                var st = g.State.Ending;
                g.Tick(0.1f);
                dwell[st] = (dwell.TryGetValue(st, out var d) ? d : 0) + 0.1f;
            }
        }

        [Test]
        public void TickDrivesEndingWithOnlyPlayerActions()
        {
            var g = NewEnding();
            var a = new AutoPlayer(g);
            var dwell = new Dictionary<EndingStep, float>();
            Assert.AreEqual(EndingStep.HandoverRequest, g.State.Ending);
            Assert.IsFalse(g.State.Flag("switch_authorized"), "原班长签字以前总闸不能拉");

            Run(g, 30, dwell);
            Assert.AreEqual(EndingStep.PullSwitch, g.State.Ending, "停在拉闸，等玩家");
            Assert.IsTrue(g.Dialogue.Spoken.Contains("J7") && g.Dialogue.Spoken.Contains("J8"));
            Assert.IsTrue(g.State.Flag("switch_authorized"));
            Assert.AreEqual("N9", g.State.Node);
            Assert.IsFalse(g.Ending.Cinematic, "拉闸前还能走动、点东西");

            // 玩家去控制间东墙点总闸 → 交接单面板 → 拉下
            a.Goto("N9");
            a.Use("main_switch");
            Assert.IsTrue(g.Bus.History.Any(e => e.Type == Ev.OpenView && e.A == "switch"));
            Assert.IsTrue(g.Ending.PullSwitch());
            Assert.AreEqual(EndingStep.LightsOut, g.State.Ending);
            Assert.IsTrue(g.Ending.Cinematic);
            Assert.IsFalse(g.Ending.PullSwitch(), "只能拉一次");

            Run(g, 60, dwell);
            Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending, "停在命名 / 签名，等玩家");
            Assert.IsFalse(g.Ending.Cinematic);
            Assert.GreaterOrEqual(dwell[EndingStep.Silence], 4.95f, "静默 5 秒");
            Assert.GreaterOrEqual(dwell[EndingStep.LightsOut], 5.95f);

            // 衡启动后四条规则依次出现，顺序与谜题一致；然后是未登记信号
            var ruleLines = g.State.Log.Where(l => l.Module == "RULE" && RuleIds.All.Contains(l.Text)).Select(l => l.Text).ToArray();
            CollectionAssert.AreEqual(RuleIds.All, ruleLines);
            Assert.IsTrue(g.State.Log.Any(l => l.Text.Contains("未登记微弱信号")));

            // 命名：已有的 ID 不收；空的不收；小写和符号会被规整
            Assert.IsFalse(g.Ending.NameRule("sun_halfbeat"));
            Assert.IsFalse(g.Ending.NameRule("  ！？ "));
            Assert.IsTrue(g.Ending.Sign());
            Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending, "只签名不命名，不往下走");
            Run(g, 20, dwell);
            Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending);
            Assert.IsTrue(g.Ending.NameRule("lin tail-hum 01"));
            Assert.AreEqual("LINTAILHUM01", g.State.PlayerRuleName);
            Assert.AreEqual(EndingStep.Credits, g.State.Ending);
            Assert.IsTrue(g.Ending.Cinematic);

            Run(g, 10, dwell);
            Assert.AreEqual(EndingStep.Finished, g.State.Ending);
            Assert.AreEqual(Phase.Done, g.State.Phase);

            // 步骤顺序
            var steps = g.Bus.History.Where(e => e.Type == Ev.EndingStepChanged).Select(e => (EndingStep)e.N).ToArray();
            CollectionAssert.AreEqual(new[] {
                EndingStep.HandoverRequest, EndingStep.JianguoSign, EndingStep.PullSwitch, EndingStep.LightsOut, EndingStep.Silence,
                EndingStep.Siren, EndingStep.HengBoot, EndingStep.RulesRoll, EndingStep.UnknownSignal, EndingStep.NameAndSign,
                EndingStep.Credits, EndingStep.Finished }, steps);
            float auto = steps.Sum(s => EndingSystem.StepSeconds(s));
            TestContext.WriteLine($"结局自动段合计 {auto:0.0} 秒（不含玩家拉闸、命名、签名）");
        }

        /// <summary>命名、签名哪个先都行。</summary>
        [Test]
        public void NameBeforeSignAlsoFinishes()
        {
            var g = NewEnding();
            new AutoPlayer(g).Goto("N9");
            for (int i = 0; i < 300 && g.State.Ending != EndingStep.PullSwitch; i++) g.Tick(0.1f);
            Assert.IsTrue(g.Ending.PullSwitch());
            for (int i = 0; i < 600 && g.State.Ending != EndingStep.NameAndSign; i++) g.Tick(0.1f);
            Assert.IsTrue(g.Ending.NameRule("XIAOMAN_HUM"));
            Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending);
            Assert.IsTrue(g.Ending.Sign());
            Assert.AreEqual(EndingStep.Credits, g.State.Ending);
        }

        /// <summary>命名的时候存档退出，读档回来还停在命名 / 签名，已签的名字还在。</summary>
        [Test]
        public void SaveAtNameAndSignThenResume()
        {
            var g = NewEnding();
            for (int i = 0; i < 300 && g.State.Ending != EndingStep.PullSwitch; i++) g.Tick(0.1f);
            g.Ending.PullSwitch();
            for (int i = 0; i < 600 && g.State.Ending != EndingStep.NameAndSign; i++) g.Tick(0.1f);
            g.Ending.Sign();
            string saved = null; g.Save(s => saved = s);
            var g2 = new GameSession();
            g2.ReplaceState(SaveCodec.Decode(saved));
            Assert.AreEqual(EndingStep.NameAndSign, g2.State.Ending);
            Assert.IsTrue(g2.State.PlayerSigned);
            for (int i = 0; i < 100; i++) g2.Tick(0.1f);
            Assert.AreEqual(EndingStep.NameAndSign, g2.State.Ending, "读档后不会自己跳过命名");
            Assert.IsTrue(g2.Ending.NameRule("TAIL_HUM"));
            Assert.AreEqual(EndingStep.Credits, g2.State.Ending);
        }
    }
}

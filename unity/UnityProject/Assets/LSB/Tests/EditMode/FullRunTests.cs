// 《最后一次交班》Demo α —— 2F：从新游戏开始完整跑一次，不靠开发菜单
// 只走正式玩家界面能触发的入口：
//   标题页“打卡上班” = Flow.NewGame；开场黑屏 6 秒（或点一下）= Flow.FinishIntro；
//   四题里的点击 / 面板操作（AutoPlayer 只调玩家能做的操作）；规则卡“收起” = Flow.CompletePuzzle；
//   结局只靠每帧 Tick + 拉总闸 + 命名 + 签名。不调 JumpTo、不调 Ending.Advance、不改状态。
// ⚠ 这是自动化功能验证，不是真人试玩数据。
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class FullRunTests
    {
        /// <summary>按 0.1 秒一帧推进，最多 seconds 秒，直到 until 成立。</summary>
        static void Frames(GameSession g, float seconds, System.Func<bool> until = null)
        {
            for (float t = 0; t < seconds; t += 0.1f)
            {
                if (until != null && until()) return;
                g.Tick(0.1f);
            }
        }

        [Test]
        public void NewGameToDoneLikeRuntimeNoDevMenu()
        {
            var g = new GameSession();
            var a = new AutoPlayer(g);
            PlaytestLog.TesterId = PlaytestLog.NormalizeTesterId("p-01");
            try
            {
                g.Flow.NewGame();                              // 标题页“打卡上班（新游戏）”
                Assert.AreEqual(Phase.Intro, g.State.Phase);
                Frames(g, 6f);                                 // 开场黑屏手写字
                g.Flow.FinishIntro();
                Assert.AreEqual(Phase.P1, g.State.Phase);

                a.SolveP1(); Frames(g, 4f); g.Flow.CompletePuzzle();   // 规则卡动画 + 等级通知，玩家点“收起”
                Assert.AreEqual(Phase.P2, g.State.Phase);
                a.SolveP2(); Frames(g, 4f); g.Flow.CompletePuzzle();
                Assert.AreEqual(Phase.P3, g.State.Phase);
                a.SolveP3(); Frames(g, 4f); g.Flow.CompletePuzzle();
                Assert.AreEqual(Phase.P4, g.State.Phase);
                a.SolveP4(); Frames(g, 4f); g.Flow.CompletePuzzle();
                Assert.AreEqual(Phase.Ending, g.State.Phase);

                // 结局：自己往下走，停在拉闸
                Frames(g, 60f, () => g.State.Ending == EndingStep.PullSwitch);
                Assert.AreEqual(EndingStep.PullSwitch, g.State.Ending);
                a.Goto("N9");
                a.Use("main_switch");                          // → 交接单 + 总闸面板
                Assert.IsTrue(g.Ending.PullSwitch());           // 手柄拖到底
                Frames(g, 120f, () => g.State.Ending == EndingStep.NameAndSign);
                Assert.AreEqual(EndingStep.NameAndSign, g.State.Ending);
                Assert.IsTrue(g.Ending.Sign());
                Assert.IsTrue(g.Ending.NameRule("LIN_TAIL_HUM"));
                Frames(g, 30f, () => g.State.Phase == Phase.Done);
                Assert.AreEqual(Phase.Done, g.State.Phase, string.Join("\n", a.Trace));

                Assert.IsTrue(RuleIds.All.All(r => g.State.Rule(r) == RuleStatus.Confirmed));
                Assert.AreEqual(4, g.State.LampsOff);
                Assert.AreEqual("23:58", g.State.Clock);

                // 试玩记录：没有跳关；编号写进会话名；每一段都有耗时；卡点切换都记下了
                Assert.IsFalse(g.Playtest.Entries.Any(e => e.Type == "dev_jump"));
                StringAssert.StartsWith("P01_", g.Playtest.SessionId);
                var summary = g.Playtest.SummaryCsv();
                foreach (var p in new[] { "Intro", "P1", "P2", "P3", "P4", "Ending" }) StringAssert.Contains("," + p + ",", summary);
                Assert.Greater(g.Playtest.Entries.Count(e => e.Type == "stage"), 8);
                StringAssert.Contains("game_finished", g.Playtest.ToCsv());
                TestContext.WriteLine("自动全程（不用开发菜单）汇总：\n" + summary);
                // 给汇总脚本做自检用：设了环境变量才导出（导出的是自动化记录，不是试玩数据）
                var outDir = System.Environment.GetEnvironmentVariable("LSB_EXPORT_DIR");
                if (!string.IsNullOrEmpty(outDir))
                {
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, g.Playtest.SessionId + ".csv"), g.Playtest.ToCsv());
                    File.WriteAllText(Path.Combine(outDir, g.Playtest.SessionId + "_summary.csv"), summary);
                }
            }
            finally { PlaytestLog.TesterId = ""; }
        }

        /// <summary>正式流程的代码（LSB.Runtime）不引用开发菜单、自动玩家、跳关，结局也不靠 Advance 推进。</summary>
        [Test]
        public void RuntimeCodeDoesNotDependOnDevTools()
        {
            var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()) ?? "", "..", "..", "Scripts", "Runtime"));
            if (!Directory.Exists(dir)) Assert.Inconclusive("找不到 Runtime 源码目录：" + dir);
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 10);
            foreach (var f in files)
            {
                var code = string.Join("\n", File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//")));
                foreach (var w in new[] { "AutoPlayer", "DevSetup", "JumpTo", "Ending.Advance(", "LSB.Dev" })
                    Assert.IsFalse(code.Contains(w), $"{Path.GetFileName(f)} 引用了 {w}");
            }
        }

        static string Here([CallerFilePath] string path = "") => path;

        [Test]
        public void TesterIdNormalization()
        {
            Assert.AreEqual("P01", PlaytestLog.NormalizeTesterId(" p-01 "));
            Assert.AreEqual("ABCDEFGH", PlaytestLog.NormalizeTesterId("abcdefghijk"));
            Assert.AreEqual("", PlaytestLog.NormalizeTesterId("林"));
        }
    }
}

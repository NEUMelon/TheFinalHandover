// 《最后一次交班》Demo α —— 数据完整性测试（Unity Test Runner 的 EditMode 测试；云端用 dotnet test 跑同一份源文件）
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class DataIntegrityTests
    {
        static readonly ContentDB C = new ContentDB();

        [Test]
        public void TenNodesAndFiveCloseUps()
        {
            Assert.AreEqual(10, C.Nodes.Count);
            Assert.AreEqual(5, C.CloseUps.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 10).Select(i => "N" + i), C.Nodes.Select(n => n.Id));
        }

        [Test]
        public void NodeGraphIsSymmetricAndConnected()
        {
            foreach (var a in C.Nodes)
                foreach (var b in a.Neighbors)
                {
                    Assert.IsNotNull(C.Node(b), $"{a.Id} → 不存在的节点 {b}");
                    CollectionAssert.Contains(C.Node(b).Neighbors, a.Id, $"{a.Id} → {b} 不能走回来");
                }
            var seen = new HashSet<string> { "N1" };
            var q = new Queue<string>(seen);
            while (q.Count > 0) foreach (var n in C.Node(q.Dequeue()).Neighbors) if (seen.Add(n)) q.Enqueue(n);
            Assert.AreEqual(10, seen.Count, "有节点从 N1 走不到");
        }

        [Test]
        public void CloseUpsEnterFromListedNodes()
        {
            foreach (var cu in C.CloseUps)
                foreach (var n in cu.EntryNodes)
                    CollectionAssert.Contains(C.Node(n).CloseUps, cu.Id, $"{cu.Id} 写了从 {n} 进入，但 {n} 没有这个入口");
        }

        [Test]
        public void AllTwentySixSpecInteractablesPresent()
        {
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 26), C.Interactables.Select(i => i.SpecNo).Distinct());
            Assert.AreEqual(C.Interactables.Count, C.Interactables.Select(i => i.Id).Distinct().Count(), "交互物 ID 重复");
            foreach (var i in C.Interactables)
                foreach (var v in i.VisibleFrom)
                    Assert.IsTrue(C.Node(v) != null || C.CloseUp(v) != null, $"{i.Id} 的可见位置 {v} 不存在");
        }

        [Test]
        public void InteractableInsideWorkshop()
        {
            // 白盒规格 §1：36 × 18 m，净高到屋架下弦 ≈ 9 m
            foreach (var i in C.Interactables)
            {
                Assert.That(i.Pos.x, Is.InRange(0f, 36f), i.Id);
                Assert.That(i.Pos.z, Is.InRange(0f, 18f), i.Id);
                Assert.That(i.Pos.y, Is.InRange(0f, 9f), i.Id);
            }
            foreach (var n in C.Nodes)
            {
                Assert.That(n.Pos.x, Is.InRange(0f, 36f), n.Id);
                Assert.That(n.Pos.z, Is.InRange(0f, 18f), n.Id);
            }
        }

        [Test]
        public void RulesReferenceExistingThings()
        {
            foreach (var r in C.Rules)
            {
                Assert.IsNotNull(C.Interactable(r.Target), "规则指向不存在的交互物 " + r.Target);
                foreach (var e in r.RequireEvidence.Concat(r.ForbidEvidence)) Assert.IsNotNull(C.Ev(e), e);
                foreach (var fx in r.Effects)
                {
                    if (fx.Kind == EffectKind.GiveEvidence) Assert.IsNotNull(C.Ev(fx.A), fx.A);
                    if (fx.Kind == EffectKind.Say && fx.A != "@last") Assert.IsTrue(C.Lines.ContainsKey(fx.A), fx.A);
                    if (fx.Kind == EffectKind.EnterCloseUp) Assert.IsNotNull(C.CloseUp(fx.A), fx.A);
                    if (fx.Kind == EffectKind.MemoryDevelop) Assert.IsTrue(C.Memories.ContainsKey(fx.A), fx.A);
                }
            }
            foreach (var p in C.Pages) if (p.GivesEvidence != null) Assert.IsNotNull(C.Ev(p.GivesEvidence), p.Id);
            foreach (var s in C.Spreads) { Assert.IsNotNull(C.Page(s.Left), s.Id); Assert.IsNotNull(C.Page(s.Right), s.Id); }
        }

        [Test]
        public void FourTriadsThreeByThreeOneCorrectEach()
        {
            Assert.AreEqual(4, C.Triads.Count);
            CollectionAssert.AreEqual(RuleIds.All, C.Triads.OrderBy(t => t.Phase).Select(t => t.RuleId));
            foreach (var t in C.Triads)
            {
                Assert.AreEqual(3, t.Columns.Length, t.RuleId);
                foreach (var c in t.Columns)
                {
                    Assert.AreEqual(3, c.Options.Length, t.RuleId + "." + c.Key);
                    Assert.AreEqual(1, c.Options.Count(o => o.Ok), t.RuleId + "." + c.Key + " 必须恰好一个正确项");
                    foreach (var o in c.Options)
                    {
                        foreach (var n in o.Needs) Assert.IsNotNull(C.Ev(n), $"{t.RuleId}.{o.Id} 需要不存在的证据 {n}");
                        if (!o.Ok) Assert.IsFalse(string.IsNullOrEmpty(o.FbLog), $"{t.RuleId}.{o.Id} 错误项要有衡的反馈");
                        Assert.IsFalse(string.IsNullOrEmpty(o.Text));
                    }
                }
                foreach (var p in t.Prerequisites) Assert.IsNotNull(C.Ev(p), p);
                Assert.IsTrue(C.RuleCards.ContainsKey(t.RuleId));
            }
        }

        [Test]
        public void TriadAnswersMatchFactChain()
        {
            // 事实链各题 §6 的正确三联（主层文字改成玩家语言，专业说法 Pro 必须保留关键口径）
            StringAssert.Contains("L1", C.Triad(Phase.P1).Columns[1].Options.First(o => o.Ok).Pro);
            StringAssert.Contains("2–4kHz", C.Triad(Phase.P2).Columns[2].Options.First(o => o.Ok).Pro);
            StringAssert.Contains("边频", C.Triad(Phase.P2).Columns[2].Options.First(o => o.Ok).Pro);
            StringAssert.Contains("启动许可", C.Triad(Phase.P3).Columns[2].Options.First(o => o.Ok).Pro);
            StringAssert.Contains("安全相关控制回路", C.Triad(Phase.P3).Columns[2].Options.First(o => o.Ok).Pro);
            StringAssert.Contains("一半", C.Triad(Phase.P4).Columns[2].Options.First(o => o.Ok).Pro);
        }

        [Test]
        public void RuleCardsKeepFrozenWording()
        {
            Assert.AreEqual("周铁生", C.RuleCards[RuleIds.ZhouLeft].Owner);
            StringAssert.Contains("2026 年林小满复测", C.RuleCards[RuleIds.ZhouClick].Source);   // B-2：频带来自 2026 复测
            StringAssert.Contains("1987", C.RuleCards[RuleIds.JianguoGuard].Source);
            CollectionAssert.Contains(C.RuleCards[RuleIds.JianguoGuard].Binds, "LT01_GUARD_CLSD");
            Assert.AreEqual("孙桂兰", C.RuleCards[RuleIds.SunHalfbeat].Owner);
        }

        [Test]
        public void FrozenWordingInPages()
        {
            // 1983 只标定测点与听感：1983 页面里不得出现频率数字
            foreach (var line in C.PageText["note1983"].Concat(C.PageText["sketch1983"]))
                Assert.IsFalse(line.Contains("kHz") || line.Contains("Hz"), "1983 页面出现了频率：" + line);
            Assert.IsTrue(C.PageText["printout2026"].Any(l => l.Contains("2026")) && C.PageText["printout2026"].Any(l => l.Contains("2–4")));
            Assert.AreEqual("夹在工牌套中的工作照", C.ReaderText["photo"][0]);
        }

        [Test]
        public void JianguoAtMostTenLines()
        {
            Assert.LessOrEqual(C.Lines.Values.Count(l => l.Speaker == "周建国"), 10);
        }

        [Test]
        public void DeductionAnswersAreInWordBank()
        {
            var d = C.Deduction;
            Assert.AreEqual(d.Blanks.Length + 1, d.Segments.Length);
            foreach (var b in d.Blanks) CollectionAssert.Contains(d.WordBank, b.Answer, b.Key);
            foreach (var kv in d.WordNeeds) { CollectionAssert.Contains(d.WordBank, kv.Key); foreach (var e in kv.Value) Assert.IsNotNull(C.Ev(e), e); }
            Assert.AreEqual(d.WordBank.Length, d.WordBank.Distinct().Count());
        }

        [Test]
        public void TimeAnchorsCoverSixteenToMidnightInOrder()
        {
            var t = C.Time.OrderBy(a => a.Phase).ToList();
            Assert.AreEqual(16 * 60, t.First().StartMinute);
            Assert.LessOrEqual(t.Last().EndMinute, 24 * 60);
            for (int i = 1; i < t.Count; i++) Assert.GreaterOrEqual(t[i].StartMinute, t[i - 1].EndMinute, t[i].Phase.ToString());
        }

        [Test]
        public void EvidenceIdsUniqueAndHavePaperNumberWhenNecessary()
        {
            Assert.AreEqual(C.Evidence.Count, C.Evidence.Select(e => e.Id).Distinct().Count());
            foreach (var e in C.Evidence.Where(e => e.Grade == EvidenceGrade.Necessary && e.Id != "E4_3"))
                StringAssert.StartsWith("贴页", e.Paper, e.Id);
        }
    }
}

// 《最后一次交班》Demo α —— 自动通关脚本（开发菜单“跳到某一项 / 解掉这一项”用）
// 只调用玩家能做的操作（走节点、拿工具、点物件、翻交班本、小玩法、三联板、规则卡），
// 不直接改状态，所以跳关之后的状态和真人玩到那里一样。
// ⚠ 自动通关只证明“流程能走通”，不是试玩数据。
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed class AutoPlayer
    {
        readonly GameSession g;
        public AutoPlayer(GameSession g) { this.g = g; }

        /// <summary>每一步做了什么（测试失败时用来定位）。</summary>
        public readonly List<string> Trace = new List<string>();

        GameState S => g.State;

        void Note(string s) => Trace.Add($"[{S.Phase}] {s}");

        void Fail(string why) => throw new InvalidOperationException($"AutoPlayer 卡住：{why}（阶段 {S.Phase}，节点 {S.Node}，特写 {S.CloseUp ?? "-"}）\n" + string.Join("\n", Trace.Skip(Math.Max(0, Trace.Count - 12))));

        /// <summary>沿节点图走过去（广度优先，逐个相邻节点移动，和玩家点导航一样）。</summary>
        public void Goto(string node)
        {
            if (S.CloseUp != null) g.Nav.ExitCloseUp();
            if (S.Node == node) return;
            var prev = new Dictionary<string, string> { [S.Node] = null };
            var q = new Queue<string>(); q.Enqueue(S.Node);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == node) break;
                foreach (var n in g.Content.Node(c).Neighbors)
                    if (!prev.ContainsKey(n)) { prev[n] = c; q.Enqueue(n); }
            }
            if (!prev.ContainsKey(node)) Fail("到不了 " + node);
            var path = new List<string>();
            for (var c = node; c != null; c = prev[c]) path.Add(c);
            path.Reverse();
            foreach (var n in path.Skip(1)) if (!g.Nav.MoveTo(n)) Fail("走不动 " + n);
            Note("到 " + node);
        }

        public void Tool(ToolId t) { if (S.Tool != t) g.Interaction.SelectTool(t); }

        public void Use(string target, ToolId tool = ToolId.None)
        {
            Tool(tool);
            var r = g.Interaction.Use(target);
            if (!r.Reachable) Fail("点不到 " + target);
            Note($"点 {target}（{tool}）" + (r.Matched ? "" : " 无反应"));
        }

        public void CloseUp(string cu) { if (!g.Nav.EnterCloseUp(cu)) Fail("进不了特写 " + cu); Note("特写 " + cu); }

        void Need(string ev) { if (!S.Has(ev)) Fail("没拿到 " + ev); }

        public void ReadPage(string page) { g.Ledger.ReadPage(page); Note("读 " + page); }

        /// <summary>按正确答案填三联板并确认，然后把规则卡推到“已确认”。</summary>
        public void SolveTriad()
        {
            var t = g.Triad.Current;
            if (!g.Triad.CanOpen(out var why)) Fail("三联板打不开：" + why);
            foreach (var c in t.Columns)
            {
                var ok = c.Options.First(o => o.Ok);
                if (g.Triad.Picked(c.Key) != ok.Id && !g.Triad.Pick(c.Key, ok.Id)) Fail("选不了 " + c.Key + ":" + ok.Id);
            }
            var r = g.Triad.Confirm();
            if (r.Outcome != TriadOutcome.Solved) Fail("三联板没通过：" + r.Outcome);
            Note("三联 " + t.RuleId);
        }

        public void ConfirmRule(string id)
        {
            if (id == RuleIds.JianguoGuard)
            {
                g.Puzzles.TwinRestore();
                foreach (var c in GuardTwinModel.Cases) g.Puzzles.TwinTest(c.Id);
            }
            if (!g.Rules.SetPending(id)) Fail("规则进不了待验证 " + id);
            if (!g.Rules.Confirm(id)) Fail("规则确认不了 " + id);
            Note("确认 " + id);
        }

        // =====================================================================
        public void SolveIntro()
        {
            if (S.Phase == Phase.Title) g.Flow.NewGame();
            if (S.Phase != Phase.Intro) return;
            Goto("N1");
            Use("ledger");
            g.Flow.FinishIntro();
        }

        public void SolveP1()
        {
            Goto("N2");
            if (!S.Flag("locker_open")) Use("locker_zhou");
            Use("gloves"); Use("badge_photo");
            Need("E1_2");
            Goto("N4");
            if (!S.Has("E1_3")) Use("l1_mark", ToolId.Torch);
            Need("E1_3");
            Use("l1_mark", ToolId.Probe);          // → 特写 CU1 + 听 L1
            Use("r1_sensor", ToolId.Probe);        // → 听 R1
            ReadPage("sketch1983");
            Need("E1_4");
            g.Puzzles.CompareChoose("L1");
            Need("E1_5");
            Tool(ToolId.None);
            SolveTriad();
            ConfirmRule(RuleIds.ZhouLeft);
        }

        public void SolveP2()
        {
            Goto("N4");
            Use("l1_mark", ToolId.Probe);
            g.Puzzles.JudgeBox(new SpecBox(1.0f, 3.0f, 2200f, 3800f));
            Need("E2_1");
            Use("r1_sensor", ToolId.Probe);
            g.Puzzles.Sideband("ok");
            Need("E2_2");
            ReadPage("note1983");
            Need("E2_3");
            Tool(ToolId.None);
            SolveTriad();
            ConfirmRule(RuleIds.ZhouClick);
        }

        public void SolveP3()
        {
            Need("E3_1");
            Goto("N3");
            if (!S.Has("E3_2")) Use("guard", ToolId.Torch);
            Need("E3_2");
            Goto("N6");
            if (!S.Flag("cabinet_open")) Use("cabinet_door", ToolId.Driver);
            else { Tool(ToolId.None); Use("cabinet_door"); }
            Tool(ToolId.None);
            Use("relay_guard");
            Need("E3_3");
            ReadPage("brief1987"); ReadPage("change1987"); ReadPage("ticket1987");
            Need("E3_6"); Need("E3_7"); Need("E3_8");
            foreach (var b in g.Content.Deduction.Blanks) if (!g.Puzzles.Fill(b.Key, b.Answer) && !S.Has("E3_INF")) Fail("填不进 " + b.Key);
            g.Puzzles.CheckDeduction();
            Need("E3_INF");
            SolveTriad();
            ConfirmRule(RuleIds.JianguoGuard);
        }

        public void SolveP4()
        {
            Goto("N8");
            Use("crane_tablet");                   // → 特写 CU4，首次自动试吊：水洒
            Need("E4_1");
            Goto("N1");
            Use("drawer");                         // → 特写 CU5
            if (S.CloseUp != "CU5") CloseUp("CU5");
            Use("tape"); Use("recorder");
            g.Puzzles.PlayTape();
            g.Puzzles.TapeFinished();
            Need("E4_2"); Need("E4_4");
            Goto("N8");
            Use("crane_tablet");
            g.Puzzles.SetT2(0.5f);
            g.Puzzles.Trial();
            Need("E4_TUNE");
            SolveTriad();
            ConfirmRule(RuleIds.SunHalfbeat);
        }

        public void SolveEnding(string ruleName = "LIN_TAIL_HUM")
        {
            int guard = 0;
            while (S.Ending != EndingStep.PullSwitch && guard++ < 20) g.Ending.Advance();
            Goto("N9");
            Use("main_switch");
            if (!g.Ending.PullSwitch()) Fail("拉不了总闸");
            guard = 0;
            while (S.Ending != EndingStep.NameAndSign && guard++ < 20) g.Ending.Advance();
            if (!g.Ending.NameRule(ruleName)) Fail("命名失败");
            if (!g.Ending.Sign()) Fail("签名失败");
            g.Ending.Advance();
            Note("结局完成");
        }

        /// <summary>把当前这一项解掉，并进入下一项（规则确认后的收尾台词也照常播）。</summary>
        public void SolveCurrent()
        {
            switch (S.Phase)
            {
                case Phase.Title: case Phase.Intro: SolveIntro(); return;
                case Phase.P1: SolveP1(); break;
                case Phase.P2: SolveP2(); break;
                case Phase.P3: SolveP3(); break;
                case Phase.P4: SolveP4(); break;
                case Phase.Ending: SolveEnding(); return;
                default: return;
            }
            g.Flow.CompletePuzzle();
        }

        /// <summary>开发菜单跳关：从新游戏开始自动玩到目标阶段的开头。</summary>
        public static AutoPlayer JumpTo(GameSession g, Phase target)
        {
            var a = new AutoPlayer(g);
            g.Flow.NewGame();
            int guard = 0;
            while (g.State.Phase < target && g.State.Phase != Phase.Done && guard++ < 10) a.SolveCurrent();
            g.Playtest.Record("dev_jump", target.ToString());
            return a;
        }
    }
}

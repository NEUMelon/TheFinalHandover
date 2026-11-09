// 《最后一次交班》Demo α —— 离线孪生：LT01 主轴启动（谜题三，衡的屏幕）
// 玩家做的不是“写一段 PLC 的 if”，而是把一条丢掉的安全要求补回来，再亲手试两种情况：
//   护罩开着按启动 → 应当不转；护罩合上按启动 → 应当能转。
// 补回之前也能试：会看到“护罩开着，主轴照样转”，这就是新程序缺的那一条。
// 两种情况在补回后都符合要求，规则卡才从“草拟”进入“待验证”（KnowledgeRuleSystem.CanSetPending）。
// ⚠ 逻辑示意，不连真机，不是真实安全回路设计，也不代表任何安全等级评估（GuardTwinModel）。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class TwinView : ModalView, ITickView
    {
        public override string Id => "twin";

        /// <summary>每种情况在“现行新程序 / 补回后”各自最近一次的结果（只是界面记录，不进存档）。</summary>
        readonly Dictionary<string, TwinRun> lastRun = new Dictionary<string, TwinRun>();
        readonly Dictionary<string, VisualElement> spokes = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, float> spinUntil = new Dictionary<string, float>();
        readonly Dictionary<string, float> angle = new Dictionary<string, float>();
        float lastTick;

        public TwinView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "sys") { Panel.AddToClassList("twin"); }

        protected override void Build(string arg)
        {
            lastTick = Now;
            Render();
        }

        static string Key(string caseId, bool restored) => caseId + (restored ? ":r" : ":c");

        void Render()
        {
            Panel.Clear();
            spokes.Clear();
            var p = g.Puzzles;
            var top = Box("mes-top");
            top.Add(L("LT01 主轴启动　离线孪生", "sys-name"));
            top.Add(L("离线仿真，不连真机", "sys-dim"));
            Panel.Add(top);

            if (g.State.Phase != Phase.P3) { Panel.Add(L("这一项不用孪生。", "sys-line")); Foot(); return; }

            // ---- 启动条件示意 ----
            bool restored = p.TwinRestored;
            var logic = Box("twin-logic");
            var ins = Box("twin-ins");
            ins.Add(Node("按启动", true));
            var guard = Node("护罩合上", restored);
            ins.Add(guard);
            logic.Add(ins);
            var mid = Box("twin-mid");
            mid.Add(L("──▶", "twin-arrow"));
            var gate = L(restored ? "两样都满足" : "只看启动", "twin-gate");
            gate.EnableInClassList("on", restored);
            mid.Add(gate);
            mid.Add(L(restored ? "──▶" : "× 护罩这一条没接上", restored ? "twin-arrow" : "twin-miss"));
            logic.Add(mid);
            logic.Add(Node("主轴启动", true));
            Panel.Add(logic);
            if (Pro) Panel.Add(L("LT01_GUARD_CLSD（护罩闭合反馈）→ 安全相关控制回路 → LT01_SPDL_START_PERM（主轴启动许可）", "f-mono", "sys-dim"));

            // ---- 补回 ----
            var fix = Box("twin-fix");
            if (restored)
                fix.Add(L("已补回：护罩合上，主轴才许启动。（依据：一九八七年安全整改变更单）", "sys-line", "ok"));
            else
            {
                var b = B("补回：护罩合上，主轴才许启动", Restore, "sys-btn");
                b.SetEnabled(p.TwinAvailable);
                fix.Add(b);
                fix.Add(L(p.TwinAvailable ? "依据：安全整改变更单（副本）。提出 周铁生，施工 车间电工。" : "先在三联板上把这一项想清楚，再来补。现在可以先试试新程序。", "sys-dim"));
            }
            Panel.Add(fix);

            // ---- 两种情况 ----
            var cases = Box("twin-cases");
            foreach (var c in GuardTwinModel.Cases) cases.Add(CaseCard(c, restored));
            Panel.Add(cases);

            if (p.TwinComplete)
            {
                var ok = Box("twin-done");
                ok.Add(L("两种情况都符合要求。", "sys-line", "ok"));
                if (g.State.Rule(RuleIds.JianguoGuard) == RuleStatus.Draft)
                    ok.Add(B("提交待验证 ▶", () => host.Open("rulecard", RuleIds.JianguoGuard), "sys-btn", "on"));
                Panel.Add(ok);
            }
            Foot();
        }

        void Foot()
        {
            var foot = Box("mes-foot");
            foot.Add(B("返回（Esc）", CloseSelf, "sys-btn"));
            foot.Add(L("模拟 / 占位　逻辑示意，不是安全回路设计", "sys-dim"));
            Panel.Add(foot);
        }

        static VisualElement Node(string text, bool on)
        {
            var n = Box("twin-node");
            n.EnableInClassList("off", !on);
            n.Add(L(text, "twin-node-t"));
            return n;
        }

        VisualElement CaseCard(TwinCase c, bool restored)
        {
            var card = Box("twin-case");
            card.Add(L(c.Name, "sys-q"));
            // 小车床：卡盘 + 护罩（开 / 合）
            var fig = Box("twin-fig");
            var chuck = Box("twin-chuck");
            var spoke = Box("twin-spoke");
            chuck.Add(spoke);
            fig.Add(chuck);
            var cover = Box("twin-cover", c.GuardClosed ? "closed" : "open");
            fig.Add(cover);
            fig.Add(L(c.GuardClosed ? "护罩合上" : "护罩开着", "sys-dim", "twin-fig-l"));
            card.Add(fig);
            spokes[c.Id] = spoke;
            if (angle.TryGetValue(c.Id, out var a)) Rot(spoke, a);

            var run = B("按启动", () => Test(c.Id), "sys-btn");
            card.Add(run);
            foreach (var mode in new[] { false, true })
            {
                if (mode && !restored && !lastRun.ContainsKey(Key(c.Id, true))) continue;
                var r = Box("mes-row");
                r.Add(L(mode ? "补回后" : "现行新程序", "mes-name"));
                if (lastRun.TryGetValue(Key(c.Id, mode), out var res))
                {
                    var v = L((res.SpindleRuns ? "主轴转了" : "主轴没转") + (res.Pass ? "　符合要求" : "　不符合要求"), "mes-val");
                    v.AddToClassList(res.Pass ? "ok" : "warn");
                    r.Add(v);
                }
                else r.Add(L("还没试", "mes-val", "sys-dim"));
                card.Add(r);
            }
            return card;
        }

        void Restore()
        {
            var r = g.Puzzles.TwinRestore();
            if (r.Ok) { g.Sfx("relay"); lastRun.Remove(Key("open_start", true)); lastRun.Remove(Key("closed_start", true)); }
            Render();
        }

        void Test(string caseId)
        {
            var run = g.Puzzles.TwinTest(caseId);
            if (run == null) return;
            lastRun[Key(caseId, run.Restored)] = run;
            if (run.SpindleRuns)
            {
                spinUntil[caseId] = Now + 2.5f;
                g.Sfx(run.Case.GuardClosed ? "steady" : "guardOpenStart");
            }
            else { spinUntil.Remove(caseId); g.Sfx("relay"); }
            Render();
        }

        public void Tick()
        {
            float dt = Now - lastTick; lastTick = Now;
            foreach (var kv in spokes)
            {
                if (!spinUntil.TryGetValue(kv.Key, out var until) || Now > until) continue;
                angle.TryGetValue(kv.Key, out var a);
                a = (a + dt * 900f) % 360f;
                angle[kv.Key] = a;
                Rot(kv.Value, a);
            }
        }

        protected override void OnEvent(GameEvent e)
        {
            if (e.Type == Ev.RuleStatusChanged) Panel.schedule.Execute(Render);
        }
    }
}

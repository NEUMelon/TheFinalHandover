// 《最后一次交班》Demo α —— 经验规则卡 + 技术等级考核合格通知（UI-DS §5.4 / §5.7）
// “记住与告别同帧”：
//   0–600ms     纸面 Source 手写字拆成点阵，点阵飞向屏幕（灰盒：Source 变淡，屏幕上点阵逐个出现）
//   600ms       规则进入“待验证”（衡按规则复核，衡日志 TWIN 一行）
//   600–1400ms  屏幕上逐字打出 Rule ID（每字 80ms）
//   1400ms      盖“合格”章 = CONFIRMED = 交接状态板对应的灯熄灭（同一帧，KnowledgeRuleSystem.Confirm）
//   1600ms      等级通知从下方滑入；停 2.5 秒后可以收起；收起 = 进入下一项（FlowSystem.CompletePuzzle）
// 谜题三的规则要先在孪生里试完两种情况才进入“待验证”（2D 接入孪生面板；这里只停在“草拟”并给入口）。
// 中途按 Esc：动画快进到结果，规则照样确认，不会停在半截。
using System.Linq;
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class RuleCardView : ModalView, ITickView
    {
        public override string Id => "rulecard";

        const float TPending = 0.6f, TConfirm = 1.4f, TPromo = 1.6f, PromoHold = 2.5f;
        string ruleId;
        float t0;
        bool waitingTwin, promoShown, finished;
        float promoAt;
        Label paperStatus, screenStatus, screenId, screenDots, paperSource, levelLine;
        VisualElement promo, stamp;
        Button promoClose;
        readonly VisualElement[] lamps = new VisualElement[4];

        public RuleCardView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "rulecard-wrap") { }

        protected override bool CloseOnDimClick => false;

        protected override void Build(string arg)
        {
            ruleId = arg ?? g.Content.Triad(g.State.Phase)?.RuleId;
            var card = g.Rules.Card(ruleId);
            promoShown = false; finished = false; waitingTwin = false;
            g.Hints.Paused = true;
            Panel.Clear();
            if (card == null) { Panel.Add(L("没有这张规则卡。", "panel-body")); return; }

            var st = g.State.Rule(ruleId);
            // 从哪一刻开始播：读档 / 中途重开时接着往下走
            t0 = Now - (st == RuleStatus.Pending ? TPending : st == RuleStatus.Confirmed ? TConfirm : 0f);
            waitingTwin = st == RuleStatus.Draft && !g.Rules.CanSetPending(ruleId);

            Panel.Add(L(card.PlainSummary, "f-hand-lin", "rc-summary"));

            var row = Box("rc-row");
            // ---- 纸面卡 600×360 ----
            var paper = Box("rc-paper");
            paper.Add(Field("Rule ID", "规则编号", L(card.Id, "f-mono", "rc-id")));
            paperSource = L(card.Source, "rc-val");
            paper.Add(Field("Source", "来源", paperSource));
            paper.Add(Field("Measurement Point", "测点", L(card.Point, "rc-val")));
            paper.Add(Field("Feature", "特征", L(card.Feature, "rc-val")));
            paper.Add(Field("Binds", "绑定变量", L(string.Join("\n", card.Binds), "f-mono", "rc-bind")));
            paperStatus = L("草拟", "f-hand-lin", "rc-status-paper");
            paper.Add(Field("Status", "状态", paperStatus));
            stamp = L("合格", "rc-stamp");
            UiRoot.Show(stamp, false);
            paper.Add(stamp);
            row.Add(paper);

            // ---- 衡屏幕卡 640×360（字段名、顺序与纸面一致） ----
            var screen = Box("rc-screen");
            screenId = L("", "f-mono", "rc-id-sys");
            screen.Add(SysField("RULE ID", screenId));
            screenDots = L("", "rc-sys-val");
            screen.Add(SysField("SOURCE", screenDots));
            screen.Add(SysField("MEASUREMENT POINT", L(card.Point, "rc-sys-val")));
            screen.Add(SysField("FEATURE", L(card.Feature, "rc-sys-val")));
            screen.Add(SysField("BINDS", L(string.Join("\n", card.Binds), "f-mono", "rc-sys-val")));
            screenStatus = L("DRAFT", "f-mono", "rc-sys-status");
            screen.Add(SysField("STATUS", screenStatus));
            row.Add(screen);
            Panel.Add(row);

            // ---- 交接状态板（缩略） ----
            var board = Box("rc-board");
            board.Add(L("交接状态板", "rc-board-t"));
            for (int i = 0; i < 4; i++)
            {
                var lamp = Box("lamp", "rc-lamp");
                lamp.EnableInClassList("off", !g.State.Lamps[i]);
                lamps[i] = lamp;
                board.Add(lamp);
            }
            board.Add(L("灯亮：旧办法管着　灯灭：交给衡", "rc-board-s"));
            Panel.Add(board);

            if (waitingTwin)
            {
                var w = Box("rc-wait");
                w.Add(L("这一条要先在孪生里试一遍：护罩开着按启动，护罩合上按启动。", "panel-body"));
                w.Add(B("去离线验证 ▶", () => host.Open("twin"), "btn"));
                w.Add(B("合上", CloseSelf, "btn"));
                Panel.Add(w);
            }

            promo = BuildPromo(card);
            UiRoot.Show(promo, false);
            Panel.Add(promo);
            Tick();
        }

        static VisualElement Field(string en, string zh, VisualElement value)
        {
            var r = Box("rc-field");
            var k = Box("rc-key");
            k.Add(L(en.ToUpperInvariant(), "f-mono", "rc-key-en"));
            k.Add(L(zh, "rc-key-zh"));
            r.Add(k);
            r.Add(value);
            return r;
        }

        static VisualElement SysField(string en, VisualElement value)
        {
            var r = Box("rc-field", "sys");
            r.Add(L(en, "f-mono", "rc-key-sys"));
            r.Add(value);
            return r;
        }

        VisualElement BuildPromo(RuleCardDef card)
        {
            var p = Box("promo");
            p.Add(L("技术等级考核合格通知", "f-serif-b", "promo-title"));
            int from = System.Math.Max(0, g.State.Level - (g.State.Rule(card.Id) == RuleStatus.Confirmed ? 1 : 0));
            string fromName = KnowledgeRuleSystem.Levels[from];
            string toName = KnowledgeRuleSystem.Levels[System.Math.Min(from + 1, KnowledgeRuleSystem.Levels.Length - 1)];
            p.Add(Kv("持证者", "衡"));
            p.Add(Kv("工种", "设备诊断（学艺）"));
            levelLine = L($"{fromName}　→　{toName}", "f-hand-lin", "promo-level");
            var lv = Box("kv-row"); lv.Add(L("等级", "kv-k")); lv.Add(levelLine); p.Add(lv);
            p.Add(Kv("依据", card.Id, "f-mono"));
            p.Add(Kv("评定", "林小满"));
            p.Add(Kv("编号", $"HENG-2026-{RuleIds.IndexOf(card.Id) + 1:000}", "f-mono"));
            var ladder = Box("promo-ladder");
            for (int i = 0; i < KnowledgeRuleSystem.Levels.Length; i++)
            {
                var step = L(KnowledgeRuleSystem.Levels[i], "promo-step");
                step.EnableInClassList("reached", i <= from + 1);
                step.EnableInClassList("new", i == from + 1);
                ladder.Add(step);
            }
            p.Add(ladder);
            p.Add(L("合格", "rc-stamp", "promo-stamp"));
            promoClose = B("收起", Finish, "btn");
            promoClose.SetEnabled(false);
            p.Add(promoClose);
            return p;
        }

        static VisualElement Kv(string k, string v, string font = "f-serif")
        {
            var r = Box("kv-row");
            r.Add(L(k, "kv-k")); r.Add(L(v, "kv-v", font));
            return r;
        }

        public void Tick()
        {
            if (ruleId == null || finished) return;
            var card = g.Rules.Card(ruleId);
            if (card == null) return;
            if (waitingTwin)
            {
                // 2D：孪生两种情况都试完之后，回来接着播
                if (g.Rules.CanSetPending(ruleId)) { waitingTwin = false; t0 = Now; }
                else { ScreenAt(0.3f, card); return; }
            }
            float t = Now - t0;
            var st = g.State.Rule(ruleId);
            if (t >= TPending && st == RuleStatus.Draft) { g.Rules.SetPending(ruleId); st = g.State.Rule(ruleId); }
            if (t >= TConfirm && st == RuleStatus.Pending)
            {
                g.Rules.Confirm(ruleId);   // 同一帧：灯灭 + 晋升
                g.Sfx("stamp"); g.Sfx("lamp_off");
                st = g.State.Rule(ruleId);
            }
            ScreenAt(t, card);
            if (t >= TPromo && !promoShown && st == RuleStatus.Confirmed)
            {
                promoShown = true; promoAt = Now;
                UiRoot.Show(promo, true);
                promo.schedule.Execute(() => promo.AddToClassList("in")).StartingIn(16);
            }
            if (promoShown && Now - promoAt >= PromoHold) promoClose.SetEnabled(true);
        }

        void ScreenAt(float t, RuleCardDef card)
        {
            var st = g.State.Rule(ruleId);
            // 0–600ms：纸面 Source 变淡，屏幕上点阵一个个出现
            float k = System.Math.Min(1f, t / TPending);
            paperSource.style.opacity = st == RuleStatus.Draft ? 1f - 0.6f * k : 0.4f;
            int dots = (int)(k * 24);
            screenDots.text = st == RuleStatus.Draft ? new string('·', dots) : card.Source;
            // 600–1400ms：逐字打出 Rule ID（80ms 一个字）
            int n = t < TPending ? 0 : System.Math.Min(card.Id.Length, (int)((t - TPending) / 0.08f) + 1);
            screenId.text = st == RuleStatus.Confirmed ? card.Id : card.Id.Substring(0, n) + (n < card.Id.Length ? "_" : "");
            switch (st)
            {
                case RuleStatus.Draft: paperStatus.text = "草拟"; screenStatus.text = "DRAFT"; break;
                case RuleStatus.Pending:
                    paperStatus.text = "待验证"; paperStatus.AddToClassList("pending");
                    screenStatus.text = ((int)(t * 2) % 2 == 0 ? "● " : "　") + "PENDING"; break;   // 1Hz 闪烁点
                case RuleStatus.Confirmed:
                    paperStatus.text = "已确认"; paperStatus.RemoveFromClassList("pending"); paperStatus.AddToClassList("ok");
                    screenStatus.text = "CONFIRMED"; screenStatus.AddToClassList("ok");
                    UiRoot.Show(stamp, true);
                    break;
            }
            for (int i = 0; i < 4; i++) lamps[i].EnableInClassList("off", !g.State.Lamps[i]);
        }

        /// <summary>“收起”或 Esc：把还没走完的状态走完，然后进入下一项。</summary>
        void Finish() => CloseSelf();

        protected override void OnClose()
        {
            g.Hints.Paused = false;
            if (finished || ruleId == null) return;
            finished = true;
            var st = g.State.Rule(ruleId);
            if (st == RuleStatus.Draft && g.Rules.CanSetPending(ruleId)) { g.Rules.SetPending(ruleId); st = g.State.Rule(ruleId); }
            if (st == RuleStatus.Pending) { g.Rules.Confirm(ruleId); st = g.State.Rule(ruleId); }
            if (st == RuleStatus.Confirmed && g.State.PuzzleSolved)
                ui.Root.schedule.Execute(() => g.Flow.CompletePuzzle());
        }
    }
}

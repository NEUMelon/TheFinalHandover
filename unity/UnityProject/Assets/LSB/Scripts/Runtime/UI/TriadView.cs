// 《最后一次交班》Demo α —— 三联板（UI-DS §5.2）
// 三格横排：一联 异常现象 / 二联 经验判断 / 三联 数据特征；每格 3 个选项条；右侧“确认”章。
// 选项缺证据时显示“待取证”（灰，不可选）。填错不扣分、不重来：冲突的格子标安全黄，衡报“规则冲突”并在仿真里放一段“听起来不对”的声音。
// 三格一致 → 格子盖绿色“合格”，0.6 秒后翻到经验规则卡。
using System.Linq;
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class TriadView : ModalView
    {
        public override string Id => "triad";
        TriadResult last;
        bool solved;

        public TriadView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "paper", null) { Panel.AddToClassList("triad"); }

        protected override void Build(string arg)
        {
            last = null; solved = false;
            Render();
        }

        void Render()
        {
            Panel.Clear();
            var t = g.Triad.Current;
            if (t == null) { Panel.Add(L("这一项没有三联板。", "panel-body")); Panel.Add(B("合上", CloseSelf, "btn")); return; }

            var head = Box("triad-head");
            head.Add(L("三联板", "panel-title"));
            head.Add(L(t.Title, "triad-sub"));
            var pro = B(Pro ? "专业说法：开" : "专业说法：关", () => { g.State.Settings.ShowPro = !g.State.Settings.ShowPro; Render(); }, "chip", "pro-toggle");
            head.Add(pro);
            Panel.Add(head);

            var body = Box("triad-body");
            foreach (var c in t.Columns) body.Add(Column(c));

            var stampCol = Box("triad-stamp-col");
            var stamp = B(solved ? "合格" : "确认", Confirm, "triad-stamp");
            stamp.EnableInClassList("done", solved);
            stamp.SetEnabled(!solved && g.Triad.AllFilled);
            stampCol.Add(stamp);
            stampCol.Add(L(g.Triad.AllFilled ? "三格都填了，盖章确认。" : "三格都填上，才能盖章。", "triad-tip"));
            body.Add(stampCol);
            Panel.Add(body);

            var fb = Box("triad-fb");
            if (last != null && last.Outcome == TriadOutcome.Conflict)
            {
                fb.Add(L("衡 > 规则冲突", "fb-head"));
                var o = last.Feedback;
                if (o != null)
                {
                    fb.Add(L($"{o.FbModule ?? "RULE"} > {o.FbLog}", "fb-line"));
                    if (!string.IsNullOrEmpty(o.FbCaption)) fb.Add(L(o.FbCaption, "fb-cap"));
                }
                fb.Add(L("标黄的格子再想想。填错不扣分。", "page-faint"));
            }
            else if (last != null && last.Outcome == TriadOutcome.NotFilled) fb.Add(L("三格都填上，才能盖章。", "page-faint"));
            Panel.Add(fb);

            var foot = Box("triad-foot");
            foot.Add(B("回交班本（Esc）", CloseSelf, "btn"));
            Panel.Add(foot);
        }

        VisualElement Column(TriadColumn c)
        {
            var col = Box("triad-col");
            string picked = g.Triad.Picked(c.Key);
            bool conflict = last != null && last.Outcome == TriadOutcome.Conflict && last.WrongColumns.Contains(c.Key);
            col.EnableInClassList("conflict", conflict);
            col.EnableInClassList("ok", solved);
            col.EnableInClassList("filled", picked != null);
            col.Add(L(c.No, "f-hand-lin", "triad-no"));
            col.Add(L(c.Title, "triad-title"));
            var slot = Box("triad-slot");
            var po = c.Options.FirstOrDefault(o => o.Id == picked);
            slot.Add(L(po != null ? po.Text : "（空）", po != null ? "f-hand-lin" : "page-faint", "slot-text"));
            if (solved) slot.Add(L("合格", "stamp-mini"));
            col.Add(slot);
            foreach (var o in c.Options)
            {
                var opt = o;
                bool locked = g.Triad.IsLocked(o);
                var b = new Button(() => { if (solved) return; if (g.Triad.Pick(c.Key, opt.Id)) { last = null; g.Sfx("tick"); Render(); } });
                b.AddToClassList("triad-opt");
                b.EnableInClassList("locked", locked);
                b.EnableInClassList("sel", picked == o.Id);
                b.Add(L(locked ? "待取证" : o.Text, "opt-text"));
                if (!locked && Pro && o.Pro != o.Text) b.Add(L(o.Pro, "opt-pro"));
                b.SetEnabled(!locked && !solved);
                col.Add(b);
            }
            return col;
        }

        /// <summary>三联已一致但玩家在翻到规则卡之前就合上了：照样翻到规则卡，不让规则停在“草拟”。</summary>
        protected override void OnClose()
        {
            if (!solved) return;
            var id = g.Content.Triad(g.State.Phase)?.RuleId;
            if (id == null) return;
            ui.Root.schedule.Execute(() => { if (!host.IsOpen("rulecard") && g.State.Rule(id) != RuleStatus.Confirmed) host.Open("rulecard", id); });
        }

        void Confirm()
        {
            if (solved) return;
            last = g.Triad.Confirm();
            if (last.Outcome == TriadOutcome.Solved)
            {
                solved = true;
                g.Sfx("stamp");
                Render();
                var id = g.Triad.Current?.RuleId ?? g.Content.Triad(g.State.Phase).RuleId;
                Panel.schedule.Execute(() => { if (IsOpen) host.Open("rulecard", id); }).StartingIn(600);
                return;
            }
            Render();
        }
    }
}

// 《最后一次交班》Demo α —— 总闸与交接单（结局，控制间东墙）
// 全片唯一一次真正停机（主项目文档 §5.5）：原班长签字授权以后，玩家按交接单拉下总闸。
// 操作：按住手柄往下拖到底（或点“拉下总闸”按钮，键盘 / 触屏可用）。
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class SwitchView : ModalView
    {
        public override string Id => "switch";
        const float Travel = 200f;

        VisualElement handle;
        float downY, offset;
        bool dragging, pulled;

        public SwitchView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "paper", "side") { Panel.AddToClassList("switch-sheet"); }

        protected override void Build(string arg)
        {
            pulled = g.State.Ending > EndingStep.PullSwitch;
            offset = pulled ? Travel : 0;
            Panel.Clear();
            Panel.Add(L("交接单", "panel-title"));
            Panel.Add(L("三号车间　中班　二十三点五十分", "triad-sub"));
            var rows = Box("switch-rows");
            for (int i = 0; i < 4; i++)
            {
                var r = Box("kv-row");
                r.Add(L($"{"一二三四"[i]}　{g.Content.CheckRows[i].Item}", "kv-v"));
                r.Add(L(g.State.Lamps[i] ? "—" : "交给衡", "kv-k", "f-hand-lin"));
                rows.Add(r);
            }
            var sign = Box("kv-row");
            sign.Add(L("原班长签字", "kv-v"));
            sign.Add(L(g.State.Ending >= EndingStep.PullSwitch ? "周建国" : "", "kv-k", "f-hand-jian"));
            rows.Add(sign);
            var step = Box("kv-row", "cur");
            step.Add(L("拉下总闸，停机切换", "kv-v"));
            step.Add(L(pulled ? "已断开" : "", "kv-k", "f-hand-lin"));
            rows.Add(step);
            Panel.Add(rows);

            // 手柄：按住往下拖
            var box = Box("switch-box");
            var slot = Box("switch-slot");
            handle = Box("switch-handle");
            handle.Add(L(pulled ? "断" : "合", "switch-handle-t"));
            slot.Add(handle);
            box.Add(slot);
            box.Add(L(pulled ? "总闸已断开。" : "按住手柄，往下拖到底。", "page-faint"));
            Panel.Add(box);
            handle.style.top = offset;
            handle.RegisterCallback<PointerDownEvent>(e => { if (pulled) return; dragging = true; downY = e.position.y - offset; handle.CapturePointer(e.pointerId); });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                offset = System.Math.Max(0, System.Math.Min(Travel, e.position.y - downY));
                handle.style.top = offset;
                if (offset >= Travel - 2) Pull();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                dragging = false; handle.ReleasePointer(e.pointerId);
                if (!pulled) { offset = 0; handle.style.top = 0; }   // 没拉到底，弹回去
            });

            var foot = Box("triad-foot");
            var b = B("拉下总闸", Pull, "btn");
            b.SetEnabled(!pulled && g.State.Ending == EndingStep.PullSwitch);
            foot.Add(b);
            foot.Add(B("返回（Esc）", CloseSelf, "btn"));
            Panel.Add(foot);
        }

        void Pull()
        {
            if (pulled) return;
            if (!g.Ending.PullSwitch()) return;
            pulled = true; dragging = false;
            offset = Travel; handle.style.top = Travel;
            g.Playtest.Record("switch_pulled");
            // 拉下以后合上交接单，镜头转回车间看逐排熄灯（WorldPresenter）
            Panel.schedule.Execute(() => { if (IsOpen) CloseSelf(); }).StartingIn(700);
        }
    }
}

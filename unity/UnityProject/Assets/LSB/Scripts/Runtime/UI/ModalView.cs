// 《最后一次交班》Demo α —— 面板基类
// 一个面板 = 一层半透明遮罩 + 一张纸（或衡的屏幕）。打开时世界里不能点（ViewHost.AnyOpen）。
// 开合动效用 USS transition（UI-DS：打开 D-Slow 480ms，关闭 D-Base 240ms，纸张 ease-out-cubic）。
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public abstract class ModalView : IGameView
    {
        protected readonly GameSession g;
        protected readonly UiRoot ui;
        protected readonly ViewHost host;
        protected readonly VisualElement Dim;
        protected readonly VisualElement Panel;
        bool open;

        public abstract string Id { get; }
        public bool IsOpen => open;

        /// <param name="panelClass">paper（纸面）/ sys（衡的屏幕）/ 其它自定义类</param>
        /// <param name="dimClass">遮罩样式；side = 不压暗，面板靠右（衡的平板）</param>
        protected ModalView(GameSession g, UiRoot ui, ViewHost host, string panelClass, string dimClass = null)
        {
            this.g = g; this.ui = ui; this.host = host;
            Dim = UiRoot.Box("modal-dim", "view-dim");
            if (!string.IsNullOrEmpty(dimClass)) Dim.AddToClassList(dimClass);
            Panel = UiRoot.Box(panelClass, "view-panel");
            Dim.Add(Panel);
            UiRoot.Show(Dim, false);
            ui.ViewLayer.Add(Dim);
            // 点遮罩空白处 = 合上（点纸面本身不算）
            Dim.RegisterCallback<PointerDownEvent>(e => { if (e.target == Dim && CloseOnDimClick) host.CloseTop(); });
            g.Bus.Raised += e => { if (open) OnEvent(e); };
        }

        protected virtual bool CloseOnDimClick => true;

        public void Open(string arg)
        {
            open = true;
            UiRoot.Show(Dim, true);
            Dim.RemoveFromClassList("in");
            Build(arg);
            // 下一帧加上 .in，触发 USS 过渡（从下方抬起 + 淡入）
            Dim.schedule.Execute(() => Dim.AddToClassList("in")).StartingIn(16);
            g.Playtest.Record("view_open", Id, arg);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            OnClose();
            Dim.RemoveFromClassList("in");
            UiRoot.Show(Dim, false);
        }

        /// <summary>面板里的“合上”按钮。</summary>
        protected void CloseSelf() => host.CloseTop();

        protected abstract void Build(string arg);
        protected virtual void OnClose() { }
        protected virtual void OnEvent(GameEvent e) { }

        // ---------------- 小工具 ----------------
        protected static Label L(string text, params string[] cls) => UiRoot.L(text, cls);
        protected static VisualElement Box(params string[] cls) => UiRoot.Box(cls);
        protected static Button B(string text, System.Action a, params string[] cls) => UiRoot.B(text, a, cls);

        /// <summary>第二层（专业说法）是否显示：设置里“专业说法”开关。</summary>
        protected bool Pro => g.State.Settings.ShowPro;

        protected static void Rot(VisualElement v, float deg) => v.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));

        protected static float Now => Time.unscaledTime;
    }
}

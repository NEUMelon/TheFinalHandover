// 《最后一次交班》Demo α —— 结局过场（覆盖层）+ 结局各步骤开哪个面板
// 主项目文档 §5.3 12:30–15:00：
//   衡请求确认交接 → 交班本最后一页，周建国一只手入画签字（EndingView）→ 合上，玩家去拉总闸（SwitchView）
//   → 主轴降速、灯由近及远逐排熄灭 → 静默 5 秒 → 汽笛 → 冷白亮起，衡启动，日志滚动，四条规则各停一下
//   → 未登记微弱信号，申请命名 → 交班本最后一页：签名 + 命名（EndingView）→ 远景背影 → 黑屏献词 → 本局完成。
// 过场期间：上下黑边、HUD 收起、世界里不能点；字幕显示在这一层（HUD 收起了）。
// 自动步骤的节奏由 EndingSystem.Tick 按 StepSeconds 推进，这里只负责画面。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class EndingOverlay
    {
        public static EndingOverlay Instance { get; private set; }
        /// <summary>过场中：世界里不能点（InteractionController）。</summary>
        public static bool Cinematic => Instance != null && Instance.g.Ending.Cinematic;

        readonly GameSession g;
        readonly ViewHost views;
        readonly HudView hud;
        readonly VisualElement root, barTop, barBottom, caps, logBox, black, doneCard;
        readonly Label dedication;
        readonly Queue<(string text, string kind, bool rule)> pending = new Queue<(string, string, bool)>();
        float nextLineAt, creditsAt = -1, doneAt = -1;

        public EndingOverlay(GameSession g, UiRoot ui, ViewHost views, HudView hud)
        {
            Instance = this;
            this.g = g; this.views = views; this.hud = hud;
            root = UiRoot.Box("ending-ov");
            root.pickingMode = PickingMode.Ignore;
            barTop = UiRoot.Box("cine-bar", "top"); barBottom = UiRoot.Box("cine-bar", "bottom");
            root.Add(barTop); root.Add(barBottom);
            logBox = UiRoot.Box("cine-log"); logBox.pickingMode = PickingMode.Ignore;
            root.Add(logBox);
            caps = UiRoot.Box("mem-caps"); caps.pickingMode = PickingMode.Ignore;
            root.Add(caps);
            black = UiRoot.Box("cine-black"); black.pickingMode = PickingMode.Ignore;
            dedication = UiRoot.L(ContentDB.Dedication, "f-serif", "cine-dedication");
            black.Add(dedication);
            root.Add(black);
            doneCard = UiRoot.Box("cine-done");
            root.Add(doneCard);
            UiRoot.Show(doneCard, false);
            UiRoot.Show(root, false);
            ui.OverlayLayer.Add(root);
            g.Bus.Raised += OnEvent;
        }

        void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case Ev.EndingStepChanged: OnStep((EndingStep)e.N); break;
                case Ev.PhaseChanged:
                    if (e.B == "load" || e.B == "new") Resync();
                    if (e.A == Phase.Done.ToString()) doneAt = Time.unscaledTime + 1.5f;
                    break;
                case Ev.Caption: if (g.Ending.Cinematic) Cap(e.A); break;
                case Ev.Line:
                    if (g.Ending.Cinematic && g.Content.Lines.TryGetValue(e.A, out var l)) Cap(g.Dialogue.Who(l) + "　" + l.Text);
                    break;
                case Ev.HengLog:
                    // 衡启动以后的日志：逐行打到画面中间（规则那几行停一下）
                    if (g.State.Phase == Phase.Ending && g.State.Ending >= EndingStep.HengBoot && g.State.Ending <= EndingStep.UnknownSignal)
                        pending.Enqueue((e.A, LastKind(), e.B == "RULE" && System.Array.IndexOf(RuleIds.All, e.A) >= 0));
                    break;
            }
        }

        string LastKind() => g.State.Log.Count > 0 ? g.State.Log[g.State.Log.Count - 1].Kind : "";

        void OnStep(EndingStep st)
        {
            switch (st)
            {
                case EndingStep.HandoverRequest:
                    // 镜头先到控制间门口（EndingSystem 把节点挪到 N9），再翻开最后一页
                    root.schedule.Execute(() => { if (g.State.Ending <= EndingStep.JianguoSign) views.Open("ending"); }).StartingIn(900);
                    break;
                case EndingStep.PullSwitch:
                    views.CloseAll();
                    g.Caption("按交接单，拉下总闸。");
                    break;
                case EndingStep.LightsOut:
                    views.CloseAll();
                    Enter();
                    break;
                case EndingStep.NameAndSign:
                    Leave();
                    views.Open("ending");
                    break;
                case EndingStep.Credits:
                    views.CloseAll();
                    Enter();
                    logBox.Clear();
                    creditsAt = Time.unscaledTime;
                    break;
            }
            hud.Refresh();
        }

        void Enter()
        {
            UiRoot.Show(root, true);
            root.AddToClassList("on");
            hud.SetVisible(false);
            caps.Clear();
        }

        void Leave()
        {
            root.RemoveFromClassList("on");
            UiRoot.Show(root, false);
            hud.SetVisible(true);
            logBox.Clear(); pending.Clear();
        }

        /// <summary>读档 / 新游戏：按当前步骤摆好画面。</summary>
        void Resync()
        {
            creditsAt = -1; doneAt = -1; pending.Clear(); logBox.Clear(); caps.Clear();
            black.RemoveFromClassList("on"); dedication.RemoveFromClassList("on");
            UiRoot.Show(doneCard, false);
            var s = g.State;
            if (s.Phase != Phase.Ending) { Leave(); return; }
            if (g.Ending.Cinematic) Enter(); else Leave();
            if (s.Ending == EndingStep.NameAndSign || s.Ending <= EndingStep.JianguoSign && s.Ending > EndingStep.None)
                root.schedule.Execute(() => views.Open("ending")).StartingIn(50);
            if (s.Ending >= EndingStep.Credits) creditsAt = Time.unscaledTime;
        }

        void Cap(string text)
        {
            var l = UiRoot.L(text, "mem-cap");
            l.pickingMode = PickingMode.Ignore;
            caps.Add(l);
            while (caps.childCount > 2) caps.RemoveAt(0);
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            // 衡的日志：一行一行出来；四条规则每条停 0.8 秒
            if (pending.Count > 0 && now >= nextLineAt)
            {
                var (text, kind, rule) = pending.Dequeue();
                var l = UiRoot.L(rule ? text : $"[{g.State.Clock}] {text}", rule ? "f-mono" : "f-sans", rule ? "cine-rule" : "cine-line");
                if (!string.IsNullOrEmpty(kind)) l.AddToClassList(kind);
                l.pickingMode = PickingMode.Ignore;
                logBox.Add(l);
                while (logBox.childCount > 9) logBox.RemoveAt(0);
                nextLineAt = now + (rule ? 0.8f : 0.45f);
            }
            // 献词：背影走出去 2.4 秒后黑下来，再浮出献词（原型 finale 的节奏）
            if (creditsAt > 0)
            {
                float t = now - creditsAt;
                black.EnableInClassList("on", t > 2.4f);
                dedication.EnableInClassList("on", t > 3.6f);
            }
            if (doneAt > 0 && now >= doneAt) { doneAt = -1; ShowDone(); }
        }

        void ShowDone()
        {
            doneCard.Clear();
            doneCard.Add(UiRoot.L("Demo α　本局完成", "panel-title"));
            doneCard.Add(UiRoot.L($"用时 {System.TimeSpan.FromSeconds(g.State.PlayTime):mm\\:ss}　新规则 {g.State.PlayerRuleName}", "panel-note"));
            doneCard.Add(UiRoot.L("试玩记录已导出到 " + SaveStore.PlaytestDir, "panel-note"));
            doneCard.Add(UiRoot.B("回到标题", () => { UiRoot.Show(doneCard, false); Leave(); black.RemoveFromClassList("on"); dedication.RemoveFromClassList("on"); creditsAt = -1; views.ShowTitle(); }, "btn"));
            UiRoot.Show(doneCard, true);
        }
    }
}

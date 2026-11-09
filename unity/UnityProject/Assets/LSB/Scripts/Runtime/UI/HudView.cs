// 《最后一次交班》Demo α —— 游戏内 HUD
// 左上：时刻 / 当前点检项 / 位置；右上：衡日志；右侧：页边提示；左下：导航；底部：字幕；右下：工具箱、交接灯、交班本。
// 主层只说玩家语言（UX 原则）：工具、地点、物件都用中文名，不出现 L1 / kHz 之类。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class HudView
    {
        readonly GameSession g;
        readonly UiRoot ui;
        public readonly VisualElement Root;

        Label time, item, place, hover, feedback, transitionText, buildTag;
        VisualElement nav, captions, ticker, hintNote, transition, lamps;
        Button hintBtn, ledgerBtn;
        readonly Dictionary<ToolId, Button> tools = new Dictionary<ToolId, Button>();
        readonly List<(Label label, float until)> liveCaptions = new List<(Label, float)>();
        float feedbackUntil, transitionUntil;

        public System.Action<string> OnNavigate;       // 节点 ID
        public System.Action<string> OnCloseUp;        // 特写 ID
        public System.Action OnBack;
        public System.Action OnLedger;

        public HudView(GameSession g, UiRoot ui)
        {
            this.g = g; this.ui = ui;
            Root = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("layer");
            ui.HudLayer.Add(Root);
            Build();
            g.Bus.Raised += OnEvent;
        }

        void Build()
        {
            var clock = UiRoot.Box("hud-clock"); clock.pickingMode = PickingMode.Ignore;
            time = UiRoot.L("16:00", "time"); item = UiRoot.L("", "item"); place = UiRoot.L("", "place");
            clock.Add(time); clock.Add(item); clock.Add(place);
            Root.Add(clock);

            ticker = UiRoot.Box("heng-ticker"); ticker.pickingMode = PickingMode.Ignore;
            Root.Add(ticker);

            hintNote = UiRoot.Box("hint-note"); hintNote.pickingMode = PickingMode.Ignore;
            Root.Add(hintNote);
            hintBtn = UiRoot.B("需要提示", () => g.Hints.RequestManual(), "chip", "hint-btn");
            Root.Add(hintBtn);

            nav = UiRoot.Box("nav"); nav.pickingMode = PickingMode.Ignore;
            Root.Add(nav);

            captions = UiRoot.Box("captions"); captions.pickingMode = PickingMode.Ignore;
            Root.Add(captions);
            feedback = UiRoot.L("", "feedback"); feedback.pickingMode = PickingMode.Ignore;
            Root.Add(feedback);

            var tb = UiRoot.Box("tools"); tb.pickingMode = PickingMode.Ignore;
            lamps = UiRoot.Box("lamps"); lamps.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 4; i++) lamps.Add(UiRoot.Box("lamp"));
            tb.Add(lamps);
            int n = 1;
            foreach (var t in new[] { ToolId.Probe, ToolId.Torch, ToolId.Caliper, ToolId.Driver })
            {
                var tool = t;
                var b = UiRoot.B(InteractionSystem.ToolName(t), () => g.Interaction.SelectTool(tool), "tool");
                b.Add(UiRoot.L((n++).ToString(), "no"));
                tools[t] = b; tb.Add(b);
            }
            ledgerBtn = UiRoot.B("交班本", () => OnLedger?.Invoke(), "ledger-btn");
            tb.Add(ledgerBtn);
            Root.Add(tb);

            hover = UiRoot.L("", "hover"); hover.pickingMode = PickingMode.Ignore;
            Root.Add(hover);

            transition = UiRoot.Box("transition"); transition.pickingMode = PickingMode.Ignore;
            transitionText = UiRoot.L("", "t"); transition.Add(transitionText);
            ui.OverlayLayer.Add(transition);

            buildTag = UiRoot.L("Demo α · 灰盒 / 占位资源 · 音频与诊断数据为程序合成的模拟数据", "build-tag");
            buildTag.pickingMode = PickingMode.Ignore;
            Root.Add(buildTag);
        }

        public void SetVisible(bool on) => UiRoot.Show(Root, on);

        // =====================================================================
        public void Refresh()
        {
            var s = g.State;
            time.text = s.Clock + "　中班";
            var row = g.Content.Row(s.Phase);
            item.text = s.Phase == Phase.Intro ? "交班" : s.Phase == Phase.Ending ? "交班　最后一页" : row != null ? $"点检第{"一二三四"[PhaseUtil.PuzzleIndex(s.Phase)]}项　{row.Item}" : "";
            var cu = g.Nav.CurrentCloseUp;
            place.text = cu != null ? cu.Name : g.Nav.Current?.Name ?? "";
            RefreshNav();
            RefreshTools();
            RefreshHint();
            RefreshTicker();
            for (int i = 0; i < 4; i++) lamps[i].EnableInClassList("off", !s.Lamps[i]);
            bool hasNew = g.Content.Pages.Any(p => g.Ledger.HasNew(p.Id) && p.GivesEvidence != null);
            ledgerBtn.EnableInClassList("new", hasNew);
        }

        void RefreshNav()
        {
            nav.Clear();
            if (g.State.CloseUp != null)
            {
                nav.Add(UiRoot.B("← 返回（Esc）", () => OnBack?.Invoke(), "chip", "back"));
                return;
            }
            foreach (var c in g.Nav.CloseUpsHere())
            {
                var id = c.Id;
                nav.Add(UiRoot.B("细看：" + c.Name, () => OnCloseUp?.Invoke(id), "chip", "cu"));
            }
            foreach (var n in g.Nav.Neighbors().OrderBy(n => n.Id.Length).ThenBy(n => n.Id))
            {
                var id = n.Id;
                nav.Add(UiRoot.B("去 " + n.Name, () => OnNavigate?.Invoke(id), "chip"));
            }
        }

        void RefreshTools()
        {
            foreach (var kv in tools) kv.Value.EnableInClassList("sel", g.State.Tool == kv.Key);
        }

        void RefreshHint()
        {
            hintNote.Clear();
            var texts = g.Hints.VisibleTexts.ToList();
            UiRoot.Show(hintNote, texts.Count > 0);
            foreach (var t in texts) hintNote.Add(UiRoot.L(t, "h"));
            UiRoot.Show(hintBtn, !g.State.Settings.HintsOn && g.Hints.Current != null && g.Hints.Level < 3 && texts.Count == 0);
        }

        void RefreshTicker()
        {
            ticker.Clear();
            ticker.Add(UiRoot.L($"衡 · {g.Rules.LevelName}　交接 {g.State.LampsOff}/4", "head"));
            foreach (var l in g.State.Log.Skip(System.Math.Max(0, g.State.Log.Count - 4)))
            {
                var line = UiRoot.L($"[{l.Time}] {l.Module} > {l.Text}", "line");
                if (!string.IsNullOrEmpty(l.Kind)) line.AddToClassList(l.Kind);
                ticker.Add(line);
            }
        }

        // =====================================================================
        public void Hover(string text, bool actionable, Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(text)) { UiRoot.Show(hover, false); return; }
            hover.text = text;
            hover.EnableInClassList("act", actionable);
            UiRoot.Show(hover, true);
            var panel = ui.Root.panel;
            if (panel == null) return;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            hover.style.left = p.x + 18; hover.style.top = p.y + 12;
        }

        public void Caption(string text, string who, float seconds = 3.2f)
        {
            if (!g.State.Settings.Captions && who == null) return;
            var l = UiRoot.L(who == null ? text : $"<color=#E8B45A>{who}</color>　{text}", "caption");
            l.enableRichText = true;
            l.pickingMode = PickingMode.Ignore;
            captions.Add(l);
            liveCaptions.Add((l, Time.unscaledTime + seconds + text.Length * 0.06f));
            while (liveCaptions.Count > 3) { captions.Remove(liveCaptions[0].label); liveCaptions.RemoveAt(0); }
        }

        public void Feedback(string text, bool ok)
        {
            feedback.text = text;
            feedback.EnableInClassList("ok", ok); feedback.EnableInClassList("no", !ok);
            UiRoot.Show(feedback, true);
            feedbackUntil = Time.unscaledTime + 2.6f;
        }

        public void TransitionCard(string text, float seconds = 2.4f)
        {
            transitionText.text = text;
            UiRoot.Show(transition, true);
            transitionUntil = Time.unscaledTime + seconds;
        }

        public bool TransitionShowing => transition.resolvedStyle.display == DisplayStyle.Flex && Time.unscaledTime < transitionUntil;

        public void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = liveCaptions.Count - 1; i >= 0; i--)
                if (now > liveCaptions[i].until) { captions.Remove(liveCaptions[i].label); liveCaptions.RemoveAt(i); }
            if (feedbackUntil > 0 && now > feedbackUntil) { UiRoot.Show(feedback, false); feedbackUntil = 0; }
            if (transitionUntil > 0 && now > transitionUntil) { UiRoot.Show(transition, false); transitionUntil = 0; }
        }

        void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case Ev.Caption: Caption(e.A, e.B); break;
                case Ev.Line:
                    var line = g.Content.Lines[e.A];
                    Caption(line.Text, g.Dialogue.Who(line), 3.6f);
                    break;
                case Ev.Feedback: Feedback(e.A, e.B == "ok"); break;
                case Ev.TransitionCard: TransitionCard(e.A); break;
                case Ev.ToolChanged: RefreshTools(); break;
                case Ev.HintChanged: case Ev.HengLog: case Ev.NodeChanged: case Ev.CloseUpChanged: case Ev.PhaseChanged:
                case Ev.ClockChanged: case Ev.LampOff: case Ev.EvidenceAcquired: case Ev.LevelUp: case Ev.RuleStatusChanged:
                    Refresh(); break;
            }
        }
    }
}

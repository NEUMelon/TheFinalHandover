// 《最后一次交班》Demo α —— 天车防摇曲线（谜题四，衡的调试平板，特写 CU4）
// 主层只说：天车停车分两段收；第一段固定，第二段的时刻你来拖；钩子摆一个来回 = 1 拍。
// 时间轴：每拍一条竖线；听过磁带以后，衡把铃点标上去（每半拍一个）。
// 下面的曲线是“上一次 / 这一次”试吊时吊钩相对小车的摆动，由 CraneZvModel 真的积分出来：第二段挪了，曲线和水的结果都跟着变。
// 面板靠右（不压暗），左边场景里的吊钩和搪瓷缸同时按同一份仿真结果摆（WorldPresenter.PlayTrial）。
// ⚠ 模拟 / 占位：二维单摆示意，不是起重机控制模型；绳长、速度、减速度都是占位值。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class CraneView : ModalView, ITickView
    {
        public override string Id => "crane";

        const int GW = 800, GH = 220, LaneH = 46;
        const float B0 = -0.25f, B1 = 2.75f;          // 时间轴：第一段收车开始前 0.25 拍 → 之后 2.75 拍
        static float Lead => 1.0f;                     // CraneZvModel.Run：收车前匀速 1 秒

        readonly Texture2D tex;
        readonly Color32[] px = new Color32[GW * GH];
        readonly float yScale;
        VisualElement graph, lane, seg2, playhead, water, mug;
        Label t2Label, result;
        CraneTrial prev, cur;
        float replayAt = -1;
        readonly List<CraneTrial> history = new List<CraneTrial>();
        bool dragging;

        public CraneView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "sys", "side")
        {
            Panel.AddToClassList("crane");
            tex = new Texture2D(GW, GH, TextureFormat.RGBA32, false) { name = "LSB_占位_天车摆动曲线", wrapMode = TextureWrapMode.Clamp };
            // 纵向比例：按“第二段在一整拍”（最晃的情况）定，这样挪对了以后曲线明显变平
            var worst = CraneZvModel.Simulate(CraneZvModel.DefaultRope, 1f);
            float m = 0; for (int i = 0; i < worst.Time.Length; i++) m = Mathf.Max(m, Mathf.Abs(worst.HookX[i] - worst.TrolleyX[i]));
            yScale = (GH * 0.45f) / Mathf.Max(0.01f, m);
        }

        float T => CraneZvModel.Period(CraneZvModel.DefaultRope);
        static float XOfBeat(float b) => (b - B0) / (B1 - B0) * GW;
        static float BeatOfX(float x) => B0 + x / GW * (B1 - B0);

        protected override void Build(string arg)
        {
            cur = g.Puzzles.LastTrial;
            if (cur != null && history.Count == 0) history.Add(cur);
            Render();
            if (cur != null) replayAt = Now;   // 打开时把最近一次试吊再放一遍（第一次就是衡自动试吊：水洒）
        }

        void Render()
        {
            Panel.Clear();
            var head = Box("diag-head");
            head.Add(L("CR01 天车试吊", "sys-name"));
            head.Add(L($"绳长 {CraneZvModel.DefaultRope:0.0} 米", "sys-dim"));
            head.Add(B(Pro ? "专业说法：开" : "专业说法：关", () => { g.State.Settings.ShowPro = !g.State.Settings.ShowPro; Render(); }, "sys-btn", "small"));
            Panel.Add(head);
            Panel.Add(L("天车停车分两段收。第一段固定，第二段什么时候收，由你来拖。钩子摆一个来回，叫 1 拍。", "sys-dim"));
            if (Pro) Panel.Add(L($"摆动周期估计 {T:0.0} 秒；两段整形（ZV）幅值已算好，只调第二段指令时刻 t2。", "f-mono", "sys-dim"));

            // ---- 时间轴 + 曲线 ----
            graph = Box("crane-graph");
            graph.style.width = GW; graph.style.height = GH;
            var curve = Box("crane-curve");
            curve.style.backgroundImage = new StyleBackground(tex);
            graph.Add(curve);
            var mid = Box("crane-zero"); mid.style.top = GH / 2f; graph.Add(mid);
            for (int b = 0; b <= 2; b++)
            {
                var line = Box("crane-beat"); line.style.left = XOfBeat(b); graph.Add(line);
                var lab = L(b == 0 ? "第一段收车" : $"{b} 拍", "crane-beat-l"); lab.style.left = XOfBeat(b) + 4; graph.Add(lab);
            }
            if (g.State.Has("E4_4"))
            {
                // 衡标的铃点：每半拍一个（磁带里的铃）
                for (float b = 0.5f; b < B1; b += 0.5f)
                {
                    var bell = Box("crane-bell"); bell.style.left = XOfBeat(b) - 5; graph.Add(bell);
                    if (Mathf.Abs(b - 0.5f) < 0.01f) { var bl = L("铃", "crane-bell-l"); bl.style.left = XOfBeat(b) - 7; graph.Add(bl); }
                }
            }
            playhead = Box("crane-playhead"); UiRoot.Show(playhead, false); graph.Add(playhead);
            graph.Add(L("钩子往前", "crane-ax", "top")); graph.Add(L("钩子往后", "crane-ax", "bottom"));
            Panel.Add(graph);

            // ---- 收车指令（第二段可拖） ----
            lane = Box("crane-lane");
            lane.style.width = GW; lane.style.height = LaneH;
            float segBeats = (CraneZvModel.V0 / 2f / CraneZvModel.Decel) / T;
            var seg1 = Box("crane-seg", "fixed"); seg1.style.left = XOfBeat(0); seg1.style.width = Mathf.Max(10, XOfBeat(segBeats) - XOfBeat(0)); seg1.Add(L("一", "crane-seg-t")); lane.Add(seg1);
            seg2 = Box("crane-seg", "drag"); seg2.style.width = Mathf.Max(10, XOfBeat(segBeats) - XOfBeat(0)); seg2.Add(L("二", "crane-seg-t")); lane.Add(seg2);
            PlaceSeg2();
            lane.RegisterCallback<PointerDownEvent>(e => { dragging = true; lane.CapturePointer(e.pointerId); DragTo(e.localPosition.x); });
            lane.RegisterCallback<PointerMoveEvent>(e => { if (dragging) DragTo(e.localPosition.x); });
            lane.RegisterCallback<PointerUpEvent>(e => { dragging = false; lane.ReleasePointer(e.pointerId); });
            Panel.Add(lane);
            Panel.Add(L("下面这条是收车指令：拖“二”，或者用按钮一格一格挪。", "sys-dim"));

            // ---- 操作 ----
            var ctl = Box("crane-ctl");
            ctl.Add(B("◀ 早一点", () => Nudge(-1), "sys-btn"));
            t2Label = L("", "crane-t2");
            ctl.Add(t2Label);
            ctl.Add(B("晚一点 ▶", () => Nudge(1), "sys-btn"));
            var trial = B("试吊", Trial, "sys-btn", "on", "crane-go");
            ctl.Add(trial);
            Panel.Add(ctl);
            UpdateT2Label();

            // ---- 结果：搪瓷缸 + 最近几次 ----
            var res = Box("crane-res");
            mug = Box("crane-mug");
            water = Box("crane-water");
            mug.Add(water);
            res.Add(mug);
            var col = Box("crane-hist");
            result = L(cur == null ? "还没试吊。" : OutcomeText(cur), "sys-line");
            col.Add(result);
            foreach (var h in history.AsEnumerable().Reverse().Take(4))
                col.Add(L($"第二段 {h.T2Beats:0.00} 拍　→　{OutcomeText(h)}" + (Pro ? $"　残余摆动 {h.Residual * 100:0}%" : ""), "sys-dim"));
            res.Add(col);
            Panel.Add(res);

            if (g.State.Has("E4_TUNE"))
            {
                var ok = Box("twin-done");
                ok.Add(L("记下了：" + g.Content.Ev("E4_TUNE").Name, "sys-line", "ok"));
                if (g.Triad.CanOpen(out _)) ok.Add(B("填三联板 ▶", () => host.Open("triad"), "sys-btn", "on"));
                Panel.Add(ok);
            }
            else if (cur != null && cur.Outcome != CraneOutcome.Spill && !g.State.Has("E4_2"))
                Panel.Add(L("水是稳了，可还不知道这个时刻是哪儿来的。", "sys-line"));

            var foot = Box("mes-foot");
            foot.Add(B("返回（Esc）", CloseSelf, "sys-btn"));
            foot.Add(L("模拟 / 占位　二维单摆示意，不是起重机控制模型", "sys-dim"));
            Panel.Add(foot);
            DrawCurves();
        }

        static string OutcomeText(CraneTrial t) => t.Outcome == CraneOutcome.Spill ? "水洒了" : t.Outcome == CraneOutcome.Shake ? "水面轻晃，没洒" : "水面静止";

        void PlaceSeg2() { if (seg2 != null) seg2.style.left = XOfBeat(g.State.CraneT2); }

        void UpdateT2Label()
        {
            if (t2Label == null) return;
            float b = g.State.CraneT2;
            t2Label.text = $"第二段：第一段之后 {b:0.00} 拍" + (Pro ? $"（{b * T:0.0} 秒）" : "");
        }

        void DragTo(float x)
        {
            float before = g.State.CraneT2;
            g.Puzzles.SetT2(BeatOfX(x));
            if (System.Math.Abs(before - g.State.CraneT2) > 1e-4f) g.Sfx("tick");
            PlaceSeg2(); UpdateT2Label();
        }

        void Nudge(int dir)
        {
            g.Puzzles.SetT2(g.State.CraneT2 + dir * CraneZvModel.StepBeats);
            g.Sfx("tick");
            PlaceSeg2(); UpdateT2Label();
        }

        void Trial()
        {
            if (cur != null) prev = cur;
            var t = g.Puzzles.Trial();
            if (t == null) return;
            cur = t;
            history.Add(t);
            replayAt = Now;
            Render();
        }

        protected override void OnEvent(GameEvent e)
        {
            // 第一次进特写：面板先打开，衡的自动试吊随后才算出来 → 这里接住，回放“水洒”
            if (e.Type == Ev.CraneTrial && e.B == "auto" && g.Puzzles.LastTrial != null && g.Puzzles.LastTrial != cur)
            {
                cur = g.Puzzles.LastTrial; history.Add(cur); replayAt = Now;
                Panel.schedule.Execute(Render);
            }
            else if (e.Type == Ev.EvidenceAcquired) Panel.schedule.Execute(Render);
        }

        // =====================================================================
        // 曲线（贴图）：上一次暗、这一次亮
        // =====================================================================
        void DrawCurves()
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            if (prev != null) Plot(prev, new Color32(230, 240, 245, 70));
            if (cur != null) Plot(cur, cur.Outcome == CraneOutcome.Still ? new Color32(159, 214, 180, 255) : new Color32(230, 240, 245, 230));
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        void Plot(CraneTrial t, Color32 c)
        {
            float lastX = -1, lastY = 0;
            for (int i = 0; i < t.Time.Length; i++)
            {
                float b = (t.Time[i] - Lead) / t.Period;
                if (b < B0 || b > B1) continue;
                float x = XOfBeat(b), y = GH / 2f - (t.HookX[i] - t.TrolleyX[i]) * yScale;
                if (lastX >= 0) Line(lastX, lastY, x, y, c);
                lastX = x; lastY = y;
            }
        }

        void Line(float x0, float y0, float x1, float y1, Color32 c)
        {
            int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, u)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, u));
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (x < 0 || x >= GW || yy < 0 || yy >= GH) continue;
                    px[(GH - 1 - yy) * GW + x] = c;      // Texture2D 原点在左下
                }
            }
        }

        // =====================================================================
        // 回放：播放头 + 搪瓷缸里的水（和场景里的吊钩用同一份结果）
        // =====================================================================
        public void Tick()
        {
            if (cur == null || replayAt < 0 || playhead == null) return;
            float t = Now - replayAt;
            int i = Mathf.Clamp((int)(t / cur.SampleDt), 0, cur.Time.Length - 1);
            float b = (cur.Time[i] - Lead) / cur.Period;
            bool on = b >= B0 && b <= B1 && t < cur.Time[cur.Time.Length - 1];
            UiRoot.Show(playhead, on);
            if (on) playhead.style.left = XOfBeat(b);
            float th = cur.Angle[i];
            Rot(water, -th * Mathf.Rad2Deg * 6f);
            bool spilled = cur.Outcome == CraneOutcome.Spill && cur.Time[i] > cur.StopTime + cur.Period * 0.25f;
            water.EnableInClassList("spilled", spilled);
            if (i >= cur.Time.Length - 1) { replayAt = -1; Rot(water, 0); }
        }
    }
}

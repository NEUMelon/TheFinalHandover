// 《最后一次交班》Demo α —— 衡的平板：听音诊断
// 谜题一：左右听音对比（信号条）。谜题二：左边的时频图上框出“一下一下的竖条”；右边看齿轮指示，判“正常 / 异常”。
// 主层只说“左边 / 右边、咔哒 / 嗡、正常 / 异常”；L1 / R1、kHz、啮合线、边频指标放第二层（“专业说法”开关）。
// 时频图、冲击时刻、边频指标、信号条比例全部沿用 UI 原型 data.js / spectro.js（SpectroModel），界面上标“模拟 / 占位数据”。
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class DiagView : ModalView, ITickView
    {
        public override string Id => "diag";
        public DiagView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "sys", "side") { }

        protected override bool CloseOnDimClick => false;

        // 时频图显示尺寸（与原型 screens.css .spec-box 一致）
        const float SW = 680, SH = 280;
        VisualElement spec, selBox, playhead, flash;
        Label clickWord, soundLine, specMsg;
        Vector2? dragFrom;
        int lastK = -1;
        float wordUntil;

        protected override void Build(string arg)
        {
            Panel.Clear();
            spec = null; lastK = -1;
            Panel.AddToClassList("diag");
            Panel.EnableInClassList("wide", g.State.Phase == Phase.P2);
            var head = Box("diag-head");
            head.Add(L("衡 · 听音诊断", "sys-head"));
            head.Add(B(Pro ? "专业说法：开" : "专业说法：关", () => { g.State.Settings.ShowPro = !g.State.Settings.ShowPro; Build(null); }, "sys-btn", "small"));
            Panel.Add(head);
            if (g.State.Phase == Phase.P1) BuildCompare();
            else if (g.State.Phase == Phase.P2) BuildSpectro();
            else
            {
                Panel.Add(L("这一项不用听音。", "sys-line"));
                PointRow();
            }
            Panel.Add(L(SpectroModel.DataLabel + "　声音和图都是程序合成的示意", "data-label"));
            Panel.Add(B("收起平板（Esc）", CloseSelf, "btn", "sys-btn"));
        }

        // =====================================================================
        // 谜题一：左右对比
        // =====================================================================
        void BuildCompare()
        {
            var s = g.State;
            Panel.Add(L("同一台车，两头各听一下。", "sys-line"));
            Panel.Add(Side("L1", "左边", "老周的点", s.Has("E1_3") ? null : "先找到左端的点"));
            Panel.Add(Side("R1", "右边", "衡装的传感器", null));

            var q = Box("diag-q");
            if (s.Has("E1_5"))
            {
                q.Add(L("已记下：左边听得清轴承（" + g.Content.Ev("E1_5").Paper + "）", "sys-line", "ok"));
            }
            else
            {
                q.Add(L("哪边听得清后轴承？", "sys-q"));
                var row = Box("diag-choice");
                bool both = s.Flag("heard_L1") && s.Flag("heard_R1");
                var l = B("左边", () => Choose("L1"), "sys-btn"); l.SetEnabled(both);
                var r = B("右边", () => Choose("R1"), "sys-btn"); r.SetEnabled(both);
                row.Add(l); row.Add(r);
                q.Add(row);
                if (!both) q.Add(L("两边都听过，再选。", "sys-dim"));
            }
            Panel.Add(q);
        }

        VisualElement Side(string point, string name, string sub, string lockedWhy)
        {
            var s = g.State;
            bool heard = s.Flag("heard_" + point);
            bool playing = g.Puzzles.DiagPoint == point;
            var v = Box("diag-side");
            v.EnableInClassList("playing", playing);
            var head = Box("diag-side-head");
            head.Add(L(name, "sys-name"));
            head.Add(L(Pro ? $"{sub}（{point}）" : sub, "sys-dim"));
            var b = B(playing ? "正在听…" : "听" + name, () => { g.Puzzles.Listen(point); Build(null); }, "sys-btn");
            b.SetEnabled(lockedWhy == null);
            head.Add(b);
            v.Add(head);
            if (lockedWhy != null) { v.Add(L(lockedWhy, "sys-dim")); return v; }
            if (!heard) { v.Add(L("还没听。", "sys-dim")); return v; }
            v.Add(Bar("轴承的声音", SpectroModel.BearingShare(point), "brg"));
            v.Add(Bar("齿轮的嗡", SpectroModel.GearShare(point), "gear"));
            if (Pro) v.Add(L($"{point}：轴承成分 {SpectroModel.BearingShare(point):0.00}　齿轮 {SpectroModel.GearShare(point):0.00}", "sys-dim"));
            return v;
        }

        static VisualElement Bar(string label, float share, string cls)
        {
            var row = Box("bar-row");
            row.Add(L(label, "bar-label"));
            var track = Box("bar-track");
            var fill = Box("bar-fill", cls);
            fill.style.width = Length.Percent(share * 100f);
            track.Add(fill);
            row.Add(track);
            return row;
        }

        void Choose(string side)
        {
            g.Puzzles.CompareChoose(side);
            Build(null);
        }

        // =====================================================================
        // 谜题二：时频图 + 齿轮指示
        // =====================================================================
        void PointRow()
        {
            var row = Box("diag-points");
            foreach (var (pt, name) in new[] { ("L1", "左边　老周的点"), ("R1", "右边　衡的传感器") })
            {
                var p = pt;
                var b = B(Pro ? $"{name}（{pt}）" : name, () => { g.Puzzles.Listen(p); Build(null); }, "sys-btn");
                b.EnableInClassList("on", g.Puzzles.DiagPoint == pt);
                row.Add(b);
            }
            Panel.Add(row);
        }

        void BuildSpectro()
        {
            var p = g.Puzzles.DiagPoint;
            PointRow();
            soundLine = L(p == "L1" ? "〔左边：嗡……咔哒、咔哒、咔哒〕" : p == "R1" ? "〔右边：一片均匀的嗡〕" : "选一边听。", "sys-dim", "sound-line");
            Panel.Add(soundLine);
            if (p == null) return;

            // ---- 时频图 ----
            var wrap = Box("spec-wrap");
            var yax = Box("spec-yax");
            if (Pro) { yax.Add(L("kHz", "ax")); foreach (var k in new[] { 8, 6, 4, 2, 0 }) { var t = L(k.ToString(), "ax"); yax.Add(t); } }
            else { yax.Add(L("高音", "ax")); yax.Add(Box("ax-fill")); yax.Add(L("低音", "ax")); }
            wrap.Add(yax);

            spec = Box("spec");
            spec.style.backgroundImage = new StyleBackground(SpectroTexture.Get(p));
            if (p == "L1")
            {
                var band = Abs(Box("spec-band"), 0, (1 - SpectroModel.BandHi / SpectroModel.FMax) * SH, SW, (SpectroModel.BandHi - SpectroModel.BandLo) / SpectroModel.FMax * SH);
                band.Add(L("本机共振带（2026 复测）", "spec-tag"));
                spec.Add(band);
            }
            var gmf = Abs(Box("spec-gmf"), 0, (1 - SpectroModel.Gmf / SpectroModel.FMax) * SH, SW, 1);
            gmf.Add(L(Pro ? "GMF 620Hz　齿轮啮合" : "这条横线：齿轮的嗡", "spec-tag", "under"));
            spec.Add(gmf);
            flash = Abs(Box("spec-flash"), 0, 0, 12, SH); UiRoot.Show(flash, false); spec.Add(flash);
            selBox = Abs(Box("spec-sel"), 0, 0, 0, 0); UiRoot.Show(selBox, false); spec.Add(selBox);
            playhead = Abs(Box("spec-head"), 0, 0, 1, SH); spec.Add(playhead);
            foreach (var c in new[] { flash, selBox, playhead }) c.pickingMode = PickingMode.Ignore;
            foreach (var c in spec.Children()) c.pickingMode = PickingMode.Ignore;
            if (p == "L1") BindDrag();
            wrap.Add(spec);
            Panel.Add(wrap);
            var xax = Box("spec-xax");
            xax.Add(L(Pro ? "0　　　时间（秒）　　　6" : "时间 →", "ax"));
            Panel.Add(xax);

            var strip = Box("click-strip");
            strip.Add(L(p == "L1" ? "一下一下：" : "", "sys-dim"));
            clickWord = L("咔哒", "click-word");
            UiRoot.Show(clickWord, p == "L1");
            strip.Add(clickWord);
            Panel.Add(strip);

            specMsg = L("", "sys-line", "spec-msg");
            Panel.Add(specMsg);
            if (p == "L1")
            {
                if (g.State.Has("E2_1")) { specMsg.text = "已标记：一下一下的咔哒（" + g.Content.Ev("E2_1").Paper + "）"; specMsg.AddToClassList("ok"); ShowOkBox(); }
                else specMsg.text = "按住鼠标拖动，把一下一下竖着的亮条框起来。";
            }
            else BuildGauge();
        }

        static VisualElement Abs(VisualElement v, float x, float y, float w, float h)
        {
            v.style.position = Position.Absolute; v.style.left = x; v.style.top = y; v.style.width = w; v.style.height = h;
            return v;
        }

        /// <summary>右边：齿轮指示（边频指标）。绿区 = 正常。</summary>
        void BuildGauge()
        {
            var box = Box("gauge-box");
            box.Add(L(Pro ? "边频指标 LT01_GB_GMF_SB_IDX" : "齿轮指示", "sys-name"));
            var gauge = Box("gauge");
            float th = SpectroModel.SidebandThreshold / SpectroModel.SidebandMax;
            var norm = Box("gauge-norm"); norm.style.width = Length.Percent(th * 100); gauge.Add(norm);
            var bad = Box("gauge-bad"); bad.style.left = Length.Percent(th * 100); gauge.Add(bad);
            var needle = Box("gauge-needle"); needle.style.left = Length.Percent(SpectroModel.SidebandIndex / SpectroModel.SidebandMax * 100); gauge.Add(needle);
            box.Add(gauge);
            box.Add(L(Pro ? $"当前 {SpectroModel.SidebandIndex:0.00}　阈值 {SpectroModel.SidebandThreshold:0.00}　满量程 {SpectroModel.SidebandMax:0.00}" : "左边绿区 = 正常　右边红区 = 磨坏了", "sys-dim"));
            var ask = Box("diag-choice");
            ask.Add(L("齿轮状态", "sys-q"));
            bool done = g.State.Has("E2_2");
            var ok = B("正常", () => { g.Puzzles.Sideband("ok"); Build(null); }, "sys-btn"); ok.EnableInClassList("on", done);
            var no = B("异常", () => { g.Puzzles.Sideband("bad"); specMsg.text = "〔对比：齿轮真磨坏了是这个声音，然后接回现在的右边〕"; }, "sys-btn");
            ask.Add(ok); ask.Add(no);
            box.Add(ask);
            Panel.Add(box);
            specMsg.text = done ? "已记下：齿轮正常（" + g.Content.Ev("E2_2").Paper + "）" : "";
            specMsg.EnableInClassList("ok", done);
        }

        void BindDrag()
        {
            spec.RegisterCallback<PointerDownEvent>(e =>
            {
                if (g.State.Has("E2_1")) return;
                dragFrom = Clamp(spec.WorldToLocal(e.position));
                spec.CapturePointer(e.pointerId);
                selBox.RemoveFromClassList("ok"); selBox.RemoveFromClassList("bad");
                Place(dragFrom.Value, dragFrom.Value);
                UiRoot.Show(selBox, true);
            });
            spec.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (dragFrom == null) return;
                Place(dragFrom.Value, Clamp(spec.WorldToLocal(e.position)));
            });
            spec.RegisterCallback<PointerUpEvent>(e =>
            {
                if (dragFrom == null) return;
                var a = dragFrom.Value; var b = Clamp(spec.WorldToLocal(e.position));
                dragFrom = null;
                spec.ReleasePointer(e.pointerId);
                if (Mathf.Abs(b.x - a.x) < 6 || Mathf.Abs(b.y - a.y) < 6) { UiRoot.Show(selBox, false); return; }
                var box = new SpecBox(X2T(a.x), X2T(b.x), Y2F(a.y), Y2F(b.y));
                var r = g.Puzzles.JudgeBox(box);
                specMsg.text = r.Message;
                specMsg.EnableInClassList("ok", r.Ok);
                selBox.EnableInClassList(r.Ok ? "ok" : "bad", true);
                if (r.Ok) { lastBox = (Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)); }
            });
        }

        (float x, float y, float w, float h)? lastBox;
        void ShowOkBox()
        {
            if (lastBox == null) return;
            var v = lastBox.Value;
            selBox.style.left = v.x; selBox.style.top = v.y; selBox.style.width = v.w; selBox.style.height = v.h;
            selBox.AddToClassList("ok");
            UiRoot.Show(selBox, true);
        }

        static Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, 0, SW), Mathf.Clamp(p.y, 0, SH));
        static float X2T(float x) => x / SW * SpectroModel.Duration;
        static float Y2F(float y) => (1 - y / SH) * SpectroModel.FMax;
        static float T2X(float t) => t / SpectroModel.Duration * SW;

        void Place(Vector2 a, Vector2 b)
        {
            selBox.style.left = Mathf.Min(a.x, b.x); selBox.style.top = Mathf.Min(a.y, b.y);
            selBox.style.width = Mathf.Abs(b.x - a.x); selBox.style.height = Mathf.Abs(b.y - a.y);
        }

        /// <summary>每帧：播放头跟着声音走；左边每个冲击经过播放头时，竖条闪一下、“咔哒”字样亮一下。</summary>
        public void Tick()
        {
            if (spec == null || playhead == null) return;
            float pos = ProceduralAudio.LoopPosition;
            if (pos < 0) { UiRoot.Show(playhead, false); return; }
            UiRoot.Show(playhead, true);
            playhead.style.left = T2X(pos);
            if (g.Puzzles.DiagPoint != "L1") return;
            int k = SpectroModel.LastImpact(pos);
            if (k >= 0)
            {
                float dt = pos - SpectroModel.Impacts[k];
                bool on = dt >= 0 && dt < 0.18f;
                UiRoot.Show(flash, on);
                if (on) { flash.style.left = T2X(SpectroModel.Impacts[k]) - 5; flash.style.opacity = 1 - dt / 0.18f; }
            }
            if (k != lastK) { lastK = k; wordUntil = Now + 0.11f; }
            clickWord.EnableInClassList("on", Now < wordUntil);
        }

        protected override void OnEvent(GameEvent e)
        {
            if (e.Type != Ev.EvidenceAcquired) return;
            if (e.A == "E1_5" || e.A == "E2_2") Panel.schedule.Execute(() => Build(null));
            if (e.A == "E2_1") Panel.schedule.Execute(() => { Build(null); ShowOkBox(); }).StartingIn(520);
        }
    }
}

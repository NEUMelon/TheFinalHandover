// 《最后一次交班》Demo α —— 交班本最后一页（结局，移植 UI 原型“10 结局签名”）
// 两个时刻打开：
//   ① 衡请求原班长确认交接 → 周建国一只手入画，在左页“交班人”栏签字（灰盒：一只手的方块从下方伸进来）。签完自动合上，玩家去拉总闸。
//   ② 衡启动、发现未登记信号以后 → 右页“接班人”签名板 + 下方衡的屏幕：日志 + 一张待命名的规则卡（ID 栏可输入）。
//      签名、命名两步顺序不限，都完成才进献词（EndingSystem.TryFinish）。
// 签名板：按住鼠标书写（画到贴图上）；“代签”按钮用林小满笔迹写出名字（原型同款，键盘 / 触屏可用）。
using System.Linq;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class EndingView : ModalView, IPinnedView
    {
        public override string Id => "ending";
        const int SW = 700, SH = 220;

        static readonly string[] LampNames = { "测点", "诊断", "联锁", "天车" };
        readonly Texture2D sigTex;
        readonly Color32[] sigPx = new Color32[SW * SH];
        bool drawn, down, padInit, usedAuto;
        Vector2 last;
        Label jianguo, autoSig, idEcho;
        VisualElement hand, sigPad, dual, caps;
        TextField idField;
        float signAt = -1;

        public EndingView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "ledger-book")
        {
            Panel.AddToClassList("ending-book");
            sigTex = new Texture2D(SW, SH, TextureFormat.RGBA32, false) { name = "LSB_签名板" };
            Panel.schedule.Execute(Animate).Every(33);
        }

        protected override bool CloseOnDimClick => false;

        /// <summary>签字、签名命名这两段 Esc 关不掉（结局要走完）。</summary>
        public bool Pinned => g.State.Phase == Phase.Ending && (g.State.Ending <= EndingStep.JianguoSign || g.State.Ending == EndingStep.NameAndSign);

        bool Naming => g.State.Ending >= EndingStep.NameAndSign;

        protected override void Build(string arg)
        {
            if (g.State.Ending == EndingStep.JianguoSign && signAt < 0) signAt = Now;
            Panel.Clear();
            var spread = Box("ledger-spread");
            spread.Add(LeftPage());
            spread.Add(Box("ledger-gutter"));
            spread.Add(RightPage());
            Panel.Add(spread);
            if (Naming) Panel.Add(SysStrip());
            caps = Box("end-caps");
            Panel.Add(caps);
        }

        VisualElement LeftPage()
        {
            var v = Box("ledger-page", "left");
            v.Add(L("交班　最后一页", "page-head"));
            for (int i = 0; i < 4; i++)
            {
                var r = Box("end-rule");
                r.Add(L($"{i + 1}　{LampNames[i]}", "f-hand-lin", "end-rule-n"));
                r.Add(L(RuleIds.All[i], "f-mono", "end-rule-id"));
                v.Add(r);
            }
            var sig = Box("end-sig");
            sig.Add(L("交班人", "end-sig-k"));
            jianguo = L("周建国", "f-hand-jian", "end-sig-jian");
            jianguo.style.opacity = g.State.Ending > EndingStep.JianguoSign ? 1 : 0;
            sig.Add(jianguo);
            v.Add(sig);
            v.Add(L("二十三点五十分", "f-hand-jian", "end-sig-t"));
            hand = Box("end-hand");
            hand.Add(L("灰盒：周建国的手", "placeholder-tag"));
            UiRoot.Show(hand, false);
            v.Add(hand);
            return v;
        }

        VisualElement RightPage()
        {
            var v = Box("ledger-page", "right");
            v.Add(L("接班", "page-head"));
            var sig = Box("end-sig", "lin");
            sig.Add(L("接班人", "end-sig-k"));
            sigPad = Box("sigpad");
            sigPad.style.width = SW; sigPad.style.height = SH;
            sigPad.style.backgroundImage = new StyleBackground(sigTex);
            autoSig = L("", "f-hand-lin", "sig-auto");
            sigPad.Add(autoSig);
            var ph = L(Naming ? "在这里签名（按住鼠标书写）" : "等原班长签字", "sig-ph");
            sigPad.Add(ph);
            sig.Add(sigPad);
            v.Add(sig);
            if (!padInit) { ClearPad(); padInit = true; }
            if (drawn || usedAuto) ph.text = "";
            if (usedAuto) autoSig.text = "林小满";
            if (Naming && !g.State.PlayerSigned)
            {
                sigPad.RegisterCallback<PointerDownEvent>(e => { down = true; drawn = true; ph.text = ""; last = e.localPosition; sigPad.CapturePointer(e.pointerId); Dot(last); sigTex.Apply(false); });
                sigPad.RegisterCallback<PointerMoveEvent>(e => { if (!down) return; Vector2 p = e.localPosition; Stroke(last, p); last = p; sigTex.Apply(false); });
                sigPad.RegisterCallback<PointerUpEvent>(e => { down = false; sigPad.ReleasePointer(e.pointerId); });
                var acts = Box("sig-actions");
                acts.Add(B("代签", () => { autoSig.text = "林小满"; ph.text = ""; drawn = true; usedAuto = true; }, "chip"));
                acts.Add(B("清除", () => { ClearPad(); autoSig.text = ""; drawn = false; usedAuto = false; ph.text = "在这里签名（按住鼠标书写）"; }, "chip"));
                acts.Add(B("确认签名", ConfirmSign, "btn"));
                v.Add(acts);
            }
            else if (g.State.PlayerSigned) { ph.text = ""; if (!drawn) autoSig.text = "林小满"; }   // 读档回来：手写的笔画不进存档，用笔迹字体补上
            dual = Box("end-dual");
            v.Add(dual);
            if (g.State.PlayerSigned) ShowDual();
            return v;
        }

        /// <summary>下方衡的屏幕：最近的日志 + 待命名的规则卡。</summary>
        VisualElement SysStrip()
        {
            var sys = Box("end-sys");
            var log = Box("end-log");
            foreach (var l in g.State.Log.Skip(System.Math.Max(0, g.State.Log.Count - 7)))
            {
                var line = L($"[{l.Time}] {l.Module} > {l.Text}", "mes-line");
                if (!string.IsNullOrEmpty(l.Kind)) line.AddToClassList(l.Kind);
                log.Add(line);
            }
            sys.Add(log);
            var card = Box("rc-screen", "end-card");
            var idRow = Box("rc-field", "sys");
            idRow.Add(L("RULE ID", "f-mono", "rc-key-sys"));
            if (string.IsNullOrEmpty(g.State.PlayerRuleName))
            {
                idField = new TextField { maxLength = 20 };
                idField.AddToClassList("end-id");
                idField.RegisterValueChangedCallback(e =>
                {
                    var n = EndingSystem.NormalizeRuleName(e.newValue);
                    if (n != e.newValue) idField.SetValueWithoutNotify(n);
                });
                idField.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ConfirmName(); e.StopPropagation(); });
                idRow.Add(idField);
                idRow.Add(B("确认命名", ConfirmName, "sys-btn", "small"));
            }
            else idRow.Add(L(g.State.PlayerRuleName, "f-mono", "rc-id-sys"));
            card.Add(idRow);
            card.Add(SysField("SOURCE", "林小满，2026 年交接班记录"));
            card.Add(SysField("MEASUREMENT POINT", "LT01 主轴尾端"));
            card.Add(SysField("FEATURE", "（待描述）未登记微弱信号"));
            card.Add(SysField("STATUS", string.IsNullOrEmpty(g.State.PlayerRuleName) ? "UNNAMED" : "PENDING　待师傅评定"));
            idEcho = L("只能用大写字母、数字和下划线，最多 20 个。", "sys-dim");
            card.Add(idEcho);
            sys.Add(card);
            return sys;
        }

        static VisualElement SysField(string k, string v)
        {
            var r = Box("rc-field", "sys");
            r.Add(L(k, "f-mono", "rc-key-sys"));
            r.Add(L(v, "rc-sys-val"));
            return r;
        }

        void ConfirmSign()
        {
            if (!drawn) { g.Caption("先在接班人栏签名。"); return; }
            if (g.Ending.Sign()) { ShowDual(); Panel.schedule.Execute(Rebuild).StartingIn(50); }
        }

        void ConfirmName()
        {
            if (idField == null) return;
            if (g.Ending.NameRule(idField.value)) Panel.schedule.Execute(Rebuild).StartingIn(50);
            else idEcho.text = string.IsNullOrEmpty(EndingSystem.NormalizeRuleName(idField.value)) ? "先写一个名字。" : "这个名字已经有了，换一个。";
        }

        protected override void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                // 周建国两句、衡的提醒：遮罩压着 HUD 字幕，这里在本子下面再显示一遍
                case Ev.Line:
                    if (g.Content.Lines.TryGetValue(e.A, out var l)) Cap(g.Dialogue.Who(l) + "　" + l.Text);
                    break;
                case Ev.Caption: Cap(e.A); break;
                case Ev.EndingStepChanged:
                    if ((EndingStep)e.N == EndingStep.JianguoSign) signAt = Now;
                    break;
            }
        }

        void Cap(string text)
        {
            if (caps == null) return;
            caps.Add(L(text, "mem-cap"));
            while (caps.childCount > 2) caps.RemoveAt(0);
        }

        void Rebuild() { if (IsOpen && g.State.Ending == EndingStep.NameAndSign) Build(null); }

        /// <summary>签名同时以手写体和等宽字体出现（主项目文档 §5.3 14:40）。</summary>
        void ShowDual()
        {
            dual.Clear();
            dual.Add(L("林小满", "f-hand-lin", "end-dual-h"));
            dual.Add(L("LIN_XIAOMAN · 23:58", "f-mono", "end-dual-m"));
        }

        /// <summary>周建国签字：手从下方伸进来，名字一点点显出来，手再收回去（灰盒）。</summary>
        void Animate()
        {
            if (!IsOpen || jianguo == null) return;
            if (g.State.Ending == EndingStep.JianguoSign && signAt > 0)
            {
                float t = Now - signAt;
                UiRoot.Show(hand, t > 0.2f && t < 3.8f);
                hand.style.translate = new Translate(0, Mathf.Lerp(260, 0, Mathf.Clamp01((t - 0.2f) / 0.6f)) + Mathf.Lerp(0, 260, Mathf.Clamp01((t - 3.2f) / 0.6f)), 0);
                jianguo.style.opacity = Mathf.Clamp01((t - 0.9f) / 1.6f);
            }
            else if (g.State.Ending > EndingStep.JianguoSign) { jianguo.style.opacity = 1; UiRoot.Show(hand, false); }
        }

        // ---------------- 签名板 ----------------
        void ClearPad()
        {
            for (int i = 0; i < sigPx.Length; i++) sigPx[i] = new Color32(0, 0, 0, 0);
            sigTex.SetPixels32(sigPx);
            sigTex.Apply(false);
        }

        void Stroke(Vector2 a, Vector2 b)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f) + 1;
            for (int k = 0; k <= n; k++) Dot(Vector2.Lerp(a, b, k / (float)n));
        }

        void Dot(Vector2 p)
        {
            var ink = new Color32(31, 58, 95, 255);    // C-Ink-Blue
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    if (dx * dx + dy * dy > 9) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x >= SW || y < 0 || y >= SH) continue;
                    sigTex.SetPixel(x, SH - 1 - y, ink);
                }
        }
    }
}

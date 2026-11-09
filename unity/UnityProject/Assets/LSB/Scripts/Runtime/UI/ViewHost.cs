// 《最后一次交班》Demo α —— 面板管理：标题（打卡）、开场、以及所有 2D 面板
// 正式面板：2B 接入交班本、细看（阅读器）、听音对比（谜题一）、三联板、经验规则卡 + 等级通知。
// 2C 接入时频图、衡的屏幕；2D 接入推断单、离线孪生（记忆显影在覆盖层，见 MemoryOverlay）；2E 接入天车、磁带、总闸、交班本最后一页（结局过场在覆盖层，见 EndingOverlay）。
// 占位面板只剩兜底作用：没注册的面板 ID 会显示【占位】，不假装已经做完。
using System;
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public interface IGameView
    {
        string Id { get; }
        bool IsOpen { get; }
        void Open(string arg);
        void Close();
    }

    /// <summary>打开时每帧要更新的面板（动画、键盘翻页）。</summary>
    public interface ITickView { void Tick(); }

    /// <summary>暂时不许用 Esc 关掉的面板（结局签字、签名命名）。</summary>
    public interface IPinnedView { bool Pinned { get; } }

    public sealed class ViewHost
    {
        readonly GameSession g;
        readonly UiRoot ui;
        readonly Dictionary<string, IGameView> views = new Dictionary<string, IGameView>();
        readonly PlaceholderView placeholder;
        readonly Stack<(string id, string arg)> back = new Stack<(string, string)>();
        string currentId, currentArg;
        VisualElement title, intro;
        bool titleShown = true, introShown;
        float introUntil;

        public event Action<string> Opened, Closed;

        public ViewHost(GameSession g, UiRoot ui)
        {
            this.g = g; this.ui = ui;
            placeholder = new PlaceholderView(g, ui, this);
            g.Bus.Raised += e => { if (e.Type == Ev.OpenView) Open(e.A, e.B); };
            BuildTitle();
            BuildIntro();
        }

        public void Register(IGameView v) => views[v.Id] = v;
        public T Get<T>(string id) where T : class, IGameView => views.TryGetValue(id, out var v) ? v as T : null;

        public bool AnyOpen => currentId != null || IsTitleShowing || IsIntroShowing;
        public bool IsOpen(string id) => currentId == id;
        public string Current => currentId;
        public bool IsTitleShowing => titleShown;
        public bool IsIntroShowing => introShown;

        /// <summary>打开一个面板（关掉其它面板，清空“返回”栈）。</summary>
        public void Open(string id, string arg = null)
        {
            back.Clear();
            Show(id, arg);
        }

        /// <summary>从当前面板再打开一个（如交班本 → 工作照、交班本 → 三联板）：合上新面板时回到原来那个。</summary>
        public void Push(string id, string arg = null)
        {
            if (currentId != null) back.Push((currentId, currentArg));
            Show(id, arg);
        }

        void Show(string id, string arg)
        {
            CloseCurrent();
            currentId = id; currentArg = arg;
            if (views.TryGetValue(id, out var v)) v.Open(arg); else placeholder.Open(id, arg);
            Opened?.Invoke(id);
        }

        void CloseCurrent()
        {
            if (currentId == null) return;
            var id = currentId;
            if (views.TryGetValue(id, out var v)) { if (v.IsOpen) v.Close(); }
            else if (placeholder.IsOpen) placeholder.Close();
            currentId = null; currentArg = null;
            g.Bus.Raise(Ev.CloseView, id);
            Closed?.Invoke(id);
        }

        public void CloseAll()
        {
            back.Clear();
            CloseCurrent();
        }

        /// <summary>Esc / 合上：关当前面板，有“返回”就回到上一个；没有面板时返回 false，交给相机退出特写。</summary>
        public bool CloseTop()
        {
            if (currentId == null) return false;
            if (views.TryGetValue(currentId, out var cv) && cv is IPinnedView pv && pv.Pinned && cv.IsOpen) return true;
            CloseCurrent();
            if (back.Count > 0) { var (id, arg) = back.Pop(); Show(id, arg); }
            return true;
        }

        /// <summary>面板自己合上（按钮）时调用，等同 Esc。</summary>
        public void Closing(IGameView v) { if (currentId == v.Id) CloseTop(); }

        // ---------------- 标题（打卡） ----------------
        void BuildTitle()
        {
            title = UiRoot.Box("title");
            title.Add(UiRoot.L("最后一次交班", "name"));
            title.Add(UiRoot.L("Demo α　垂直切片　灰盒版", "sub"));
            // 试玩编号（2F 可用性测试用，可不填）：写进试玩记录文件名，和观察记录表对上
            var tester = new TextField("试玩编号（可不填）") { maxLength = 8, value = PlaytestLog.TesterId };
            tester.AddToClassList("title-tester");
            tester.RegisterValueChangedCallback(e => { var n = PlaytestLog.NormalizeTesterId(e.newValue); if (n != e.newValue) tester.SetValueWithoutNotify(n); });
            title.Add(UiRoot.B("打卡上班（新游戏）", () => { PlaytestLog.TesterId = PlaytestLog.NormalizeTesterId(tester.value); HideTitle(); g.Flow.NewGame(); ShowIntro(); }, "btn"));
            var cont = UiRoot.B("继续（读档）", () => { if (SaveStore.TryLoad(g)) HideTitle(); }, "btn");
            cont.SetEnabled(SaveStore.HasSave);
            title.Add(cont);
            title.Add(tester);
            title.Add(UiRoot.L("鼠标点击交互　1–4 工具　L 交班本　按住空格 高亮可交互物　Esc 返回　F1 开发菜单（仅开发构建）", "sub"));
            ui.OverlayLayer.Add(title);
        }

        public void ShowTitle() { titleShown = true; introShown = false; UiRoot.Show(title, true); UiRoot.Show(intro, false); }
        public void HideTitle() { titleShown = false; UiRoot.Show(title, false); }

        // ---------------- 开场黑屏手写字（主项目文档 §5.3 0:00–0:40） ----------------
        void BuildIntro()
        {
            intro = UiRoot.Box("title");
            intro.Add(UiRoot.L(ContentDB.IntroLine, "intro-line"));
            intro.Add(UiRoot.L("点击继续", "sub"));
            intro.RegisterCallback<PointerDownEvent>(_ => FinishIntro());
            UiRoot.Show(intro, false);
            ui.OverlayLayer.Add(intro);
        }

        void ShowIntro() { introShown = true; UiRoot.Show(intro, true); introUntil = Time.unscaledTime + 6f; }

        void FinishIntro()
        {
            if (!IsIntroShowing) return;
            introShown = false;
            UiRoot.Show(intro, false);
            introUntil = 0;
            g.Flow.FinishIntro();
        }

        public void Tick()
        {
            if (introUntil > 0 && Time.unscaledTime > introUntil) FinishIntro();
            if (currentId != null && views.TryGetValue(currentId, out var v) && v is ITickView t && v.IsOpen) t.Tick();
            // 面板被自己内部逻辑关掉（如规则卡收起后进入下一项）时同步状态
            if (currentId != null && views.TryGetValue(currentId, out var w) && !w.IsOpen) CloseTop();
            if (currentId != null && !views.ContainsKey(currentId) && !placeholder.IsOpen) CloseTop();
        }
    }

    /// <summary>
    /// 【占位】面板：把这个面板要承载的内容按纯文字摆出来，保证 2A 灰盒里流程能看、能点、能推进。
    /// 交班本占位版可以“读这页”（拿到贴页证据），其余面板只展示内容。正式界面替换后本类只剩兜底作用。
    /// </summary>
    public sealed class PlaceholderView : IGameView
    {
        readonly GameSession g;
        readonly UiRoot ui;
        readonly ViewHost host;
        readonly VisualElement dim, panel;
        public string Id => "placeholder";
        public string Current { get; private set; }
        public bool IsOpen => Current != null;

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "ledger", "交班本" }, { "reader", "细看" }, { "diag", "听音诊断（衡的平板）" }, { "mes", "衡的屏幕" },
            { "tape", "录音机" }, { "crane", "天车防摇曲线（衡的平板）" }, { "switch", "总闸" }, { "ending", "交班本最后一页" },
        };
        static readonly Dictionary<string, string> Stage = new Dictionary<string, string>
        {
            { "ledger", "2B" }, { "reader", "2B" }, { "diag", "2B（信号条）/ 2C（时频图、齿轮指示）" }, { "mes", "2C" },
            { "tape", "2E" }, { "crane", "2E" }, { "switch", "2E" }, { "ending", "2E" },
        };

        public PlaceholderView(GameSession g, UiRoot ui, ViewHost host)
        {
            this.g = g; this.ui = ui; this.host = host;
            dim = UiRoot.Box("modal-dim");
            panel = UiRoot.Box("paper");
            panel.style.width = 1100; panel.style.maxHeight = 900;
            dim.Add(panel);
            UiRoot.Show(dim, false);
            ui.ViewLayer.Add(dim);
        }

        public void Open(string arg) => Open("?", arg);

        public void Open(string id, string arg)
        {
            Current = id;
            panel.Clear();
            panel.Add(UiRoot.L($"【占位界面】正式界面在阶段 {(Stage.TryGetValue(id, out var st) ? st : "2B–2E")} 接入", "placeholder-tag"));
            panel.Add(UiRoot.L(Names.TryGetValue(id, out var nm) ? nm : id, "panel-title"));
            var body = new ScrollView();
            body.style.maxHeight = 640;
            panel.Add(body);
            switch (id)
            {
                case "ledger": Ledger(body); break;
                case "reader":
                    if (g.Content.ReaderText.TryGetValue(arg ?? "", out var lines)) foreach (var l in lines) body.Add(UiRoot.L(l, "panel-body"));
                    break;
                case "diag":
                    body.Add(UiRoot.L(arg == "L1" ? "左边（L1）：听针贴在老周的点上。" : "右边（R1）：衡装的临时传感器。", "panel-body"));
                    body.Add(UiRoot.L($"轴承成分 {SpectroModel.BearingShare(arg):0.00}　齿轮成分 {SpectroModel.GearShare(arg):0.00}（{SpectroModel.DataLabel}）", "panel-note"));
                    break;
                case "mes":
                    foreach (var l in g.State.Log.Skip(System.Math.Max(0, g.State.Log.Count - 12))) body.Add(UiRoot.L($"[{l.Time}] {l.Module} > {l.Text}", "panel-note"));
                    break;
                default:
                    body.Add(UiRoot.L("这个面板的逻辑已在逻辑层实现并通过自动化测试；界面尚未接入。开发菜单（F1）可以直接操作。", "panel-note"));
                    break;
            }
            panel.Add(UiRoot.B("合上（Esc）", () => host.CloseTop(), "btn"));
            UiRoot.Show(dim, true);
        }

        void Ledger(VisualElement body)
        {
            foreach (var sp in g.Ledger.Spreads)
            {
                if (!g.Ledger.IsOpen(sp)) { body.Add(UiRoot.L($"〔{sp.Tab}〕到这一项再翻。", "panel-note")); continue; }
                body.Add(UiRoot.L($"〔{sp.Tab}〕", "panel-body"));
                foreach (var pid in new[] { sp.Left, sp.Right }.Distinct())
                {
                    var page = g.Content.Page(pid);
                    if (g.Content.PageText.TryGetValue(pid, out var text)) body.Add(UiRoot.L(string.Join("　", text), "panel-note"));
                    else body.Add(UiRoot.L(page.Title, "panel-note"));
                    if (g.Ledger.HasNew(pid))
                    {
                        var id = pid;
                        body.Add(UiRoot.B("读这页：" + page.Title, () => { g.Ledger.ReadPage(id); Open("ledger", null); }, "btn"));
                    }
                }
            }
            var row = g.Content.Row(g.State.Phase);
            if (row != null) body.Add(UiRoot.L($"本班：{row.Item}　异常：{row.Anomaly}", "panel-body"));
            if (g.Triad.CanOpen(out var why)) body.Add(UiRoot.L("三联板可以填了（2B 接入正式三联板；现在可用开发菜单完成）。", "panel-note"));
            else if (g.Triad.Current != null) body.Add(UiRoot.L("三联板：" + why, "panel-note"));
        }

        public void Close()
        {
            Current = null;
            UiRoot.Show(dim, false);
        }
    }
}

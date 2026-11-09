// 《最后一次交班》Demo α —— 启动入口
// 场景里只需要一个挂了 GameBootstrap 的空物体（编辑器菜单生成的 Scenes/DemoAlpha.unity 已经放好）。
// 它负责：建逻辑会话 → 生成（或找到）灰盒 → 相机 → UI → 音频 → 交互。开发菜单由 LSB.DevTools 自己挂上来，正式流程不依赖它。
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        public GameSession Session { get; private set; }
        public WorkshopRefs World { get; private set; }
        public NodeCameraRig Rig { get; private set; }
        public UiRoot Ui { get; private set; }
        public HudView Hud { get; private set; }
        public ViewHost Views { get; private set; }
        public WorldPresenter Presenter { get; private set; }
        public ProceduralAudio Audio { get; private set; }
        public InteractionController Interaction { get; private set; }
        public MemoryOverlay Memory { get; private set; }
        public EndingOverlay Ending { get; private set; }

        [Tooltip("自动存档间隔（秒）。0 = 只在每项完成时存。")]
        public float AutosaveSeconds = 60f;
        float nextSave;

        /// <summary>开发菜单等外部工具在这里挂钩（避免 Runtime 程序集引用 DevTools）。</summary>
        public static event System.Action<GameBootstrap> Booted;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Session = new GameSession();

            // 灰盒：场景里已有（编辑器生成）就用，没有就当场生成
            var existing = GameObject.Find(WorkshopGraybox.RootName);
            World = existing != null ? existing.GetComponent<WorkshopRefs>() : null;
            if (World != null) World.Reindex();
            if (World == null || World.Interactables.Count != Session.Content.Interactables.Count) World = WorkshopGraybox.Build(Session.Content);

            // 相机
            var cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
            Rig = cam.GetComponent<NodeCameraRig>();
            if (Rig == null) Rig = cam.gameObject.AddComponent<NodeCameraRig>();

            Ui = UiRoot.Create(transform);
            Hud = new HudView(Session, Ui);
            Views = new ViewHost(Session, Ui);

            Audio = gameObject.AddComponent<ProceduralAudio>();
            Audio.Bind(Session);

            Presenter = gameObject.AddComponent<WorldPresenter>();
            Presenter.Bind(Session, Rig, World, Audio);

            Interaction = gameObject.AddComponent<InteractionController>();
            Interaction.Session = Session; Interaction.Rig = Rig; Interaction.World = World; Interaction.Ui = Ui; Interaction.Hud = Hud; Interaction.Views = Views;
            var torch = gameObject.AddComponent<TorchLight>();
            torch.Session = Session; torch.Rig = Rig;

            // 正式面板（2B：交班本、细看、平板、三联板、规则卡；2C：衡的屏幕、时频图；2D：推断单、离线孪生）。其余在 2E 接入前仍走占位面板
            Views.Register(new LedgerView(Session, Ui, Views));
            Views.Register(new ReaderView(Session, Ui, Views));
            Views.Register(new DiagView(Session, Ui, Views));
            Views.Register(new TriadView(Session, Ui, Views));
            Views.Register(new RuleCardView(Session, Ui, Views));
            Views.Register(new MesView(Session, Ui, Views));          // 2C
            Views.Register(new DeductionView(Session, Ui, Views));    // 2D
            Views.Register(new TwinView(Session, Ui, Views));         // 2D
            Views.Register(new CraneView(Session, Ui, Views));        // 2E
            Views.Register(new TapeView(Session, Ui, Views));         // 2E
            Views.Register(new SwitchView(Session, Ui, Views));       // 2E
            Views.Register(new EndingView(Session, Ui, Views));       // 2E
            Memory = new MemoryOverlay(Session, Ui, Audio);           // 2D：记忆显影（覆盖层，在面板之上）
            Ending = new EndingOverlay(Session, Ui, Views, Hud);      // 2E：结局过场（黑边、衡的日志、献词）

            Hud.OnNavigate = id => Session.Nav.MoveTo(id);
            Hud.OnCloseUp = id => Session.Nav.EnterCloseUp(id);
            Hud.OnBack = () => { if (!Views.CloseTop()) Session.Nav.ExitCloseUp(); };
            Hud.OnLedger = () => Views.Open("ledger");

            Session.Bus.Raised += e =>
            {
                if (e.Type == Ev.RuleStatusChanged && e.N == (int)RuleStatus.Confirmed) SaveStore.Save(Session);
                if (e.Type == Ev.PhaseChanged && e.A == Phase.Done.ToString()) SaveStore.ExportPlaytest(Session);
                if (e.Type == Ev.PhaseChanged && e.B == "load") ResumeAfterLoad();
            };

            Views.ShowTitle();
            Hud.Refresh();
            Booted?.Invoke(this);
        }

        /// <summary>
        /// 读档时如果正好停在规则卡动画中间（三联已一致、规则还没确认，或已确认但还没收起等级通知），
        /// 直接把规则卡翻出来接着走，不让流程卡住。
        /// </summary>
        void ResumeAfterLoad()
        {
            var s = Session.State;
            if (!PhaseUtil.IsPuzzle(s.Phase)) return;
            var id = RuleIds.ForPuzzle(PhaseUtil.PuzzleIndex(s.Phase));
            var st = s.Rule(id);
            if (s.PuzzleSolved || st == RuleStatus.Pending || (st == RuleStatus.Draft && id != RuleIds.JianguoGuard))
                Ui.Root.schedule.Execute(() => Views.Open("rulecard", id));
        }

        void Update()
        {
            if (Session == null) return;
            Session.Tick(Time.unscaledDeltaTime);   // 翻交班本时提示计时照走：玩家在本子里卡住也算卡住
            Hud.Tick();
            Views.Tick();
            Memory.Tick();
            Ending.Tick();
            if (AutosaveSeconds > 0 && Session.State.Phase > Phase.Intro && Session.State.Phase < Phase.Done && Time.unscaledTime > nextSave)
            {
                nextSave = Time.unscaledTime + AutosaveSeconds;
                SaveStore.Save(Session);
            }
        }

        void OnApplicationQuit()
        {
            if (Session == null) return;
            if (Session.State.Phase > Phase.Intro && Session.State.Phase < Phase.Done) SaveStore.Save(Session);
            if (Session.Playtest.Entries.Count > 1) SaveStore.ExportPlaytest(Session);
        }
    }
}

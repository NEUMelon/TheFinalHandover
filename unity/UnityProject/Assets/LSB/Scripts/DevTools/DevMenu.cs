// 《最后一次交班》Demo α —— 开发调试菜单（F1 或 ~ 键）
// 与玩家界面分开：独立程序集 LSB.DevTools、IMGUI 绘制，不进 UI Toolkit 的玩家界面层。
// 只在编辑器和 Development Build 里出现；正式试玩（2F）用非开发构建，或启动参数 -lsbNoDev 关掉。
// 跳关 / 解题都走 AutoPlayer（只调用玩家能做的操作），所以跳过去的状态和真人玩到那里一样。
using System.Linq;
using LSB.Core;
using LSB.Unity;
using UnityEngine;

namespace LSB.Dev
{
    public sealed class DevMenu : MonoBehaviour
    {
        GameBootstrap boot;
        bool open;
        Vector2 scroll;
        Rect win = new Rect(20, 20, 470, 760);
        int tab;
        float t2 = 1f;
        string ruleName = "LIN_TAIL_HUM";
        string lastMsg = "";
        static readonly string[] Tabs = { "状态", "跳关", "谜题", "位置", "设置" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            GameBootstrap.Booted += b =>
            {
                if (!(Application.isEditor || Debug.isDebugBuild)) return;
                if (System.Environment.GetCommandLineArgs().Contains("-lsbNoDev")) return;
                b.gameObject.AddComponent<DevMenu>().boot = b;
            };
        }

        GameSession G => boot.Session;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote)) open = !open;
        }

        void OnGUI()
        {
            if (boot == null) return;
            GUI.color = new Color(1, 1, 1, 0.9f);
            GUI.Label(new Rect(Screen.width - 230, Screen.height - 22, 230, 20), "DEV · F1 开发菜单（非玩家界面）");
            if (!open) return;
            win = GUILayout.Window(19560, win, Draw, "开发菜单 · Demo α（非玩家界面）");
        }

        void Draw(int id)
        {
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawState(); break;
                case 1: DrawJump(); break;
                case 2: DrawPuzzle(); break;
                case 3: DrawPlaces(); break;
                case 4: DrawSettings(); break;
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(lastMsg)) GUILayout.Label(lastMsg);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void DrawState()
        {
            var s = G.State;
            GUILayout.Label($"阶段 {s.Phase}　卡点 {G.Hints.Stage}　提示 L{G.Hints.Level}（{G.Hints.Timer:0}s）");
            GUILayout.Label($"节点 {s.Node}　特写 {s.CloseUp ?? "-"}　工具 {s.Tool}　时钟 {s.Clock}");
            GUILayout.Label($"等级 {G.Rules.LevelName}　交接 {s.LampsOff}/4　游玩 {s.PlayTime:0}s　{(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)):0} FPS");
            GUILayout.Label("规则：" + string.Join("  ", RuleIds.All.Select(r => r + ":" + s.Rule(r))));
            GUILayout.Label("证据：" + string.Join(" ", s.Evidence.OrderBy(x => x)));
            GUILayout.Label("旗标：" + string.Join(" ", s.Flags.OrderBy(x => x)));
            if (s.Phase == Phase.Ending) GUILayout.Label("结局步骤：" + s.Ending);
            GUILayout.Label($"试玩记录 {G.Playtest.Entries.Count} 条（自动采集的客观行为，不是问卷）");
            if (GUILayout.Button("导出试玩记录 CSV")) lastMsg = "已导出：" + SaveStore.ExportPlaytest(G);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("存档")) { SaveStore.Save(G); lastMsg = "已存档"; }
            if (GUILayout.Button("读档")) { lastMsg = SaveStore.TryLoad(G) ? "已读档" : "没有存档"; boot.Views.HideTitle(); boot.Presenter.SyncAll(true); boot.Hud.Refresh(); }
            GUILayout.EndHorizontal();
        }

        void DrawJump()
        {
            GUILayout.Label("从新游戏自动玩到某一项开头（走真实操作，不改状态）");
            foreach (var p in new[] { Phase.Intro, Phase.P1, Phase.P2, Phase.P3, Phase.P4, Phase.Ending })
                if (GUILayout.Button("跳到 " + PhaseName(p))) Run(() => { AutoPlayer.JumpTo(G, p); });
            GUILayout.Space(8);
            if (GUILayout.Button("解掉当前这一项（并进入下一项）")) Run(() => new AutoPlayer(G).SolveCurrent());
            if (G.State.PuzzleSolved && GUILayout.Button("规则已确认 → 进入下一项")) Run(() => G.Flow.CompletePuzzle());
        }

        void DrawPuzzle()
        {
            var s = G.State;
            GUILayout.Label("当前阶段可拿的证据：");
            foreach (var e in G.Evidence.ForPhase(s.Phase))
                if (!s.Has(e.Id) && GUILayout.Button($"给 {e.Id}　{e.Name}")) Run(() => G.Evidence.Give(e.Id));
            GUILayout.Space(6);
            switch (s.Phase)
            {
                case Phase.P1:
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("听左 L1")) G.Puzzles.Listen("L1");
                    if (GUILayout.Button("听右 R1")) G.Puzzles.Listen("R1");
                    if (GUILayout.Button("选左边清楚")) Msg(G.Puzzles.CompareChoose("L1").Message);
                    GUILayout.EndHorizontal();
                    break;
                case Phase.P2:
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("听左 L1")) G.Puzzles.Listen("L1");
                    if (GUILayout.Button("框对")) Msg(G.Puzzles.JudgeBox(new SpecBox(1, 3, 2200, 3800)).Message);
                    if (GUILayout.Button("框错")) Msg(G.Puzzles.JudgeBox(new SpecBox(1, 3, 400, 3800)).Message);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("听右 R1")) G.Puzzles.Listen("R1");
                    if (GUILayout.Button("齿轮：正常")) Msg(G.Puzzles.Sideband("ok").Message);
                    if (GUILayout.Button("齿轮：异常")) Msg(G.Puzzles.Sideband("bad").Message);
                    GUILayout.EndHorizontal();
                    break;
                case Phase.P3:
                    if (GUILayout.Button("推断单：按正确答案填")) foreach (var b in G.Content.Deduction.Blanks) G.Puzzles.Fill(b.Key, b.Answer);
                    if (GUILayout.Button("推断单：检查")) Msg(G.Puzzles.CheckDeduction().Message);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("孪生：补回启动条件")) Msg(G.Puzzles.TwinRestore().Message);
                    foreach (var c in GuardTwinModel.Cases) if (GUILayout.Button(c.Name)) Msg(G.Puzzles.TwinTest(c.Id)?.Log);
                    GUILayout.EndHorizontal();
                    break;
                case Phase.P4:
                    GUILayout.Label($"第二段收车时刻 {t2:0.00} 拍");
                    t2 = GUILayout.HorizontalSlider(t2, CraneZvModel.MinBeats, CraneZvModel.MaxBeats);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("设定并试吊")) { G.Puzzles.SetT2(t2); var tr = G.Puzzles.Trial(); Msg(tr == null ? "" : tr.Outcome + "　" + tr.ToLog()); }
                    if (GUILayout.Button("放磁带（直接放完）")) { G.Puzzles.PlayTape(); G.Puzzles.TapeFinished(); }
                    GUILayout.EndHorizontal();
                    break;
                case Phase.Ending:
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("推进一步")) G.Ending.Advance();
                    if (GUILayout.Button("拉总闸")) G.Ending.PullSwitch();
                    GUILayout.EndHorizontal();
                    ruleName = GUILayout.TextField(ruleName, 20);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("命名")) G.Ending.NameRule(ruleName);
                    if (GUILayout.Button("签名")) G.Ending.Sign();
                    GUILayout.EndHorizontal();
                    break;
            }
            var t = G.Triad.Current;
            if (t != null)
            {
                GUILayout.Space(6);
                GUILayout.Label($"三联板 {t.RuleId}：{(G.Triad.CanOpen(out var why) ? "可填" : why)}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("按正确答案填并确认")) Run(() => new AutoPlayer(G).SolveTriad());
                if (GUILayout.Button("待验证")) G.Rules.SetPending(t.RuleId);
                if (GUILayout.Button("确认规则")) G.Rules.Confirm(t.RuleId);
                GUILayout.EndHorizontal();
            }
        }

        void DrawPlaces()
        {
            GUILayout.Label("瞬移到节点（不走相邻关系）：");
            foreach (var n in G.Content.Nodes) if (GUILayout.Button($"{n.Id}　{n.Name}")) G.Nav.MoveTo(n.Id, true);
            GUILayout.Label("进入特写：");
            foreach (var c in G.Content.CloseUps) if (GUILayout.Button($"{c.Id}　{c.Name}")) { G.Nav.MoveTo(c.EntryNodes[0], true); G.Nav.EnterCloseUp(c.Id, true); }
            if (GUILayout.Button("拉总闸机位")) { var p = G.Content.SwitchPose; boot.Rig.MoveToPose(WorkshopRefs.V(p.Pos), p.Yaw, p.Pitch, p.FocalMm, 0.8f); }
        }

        void DrawSettings()
        {
            var st = G.State.Settings;
            st.HintsOn = GUILayout.Toggle(st.HintsOn, "页边提示自动出现（关 = 只有“需要提示”按钮）");
            GUILayout.Label($"提示计时倍速 {st.HintSpeed:0}×（试玩保持 1×）");
            st.HintSpeed = Mathf.Round(GUILayout.HorizontalSlider(st.HintSpeed, 1, 20));
            st.Captions = GUILayout.Toggle(st.Captions, "字幕");
            st.ShowPro = GUILayout.Toggle(st.ShowPro, "三联板显示专业说法（第二层）");
            GUILayout.Label($"音量 {st.Volume:0.00}"); st.Volume = GUILayout.HorizontalSlider(st.Volume, 0, 1);
            if (boot.World.LabelsRoot != null)
            {
                bool on = GUILayout.Toggle(boot.World.LabelsRoot.activeSelf, "显示“灰盒 / 占位”标签");
                if (on != boot.World.LabelsRoot.activeSelf) boot.World.LabelsRoot.SetActive(on);
            }
            if (GUILayout.Button("高亮全部交互物 3 秒")) StartCoroutine(FlashAll());
            if (GUILayout.Button("立即给一级提示")) G.Hints.RequestManual();
        }

        System.Collections.IEnumerator FlashAll()
        {
            foreach (var t in boot.World.Interactables.Values) t.SetHighlight(true);
            yield return new WaitForSecondsRealtime(3);
            foreach (var t in boot.World.Interactables.Values) t.SetHighlight(false);
        }

        void Run(System.Action a)
        {
            try { a(); lastMsg = "完成"; }
            catch (System.Exception e) { lastMsg = e.Message.Split('\n')[0]; Debug.LogWarning("[LSB Dev] " + e); }
            boot.Views.HideTitle();
            boot.Views.CloseAll();
            boot.Presenter.SyncAll(false);
            boot.Hud.Refresh();
        }

        void Msg(string m) { lastMsg = m ?? ""; boot.Hud.Refresh(); }

        static string PhaseName(Phase p) => p switch
        {
            Phase.Intro => "开场", Phase.P1 => "谜题一 左手", Phase.P2 => "谜题二 咔哒在左", Phase.P3 => "谜题三 一九八七年的凹痕",
            Phase.P4 => "谜题四 提前半拍", Phase.Ending => "结局", _ => p.ToString(),
        };
    }
}

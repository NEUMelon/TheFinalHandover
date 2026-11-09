// 《最后一次交班》Demo α —— Game Flow（主流程）与 Node Navigation（节点导航）
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    /// <summary>
    /// 主流程：打卡 → 开场 → 谜题一 → 二 → 三 → 四 → 结局。顺序冻结（主项目文档 §5.3），只能往前走。
    /// 开发菜单的跳关走 DevSetup，不走这里的校验。
    /// </summary>
    public sealed class FlowSystem
    {
        readonly GameSession g;
        public FlowSystem(GameSession g) { this.g = g; }

        public void NewGame()
        {
            var s = new GameState();
            g.ReplaceState(s);
            g.Playtest.BeginSession();
            s.Phase = Phase.Intro;
            s.Node = "N1";
            g.Timeline.SetAnchor(Phase.Intro);
            g.Bus.Raise(Ev.PhaseChanged, Phase.Intro.ToString(), "new", (int)Phase.Intro);
            g.Playtest.Record("phase_start", Phase.Intro.ToString());
        }

        /// <summary>开场黑屏结束（或玩家点击跳过）。</summary>
        public void FinishIntro()
        {
            if (g.State.Phase != Phase.Intro) return;
            g.Dialogue.Say("J1");
            BeginPhase(Phase.P1);
        }

        public void BeginPhase(Phase p)
        {
            var s = g.State;
            if (s.Phase != p) g.Playtest.Record("phase_end", s.Phase.ToString());
            s.Phase = p;
            s.PuzzleSolved = false;
            s.CloseUp = null;
            g.Timeline.SetAnchor(p);
            var anchor = g.Content.Anchor(p);
            if (p != Phase.P1 && anchor != null && !string.IsNullOrEmpty(anchor.Card)) g.Bus.Raise(Ev.TransitionCard, anchor.Card, anchor.Light);
            g.Bus.Raise(Ev.PhaseChanged, p.ToString(), null, (int)p);
            g.Playtest.Record("phase_start", p.ToString());
            if (g.Content.PhaseStartLogs.TryGetValue(p, out var logs))
                foreach (var l in logs) g.Log(l.Module, l.Text, l.Kind);
            g.Puzzles.OnPhaseStart(p);
            if (p == Phase.Ending) g.Ending.Begin();
            g.Hints.ResetStage();
        }

        /// <summary>规则确认后由 KnowledgeRuleSystem 调用。</summary>
        public void OnRuleConfirmed(string ruleId)
        {
            int i = PhaseUtil.PuzzleIndex(g.State.Phase);
            if (i >= 0 && RuleIds.ForPuzzle(i) == ruleId) g.State.PuzzleSolved = true;
        }

        /// <summary>
        /// 等级提升弹窗收起后由表现层调用：播完这一题的收尾台词，进入下一项。
        /// 谜题二收尾“嗯。”（事实链 §3）。谜题三的“那年我二十一。”在推断单填对时说（PuzzleSystem.CheckDeduction）。
        /// </summary>
        public void CompletePuzzle()
        {
            var s = g.State;
            if (!s.PuzzleSolved) return;
            switch (s.Phase)
            {
                case Phase.P2: g.Dialogue.Say("J3"); break;
            }
            BeginPhase(s.Phase == Phase.P4 ? Phase.Ending : s.Phase + 1);
        }

        /// <summary>献词结束：本局完成。</summary>
        public void BeginDone()
        {
            var s = g.State;
            if (s.Phase != Phase.Ending) return;
            g.Playtest.Record("phase_end", s.Phase.ToString());
            s.Phase = Phase.Done;
            g.Bus.Raise(Ev.PhaseChanged, Phase.Done.ToString(), null, (int)Phase.Done);
            g.Playtest.Record("game_finished", s.PlayTime.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>节点导航：10 个固定节点 + 5 个设备特写。不做自由移动（主项目文档 §0 类型冻结）。</summary>
    public sealed class NavigationSystem
    {
        readonly GameSession g;
        public NavigationSystem(GameSession g) { this.g = g; }

        public NodeDef Current => g.Content.Node(g.State.Node);
        public CloseUpDef CurrentCloseUp => g.State.CloseUp == null ? null : g.Content.CloseUp(g.State.CloseUp);

        public IEnumerable<NodeDef> Neighbors() => (Current?.Neighbors ?? new string[0]).Select(g.Content.Node).Where(n => n != null);
        public IEnumerable<CloseUpDef> CloseUpsHere() => (Current?.CloseUps ?? new string[0]).Select(g.Content.CloseUp).Where(c => c != null);

        public bool MoveTo(string nodeId, bool force = false)
        {
            var target = g.Content.Node(nodeId);
            if (target == null) return false;
            if (!force && !(Current?.Neighbors?.Contains(nodeId) ?? false)) return false;
            if (g.State.CloseUp != null) ExitCloseUp();
            string from = g.State.Node;
            g.State.Node = nodeId;
            g.Bus.Raise(Ev.NodeChanged, nodeId, from);
            g.Playtest.Record("move", nodeId, from);
            return true;
        }

        public bool EnterCloseUp(string cuId, bool force = false)
        {
            var cu = g.Content.CloseUp(cuId);
            if (cu == null) return false;
            if (!force && !cu.EntryNodes.Contains(g.State.Node)) return false;
            if (g.State.CloseUp == cuId) return true;
            g.State.CloseUp = cuId;
            g.Bus.Raise(Ev.CloseUpChanged, cuId, "enter");
            g.Playtest.Record("closeup", cuId);
            g.Puzzles.OnCloseUp(cuId);
            return true;
        }

        public void ExitCloseUp()
        {
            if (g.State.CloseUp == null) return;
            string was = g.State.CloseUp;
            g.State.CloseUp = null;
            g.Bus.Raise(Ev.CloseUpChanged, was, "exit");
        }

        /// <summary>当前位置能不能点这个交互物（避免隔着车间点到远处的东西）。</summary>
        public bool CanInteract(string interactableId)
        {
            var d = g.Content.Interactable(interactableId);
            if (d == null) return false;
            string here = g.State.CloseUp ?? g.State.Node;
            return d.VisibleFrom.Contains(here) || (g.State.CloseUp != null && d.VisibleFrom.Contains(g.State.Node) && IsInsideCloseUp(d));
        }

        // 特写里也能点到所在节点可见、且就在特写对象旁边的东西（例如 CU-1 里的 L1）
        bool IsInsideCloseUp(InteractableDef d)
        {
            var cu = CurrentCloseUp;
            return cu != null && (d.Pos - cu.Pivot).Length < 1.2f;
        }

        /// <summary>两节点间移动时长（白盒规格 §8：距离 / 3m/s，下限 0.8s，上限 2.5s）。</summary>
        public static float MoveSeconds(NodeDef a, NodeDef b)
        {
            float d = (a.Pos - b.Pos).Length;
            float t = d / 3f;
            if (t < 0.8f) t = 0.8f;
            if (t > 2.5f) t = 2.5f;
            return (float)System.Math.Round(t, 1);
        }
    }
}

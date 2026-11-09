// 《最后一次交班》Demo α —— Interaction（交互）/ Evidence（证据）/ Ledger（交班本）
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed class InteractionResult
    {
        public bool Reachable;
        public bool Matched;
        public InteractionRule Rule;
        public static readonly InteractionResult Unreachable = new InteractionResult();
    }

    public sealed class InteractionSystem
    {
        readonly GameSession g;
        public InteractionSystem(GameSession g) { this.g = g; }

        static readonly Dictionary<ToolId, string> ToolNames = new Dictionary<ToolId, string>
        { { ToolId.Probe, "听针" }, { ToolId.Torch, "手电" }, { ToolId.Caliper, "卡尺" }, { ToolId.Driver, "螺丝刀" } };
        public static string ToolName(ToolId t) => ToolNames.TryGetValue(t, out var n) ? n : "";

        public void SelectTool(ToolId t)
        {
            g.State.Tool = g.State.Tool == t ? ToolId.None : t;
            g.Bus.Raise(Ev.ToolChanged, g.State.Tool.ToString(), null, (int)g.State.Tool);
            g.Playtest.Record("tool", g.State.Tool.ToString());
        }

        /// <summary>这个物件在当前阶段、当前工具下会不会有反应（悬停时用来决定光标样式）。</summary>
        public InteractionRule Match(string target, ToolId tool)
        {
            var s = g.State;
            foreach (var r in g.Content.Rules)
            {
                if (r.Target != target) continue;
                if (!r.AnyTool && r.Tool != tool) continue;
                if (s.Phase < r.MinPhase || s.Phase > r.MaxPhase) continue;
                if (r.RequireFlags.Any(f => !s.Flag(f))) continue;
                if (r.RequireEvidence.Any(e => !s.Has(e))) continue;
                if (r.ForbidFlags.Any(s.Flag)) continue;
                if (r.ForbidEvidence.Any(s.Has)) continue;
                return r;
            }
            return null;
        }

        public InteractionResult Use(string target)
        {
            if (!g.Nav.CanInteract(target))
            {
                g.Playtest.Record("interact_unreachable", target, g.State.CloseUp ?? g.State.Node);
                return InteractionResult.Unreachable;
            }
            var tool = g.State.Tool;
            var rule = Match(target, tool);
            g.Playtest.Record("interact", target, tool.ToString());
            if (rule == null)
            {
                if (tool != ToolId.None) g.Caption($"这里用不上{ToolName(tool)}。");
                return new InteractionResult { Reachable = true };
            }
            Apply(rule.Effects);
            if (rule.Valid) g.ValidAction();
            return new InteractionResult { Reachable = true, Matched = true, Rule = rule };
        }

        public void Apply(IEnumerable<Effect> effects)
        {
            foreach (var e in effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.GiveEvidence: g.Evidence.Give(e.A); break;
                    case EffectKind.SetFlag: g.SetFlag(e.A); break;
                    case EffectKind.ClearFlag: g.SetFlag(e.A, false); break;
                    case EffectKind.Say: g.Dialogue.Say(e.A); break;
                    case EffectKind.Caption: g.Caption(e.A, e.B); break;
                    case EffectKind.HengLog: g.Log(e.B ?? "SYS", e.A); break;
                    case EffectKind.OpenView: g.OpenView(e.A, e.B); g.Puzzles.OnViewOpened(e.A, e.B); break;
                    case EffectKind.MemoryDevelop: g.Bus.Raise(Ev.MemoryDevelop, e.A); g.Playtest.Record("memory", e.A); break;
                    case EffectKind.Sfx: g.Sfx(e.A); break;
                    case EffectKind.EnterCloseUp: g.Nav.EnterCloseUp(e.A, true); break;
                    case EffectKind.SetTool: break;
                }
            }
        }
    }

    public sealed class EvidenceSystem
    {
        readonly GameSession g;
        public EvidenceSystem(GameSession g) { this.g = g; }

        /// <summary>获得证据：自动贴进交班本（UI-DS §5.2 证据飞入），衡记一行。</summary>
        public bool Give(string id)
        {
            var def = g.Content.Ev(id);
            if (def == null || g.State.Evidence.Contains(id)) return false;
            g.State.Evidence.Add(id);
            g.Bus.Raise(Ev.EvidenceAcquired, id, def.Paper);
            g.Playtest.Record("evidence", id);
            if (def.Grade != EvidenceGrade.Ambient) g.Log("MON", $"记录：{def.Name}。");
            g.Puzzles.OnEvidence(id);
            g.Hints.OnProgress();
            return true;
        }

        public IEnumerable<EvidenceDef> ForPhase(Phase p) => g.Content.Evidence.Where(e => e.Phase == p);
        public IEnumerable<EvidenceDef> Collected(Phase p) => ForPhase(p).Where(e => g.State.Has(e.Id));
        public IEnumerable<EvidenceDef> Necessary(Phase p) => ForPhase(p).Where(e => e.Grade == EvidenceGrade.Necessary);
    }

    public sealed class LedgerSystem
    {
        readonly GameSession g;
        public LedgerSystem(GameSession g) { this.g = g; }

        public bool IsOpen(SpreadDef s) => g.State.Phase >= s.MinPhase;
        public IEnumerable<SpreadDef> Spreads => g.Content.Spreads;

        /// <summary>阅读贴页。到了这一项才开放（页面上用建国的便签挡着：“到这一项再翻。”）。</summary>
        public bool ReadPage(string pageId)
        {
            var p = g.Content.Page(pageId);
            if (p == null || g.State.Phase < p.MinPhase) return false;
            bool first = g.State.ReadPages.Add(pageId);
            if (first) { g.Bus.Raise(Ev.PageRead, pageId); g.Playtest.Record("page", pageId); }
            if (!string.IsNullOrEmpty(p.GivesEvidence))
            {
                var ev = g.Content.Ev(p.GivesEvidence);
                if (ev != null && g.State.Phase >= ev.Phase && g.Evidence.Give(p.GivesEvidence)) { g.ValidAction(); return true; }
            }
            return first;
        }

        /// <summary>某一页有没有还没读过、且读了能拿到证据的内容（页角红三角）。</summary>
        public bool HasNew(string pageId)
        {
            var p = g.Content.Page(pageId);
            if (p == null || g.State.Phase < p.MinPhase) return false;
            if (!string.IsNullOrEmpty(p.GivesEvidence)) return !g.State.Has(p.GivesEvidence);
            return !g.State.ReadPages.Contains(pageId);
        }
    }
}

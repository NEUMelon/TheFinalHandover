// 《最后一次交班》Demo α —— Triad（三联板）/ Knowledge Rule（经验规则 + 交接状态灯 + 衡等级）/ Dialogue（对白 / 字幕）
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public enum TriadOutcome { NotOpen, NotFilled, Conflict, Solved }

    public sealed class TriadResult
    {
        public TriadOutcome Outcome;
        public List<string> WrongColumns = new List<string>();
        public TriadOption Feedback;   // 第一处错误的反馈（衡日志 + 仿真声音）
    }

    public sealed class TriadSystem
    {
        readonly GameSession g;
        public TriadSystem(GameSession g) { this.g = g; }

        public TriadDef Current => g.Content.Triad(g.State.Phase);
        string Key(string col) => g.State.Phase + "." + col;

        /// <summary>三联板能不能打开；不能时返回给玩家看的原因。</summary>
        public bool CanOpen(out string reason)
        {
            reason = null;
            var t = Current;
            if (t == null) { reason = "这一项没有三联板。"; return false; }
            if (g.State.Rule(t.RuleId) != RuleStatus.None) { reason = "这一项已经确认。"; return false; }
            if (t.Prerequisites.Any(p => !g.State.HasOrFlag(p)))
            {
                reason = t.Phase == Phase.P3 ? "先把那一年的事推出来：填推断单。" : "先把试吊做稳。";
                return false;
            }
            return true;
        }

        public bool IsLocked(TriadOption o) => o.Needs.Any(n => !g.State.Has(n));

        /// <summary>三格的正确选项都已解锁（证据齐了）：这时提醒玩家去填三联板，只提醒一次。</summary>
        public bool Ready => Current != null && g.State.Rule(Current.RuleId) == RuleStatus.None
                             && Current.Prerequisites.All(g.State.HasOrFlag)
                             && Current.Columns.All(c => c.Options.Where(o => o.Ok).All(o => !IsLocked(o)));
        public string Picked(string col) => g.State.TriadPick.TryGetValue(Key(col), out var v) ? v : null;
        public bool AllFilled => Current != null && Current.Columns.All(c => Picked(c.Key) != null);

        public bool Pick(string col, string optId)
        {
            var t = Current; if (t == null) return false;
            var c = t.Columns.FirstOrDefault(x => x.Key == col); if (c == null) return false;
            var o = c.Options.FirstOrDefault(x => x.Id == optId); if (o == null) return false;
            if (IsLocked(o)) { g.Playtest.Record("triad_locked", col, optId); return false; }
            if (Picked(col) == optId) g.State.TriadPick.Remove(Key(col));
            else g.State.TriadPick[Key(col)] = optId;
            g.Hints.OnValidAction();
            return true;
        }

        public TriadResult Confirm()
        {
            var r = new TriadResult();
            var t = Current;
            if (t == null || !CanOpen(out _)) { r.Outcome = TriadOutcome.NotOpen; return r; }
            if (!AllFilled) { r.Outcome = TriadOutcome.NotFilled; return r; }
            foreach (var c in t.Columns)
            {
                var o = c.Options.First(x => x.Id == Picked(c.Key));
                if (!o.Ok) { r.WrongColumns.Add(c.Key); if (r.Feedback == null) r.Feedback = o; }
            }
            g.ValidAction();
            if (r.WrongColumns.Count == 0)
            {
                r.Outcome = TriadOutcome.Solved;
                g.Playtest.Record("triad_ok", t.RuleId);
                g.Bus.Raise(Ev.TriadSolved, t.RuleId);
                g.Rules.BeginDraft(t.RuleId);
                g.Hints.OnProgress();
                return r;
            }
            r.Outcome = TriadOutcome.Conflict;
            g.Playtest.Record("triad_wrong", t.RuleId, string.Join("+", r.WrongColumns.Select(k => k + ":" + Picked(k))));
            // 填错：衡报“规则冲突”，按错误规则在仿真里播一段“听起来不对”的声音；不扣分、不重来（主项目文档 §3.4）
            if (r.Feedback.FbLog == null || !r.Feedback.FbLog.Contains("规则冲突")) g.Log("RULE", "规则冲突。", "conflict");
            g.Log(r.Feedback.FbModule ?? "RULE", r.Feedback.FbLog ?? "规则冲突。", "conflict");
            g.Bus.Raise(Ev.TriadConflict, string.Join(",", r.WrongColumns), r.Feedback.FbSim);
            if (!string.IsNullOrEmpty(r.Feedback.FbCaption)) g.Caption(r.Feedback.FbCaption);
            return r;
        }
    }

    public sealed class KnowledgeRuleSystem
    {
        readonly GameSession g;
        public KnowledgeRuleSystem(GameSession g) { this.g = g; }

        /// <summary>衡的技术等级（主项目文档 §5.4；Demo 上限四级）。名称取材八级工制度，待核 K-02。</summary>
        public static readonly string[] Levels = { "学徒", "一级", "二级", "三级", "四级" };
        public string LevelName => Levels[System.Math.Min(g.State.Level, Levels.Length - 1)];

        public RuleCardDef Card(string id) => g.Content.RuleCards.TryGetValue(id, out var c) ? c : null;

        void Set(string id, RuleStatus st)
        {
            g.State.Rules[id] = st;
            g.Bus.Raise(Ev.RuleStatusChanged, id, st.ToString(), (int)st);
        }

        public void BeginDraft(string id)
        {
            if (g.State.Rule(id) != RuleStatus.None) return;
            Set(id, RuleStatus.Draft);
            g.Log("RULE", $"三联一致。生成规则草案 {id}。");
        }

        /// <summary>谜题三需要玩家亲手在数字孪生里做完离线验证才能进入“待验证”。</summary>
        public bool CanSetPending(string id)
        {
            if (g.State.Rule(id) != RuleStatus.Draft) return false;
            if (id == RuleIds.JianguoGuard) return g.Puzzles.TwinComplete;
            return true;
        }

        public bool SetPending(string id)
        {
            if (!CanSetPending(id)) return false;
            Set(id, RuleStatus.Pending);
            g.Puzzles.OnRulePending(id);
            g.Log("RULE", $"{id} 进入待验证。请师傅确认。");
            g.Playtest.Record("rule_pending", id);
            return true;
        }

        /// <summary>“记住与告别同帧”：规则确认 = 交接状态灯熄灭 = 衡晋升一级（UI-DS §5.4 1400ms 处）。</summary>
        public bool Confirm(string id)
        {
            if (g.State.Rule(id) != RuleStatus.Pending) return false;
            Set(id, RuleStatus.Confirmed);
            int lamp = RuleIds.IndexOf(id);
            if (lamp >= 0 && g.State.Lamps[lamp]) { g.State.Lamps[lamp] = false; g.Bus.Raise(Ev.LampOff, id, null, lamp); }
            var card = Card(id);
            g.Log("RULE", $"经验规则 {id} 已确认。来源 {card?.Owner}。", "ok");
            switch (id)
            {
                case RuleIds.ZhouClick:
                    g.Log("DIAG", "更正：主轴后轴承早期缺陷，列入计划检修。");
                    g.Log("DIAG", "撤回“停机更换齿轮”建议。");
                    break;
                case RuleIds.JianguoGuard:
                    g.Log("SYS", "旧联锁继电器继续在回路中工作，直到交接停机。");
                    break;
            }
            g.Log("SYS", $"交接 {g.State.LampsOff}/4。");
            string from = LevelName;
            g.State.Level = System.Math.Min(g.State.Level + 1, Levels.Length - 1);
            g.Log("SYS", $"等级：{from} → {LevelName}。");
            g.Bus.Raise(Ev.LevelUp, from, LevelName, g.State.Level);
            g.Playtest.Record("rule_confirmed", id);
            g.Flow.OnRuleConfirmed(id);
            g.Hints.OnProgress();
            return true;
        }
    }

    public sealed class DialogueSystem
    {
        readonly GameSession g;
        public DialogueSystem(GameSession g) { this.g = g; }
        public readonly HashSet<string> Spoken = new HashSet<string>();

        public void Say(string lineId)
        {
            if (lineId == "@last")
            {
                if (g.State.LastLineId == null) { g.Caption("〔对讲机：沙沙的底噪〕"); return; }
                lineId = g.State.LastLineId;
            }
            if (!g.Content.Lines.TryGetValue(lineId, out var line)) return;
            if (line.Speaker == "周建国") g.State.LastLineId = lineId;
            Spoken.Add(lineId);
            g.Bus.Raise(Ev.Line, lineId, line.Speaker);
            g.Playtest.Record("line", lineId);
        }

        public string Who(LineDef l) => l.Via == "入画" ? l.Speaker : l.Via + " · " + l.Speaker;
    }
}

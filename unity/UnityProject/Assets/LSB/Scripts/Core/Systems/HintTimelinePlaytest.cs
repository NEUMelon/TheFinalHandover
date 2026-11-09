// 《最后一次交班》Demo α —— Hint（三级提示）/ Timeline（剧情时间）/ 试玩记录
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LSB.Core
{
    /// <summary>
    /// 三级提示：由“自上一次有效操作起”的卡关时长触发，默认 90 / 180 / 300 秒（主项目文档 §3.4），可在设置中关闭。
    /// 卡点（Stage）变化时，提示等级清零。
    /// </summary>
    public sealed class HintSystem
    {
        readonly GameSession g;
        public HintSystem(GameSession g) { this.g = g; }

        public static readonly float[] Thresholds = { 90f, 180f, 300f };
        public float Timer { get; private set; }
        public int Level { get; private set; }
        public string Stage { get; private set; }
        /// <summary>演出中（规则卡生成、记忆显影、结局长镜头）暂停计时。</summary>
        public bool Paused;
        readonly HashSet<string> sideLinesPlayed = new HashSet<string>();

        public HintSet Current => g.Content.Hint(g.State.Phase, Stage);
        public IEnumerable<string> VisibleTexts => Current == null ? Enumerable.Empty<string>() : Current.Levels.Take(Level);

        public void ResetTimer() { Timer = 0; }

        public void ResetStage()
        {
            Stage = PuzzleProgress.Stage(g.State);
            Level = 0; Timer = 0;
            g.Bus.Raise(Ev.HintChanged, Stage, null, 0);
            g.Playtest.Record("stage", g.State.Phase + "." + Stage);
        }

        public void OnValidAction() { Timer = 0; }

        public void OnProgress()
        {
            var st = PuzzleProgress.Stage(g.State);
            if (st == Stage) return;
            Stage = st; Level = 0; Timer = 0;
            g.Bus.Raise(Ev.HintChanged, Stage, null, 0);
            g.Playtest.Record("stage", g.State.Phase + "." + Stage);   // 卡点切换：汇总时算“在哪一步停得最久”
        }

        public void Tick(float dt)
        {
            if (Paused || !g.State.Settings.HintsOn || Current == null) return;
            Timer += dt * Math.Max(0.01f, g.State.Settings.HintSpeed);
            int lv = Timer >= Thresholds[2] ? 3 : Timer >= Thresholds[1] ? 2 : Timer >= Thresholds[0] ? 1 : 0;
            if (lv > Level) Raise(lv, "auto");
        }

        /// <summary>提示关闭时，玩家点“需要提示”手动要一级（UI-DS §5.8）。开发菜单也走这里。</summary>
        public void RequestManual()
        {
            if (Current == null || Level >= 3) return;
            Raise(Level + 1, "manual");
        }

        /// <summary>小玩法里连续判偏时，直接把提示抬到至少某一级（原型：判偏 3 次 → 页边一行铅笔字）。</summary>
        public void AtLeast(int lv, string how)
        {
            if (Current == null || Level >= lv) return;
            Raise(lv, how);
        }

        void Raise(int lv, string how)
        {
            Level = lv;
            g.Bus.Raise(Ev.HintChanged, Stage, how, lv);
            g.Playtest.Record("hint", g.State.Phase + "." + Stage, $"L{lv}:{how}");
            // 谜题一卡在“去更衣室”时，周建国在对讲机里补一句（世界观圣经台词②）
            if (g.State.Phase == Phase.P1 && Stage == "locker" && lv == 1 && sideLinesPlayed.Add("J2")) g.Dialogue.Say("J2");
        }
    }

    /// <summary>当前卡点：用于选择提示文案，也写进试玩记录（“第一次不知道做什么的位置”）。</summary>
    public static class PuzzleProgress
    {
        public static string Stage(GameState s)
        {
            switch (s.Phase)
            {
                case Phase.Intro: return "ledger";
                case Phase.P1:
                    if (!s.Has("E1_2")) return "locker";
                    if (!s.Has("E1_3")) return "mark";
                    if (!s.Has("E1_4")) return "sketch";
                    if (!s.Has("E1_5")) return "compare";
                    return s.Rule(RuleIds.ZhouLeft) == RuleStatus.None ? "triad" : "done";
                case Phase.P2:
                    if (!s.Has("E2_1")) return "spectro";
                    if (!s.Has("E2_2")) return "gear";
                    if (!s.Has("E2_3")) return "note";
                    return s.Rule(RuleIds.ZhouClick) == RuleStatus.None ? "triad" : "done";
                case Phase.P3:
                    if (!s.Has("E3_2")) return "dent";
                    if (!s.Has("E3_3")) return "cabinet";
                    if (!(s.Has("E3_6") && s.Has("E3_7") && s.Has("E3_8"))) return "papers";
                    if (!s.Has("E3_INF")) return "deduce";
                    var g3 = s.Rule(RuleIds.JianguoGuard);
                    if (g3 == RuleStatus.None) return "triad";
                    return g3 == RuleStatus.Draft ? "verify" : "done";
                case Phase.P4:
                    if (!s.Has("E4_1")) return "watch";
                    if (!s.Has("E4_2")) return "tape";
                    if (!s.Has("E4_TUNE")) return "tune";
                    return s.Rule(RuleIds.SunHalfbeat) == RuleStatus.None ? "triad" : "done";
                case Phase.Ending:
                    if (s.Ending <= EndingStep.PullSwitch) return "switch";
                    if (s.Ending == EndingStep.NameAndSign) return "name";
                    return "show";
            }
            return "none";
        }
    }

    /// <summary>剧情时间：13–15 分钟游玩压缩一个中班 16:00–24:00（世界观圣经 §1.3）。每次有效操作走 1 分钟，到本阶段上限为止。</summary>
    public sealed class TimelineSystem
    {
        readonly GameSession g;
        public TimelineSystem(GameSession g) { this.g = g; }

        public TimeAnchor Anchor => g.Content.Anchor(g.State.Phase);
        public string LightPreset => Anchor?.Light ?? "day1600";

        public void SetAnchor(Phase p)
        {
            var a = g.Content.Anchor(p);
            if (a == null) return;
            g.State.ClockMinute = a.StartMinute;
            g.Bus.Raise(Ev.ClockChanged, g.State.Clock, a.Light, g.State.ClockMinute);
        }

        public void Advance(int minutes)
        {
            var a = Anchor; if (a == null || minutes <= 0) return;
            int m = Math.Min(g.State.ClockMinute + minutes, a.EndMinute);
            if (m == g.State.ClockMinute) return;
            g.State.ClockMinute = m;
            g.Bus.Raise(Ev.ClockChanged, g.State.Clock, a.Light, m);
        }

        /// <summary>“十七点三十分”式汉字时刻（交班本手写用）。</summary>
        public static string ChineseTime(int minute)
        {
            const string D = "〇一二三四五六七八九";
            string Num(int n) => n < 10 ? D[n].ToString() : (n < 20 ? "" : D[n / 10].ToString()) + "十" + (n % 10 != 0 ? D[n % 10].ToString() : "");
            int h = minute / 60 % 24, mm = minute % 60;
            return Num(h) + "点" + (mm == 0 ? "整" : Num(mm) + "分");
        }
    }

    /// <summary>
    /// 试玩记录（阶段 2F 首轮可用性测试用）。只记录游戏内自动可得的客观行为数据；
    /// 真人试玩的观察、访谈、问卷由主持人填写模板，不由程序代填，更不由程序虚构。
    /// </summary>
    public sealed class PlaytestLog
    {
        readonly GameSession g;
        public PlaytestLog(GameSession g) { this.g = g; }

        public sealed class Entry
        {
            public double T;         // 自本次会话开始的秒数（游玩时间）
            public string Phase;
            public string Node;
            public string Type;
            public string A, B;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public string SessionId { get; private set; } = "";

        /// <summary>试玩编号（标题页填，如 P01）：拼在记录文件名前面，方便和观察记录表对上。只收字母、数字。</summary>
        public static string TesterId = "";

        public static string NormalizeTesterId(string raw)
        {
            var sb = new StringBuilder();
            foreach (var ch in (raw ?? "").ToUpperInvariant())
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) { sb.Append(ch); if (sb.Length >= 8) break; }
            return sb.ToString();
        }

        public void BeginSession(string id = null)
        {
            Entries.Clear();
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            SessionId = id ?? (string.IsNullOrEmpty(TesterId) ? stamp : TesterId + "_" + stamp);
            Record("session_start", SessionId);
        }

        public void Record(string type, string a = null, string b = null)
        {
            var s = g.State;
            Entries.Add(new Entry { T = s?.PlayTime ?? 0, Phase = s?.Phase.ToString() ?? "", Node = s == null ? "" : (s.CloseUp ?? s.Node), Type = type, A = a ?? "", B = b ?? "" });
        }

        /// <summary>空点（点到不可交互的地方）：用于统计“不知道什么能点击”的位置。</summary>
        public void EmptyClick(float sx, float sy) => Record("empty_click", sx.ToString("0.000", CultureInfo.InvariantCulture), sy.ToString("0.000", CultureInfo.InvariantCulture));

        public string ToCsv()
        {
            var sb = new StringBuilder("session,t_seconds,phase,node,type,a,b\n");
            foreach (var e in Entries)
                sb.Append(SessionId).Append(',').Append(e.T.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(e.Phase).Append(',')
                  .Append(e.Node).Append(',').Append(e.Type).Append(',').Append(Csv(e.A)).Append(',').Append(Csv(e.B)).Append('\n');
            return sb.ToString();
        }

        /// <summary>按谜题汇总：耗时、提示次数（分级）、三联错误次数、空点次数。</summary>
        public string SummaryCsv()
        {
            var sb = new StringBuilder("session,phase,seconds,hints_L1,hints_L2,hints_L3,hints_manual,triad_wrong,empty_clicks,unreachable_clicks\n");
            foreach (var p in new[] { "Intro", "P1", "P2", "P3", "P4", "Ending" })
            {
                var es = Entries.Where(e => e.Phase == p).ToList();
                if (es.Count == 0) continue;
                double start = Entries.FirstOrDefault(e => e.Type == "phase_start" && e.A == p)?.T ?? es.First().T;
                double end = Entries.FirstOrDefault(e => e.Type == "phase_end" && e.A == p)?.T ?? es.Last().T;
                int H(string lv) => es.Count(e => e.Type == "hint" && e.B.StartsWith(lv, StringComparison.Ordinal));
                sb.Append(SessionId).Append(',').Append(p).Append(',').Append((end - start).ToString("0", CultureInfo.InvariantCulture)).Append(',')
                  .Append(H("L1")).Append(',').Append(H("L2")).Append(',').Append(H("L3")).Append(',')
                  .Append(es.Count(e => e.Type == "hint" && e.B.EndsWith("manual", StringComparison.Ordinal))).Append(',')
                  .Append(es.Count(e => e.Type == "triad_wrong")).Append(',')
                  .Append(es.Count(e => e.Type == "empty_click")).Append(',')
                  .Append(es.Count(e => e.Type == "interact_unreachable")).Append('\n');
            }
            return sb.ToString();
        }

        static string Csv(string v) => v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}

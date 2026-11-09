// 《最后一次交班》Demo α —— 运行时状态 + 事件总线 + 存档编解码
// 状态只存“发生过什么”，所有显示都由 Unity 表现层根据状态和事件重新绘制。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LSB.Core
{
    public enum Ev
    {
        PhaseChanged, NodeChanged, CloseUpChanged, ToolChanged,
        EvidenceAcquired, FlagChanged, Line, Caption, HengLog, OpenView, CloseView, MemoryDevelop, Sfx,
        RuleStatusChanged, LampOff, LevelUp, HintChanged, TriadConflict, TriadSolved,
        TransitionCard, EndingStepChanged, ClockChanged, DeductionChecked, TwinTest, CraneTrial, PageRead, Feedback,
    }

    /// <summary>一条游戏事件。A/B 为字符串参数，N 为数值参数。</summary>
    public sealed class GameEvent
    {
        public Ev Type;
        public string A, B;
        public int N;
        public float F;
        public override string ToString() => $"{Type}({A}{(B != null ? "," + B : "")}{(N != 0 ? "," + N : "")})";
    }

    public sealed class EventBus
    {
        public event Action<GameEvent> Raised;
        /// <summary>最近的事件（测试与开发菜单用）。</summary>
        public readonly List<GameEvent> History = new List<GameEvent>();
        public int HistoryLimit = 2000;

        public GameEvent Raise(Ev type, string a = null, string b = null, int n = 0, float f = 0)
        {
            var e = new GameEvent { Type = type, A = a, B = b, N = n, F = f };
            History.Add(e);
            if (History.Count > HistoryLimit) History.RemoveRange(0, History.Count - HistoryLimit);
            Raised?.Invoke(e);
            return e;
        }
    }

    /// <summary>衡日志一行（UI-DS §5.5：[HH:MM] 模块 > 内容）。</summary>
    public sealed class LogEntry
    {
        public int Id;
        public string Time;
        public string Module;
        public string Text;
        public string Kind;
    }

    public sealed class GameSettings
    {
        public bool HintsOn = true;       // 页边提示（可关，关后出现“需要提示”按钮）
        public float HintSpeed = 1f;      // 演示 / 开发加速；正式试玩保持 1
        public bool Captions = true;      // 字幕（含环境关键声音）
        public bool ShowPro = false;      // 三联板显示“专业说法”（第二层信息）
        public float Volume = 0.8f;
    }

    public sealed class GameState
    {
        public Phase Phase = Phase.Title;
        public string Node = "N1";
        public string CloseUp;                 // 当前设备特写（null = 不在特写）
        public ToolId Tool = ToolId.None;
        public readonly HashSet<string> Evidence = new HashSet<string>();
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly HashSet<string> ReadPages = new HashSet<string>();
        public readonly Dictionary<string, RuleStatus> Rules = new Dictionary<string, RuleStatus>();
        public readonly bool[] Lamps = { true, true, true, true }; // true = 亮（旧方式承担）
        public int Level;                      // 0 学徒 … 4 四级
        public int ClockMinute = 16 * 60;
        public readonly Dictionary<string, string> TriadPick = new Dictionary<string, string>(); // "P2.phen" → 选项 ID
        public readonly Dictionary<string, string> DeductionFill = new Dictionary<string, string>();
        public readonly HashSet<string> TwinPassed = new HashSet<string>();
        public float CraneT2 = 1f;             // 第二段指令时刻（单位：拍 = 当前摆动周期）
        public EndingStep Ending = EndingStep.None;
        public string LastLineId;
        public string PlayerRuleName;
        public bool PlayerSigned;
        public bool PuzzleSolved;              // 当前谜题规则已确认，等待过场进入下一项
        public double PlayTime;                // 累计游玩秒数
        public readonly List<LogEntry> Log = new List<LogEntry>();
        public int LogSeq;
        public readonly GameSettings Settings = new GameSettings();

        public RuleStatus Rule(string id) => Rules.TryGetValue(id, out var s) ? s : RuleStatus.None;
        public bool Has(string evidenceId) => Evidence.Contains(evidenceId);
        public bool Flag(string f) => Flags.Contains(f);
        public bool HasOrFlag(string id) => Evidence.Contains(id) || Flags.Contains(id);
        public int LampsOff => Lamps.Count(l => !l);

        public static string FormatClock(int minute)
        {
            minute = ((minute % 1440) + 1440) % 1440;
            return $"{minute / 60:00}:{minute % 60:00}";
        }
        public string Clock => FormatClock(ClockMinute);
    }

    /// <summary>
    /// 存档：纯文本“键=值”，一行一项；集合用 | 分隔。不依赖 JsonUtility（它不支持 Dictionary / HashSet）。
    /// </summary>
    public static class SaveCodec
    {
        const string Magic = "LSB_DEMO_ALPHA_SAVE=1";

        public static string Encode(GameState s)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Magic);
            void W(string k, string v) => sb.Append(k).Append('=').Append(Esc(v)).Append('\n');
            W("phase", ((int)s.Phase).ToString(CultureInfo.InvariantCulture));
            W("node", s.Node);
            W("closeup", s.CloseUp ?? "");
            W("tool", ((int)s.Tool).ToString(CultureInfo.InvariantCulture));
            W("evidence", string.Join("|", s.Evidence.OrderBy(x => x)));
            W("flags", string.Join("|", s.Flags.OrderBy(x => x)));
            W("pages", string.Join("|", s.ReadPages.OrderBy(x => x)));
            W("rules", string.Join("|", s.Rules.Select(kv => kv.Key + ":" + (int)kv.Value)));
            W("lamps", string.Join("", s.Lamps.Select(l => l ? "1" : "0")));
            W("level", s.Level.ToString(CultureInfo.InvariantCulture));
            W("clock", s.ClockMinute.ToString(CultureInfo.InvariantCulture));
            W("triad", string.Join("|", s.TriadPick.Select(kv => kv.Key + ":" + kv.Value)));
            W("deduction", string.Join("|", s.DeductionFill.Select(kv => kv.Key + ":" + kv.Value)));
            W("twin", string.Join("|", s.TwinPassed));
            W("crane", s.CraneT2.ToString("0.###", CultureInfo.InvariantCulture));
            W("ending", ((int)s.Ending).ToString(CultureInfo.InvariantCulture));
            W("lastline", s.LastLineId ?? "");
            W("rulename", s.PlayerRuleName ?? "");
            W("signed", s.PlayerSigned ? "1" : "0");
            W("solved", s.PuzzleSolved ? "1" : "0");
            W("playtime", s.PlayTime.ToString("0.#", CultureInfo.InvariantCulture));
            W("hints", s.Settings.HintsOn ? "1" : "0");
            W("pro", s.Settings.ShowPro ? "1" : "0");
            W("captions", s.Settings.Captions ? "1" : "0");
            W("volume", s.Settings.Volume.ToString("0.##", CultureInfo.InvariantCulture));
            W("logseq", s.LogSeq.ToString(CultureInfo.InvariantCulture));
            foreach (var l in s.Log.Skip(Math.Max(0, s.Log.Count - 80)))
                W("log", $"{l.Id}\t{l.Time}\t{l.Module}\t{l.Kind}\t{l.Text}");
            return sb.ToString();
        }

        public static GameState Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(Magic, StringComparison.Ordinal)) return null;
            var s = new GameState();
            foreach (var raw in text.Split('\n'))
            {
                int i = raw.IndexOf('=');
                if (i <= 0) continue;
                string k = raw.Substring(0, i), v = Unesc(raw.Substring(i + 1));
                var parts = v.Length == 0 ? new string[0] : v.Split('|');
                switch (k)
                {
                    case "phase": s.Phase = (Phase)Int(v); break;
                    case "node": s.Node = v; break;
                    case "closeup": s.CloseUp = v.Length == 0 ? null : v; break;
                    case "tool": s.Tool = (ToolId)Int(v); break;
                    case "evidence": foreach (var p in parts) s.Evidence.Add(p); break;
                    case "flags": foreach (var p in parts) s.Flags.Add(p); break;
                    case "pages": foreach (var p in parts) s.ReadPages.Add(p); break;
                    case "rules": foreach (var p in parts) { var kv = p.Split(':'); if (kv.Length == 2) s.Rules[kv[0]] = (RuleStatus)Int(kv[1]); } break;
                    case "lamps": for (int j = 0; j < 4 && j < v.Length; j++) s.Lamps[j] = v[j] == '1'; break;
                    case "level": s.Level = Int(v); break;
                    case "clock": s.ClockMinute = Int(v); break;
                    case "triad": foreach (var p in parts) { var kv = p.Split(':'); if (kv.Length == 2) s.TriadPick[kv[0]] = kv[1]; } break;
                    case "deduction": foreach (var p in parts) { var kv = p.Split(':'); if (kv.Length == 2) s.DeductionFill[kv[0]] = kv[1]; } break;
                    case "twin": foreach (var p in parts) s.TwinPassed.Add(p); break;
                    case "crane": s.CraneT2 = float.Parse(v, CultureInfo.InvariantCulture); break;
                    case "ending": s.Ending = (EndingStep)Int(v); break;
                    case "lastline": s.LastLineId = v.Length == 0 ? null : v; break;
                    case "rulename": s.PlayerRuleName = v.Length == 0 ? null : v; break;
                    case "signed": s.PlayerSigned = v == "1"; break;
                    case "solved": s.PuzzleSolved = v == "1"; break;
                    case "playtime": s.PlayTime = double.Parse(v, CultureInfo.InvariantCulture); break;
                    case "hints": s.Settings.HintsOn = v == "1"; break;
                    case "pro": s.Settings.ShowPro = v == "1"; break;
                    case "captions": s.Settings.Captions = v == "1"; break;
                    case "volume": s.Settings.Volume = float.Parse(v, CultureInfo.InvariantCulture); break;
                    case "logseq": s.LogSeq = Int(v); break;
                    case "log":
                        var f = v.Split('\t');
                        if (f.Length >= 5) s.Log.Add(new LogEntry { Id = Int(f[0]), Time = f[1], Module = f[2], Kind = f[3], Text = string.Join("\t", f.Skip(4)) });
                        break;
                }
            }
            return s;
        }

        static int Int(string v) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        static string Esc(string v) => (v ?? "").Replace("\\", "\\\\").Replace("\n", "\\n");
        static string Unesc(string v)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] == '\\' && i + 1 < v.Length) { i++; sb.Append(v[i] == 'n' ? '\n' : v[i]); }
                else if (v[i] != '\r') sb.Append(v[i]);
            }
            return sb.ToString();
        }
    }
}

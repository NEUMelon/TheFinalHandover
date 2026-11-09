// 《最后一次交班》Demo α —— 会话：把各系统组装在一起
// Unity 表现层只持有一个 GameSession：调用它的方法（玩家操作），订阅 Bus（刷新画面）。
// 各系统之间只通过 Session 互相访问，不直接 new 彼此。
using System;

namespace LSB.Core
{
    public sealed class GameSession
    {
        public readonly ContentDB Content;
        public GameState State { get; private set; }
        public readonly EventBus Bus = new EventBus();

        public readonly FlowSystem Flow;
        public readonly NavigationSystem Nav;
        public readonly InteractionSystem Interaction;
        public readonly EvidenceSystem Evidence;
        public readonly LedgerSystem Ledger;
        public readonly TriadSystem Triad;
        public readonly KnowledgeRuleSystem Rules;
        public readonly DialogueSystem Dialogue;
        public readonly HintSystem Hints;
        public readonly TimelineSystem Timeline;
        public readonly PuzzleSystem Puzzles;
        public readonly EndingSystem Ending;
        public readonly PlaytestLog Playtest;

        public GameSession(ContentDB content = null)
        {
            Content = content ?? new ContentDB();
            State = new GameState();
            Playtest = new PlaytestLog(this);
            Flow = new FlowSystem(this);
            Nav = new NavigationSystem(this);
            Interaction = new InteractionSystem(this);
            Evidence = new EvidenceSystem(this);
            Ledger = new LedgerSystem(this);
            Triad = new TriadSystem(this);
            Rules = new KnowledgeRuleSystem(this);
            Dialogue = new DialogueSystem(this);
            Hints = new HintSystem(this);
            Timeline = new TimelineSystem(this);
            Puzzles = new PuzzleSystem(this);
            Ending = new EndingSystem(this);
        }

        /// <summary>每帧调用（Unity 的 Update）。dt 为真实秒数。</summary>
        public void Tick(float dt)
        {
            if (State.Phase == Phase.Title) return;
            State.PlayTime += dt;
            Hints.Tick(dt);
            Ending.Tick(dt);
        }

        /// <summary>替换整个状态（读档、开发菜单跳关）。</summary>
        public void ReplaceState(GameState s)
        {
            var keep = State.Settings;
            State = s ?? new GameState();
            // 设置跟随玩家，不跟随存档覆盖
            State.Settings.HintsOn = keep.HintsOn; State.Settings.HintSpeed = keep.HintSpeed;
            State.Settings.Captions = keep.Captions; State.Settings.ShowPro = keep.ShowPro; State.Settings.Volume = keep.Volume;
            Hints.ResetTimer();
            Bus.Raise(Ev.PhaseChanged, State.Phase.ToString(), "load", (int)State.Phase);
        }

        // ---------------- 通用输出 ----------------
        public void Caption(string text, string who = null) => Bus.Raise(Ev.Caption, text, who);

        public LogEntry Log(string module, string text, string kind = "")
        {
            var e = new LogEntry { Id = State.LogSeq++, Time = State.Clock, Module = module, Text = text, Kind = kind ?? "" };
            State.Log.Add(e);
            if (State.Log.Count > 400) State.Log.RemoveAt(0);
            Bus.Raise(Ev.HengLog, text, module, e.Id);
            return e;
        }

        public void Sfx(string id) => Bus.Raise(Ev.Sfx, id);

        public void OpenView(string viewId, string arg = null)
        {
            Playtest.Record("view", viewId, arg);
            Bus.Raise(Ev.OpenView, viewId, arg);
        }

        public void SetFlag(string f, bool on = true)
        {
            bool changed = on ? State.Flags.Add(f) : State.Flags.Remove(f);
            if (changed) Bus.Raise(Ev.FlagChanged, f, on ? "1" : "0");
        }

        /// <summary>玩家做了一次“有效操作”：重置提示计时，剧情时钟走一分钟（时间压缩口径）。</summary>
        public void ValidAction(int minutes = 1)
        {
            Hints.OnValidAction();
            Timeline.Advance(minutes);
        }

        public void Save(Action<string> writer) => writer?.Invoke(SaveCodec.Encode(State));
    }
}

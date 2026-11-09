// 《最后一次交班》Demo α —— 录音机（谜题四，交班桌抽屉，特写 CU5）
// 放广播站那盘采访磁带：先是广播站开场，再是孙桂兰两句；放完触发一九九八年记忆显影（先有铃声后有剪影），
// 衡把铃点标到天车的时间轴上（E4_4）。
// ⚠ 磁带人声是占位：只有字幕和程序合成的磁带底噪，没有录人声；正式版按声音圣经录制。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class TapeView : ModalView, ITickView
    {
        public override string Id => "tape";

        const float LineGap = 3.6f, Tail = 1.5f;
        readonly List<string> queue = new List<string>();
        readonly List<string> shown = new List<string>();
        float nextAt, endAt = -1;
        bool playing;
        VisualElement reelL, reelR, transcript;
        Label state;
        float spin;

        public TapeView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "tape-deck") { }

        protected override void Build(string arg)
        {
            playing = false; queue.Clear(); endAt = -1;
            Render();
        }

        void Render()
        {
            Panel.Clear();
            Panel.Add(L("单卡录音机", "tape-brand"));
            var win = Box("tape-window");
            var cas = Box("tape-cassette");
            reelL = Box("tape-reel"); reelR = Box("tape-reel");
            reelL.Add(Box("tape-spoke")); reelR.Add(Box("tape-spoke"));
            cas.Add(reelL); cas.Add(L(g.State.Flag("tape_taken") ? "广播站　孙桂兰" : "（空）", "f-hand-jian", "tape-label")); cas.Add(reelR);
            win.Add(cas);
            Panel.Add(win);

            var keys = Box("tape-keys");
            var play = B(playing ? "放音中" : (g.State.Has("E4_2") ? "▶ 再听一遍" : "▶ 放"), Play, "tape-key");
            play.SetEnabled(!playing && g.State.Flag("tape_taken"));
            keys.Add(play);
            var stop = B("■ 停", Stop, "tape-key");
            stop.SetEnabled(playing);
            keys.Add(stop);
            Panel.Add(keys);

            transcript = Box("tape-transcript");
            state = L(playing ? "沙沙……" : g.State.Has("E4_4") ? "衡把铃点标到天车的时间轴上了。" : "按“放”。", "tape-state");
            transcript.Add(state);
            foreach (var id in shown) AddLine(id);
            Panel.Add(transcript);

            var foot = Box("reader-foot");
            foot.Add(L("占位：磁带只有字幕和合成的底噪，没有录人声", "placeholder-tag"));
            foot.Add(B("合上（Esc）", CloseSelf, "btn"));
            Panel.Add(foot);
        }

        void Play()
        {
            var lines = g.Puzzles.PlayTape();
            if (lines.Count == 0) return;
            queue.Clear(); queue.AddRange(lines);
            shown.Clear();
            playing = true;
            nextAt = Now + 0.8f;
            endAt = -1;
            ProceduralAudio.Instance?.Loop("tape_hiss");
            Render();
        }

        void AddLine(string id)
        {
            if (!g.Content.Lines.TryGetValue(id, out var line)) return;
            var r = Box("tape-line");
            r.Add(L(line.Speaker, "tape-who"));
            r.Add(L(line.Text, "f-serif", "tape-text"));
            transcript.Add(r);
        }

        void Stop()
        {
            playing = false; queue.Clear(); endAt = -1;
            ProceduralAudio.Instance?.StopLoop();
            Render();
        }

        public void Tick()
        {
            if (!playing) return;
            spin = (spin + Time.unscaledDeltaTime * 240f) % 360f;
            if (reelL != null) { Rot(reelL, spin); Rot(reelR, spin); }
            if (queue.Count > 0 && Now >= nextAt)
            {
                var id = queue[0]; queue.RemoveAt(0);
                g.Dialogue.Say(id);
                shown.Add(id);
                AddLine(id);
                state.text = "";
                nextAt = Now + LineGap;
                if (queue.Count == 0) endAt = Now + LineGap + Tail;
            }
            if (endAt > 0 && Now >= endAt)
            {
                playing = false; endAt = -1;
                ProceduralAudio.Instance?.StopLoop();
                g.Puzzles.TapeFinished();   // → 一九九八年记忆显影（覆盖层，在面板之上）+ 铃点
                Render();
            }
        }

        protected override void OnClose()
        {
            playing = false; queue.Clear(); endAt = -1;
        }
    }
}

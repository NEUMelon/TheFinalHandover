// 《最后一次交班》Demo α —— 推断单（谜题三，Golden Idol 式填空，事实链 §4.5）
// 一句话七个空：先点一个空，再点下面的词；一个词只能用一次（再点会从原来的空里挪过来）。点已选中的空 = 清空。
// 右边是“旧纸”：已经找到的几张纸各写一句要点，没找到的显示“还没找到”，不用来回翻交班本。
// 词库里有些词要先找到对应的纸才会出现（PuzzleSystem.AvailableWords）。
// “对一下”：全对才算；错两处以内只说错几处，不说是哪几处（Golden Idol 的做法）。
using System.Linq;
using LSB.Core;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class DeductionView : ModalView
    {
        public override string Id => "deduction";

        /// <summary>右边“旧纸”栏：按推理顺序。</summary>
        static readonly string[] Papers = { "E3_2", "E3_6", "E3_8", "E3_7", "E3_3", "E3_1" };

        string selected;
        string result;
        bool resultOk;

        public DeductionView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "paper") { Panel.AddToClassList("deduction"); }

        protected override void Build(string arg)
        {
            result = null;
            selected = FirstEmpty();
            Render();
        }

        bool Done => g.State.Has(g.Puzzles.Deduction.GivesEvidence);

        string FirstEmpty() => g.Puzzles.Deduction.Blanks.Select(b => b.Key).FirstOrDefault(k => g.Puzzles.Filled(k) == null);

        protected override void OnEvent(GameEvent e)
        {
            if (e.Type == Ev.EvidenceAcquired) Panel.schedule.Execute(Render);
        }

        void Render()
        {
            Panel.Clear();
            var d = g.Puzzles.Deduction;
            var head = Box("triad-head");
            head.Add(L("推断单", "panel-title"));
            head.Add(L("那一年发生了什么", "triad-sub"));
            Panel.Add(head);
            if (g.State.Phase != Phase.P3) { Panel.Add(L("到第三项再填。", "panel-body")); Panel.Add(B("合上", CloseSelf, "btn")); return; }

            var body = Box("ded-body");
            // ---- 左：一句话 + 词库 ----
            var left = Box("ded-left");
            left.Add(L(Done ? "对上了。" : "先点一个空，再点下面的词。", "page-faint"));
            var sent = Box("ded-sentence");
            for (int i = 0; i < d.Segments.Length; i++)
            {
                if (!string.IsNullOrEmpty(d.Segments[i])) sent.Add(L(d.Segments[i], "f-serif", "ded-seg"));
                if (i < d.Blanks.Length) sent.Add(Blank(d.Blanks[i]));
            }
            left.Add(sent);

            var check = Box("ded-check");
            var btn = B(Done ? "对上了" : "对一下", Check, "triad-stamp", "ded-stamp");
            btn.EnableInClassList("done", Done);
            bool all = d.Blanks.All(b => g.Puzzles.Filled(b.Key) != null);
            btn.SetEnabled(!Done && all);
            check.Add(btn);
            var msg = Box("ded-msg");
            if (Done)
            {
                var inf = g.Content.Ev(d.GivesEvidence);
                msg.Add(L("记下了：" + inf.Name, "f-hand-lin", "ded-ok"));
                msg.Add(L(inf.Desc, "page-faint"));
                msg.Add(B("去填三联板 ▶", () => host.Open("triad"), "btn"));
            }
            else if (result != null) msg.Add(L(result, resultOk ? "ded-ok" : "ded-bad"));
            else msg.Add(L(all ? "七个空都填了，对一下。" : "七个空都填上，才能对。", "page-faint"));
            check.Add(msg);
            left.Add(check);

            if (!Done)
            {
                left.Add(L("词", "page-sub"));
                var bank = Box("ded-bank");
                var avail = g.Puzzles.AvailableWords().ToList();
                foreach (var w in avail)
                {
                    var word = w;
                    var used = d.Blanks.Any(b => g.Puzzles.Filled(b.Key) == w);
                    var wb = B(w, () => Put(word), "ded-word");
                    wb.EnableInClassList("used", used);
                    bank.Add(wb);
                }
                left.Add(bank);
                int hidden = d.WordBank.Length - avail.Count;
                if (hidden > 0) left.Add(L($"还有 {hidden} 个词没出现。找到更多旧纸，词会多出来。", "page-faint"));
            }
            body.Add(left);

            // ---- 右：旧纸 ----
            var right = Box("ded-papers");
            right.Add(L("旧纸", "page-sub"));
            foreach (var id in Papers)
            {
                var ev = g.Content.Ev(id);
                var p = Box("ded-paper");
                bool has = g.State.Has(id);
                p.EnableInClassList("missing", !has);
                p.Add(L(has ? ev.Name : "？　还没找到", "ded-paper-name"));
                if (has) p.Add(L(ev.Desc, "ded-paper-desc"));
                right.Add(p);
            }
            body.Add(right);
            Panel.Add(body);

            var foot = Box("triad-foot");
            foot.Add(B("回交班本（Esc）", CloseSelf, "btn"));
            Panel.Add(foot);
        }

        VisualElement Blank(DeductionBlank b)
        {
            var w = g.Puzzles.Filled(b.Key);
            var key = b.Key;
            var btn = B(w ?? "　　　　", () => Select(key), "ded-blank");
            btn.EnableInClassList("filled", w != null);
            btn.EnableInClassList("sel", !Done && selected == b.Key);
            btn.EnableInClassList("ok", Done);
            if (w != null) btn.AddToClassList("f-hand-lin");
            btn.SetEnabled(!Done);
            return btn;
        }

        void Select(string key)
        {
            if (Done) return;
            if (selected == key && g.Puzzles.Filled(key) != null) { g.Puzzles.Fill(key, null); g.Sfx("tick"); }
            selected = key;
            result = null;
            Render();
        }

        void Put(string word)
        {
            if (Done) return;
            var key = selected ?? FirstEmpty();
            if (key == null) return;
            if (!g.Puzzles.Fill(key, word)) return;
            g.Sfx("tick");
            result = null;
            // 填完一个空，自动跳到下一个空着的
            var d = g.Puzzles.Deduction;
            int i = System.Array.FindIndex(d.Blanks, b => b.Key == key);
            selected = d.Blanks.Skip(i + 1).Concat(d.Blanks.Take(i + 1)).Select(b => b.Key).FirstOrDefault(k => g.Puzzles.Filled(k) == null) ?? key;
            Render();
        }

        void Check()
        {
            var r = g.Puzzles.CheckDeduction();
            result = r.Message;
            resultOk = r.Correct;
            g.Sfx(r.Correct ? "stamp" : "tick");
            Render();
        }
    }
}

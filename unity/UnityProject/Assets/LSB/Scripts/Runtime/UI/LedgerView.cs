// 《最后一次交班》Demo α —— 交班本（UI-DS §5.1 主界面 / §5.3 证据贴页）
// 一本横开的本子：上方页签（本班 / 一 测点 / 二 异响 / 三 护罩 / 三 工票 / 四 天车 / 规则），A / D 或 ← / → 翻页，Tab 合上。
// 还没到那一项的页，被周建国的便签挡着：“到这一项再翻。”
// 有新内容的页：页角红三角；点那一页就是“细看”，读了才拿到贴页证据（LedgerSystem.ReadPage）。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class LedgerView : ModalView, ITickView
    {
        public override string Id => "ledger";

        string spreadId = "S0";
        VisualElement tabs, book, foot;
        readonly HashSet<string> seenClips = new HashSet<string>();
        static readonly HashSet<string> TriadEvidence = new HashSet<string>();

        /// <summary>点贴页可以放大看的证据 → 阅读器内容。</summary>
        static readonly Dictionary<string, string> ClipReader = new Dictionary<string, string>
        { { "E1_2", "photo" }, { "E1_6", "photo1956" }, { "E3_5", "checklist" }, { "E3_1", "compare" } };

        public LedgerView(GameSession g, UiRoot ui, ViewHost host) : base(g, ui, host, "ledger-book")
        {
            if (TriadEvidence.Count == 0)
                foreach (var t in g.Content.Triads) foreach (var c in t.Columns) foreach (var o in c.Options) if (o.Ok) foreach (var n in o.Needs) TriadEvidence.Add(n);
        }

        protected override void Build(string arg)
        {
            if (!string.IsNullOrEmpty(arg) && g.Content.Spreads.Any(s => s.Id == arg)) spreadId = arg;
            var sp = Spread;
            if (sp == null || !g.Ledger.IsOpen(sp)) spreadId = "S0";
            Render();
        }

        SpreadDef Spread => g.Content.Spreads.FirstOrDefault(s => s.Id == spreadId);

        void Render()
        {
            Panel.Clear();
            tabs = Box("ledger-tabs");
            foreach (var s in g.Content.Spreads)
            {
                var sid = s.Id;
                var t = B(s.Tab, () => Go(sid), "ledger-tab");
                bool open = g.Ledger.IsOpen(s);
                t.EnableInClassList("locked", !open);
                t.EnableInClassList("sel", sid == spreadId);
                if (open && SpreadHasNew(s)) t.Add(Box("corner-new"));
                tabs.Add(t);
            }
            Panel.Add(tabs);

            book = Box("ledger-spread");
            var sp = Spread;
            book.Add(PageFrame(sp.Left, true, sp));
            book.Add(Box("ledger-gutter"));
            book.Add(sp.Right == sp.Left ? PageFrame(sp.Left, false, sp, second: true) : PageFrame(sp.Right, false, sp));
            Panel.Add(book);

            foot = Box("ledger-foot");
            foot.Add(B("◀ A", () => Flip(-1), "chip"));
            int idx = g.Content.Spreads.IndexOf(sp) + 1;
            foot.Add(L($"第 {idx * 2 - 1}–{idx * 2} 页　{g.State.Clock}", "ledger-pageno"));
            foot.Add(B("D ▶", () => Flip(1), "chip"));
            foot.Add(B("合上（Tab / Esc）", CloseSelf, "btn", "ledger-close"));
            Panel.Add(foot);
        }

        bool SpreadHasNew(SpreadDef s) =>
            new[] { s.Left, s.Right }.Any(p => g.Content.Page(p)?.GivesEvidence != null && g.Ledger.HasNew(p))
            || new[] { s.Left, s.Right }.Any(p => FoundPhase(p) == g.State.Phase && g.Evidence.Collected(g.State.Phase).Any(e => e.Paper != "" && !seenClips.Contains(e.Id)));

        void Go(string sid)
        {
            var s = g.Content.Spreads.First(x => x.Id == sid);
            if (!g.Ledger.IsOpen(s)) { g.Bus.Raise(Ev.Feedback, "到这一项再翻。", "no"); return; }
            if (sid == spreadId) return;
            spreadId = sid;
            g.Sfx("flip");
            g.Playtest.Record("ledger_spread", sid);
            Render();
        }

        void Flip(int dir)
        {
            var list = g.Content.Spreads.Where(g.Ledger.IsOpen).ToList();
            int i = list.FindIndex(s => s.Id == spreadId);
            int j = Mathf.Clamp(i + dir, 0, list.Count - 1);
            if (j != i) Go(list[j].Id);
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Flip(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Flip(1);
        }

        protected override void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case Ev.EvidenceAcquired: case Ev.RuleStatusChanged: case Ev.PageRead: case Ev.PhaseChanged:
                    Panel.schedule.Execute(Render);   // 下一帧重画，避免在按钮回调里清空自己
                    break;
            }
        }

        // =====================================================================
        // 页面
        // =====================================================================
        VisualElement PageFrame(string pageId, bool left, SpreadDef sp, bool second = false)
        {
            var page = g.Content.Page(pageId);
            var v = Box("ledger-page", left ? "left" : "right");
            if (!g.Ledger.IsOpen(sp))
            {
                var note = Box("sticky");
                note.Add(L("到这一项再翻。", "f-hand-jian", "sticky-t"));
                note.Add(L("建国", "f-hand-jian", "sticky-s"));
                Rot(note, left ? -4 : 3);
                v.Add(note);
                return v;
            }
            switch (page.Kind)
            {
                case "shift_prev": ShiftPrev(v); break;
                case "shift_this": ShiftThis(v); break;
                case "sketch1983": Sketch1983(v, page); break;
                case "found": Found(v, page); break;
                case "rules": Rules(v, second); break;
                case "handover_last": Sheet(v, page, "f-hand-jian"); break;
                default: Sheet(v, page, page.Kind == "note1983" ? "f-hand-zhou" : page.Kind == "printout2026" ? "f-sans" : "f-serif"); break;
            }
            return v;
        }

        /// <summary>左页“上一班”：周建国的交代（建国体手写）+ 本项贴页区。</summary>
        void ShiftPrev(VisualElement v)
        {
            v.Add(L("上一班", "page-head"));
            if (g.Content.PageText.TryGetValue("prev", out var lines))
                foreach (var l in lines) v.Add(L(l, "f-hand-jian", "hand-line"));
            v.Add(L("贴页", "page-sub"));
            var area = Box("clip-area");
            foreach (var e in g.Evidence.Collected(g.State.Phase).Where(e => e.Paper != "")) area.Add(Clip(e, small: true));
            if (area.childCount == 0) area.Add(L("（这一项还没有贴页）", "page-faint"));
            v.Add(area);
        }

        /// <summary>右页“本班”：四项点检，异常 / 处理 / 签名栏；当前项下面是三联板入口。</summary>
        void ShiftThis(VisualElement v)
        {
            v.Add(L("本班　中班　十六点至二十四点", "page-head"));
            var table = Box("check-table");
            var hdr = Box("check-row", "hdr");
            hdr.Add(L("点检项", "c-item")); hdr.Add(L("异常", "c-anom")); hdr.Add(L("处理", "c-res"));
            table.Add(hdr);
            foreach (var row in g.Content.CheckRows)
            {
                var r = Box("check-row");
                bool current = row.Phase == g.State.Phase;
                bool reached = g.State.Phase >= row.Phase;
                var rule = RuleIds.ForPuzzle(PhaseUtil.PuzzleIndex(row.Phase));
                bool done = g.State.Rule(rule) == RuleStatus.Confirmed;
                r.EnableInClassList("cur", current);
                r.Add(L($"{"一二三四"[PhaseUtil.PuzzleIndex(row.Phase)]}　{row.Item}", "c-item"));
                r.Add(L(reached ? row.Anomaly : "", "c-anom", "f-hand-lin"));
                var res = L(done ? row.Result : "", "c-res", "f-hand-lin");
                r.Add(res);
                if (done) { var ok = L("合格", "stamp-mini"); r.Add(ok); }
                table.Add(r);
            }
            v.Add(table);

            // 三联板入口（第三项先填推断单）
            var t = g.Triad.Current;
            if (t != null && PhaseUtil.IsPuzzle(g.State.Phase))
            {
                var box = Box("triad-entry");
                if (g.State.Phase == Phase.P3 && !g.State.Has(g.Puzzles.Deduction.GivesEvidence)) box.Add(DeductionButton());
                var st = g.State.Rule(t.RuleId);
                if (st == RuleStatus.None)
                {
                    bool can = g.Triad.CanOpen(out var why);
                    var b = B(g.Triad.Ready ? "填三联板 ▶" : "三联板 ▶", () => host.Push("triad"), "btn", "triad-btn");
                    b.EnableInClassList("ready", g.Triad.Ready);
                    b.SetEnabled(can);
                    box.Add(b);
                    if (!can) box.Add(L(why, "page-faint"));
                    else if (!g.Triad.Ready) box.Add(L("证据还不齐，也可以先看看。", "page-faint"));
                }
                else if (st != RuleStatus.Confirmed)
                {
                    box.Add(B("看规则卡 ▶", () => host.Open("rulecard", t.RuleId), "btn"));
                }
                v.Add(box);
            }

            var sign = Box("sign-row");
            sign.Add(L("交班人", "page-sub"));
            sign.Add(L(g.State.Ending >= EndingStep.PullSwitch ? "周建国" : "", "f-hand-jian", "sign-name"));
            sign.Add(L("接班人", "page-sub"));
            sign.Add(L(g.State.PlayerSigned ? "林小满" : "", "f-hand-lin", "sign-name"));
            v.Add(sign);
        }

        /// <summary>
        /// 一九八三年测点示意（事实链 B-1）：晒图蓝剖面 + 老周手写。L1 箭头正对后轴承座；不写任何频率数字。
        /// 灰盒：用方框和圆圈拼出剖面，正式版换手绘扫描图。
        /// </summary>
        void Sketch1983(VisualElement v, PageDef page)
        {
            v.AddToClassList("blueprint");
            var lines = g.Content.PageText[page.Id];
            v.Add(L(lines[0], "f-hand-zhou", "bp-title"));
            var fig = Box("bp-fig");
            var housing = Box("bp-housing");          // 主轴箱外壳
            fig.Add(housing);
            var shaft = Box("bp-shaft");              // 主轴
            fig.Add(shaft);
            var bearing = Box("bp-bearing");          // 后轴承座（左）
            fig.Add(bearing);
            fig.Add(Pos(L(lines[1], "f-hand-zhou", "bp-label"), 40, 250));
            for (int i = 0; i < 3; i++) { var gear = Box("bp-gear"); gear.style.left = 400 + i * 72; gear.style.top = 92 + (i % 2) * 30; fig.Add(gear); }
            fig.Add(Pos(L(lines[2], "f-hand-zhou", "bp-label"), 430, 250));
            var arrow = Box("bp-arrow"); fig.Add(arrow); Rot(arrow, 55);   // L1 箭头：从左上（L1）斜着指向后轴承座中心
            var head = Box("bp-arrowhead"); fig.Add(head); Rot(head, 10);  // 右下角折线 = 45° 方向，再转 10° 对齐箭杆
            fig.Add(Pos(L("L1", "f-hand-zhou", "bp-l1"), 14, 8));
            fig.Add(Pos(L(lines[3], "f-hand-zhou", "bp-label", "bp-red"), 170, 6));
            v.Add(fig);
            v.Add(L(lines[4], "f-hand-zhou", "bp-note"));
            v.Add(L("周铁生", "f-hand-zhou", "bp-sig"));
            ReadableCorner(v, page);
        }

        static VisualElement Pos(VisualElement e, float x, float y) { e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; return e; }

        /// <summary>通用单据：第一行标题；“键|值”画成表格行。</summary>
        void Sheet(VisualElement v, PageDef page, string font)
        {
            v.AddToClassList(page.Kind == "printout2026" ? "print" : page.Kind == "note1983" ? "lined" : "old");
            if (!g.Content.PageText.TryGetValue(page.Id, out var lines)) { v.Add(L(page.Title, "page-head")); return; }
            v.Add(L(lines[0], "page-head", font == "f-sans" ? "f-sans-m" : "f-serif-b"));
            foreach (var l in lines.Skip(1))
            {
                var kv = l.Split('|');
                if (kv.Length == 2)
                {
                    var r = Box("kv-row");
                    r.Add(L(kv[0], "kv-k")); r.Add(L(kv[1], "kv-v", font));
                    v.Add(r);
                }
                else v.Add(L(l, font, font.StartsWith("f-hand") ? "hand-line" : "sheet-line"));
            }
            ReadableCorner(v, page);
        }

        /// <summary>读了能拿到贴页证据的页：页角红三角 + “点这页细看”；点整页 = 细看。</summary>
        void ReadableCorner(VisualElement v, PageDef page)
        {
            if (string.IsNullOrEmpty(page.GivesEvidence)) { if (!g.State.ReadPages.Contains(page.Id)) g.Ledger.ReadPage(page.Id); return; }
            if (!g.Ledger.HasNew(page.Id)) { v.Add(L("已贴　" + g.Content.Ev(page.GivesEvidence)?.Paper, "read-mark")); return; }
            v.Add(Box("corner-new", "page-corner"));
            v.Add(L("点这页细看", "f-hand-lin", "read-cta"));
            v.AddToClassList("readable");
            var pid = page.Id;
            v.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (!g.Ledger.HasNew(pid)) return;
                var ev = g.Content.Ev(g.Content.Page(pid).GivesEvidence);
                if (g.Ledger.ReadPage(pid)) g.Bus.Raise(Ev.Feedback, $"记下了：{ev.Name}（{ev.Paper}）", "ok", 1);
            });
        }

        /// <summary>“第 n 项　贴页”页属于哪一项（谜题二的贴页在“二 异响”两页里，不单设）。</summary>
        static Phase? FoundPhase(string pageId) => pageId == "found1" ? Phase.P1 : pageId == "found3" ? Phase.P3 : pageId == "found4" ? Phase.P4 : (Phase?)null;

        /// <summary>推断单入口：推断要用的词都出现了（证据齐了）就标红。</summary>
        Button DeductionButton()
        {
            var d = g.Puzzles.Deduction;
            var words = g.Puzzles.AvailableWords().ToList();
            bool ready = d.Blanks.All(x => words.Contains(x.Answer));
            var b = B(ready ? "填推断单 ▶" : "推断单 ▶", () => host.Push("deduction"), "btn", "triad-btn");
            b.EnableInClassList("ready", ready);
            return b;
        }

        /// <summary>某一项的贴页（全部证据，带一句说明）。</summary>
        void Found(VisualElement v, PageDef page)
        {
            var ph = FoundPhase(page.Id) ?? g.State.Phase;
            v.Add(L(page.Title, "page-head"));
            if (ph == Phase.P3 && g.State.Phase == Phase.P3)
            {
                var row = Box("triad-entry");
                if (!g.State.Has(g.Puzzles.Deduction.GivesEvidence)) row.Add(DeductionButton());
                else row.Add(L("推断已记下（贴页 15）。", "page-faint"));
                v.Add(row);
            }
            var area = Box("clip-area", "big");
            foreach (var e in g.Evidence.Collected(ph).Where(e => e.Paper != "")) area.Add(Clip(e, small: false));
            foreach (var e in g.Evidence.Collected(ph).Where(e => e.Paper == "")) area.Add(Clip(e, small: true));
            if (area.childCount == 0) area.Add(L("（还没有贴页）", "page-faint"));
            v.Add(area);
        }

        VisualElement Clip(EvidenceDef e, bool small)
        {
            var c = Box("clip", small ? "small" : "full", e.Grade == EvidenceGrade.Ambient ? "ambient" : "need");
            var tape = Box("clip-tape"); c.Add(tape);
            if (!string.IsNullOrEmpty(e.Paper)) c.Add(L(e.Paper, "f-hand-lin", "clip-no"));
            c.Add(L(small ? e.Short : e.Name, "clip-name"));
            if (!small) c.Add(L(e.Desc, "clip-desc"));
            if (TriadEvidence.Contains(e.Id)) c.Add(L("三联", "clip-key"));
            if (!seenClips.Contains(e.Id) && !small) { c.Add(Box("corner-new")); }
            Rot(c, ((e.Id.GetHashCode() & 0x7fffffff) % 7) - 3);   // UI-DS §5.3：−3°~+3° 随机
            if (ClipReader.TryGetValue(e.Id, out var reader))
            {
                c.AddToClassList("readable");
                c.RegisterCallback<PointerUpEvent>(_ => host.Push("reader", reader));
            }
            if (!small) seenClips.Add(e.Id);
            return c;
        }

        /// <summary>“规则”页：已经生成的经验规则卡（缩略）。</summary>
        void Rules(VisualElement v, bool second)
        {
            var ids = RuleIds.All.Where(id => g.State.Rule(id) != RuleStatus.None).ToList();
            var mine = second ? ids.Skip(2) : ids.Take(2);
            if (!second) v.Add(L("经验规则卡", "page-head"));
            foreach (var id in mine)
            {
                var card = g.Rules.Card(id);
                var st = g.State.Rule(id);
                var m = Box("mini-card");
                m.Add(L(card.Id, "f-mono", "mini-id"));
                m.Add(L(card.PlainSummary, "f-hand-lin", "mini-sum"));
                m.Add(L("来源：" + card.Owner, "mini-src"));
                m.Add(L(st == RuleStatus.Confirmed ? "合格" : st == RuleStatus.Pending ? "待验证" : "草拟", st == RuleStatus.Confirmed ? "stamp-mini" : "pending-mini"));
                if (st != RuleStatus.Confirmed) m.RegisterCallback<PointerUpEvent>(_ => host.Open("rulecard", card.Id));
                v.Add(m);
            }
            if (!second && ids.Count == 0) v.Add(L("（还没有规则卡。三联一致，就会生成一张。）", "page-faint"));
        }
    }
}

// 《最后一次交班》Demo α —— 文本预算（主项目文档 §7：全 Demo ≤2500 字；周建国 ≤10 句）
// 统计口径：ContentDB 中玩家主层会读到的中文字符（汉字，不含标点、数字、字母）。
//   计入：台词、交班本页面、阅读器、证据名与说明、三联主层文字、交互字幕、规则卡一句话摘要、推断单、本班点检行、记忆字幕、开场与献词、阶段开头的衡日志。
//   不计入：提示和三联填错反馈（只在卡住或填错时出现；2026-10-11 策划评审时选定这个口径）、
//           三联“专业说法”和规则卡六字段（第二层信息，默认不显示）、代码里的系统日志与按钮文字（另行统计）。
//   提示和填错反馈照样统计并打印出来，只是不算进 2500。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using NUnit.Framework;

namespace LSB.Tests
{
    public class TextBudgetTests
    {
        public static int Han(string s) => string.IsNullOrEmpty(s) ? 0 : s.Count(ch => ch >= 0x4E00 && ch <= 0x9FFF || ch == '〇');

        public static Dictionary<string, int> Count(ContentDB c)
        {
            var d = new Dictionary<string, int>();
            void Add(string k, IEnumerable<string> xs) => d[k] = xs.Sum(Han);
            Add("台词", c.Lines.Values.Select(l => l.Text));
            Add("交班本页面", c.PageText.Values.SelectMany(x => x));
            Add("阅读器", c.ReaderText.Values.SelectMany(x => x));
            Add("证据", c.Evidence.SelectMany(e => new[] { e.Name, e.Desc }));
            Add("三联主层", c.Triads.SelectMany(t => new[] { t.Title }.Concat(t.Columns.SelectMany(col => new[] { col.Title }.Concat(col.Options.Select(o => o.Text))))));
            Add("三联反馈", c.Triads.SelectMany(t => t.Columns.SelectMany(col => col.Options.SelectMany(o => new[] { o.FbLog, o.FbCaption }))));
            Add("提示", c.Hints.SelectMany(h => h.Levels));
            Add("交互字幕", c.Rules.SelectMany(r => r.Effects).Where(e => e.Kind == EffectKind.Caption || e.Kind == EffectKind.HengLog).Select(e => e.A).Distinct());
            Add("规则卡摘要", c.RuleCards.Values.Select(r => r.PlainSummary));
            Add("推断单", c.Deduction.Segments.Concat(c.Deduction.WordBank));
            Add("点检行", c.CheckRows.SelectMany(r => new[] { r.Item, r.Anomaly, r.Result }));
            Add("记忆字幕", c.Memories.Values.SelectMany(m => m.Captions));
            Add("开场献词", new[] { ContentDB.IntroLine, ContentDB.Dedication });
            Add("衡阶段日志", c.PhaseStartLogs.Values.SelectMany(x => x).Select(h => h.Text));
            return d;
        }

        public static readonly string[] NotCounted = { "提示", "三联反馈" };

        [Test]
        public void ReportTextBudget()
        {
            var d = Count(new ContentDB());
            int all = d.Values.Sum();
            int counted = d.Where(kv => !NotCounted.Contains(kv.Key)).Sum(kv => kv.Value);
            TestContext.WriteLine("文本预算（汉字数）：" + string.Join("，", d.Select(kv => kv.Key + " " + kv.Value)) + $"；计入预算 {counted} / 2500（不含提示与填错反馈）；全部文字 {all}");
            Assert.LessOrEqual(counted, 2500, "全 Demo 主层文本（不含提示与填错反馈）超过 2500 字");
        }
    }
}

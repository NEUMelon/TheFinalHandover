// 《最后一次交班》Demo α —— 内容库入口 + 交班本页面文字
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    /// <summary>本点检行（交班本“本班”页，UI 原型 pageThisShift 的数据化版本）。</summary>
    public sealed class CheckRow
    {
        public Phase Phase;
        public string Item;      // 点检项
        public string Anomaly;   // 异常（到了这一项才写上）
        public string Result;    // 处理（规则确认后写上）
    }

    public sealed class MemoryDef
    {
        public string Id;
        public string Year;          // 显影里的年份字样
        public string[] Captions;    // 依次出现的字幕（先有声音，后有画面）
        public bool SilenceBefore;   // 显影前全静音 2 秒（只有凹痕显影，主项目文档 §5.3）
    }

    public sealed partial class ContentDB
    {
        public readonly Dictionary<string, string[]> PageText = new Dictionary<string, string[]>();
        public readonly Dictionary<string, string[]> ReaderText = new Dictionary<string, string[]>();
        public readonly List<CheckRow> CheckRows = new List<CheckRow>();
        public readonly Dictionary<string, MemoryDef> Memories = new Dictionary<string, MemoryDef>();
        public readonly List<string> TapeScript = new List<string>();

        /// <summary>开场黑屏手写字（主项目文档 §5.3 0:00–0:40）。</summary>
        public const string IntroLine = "一九五六—二〇二六，最后一班，交给你了。";
        public const string Dedication = "谨以此作献给所有把手艺交出去的人。";

        public ContentDB()
        {
            BuildWorld();
            BuildStory();
            BuildPuzzles();
            BuildPages();
        }

        public CheckRow Row(Phase p) => CheckRows.FirstOrDefault(r => r.Phase == p);

        void BuildPages()
        {
            CheckRows.Add(new CheckRow { Phase = Phase.P1, Item = "LT01 主轴后轴承", Anomaly = "衡：右边听不清轴承", Result = "改在左边 L1 听（老周的点）" });
            CheckRows.Add(new CheckRow { Phase = Phase.P2, Item = "LT01 主轴异响", Anomaly = "衡：齿轮磨损，要停车换", Result = "后轴承早期毛病，计划检修，不停车" });
            CheckRows.Add(new CheckRow { Phase = Phase.P3, Item = "LT01 护罩 / 启动条件", Anomaly = "衡：新程序少一个启动条件", Result = "护罩合上，主轴才许启动" });
            CheckRows.Add(new CheckRow { Phase = Phase.P4, Item = "CR01 天车试吊", Anomaly = "停车吊钩晃，缸里水洒", Result = "第二段收车，早半拍" });

            // 上一班（建国体手写）
            PageText["prev"] = new[] { "小林：", "LT01 照常开。", "老周的东西在更衣室，你看看。", "护罩，开车前看一眼。", "建国　十六点" };
            // 1983 测点示意（老周体手写 + 晒图蓝剖面；B-1：L1 箭头指后轴承座）
            PageText["sketch1983"] = new[] { "LT01 主轴箱　测点示意　一九八三", "后轴承座", "变速齿轮", "L1 听后轴承", "L1，听这里。" };
            // 1983 设备记录（W-01：只写位置与听感，不出现频率数字）
            PageText["note1983"] = new[] { "设备记录　LT01　一九八三年", "主轴箱 听音 定点", "左端 L1：样冲眼，刷漆圈", "咔哒在左，是轴承；", "在右，是齿轮。", "声尖、脆，像表针。", "周铁生" };
            // 2026 复测单（B-2：频带数值来自 2026 年林小满复测）
            PageText["printout2026"] = new[] { "LT01 主轴箱振动复测（打印单）", "测点|L1 左端 / R1 右端", "复测|2026　林小满", "本机结构共振带|约 2–4 kHz", "说明|本机实测值，不作通用规律", "老周记的是位置和声音，频带是今年复测的。" };
            // 1987 三件（B-4、B-5）
            PageText["brief1987"] = new[] { "险情简报", "一九八七年九月十四日", "LT01，学徒一名，护罩未合即开车，工件飞出。", "险情，无人员受伤。", "三号车间" };
            PageText["ticket1987"] = new[] { "学徒工票", "姓名|周建国", "日期|一九八七年九月十四日", "工位|LT01", "工序|车外圆" };
            PageText["change1987"] = new[] { "安全整改变更单（副本）", "设备|LT01", "内容|加装护罩联锁：护罩不合，主轴不能启动", "提出|周铁生", "施工|车间电工", "签收|设备科", "副本。原件在设备科。" };
            PageText["tapeLabel"] = new[] { "盒式磁带", "广播站　孙桂兰" };
            PageText["handover"] = new[] { "交班", "最后一页", "交班人", "接班人" };

            ReaderText["photo"] = new[] { "夹在工牌套中的工作照", "老周左手持听针，伸向主轴箱左端。" };
            ReaderText["photo1956"] = new[] { "建线合影", "一九五六年　三号车间" };
            ReaderText["compare"] = new[] { "衡的比对单　LT01 主轴启动条件", "条件|旧回路　新程序", "按启动|有　有", "护罩合上|有　缺", "图纸：2025 年数字化 Rev.A" };
            ReaderText["checklist"] = new[] { "点检单　LT01", "主轴箱油位", "导轨润滑", "开车先看护罩。" };

            Memories["m1987"] = new MemoryDef { Id = "m1987", Year = "一九八七", SilenceBefore = true, Captions = new[] { "〔金属撞击声〕", "〔一声喊〕", "〔一个年轻人被拽开〕" } };
            Memories["m1998"] = new MemoryDef { Id = "m1998", Year = "一九九八", SilenceBefore = false, Captions = new[] { "〔天车铃：叮——〕", "〔一九九八年　最后一次吊运〕", "〔铃跟着钩子，一下，一下〕" } };

            TapeScript.Add("S3"); TapeScript.Add("S1"); TapeScript.Add("S2");
        }
    }
}

// 《最后一次交班》Demo α —— 内容数据结构
// 所有文案、坐标和谜题答案都放在 ContentDB 里（数据驱动），系统代码不写死任何一个谜题的文字。
using System;
using System.Collections.Generic;

namespace LSB.Core
{
    /// <summary>固定节点（白盒规格 §5）。</summary>
    public sealed class NodeDef
    {
        public string Id;            // N1..N10
        public string Name;          // 交班桌
        public V3 Pos;               // 相机位置（规格坐标）
        public float Yaw;            // 0° = 北，顺时针
        public float Pitch;          // 正值 = 俯视，负值 = 仰视
        public float FocalMm;        // 等效焦距（36mm 画幅）
        public string[] Neighbors;   // 可达节点
        public string[] CloseUps;    // 从这里能进入的设备特写
        public string Zone;          // 机加工区 / 控制间与交班区 / 更衣休息室
    }

    /// <summary>设备特写（白盒规格 §6），可在限定范围内旋转。</summary>
    public sealed class CloseUpDef
    {
        public string Id;            // CU1..CU5
        public string Name;
        public V3 Pivot;             // 旋转中心（被看物体）
        public float Distance;       // 相机到中心的距离
        public float BaseYaw, BasePitch;
        public float YawRange;       // ±
        public float PitchMin, PitchMax;
        public float FocalMm;
        public string[] EntryNodes;
        public string Panel;         // 进入后打开的 2D 面板（可空）：diag / crane / tape / cabinet
    }

    /// <summary>可交互物（白盒规格 §7，26 个）。</summary>
    public sealed class InteractableDef
    {
        public int SpecNo;           // 白盒规格 §7 的序号（1–26）
        public string Id;
        public string Name;          // 悬停时显示的名字（玩家语言，≤8 字）
        public V3 Pos;               // 中心（规格坐标）
        public V3 Size;              // 碰撞盒尺寸
        public string[] VisibleFrom; // 只有在这些节点 / 特写里才能点（避免隔着车间点到远处的东西）
        public string Puzzle;        // P1..P4 / 结局 / 氛围
        public bool PuzzleRelevant;  // 是否参与谜题（统计用：规格要求约 25 个，其中 12 个参与谜题）
    }

    public sealed class EvidenceDef
    {
        public string Id;            // E1_1 …（内部编号，玩家看不到）
        public Phase Phase;
        public string Name;          // 证据名
        public string Short;         // 交班本夹页上的短名
        public string Paper;         // 玩家看到的编号，如“贴页 3”
        public string Desc;          // 一句说明（玩家语言）
        public EvidenceGrade Grade;
    }

    public enum EffectKind
    {
        GiveEvidence, SetFlag, ClearFlag, Say, Caption, HengLog, OpenView, MemoryDevelop, Sfx, EnterCloseUp, SetTool,
    }

    public sealed class Effect
    {
        public EffectKind Kind;
        public string A;             // 主参数（证据 ID / 旗标 / 台词 ID / 文案 / 视图 ID …）
        public string B;             // 次参数（如 HengLog 的模块名、Caption 的说话人）
        public static Effect Ev(string id) => new Effect { Kind = EffectKind.GiveEvidence, A = id };
        public static Effect Flag(string id) => new Effect { Kind = EffectKind.SetFlag, A = id };
        public static Effect Unflag(string id) => new Effect { Kind = EffectKind.ClearFlag, A = id };
        public static Effect Say(string lineId) => new Effect { Kind = EffectKind.Say, A = lineId };
        public static Effect Cap(string text) => new Effect { Kind = EffectKind.Caption, A = text };
        public static Effect Log(string module, string text) => new Effect { Kind = EffectKind.HengLog, A = text, B = module };
        public static Effect View(string viewId, string arg = null) => new Effect { Kind = EffectKind.OpenView, A = viewId, B = arg };
        public static Effect Memory(string id) => new Effect { Kind = EffectKind.MemoryDevelop, A = id };
        public static Effect Sfx(string id) => new Effect { Kind = EffectKind.Sfx, A = id };
        public static Effect CloseUp(string id) => new Effect { Kind = EffectKind.EnterCloseUp, A = id };
    }

    /// <summary>交互规则：某物 + 某工具 + 条件 → 一串效果。按 ContentDB 中的顺序匹配第一条。</summary>
    public sealed class InteractionRule
    {
        public string Target;                     // 交互物 ID
        public ToolId Tool = ToolId.None;         // None = 空手；Any 见 AnyTool
        public bool AnyTool;                      // 任意工具（含空手）都匹配
        public Phase MinPhase = Phase.P1;
        public Phase MaxPhase = Phase.Ending;
        public string[] RequireFlags = Array.Empty<string>();
        public string[] RequireEvidence = Array.Empty<string>();
        public string[] ForbidFlags = Array.Empty<string>();
        public string[] ForbidEvidence = Array.Empty<string>();
        public bool Valid = true;                 // 是否算“有效操作”（重置提示计时）
        public Effect[] Effects = Array.Empty<Effect>();
    }

    /// <summary>三联板一个选项。Text 是玩家主层语言；Pro 是第二层专业说法（规则卡、技术详情里显示）。</summary>
    public sealed class TriadOption
    {
        public string Id;
        public string Text;
        public string Pro;
        public string[] Needs = Array.Empty<string>();   // 需要的证据；缺任意一条则显示“待取证”
        public bool Ok;
        public string FbModule;                          // 错误反馈：衡日志模块
        public string FbLog;                             // 错误反馈：衡日志正文
        public string FbSim;                             // 错误反馈：仿真声音 ID（可空）
        public string FbCaption;                         // 错误反馈：字幕
    }

    public sealed class TriadColumn
    {
        public string Key;      // phen / exp / data
        public string No;       // 一联
        public string Title;    // 异常现象
        public TriadOption[] Options;
    }

    public sealed class TriadDef
    {
        public Phase Phase;
        public string RuleId;
        public string Title;              // 点检第二项　LT01 主轴异响
        public TriadColumn[] Columns;
        public string[] Prerequisites = Array.Empty<string>(); // 打开三联板之前必须已有的证据或旗标（如谜题三须先完成推断）
    }

    /// <summary>经验规则卡（UI-DS §5.4 六字段，字段名和顺序不得增删改）。</summary>
    public sealed class RuleCardDef
    {
        public string Id;
        public string Type;        // 监测位置 / 诊断 / 安全联锁 / 控制时序
        public string Owner;       // 以谁命名
        public string Source;
        public string Point;
        public string Feature;
        public string[] Binds;
        public string PlainSummary;   // 主层一句话：“以后在左边听后轴承。”（玩家语言）
    }

    public sealed class LineDef
    {
        public string Id;
        public string Speaker;     // 周建国 / 孙桂兰 / 〔环境〕
        public string Via;         // 对讲机 / 磁带 / 入画
        public string Text;
    }

    /// <summary>一个“阶段”内的三级提示（UI-DS §5.8：90 / 180 / 300 秒）。</summary>
    public sealed class HintSet
    {
        public Phase Phase;
        public string Stage;            // 由 PuzzleProgress.Stage() 计算出的当前卡点
        public string[] Levels;         // 3 条：暗示 → 指向 → 半解
        public string PointsAt;         // 二、三级指向的对象（交互物 / 节点 / 页面 ID），给 Unity 画箭头
    }

    /// <summary>交班本贴页 / 页面。</summary>
    public sealed class PageDef
    {
        public string Id;
        public Phase MinPhase;       // 到这一项才开放阅读
        public string Title;
        public string Kind;          // 见 LedgerView：shift_prev / shift_this / sketch1983 / note1983 / printout2026 / brief1987 / ticket1987 / change1987 / checklist / tape / handover_last …
        public string GivesEvidence; // 阅读即获得的证据（可空）
    }

    public sealed class SpreadDef
    {
        public string Id;
        public string Tab;           // 页签文字
        public Phase MinPhase;
        public string Left, Right;   // PageDef.Id
    }

    /// <summary>Golden Idol 式推断（谜题三）：一句话里有若干空格，从词库拖词填空。</summary>
    public sealed class DeductionBlank
    {
        public string Key;
        public string Answer;          // 正确词
        public string[] FromEvidence;  // 支撑这一空的证据（用于提示与“词库里出现这个词”的前提）
    }

    public sealed class DeductionDef
    {
        public Phase Phase;
        public string[] Segments;      // 文本片段，与 Blanks 交替：Seg0 [B0] Seg1 [B1] … SegN
        public DeductionBlank[] Blanks;
        public string[] WordBank;      // 全部词（正确词 + 干扰词）
        public Dictionary<string, string[]> WordNeeds = new Dictionary<string, string[]>(); // 词 → 出现前需要的证据
        public string GivesEvidence;   // 推断完成后获得的“推断”证据
    }

    public sealed class TimeAnchor
    {
        public Phase Phase;
        public int StartMinute;   // 剧情时刻（从 0:00 起的分钟数）
        public int EndMinute;     // 本阶段时钟上限
        public string Card;       // 过场卡文字：“十七点三十分　点检第二项”
        public string Light;      // 灯光预设：day1600 / dusk1730 / night / ending
    }

    public sealed class HengLine
    {
        public string Module;     // MON / DIAG / TWIN / CR01 / SYS / RULE
        public string Text;
        public string Kind;       // "" / warn / ok / conflict
        public HengLine(string m, string t, string k = "") { Module = m; Text = t; Kind = k; }
    }
}

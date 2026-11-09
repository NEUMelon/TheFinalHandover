// 《最后一次交班》Demo α —— 逻辑层基础类型
// LSB.Core 程序集不引用 UnityEngine（asmdef: noEngineReferences），可以在 Unity 外用 dotnet 直接编译和跑单元测试。
// 坐标一律使用白盒规格（02_车间白盒规格_v0.1 §1）：单位米，X 向东，Y 向上，Z 向北；原点为车间西南角室内地面。
// Unity 是左手系 Y 向上，把规格 X→x、Y→y、Z→z 直接对应即可（北 = +z，东 = +x，Yaw 0° = 北，顺时针为正）。
using System;
using System.Collections.Generic;

namespace LSB.Core
{
    /// <summary>主流程阶段。顺序即游戏顺序（主项目文档 v1.1 §5.3），不得调换。</summary>
    public enum Phase { Title = 0, Intro = 1, P1 = 2, P2 = 3, P3 = 4, P4 = 5, Ending = 6, Done = 7 }

    /// <summary>经验规则卡 Status（UI-DS §5.4）。</summary>
    public enum RuleStatus { None = 0, Draft = 1, Pending = 2, Confirmed = 3, Unnamed = 4 }

    /// <summary>工具箱四件工具（UI-DS §5.10）。</summary>
    public enum ToolId { None = 0, Probe = 1, Torch = 2, Caliper = 3, Driver = 4 }

    /// <summary>证据分级（事实链 §0）：N 必要 / G 引导 / A 氛围。</summary>
    public enum EvidenceGrade { Necessary, Guide, Ambient }

    /// <summary>结局演出步骤（主项目文档 §5.3 12:30–15:00 + 用户阶段 2 消息 2E 的顺序）。</summary>
    public enum EndingStep
    {
        None = 0,
        HandoverRequest = 1,   // 衡请求原班长确认交接
        JianguoSign = 2,       // 周建国一只手入画，在交班本左页签字
        PullSwitch = 3,        // 玩家按交接单拉下总闸
        LightsOut = 4,         // 主轴降速，灯逐排熄灭（由近及远）
        Silence = 5,           // 静默 5 秒
        Siren = 6,             // 汽笛长鸣
        HengBoot = 7,          // 冷白系统启动
        RulesRoll = 8,         // 四条经验规则依次出现
        UnknownSignal = 9,     // 未登记微弱信号，申请命名
        NameAndSign = 10,      // 玩家命名 + 在接班人栏签名
        Credits = 11,          // 远景背影、献词
        Finished = 12,
    }

    /// <summary>不依赖 Unity 的三维向量，只用来存规格坐标。</summary>
    [Serializable]
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public float Length => (float)Math.Sqrt(x * x + y * y + z * z);
        public override string ToString() => $"({x:0.##}, {y:0.##}, {z:0.##})";
    }

    /// <summary>经验规则类型与 Rule ID 常量。Rule ID 是 provenance 标签，不是 PLC 变量（主项目文档 §5.5）。</summary>
    public static class RuleIds
    {
        public const string ZhouLeft = "ZHOU_LEFT";
        public const string ZhouClick = "ZHOU_CLICK";
        public const string JianguoGuard = "JIANGUO_GUARD";
        public const string SunHalfbeat = "SUN_HALFBEAT";
        public static readonly string[] All = { ZhouLeft, ZhouClick, JianguoGuard, SunHalfbeat };

        /// <summary>第 n 个谜题（0 起）对应的规则。</summary>
        public static string ForPuzzle(int index) => All[index];

        public static int IndexOf(string ruleId) => Array.IndexOf(All, ruleId);
    }

    public static class PhaseUtil
    {
        public static bool IsPuzzle(Phase p) => p >= Phase.P1 && p <= Phase.P4;
        /// <summary>P1→0 … P4→3；非谜题阶段返回 -1。</summary>
        public static int PuzzleIndex(Phase p) => IsPuzzle(p) ? (int)p - (int)Phase.P1 : -1;
        public static Phase FromPuzzleIndex(int i) => (Phase)((int)Phase.P1 + i);
    }

    /// <summary>键值小工具：Dictionary 取值带默认值。</summary>
    internal static class DictExt
    {
        public static TV Get<TK, TV>(this Dictionary<TK, TV> d, TK k, TV def = default) => d.TryGetValue(k, out var v) ? v : def;
    }
}

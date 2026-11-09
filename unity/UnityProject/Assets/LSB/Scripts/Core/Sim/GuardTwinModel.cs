// 《最后一次交班》Demo α —— 谜题三：LT01 主轴启动的离线数字孪生（极简）
// 玩家做的不是“写一段 PLC 的 if”，而是把一条丢掉的安全要求补回来：
//   要求：护罩闭合反馈接入安全相关控制回路，作为主轴启动许可（来源：1987 年安全整改变更单，周铁生提出）。
// 孪生只回答一件事：在给定的护罩状态和启动指令下，主轴会不会转。补回要求前后各试一次，结果真的不一样。
// ⚠ 这是教学用的逻辑示意，不是真实安全回路设计，也不代表任何安全等级（SIL / PL）评估。
namespace LSB.Core
{
    public sealed class TwinCase
    {
        public string Id;            // open_start / closed_start
        public string Name;          // 玩家语言
        public bool GuardClosed;
        public bool Start = true;
        public bool ExpectRun;       // 安全要求下应当的结果
    }

    public sealed class TwinRun
    {
        public TwinCase Case;
        public bool Restored;        // 是否已补回启动条件
        public bool SpindleRuns;
        public bool Pass;            // 结果是否符合安全要求
        public string Log;
    }

    public static class GuardTwinModel
    {
        public static readonly TwinCase[] Cases =
        {
            new TwinCase { Id = "open_start", Name = "护罩开着，按启动", GuardClosed = false, ExpectRun = false },
            new TwinCase { Id = "closed_start", Name = "护罩合上，按启动", GuardClosed = true, ExpectRun = true },
        };

        public static TwinCase Case(string id) => System.Array.Find(Cases, c => c.Id == id);

        /// <summary>新程序（2025 年数字化图纸版）：没有护罩条件，只看启动指令。补回后：启动许可 = 护罩闭合反馈。</summary>
        public static bool SpindleRuns(bool restored, bool guardClosed, bool start) => start && (!restored || guardClosed);

        public static TwinRun Run(string caseId, bool restored)
        {
            var c = Case(caseId);
            if (c == null) return null;
            bool runs = SpindleRuns(restored, c.GuardClosed, c.Start);
            bool pass = runs == c.ExpectRun;
            string log = $"{(restored ? "补回启动条件后" : "现行新程序")}：{c.Name} → 主轴{(runs ? "启动" : "不启动")}。" + (pass ? "" : "不符合安全要求。");
            return new TwinRun { Case = c, Restored = restored, SpindleRuns = runs, Pass = pass, Log = log };
        }
    }
}

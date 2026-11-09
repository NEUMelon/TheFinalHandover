// 《最后一次交班》Demo α —— 结局：第四条规则确认后
// 顺序（用户阶段 2 消息 2E）：周建国确认交接 → 签字 → 玩家按交接单拉总闸 → 灯逐排熄灭 → 静默 → 汽笛
//   → 衡冷白启动 → 四条规则依次出现 → 新的未登记信号 → 玩家命名 + 在接班人栏签名 → 献词。
// 自动步骤由 Tick 按 StepSeconds 推进（开发菜单 / 自动玩家也可以直接调 Advance()）；需要玩家动手的两步（拉闸、命名 + 签名）停在原地等玩家。
using System.Text;

namespace LSB.Core
{
    public sealed class EndingSystem
    {
        readonly GameSession g;
        public EndingSystem(GameSession g) { this.g = g; }

        GameState S => g.State;

        /// <summary>每一步的建议停留秒数（表现层可以按演出改，逻辑上只要求顺序）。</summary>
        public static float StepSeconds(EndingStep s)
        {
            switch (s)
            {
                case EndingStep.HandoverRequest: return 2.5f;
                case EndingStep.JianguoSign: return 5.0f;
                case EndingStep.LightsOut: return 6.0f;     // 6 排灯，由近及远，每排约 0.8 秒
                case EndingStep.Silence: return 5.0f;       // 主项目文档 §5.3：静默 5 秒
                case EndingStep.Siren: return 4.0f;
                case EndingStep.HengBoot: return 3.0f;
                case EndingStep.RulesRoll: return 4.0f;
                case EndingStep.UnknownSignal: return 3.0f;
                case EndingStep.Credits: return 6.0f;
                default: return 0f;
            }
        }

        /// <summary>这一步是否在等玩家操作（Advance 不会越过它）。</summary>
        public static bool WaitsForPlayer(EndingStep s) => s == EndingStep.PullSwitch || s == EndingStep.NameAndSign;

        public void Begin()
        {
            if (S.Ending != EndingStep.None) return;
            Go(EndingStep.HandoverRequest);
        }

        float timer;

        /// <summary>
        /// 演出节奏：自动步骤停够 StepSeconds 就往下走（GameSession.Tick 每帧调用）。
        /// 等玩家的两步（拉闸、命名 + 签名）不会自己过去。这样从新游戏开始不靠开发菜单也能走完结局。
        /// </summary>
        public void Tick(float dt)
        {
            if (S.Phase != Phase.Ending) return;
            var st = S.Ending;
            if (st == EndingStep.None || st == EndingStep.Finished || WaitsForPlayer(st)) { timer = 0; return; }
            timer += dt;
            if (timer >= StepSeconds(st)) { timer = 0; Advance(); }
        }

        /// <summary>过场中（拉闸以后到命名之前、献词）：世界里不能点，HUD 收起。</summary>
        public bool Cinematic => S.Phase == Phase.Ending && (S.Ending >= EndingStep.LightsOut && S.Ending <= EndingStep.UnknownSignal || S.Ending >= EndingStep.Credits);

        /// <summary>推进一个自动步骤。返回是否推进了。</summary>
        public bool Advance()
        {
            if (S.Phase != Phase.Ending) return false;
            switch (S.Ending)
            {
                case EndingStep.HandoverRequest:
                    S.ClockMinute = 23 * 60 + 50;
                    // 白盒规格 §8 结局第 1 步：镜头移到 N9（控制间门口），交班本在那里的工作台上〔派生〕
                    g.Nav.MoveTo("N9", true);
                    Go(EndingStep.JianguoSign);
                    g.Dialogue.Say("J7");
                    g.Log("SYS", "原班长签字确认。按交接单停机切换。", "ok");
                    g.Dialogue.Say("J8");
                    g.SetFlag("switch_authorized");
                    return true;
                case EndingStep.JianguoSign:
                    Go(EndingStep.PullSwitch);
                    return true;
                case EndingStep.LightsOut: Go(EndingStep.Silence); return true;
                case EndingStep.Silence: Go(EndingStep.Siren); g.Sfx("siren"); g.Caption("〔汽笛长鸣〕"); return true;
                case EndingStep.Siren:
                    S.ClockMinute = 23 * 60 + 56;
                    Go(EndingStep.HengBoot);
                    g.Log("SYS", $"衡启动。交接 {S.LampsOff}/4。");
                    return true;
                case EndingStep.HengBoot:
                    Go(EndingStep.RulesRoll);
                    foreach (var id in RuleIds.All) g.Log("RULE", id, "ok");
                    return true;
                case EndingStep.RulesRoll:
                    S.ClockMinute = 23 * 60 + 57;
                    Go(EndingStep.UnknownSignal);
                    g.Log("MON", "检测到未登记微弱信号（主轴尾端）。", "warn");
                    g.Log("RULE", "无法按 ZHOU_CLICK 规则归类。申请命名。");
                    g.Log("SYS", "等级：四级 → 待师傅评定。");
                    return true;
                case EndingStep.UnknownSignal: Go(EndingStep.NameAndSign); return true;
                case EndingStep.Credits:
                    Go(EndingStep.Finished);
                    g.Flow.BeginDone();
                    return true;
            }
            return false;
        }

        /// <summary>玩家按交接单拉下总闸（表现层：按住手柄往下拖）。</summary>
        public bool PullSwitch()
        {
            if (S.Phase != Phase.Ending || S.Ending != EndingStep.PullSwitch) return false;
            S.ClockMinute = 23 * 60 + 51;
            g.ValidAction(0);
            g.Sfx("main_switch");
            g.Log("SYS", "总闸断开。旧回路停止。");
            Go(EndingStep.LightsOut);
            return true;
        }

        /// <summary>新规则命名：只收 A–Z、0–9、下划线，最多 20 字符（UI-DS §5.4 ID 规范）。</summary>
        public static string NormalizeRuleName(string raw)
        {
            var sb = new StringBuilder();
            foreach (var ch in (raw ?? "").ToUpperInvariant())
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_') { sb.Append(ch); if (sb.Length >= 20) break; }
            return sb.ToString();
        }

        public bool NameRule(string raw)
        {
            if (S.Phase != Phase.Ending || S.Ending != EndingStep.NameAndSign) return false;
            var name = NormalizeRuleName(raw);
            if (name.Length == 0) return false;
            if (System.Array.IndexOf(RuleIds.All, name) >= 0) { g.Caption("这个名字已经有了。"); return false; }
            S.ClockMinute = 23 * 60 + 58;
            S.PlayerRuleName = name;
            g.Log("RULE", $"新规则命名：{name}。待师傅评定。");
            g.Playtest.Record("rule_named", name);
            g.Hints.OnProgress();
            TryFinish();
            return true;
        }

        /// <summary>接班人签名（表现层：手写板上画过即可；签名本身不进存档）。</summary>
        public bool Sign()
        {
            if (S.Phase != Phase.Ending || S.Ending != EndingStep.NameAndSign || S.PlayerSigned) return false;
            S.ClockMinute = 23 * 60 + 58;
            S.PlayerSigned = true;
            g.Sfx("stamp");
            g.Log("SYS", "接班人 林小满 签名已记录。");
            g.Playtest.Record("signed");
            TryFinish();
            return true;
        }

        void TryFinish()
        {
            if (S.PlayerSigned && !string.IsNullOrEmpty(S.PlayerRuleName)) Go(EndingStep.Credits);
            else if (S.PlayerSigned) g.Caption("衡在等这条新规则的名字。");
            else g.Caption("在接班人栏签下名字。");
        }

        void Go(EndingStep s)
        {
            timer = 0;
            S.Ending = s;
            g.Bus.Raise(Ev.EndingStepChanged, s.ToString(), null, (int)s);
            g.Bus.Raise(Ev.ClockChanged, S.Clock, g.Timeline.LightPreset, S.ClockMinute);
            g.Playtest.Record("ending_step", s.ToString());
        }
    }
}

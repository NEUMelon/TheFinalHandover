// 《最后一次交班》Demo α —— 四个谜题里的小玩法（交互规则管不了的部分）
// 谜题一：左右听音对比 → E1_5
// 谜题二：L1 时频图框选 → E2_1；R1 边频指示判读 → E2_2；连续 3 次判偏 → 页边出提示
// 谜题三：推断单填空（Golden Idol 式）→ E3_INF；数字孪生离线验证（补回启动条件，两种情况都试）
// 谜题四：首次试吊（水洒）→ E4_1；磁带 → E4_2 / E4_3 / E4_4；调第二段收车时刻 + 试吊 → E4_TUNE
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed class MiniResult
    {
        public bool Ok;
        public string Message;
        public static MiniResult No(string msg) => new MiniResult { Ok = false, Message = msg };
        public static MiniResult Yes(string msg) => new MiniResult { Ok = true, Message = msg };
    }

    public sealed class DeductionCheck
    {
        public bool Complete;        // 每个空都填了
        public bool Correct;
        public int Wrong;
        public List<string> WrongKeys = new List<string>();
        public string Message;
    }

    public sealed class PuzzleSystem
    {
        readonly GameSession g;
        public PuzzleSystem(GameSession g) { this.g = g; }

        GameState S => g.State;

        /// <summary>听音诊断当前测点（L1 / R1）。只是界面状态，不进存档。</summary>
        public string DiagPoint { get; private set; }
        public int Deviations { get; private set; }
        public CraneTrial LastTrial { get; private set; }

        // =====================================================================
        // 系统钩子
        // =====================================================================
        public void OnPhaseStart(Phase p)
        {
            Deviations = 0;
            DiagPoint = null;
            switch (p)
            {
                case Phase.P2:
                    g.Caption("〔LT01：嗡声里，隐约一下一下的“咔哒”〕");
                    break;
                case Phase.P3:
                    g.Evidence.Give("E3_1");
                    g.Dialogue.Say("J4");
                    break;
                case Phase.P4:
                    S.CraneT2 = 1f;
                    LastTrial = null;
                    break;
            }
        }

        public void OnCloseUp(string cuId)
        {
            if (cuId == "CU4" && S.Phase == Phase.P4)
            {
                g.OpenView("crane");
                if (!S.Has("E4_1")) FirstTrial();
            }
        }

        public void OnViewOpened(string view, string arg)
        {
            if (view == "diag" && (arg == "L1" || arg == "R1")) Listen(arg);
            if (view == "mes" && S.Phase == Phase.P2) g.Evidence.Give("E2_4");
            if (view == "tape" && S.Phase == Phase.P4) g.SetFlag("tape_loaded");
        }

        public void OnEvidence(string id)
        {
            switch (id)
            {
                case "E4_1": g.Dialogue.Say("J6"); break;
            }
            CheckTriadReady();
        }

        /// <summary>证据齐了，提醒一次去填三联板（交班本“本班”页）。</summary>
        public void CheckTriadReady()
        {
            var flag = "triad_ready_" + S.Phase;
            if (S.Flag(flag) || !g.Triad.Ready) return;
            g.SetFlag(flag);
            g.Caption("证据齐了。翻开交班本，填三联板。");
            g.Playtest.Record("triad_ready", S.Phase.ToString());
        }

        /// <summary>规则进入“待验证”时，衡按这一条规则做的复核（衡日志第二层信息）。</summary>
        public void OnRulePending(string ruleId)
        {
            switch (ruleId)
            {
                case RuleIds.ZhouLeft:
                    g.Log("TWIN", "对比复核：L1 轴承成分 0.72，R1 0.06。监测位置改为 L1。", "ok"); break;
                case RuleIds.ZhouClick:
                    g.Log("TWIN", "按规则复算：L1 周期冲击 → 后轴承；R1 边频 0.18 < 0.35 → 齿轮正常。", "ok"); break;
                case RuleIds.JianguoGuard:
                    g.Log("TWIN", "离线验证通过：护罩开着 → 不启动；护罩合上 → 启动。", "ok"); break;
                case RuleIds.SunHalfbeat:
                    var six = CraneZvModel.Simulate(6f, 0.5f);
                    g.Log("TWIN", "绳长改为 6.0m 复算：周期估计 " + six.Period.ToString("0.0") + "s，第二段跟着放在半拍，残余摆动 " + (six.Residual * 100).ToString("0") + "%。", "ok"); break;
            }
        }

        // =====================================================================
        // 谜题一 / 二：听音诊断
        // =====================================================================
        public void Listen(string point)
        {
            if (point != "L1" && point != "R1") return;
            DiagPoint = point;
            if (S.Phase == Phase.P1)
            {
                if (!S.Flag("heard_" + point))
                {
                    g.SetFlag("heard_" + point);
                    g.Log("MON", point == "L1"
                        ? $"L1（左端）：轴承成分 {SpectroModel.BearingShare("L1"):0.00}，齿轮 {SpectroModel.GearShare("L1"):0.00}。"
                        : $"R1（右端）：轴承成分 {SpectroModel.BearingShare("R1"):0.00}，齿轮 {SpectroModel.GearShare("R1"):0.00}。");
                }
            }
            g.Sfx("loop_" + point);
        }

        /// <summary>谜题一：在衡的平板上选“哪边听得清”。</summary>
        public MiniResult CompareChoose(string side)
        {
            if (S.Phase != Phase.P1) return MiniResult.No("");
            if (S.Has("E1_5")) return MiniResult.Yes("已经比过了。");
            if (!S.Flag("heard_L1") || !S.Flag("heard_R1"))
                return Feedback(false, S.Flag("heard_L1") ? "右边也听一下，再比。" : "左边也听一下，再比。");
            g.ValidAction();
            if (side == "L1")
            {
                g.Log("MON", "左右对比：左边 L1 轴承清楚；右边 R1 几乎全是齿轮声。", "ok");
                g.Evidence.Give("E1_5");
                return Feedback(true, "左边听得清。");
            }
            Deviation("compare");
            g.Log("MON", "R1 的轴承成分只有 0.06，听到的几乎全是齿轮。");
            return Feedback(false, "右边只听得到一片“嗡”。再听听左边。");
        }

        /// <summary>谜题二：在 L1 时频图上框选“一下一下的竖条”。</summary>
        public MiniResult JudgeBox(SpecBox box)
        {
            if (S.Phase != Phase.P2) return MiniResult.No("");
            if (DiagPoint != "L1") return Feedback(false, "右边不用框，看下面的齿轮指示。");
            if (S.Has("E2_1")) return MiniResult.Yes("已经标过了。");
            g.ValidAction();
            var j = SpectroModel.Judge(box);
            g.Log("DIAG", j.Message, j.Ok ? "ok" : "");
            g.Playtest.Record("spectro_box", j.Reason, j.Impacts.ToString());
            if (j.Ok) { g.Evidence.Give("E2_1"); return Feedback(true, j.Message); }
            Deviation("spectro");
            return Feedback(false, j.Message);
        }

        /// <summary>谜题二：R1 齿轮指示判读。answer = "ok"（正常）/ "bad"（异常）。</summary>
        public MiniResult Sideband(string answer)
        {
            if (S.Phase != Phase.P2) return MiniResult.No("");
            if (DiagPoint != "R1") return Feedback(false, "齿轮指示在右边 R1。");
            g.ValidAction();
            if (answer == "ok")
            {
                if (S.Has("E2_2")) return MiniResult.Yes("已经记过了。");
                g.Log("DIAG", $"R1 边频指标 {SpectroModel.SidebandIndex:0.00}，低于阈值 {SpectroModel.SidebandThreshold:0.00}。齿轮状态正常。", "ok");
                g.Evidence.Give("E2_2");
                return Feedback(true, "指针在绿区：齿轮正常。");
            }
            Deviation("sideband");
            g.Log("DIAG", $"当前边频指标 {SpectroModel.SidebandIndex:0.00}，低于阈值 {SpectroModel.SidebandThreshold:0.00}。");
            g.Sfx("ab_gearWear");
            g.Caption("〔对比：齿轮磨坏时的声音 → 现在的右边〕");
            return Feedback(false, "对比一下：齿轮真磨坏了，是这个声音。");
        }

        void Deviation(string where)
        {
            Deviations++;
            g.Playtest.Record("deviation", where, Deviations.ToString());
            if (Deviations >= 3) g.Hints.AtLeast(1, "deviation");
        }

        MiniResult Feedback(bool ok, string msg)
        {
            if (!string.IsNullOrEmpty(msg)) g.Bus.Raise(Ev.Feedback, msg, ok ? "ok" : "no", ok ? 1 : 0);
            return ok ? MiniResult.Yes(msg) : MiniResult.No(msg);
        }

        // =====================================================================
        // 谜题三：推断单
        // =====================================================================
        public DeductionDef Deduction => g.Content.Deduction;

        /// <summary>词库里当前可用的词：需要证据的词，拿到证据之后才出现。</summary>
        public IEnumerable<string> AvailableWords()
        {
            var d = Deduction;
            return d.WordBank.Where(w => !d.WordNeeds.TryGetValue(w, out var needs) || needs.All(S.Has));
        }

        public bool Fill(string key, string word)
        {
            var d = Deduction;
            if (S.Phase != Phase.P3 || S.Has(d.GivesEvidence)) return false;
            if (d.Blanks.All(b => b.Key != key)) return false;
            if (word == null) { S.DeductionFill.Remove(key); return true; }
            if (!AvailableWords().Contains(word)) return false;
            // 一个词只能放一个空：从别的空里挪过来
            foreach (var k in S.DeductionFill.Where(kv => kv.Value == word).Select(kv => kv.Key).ToList()) S.DeductionFill.Remove(k);
            S.DeductionFill[key] = word;
            g.Hints.OnValidAction();
            return true;
        }

        public string Filled(string key) => S.DeductionFill.TryGetValue(key, out var v) ? v : null;

        /// <summary>检查推断。像 Golden Idol 一样：全部对才算；错两处以内告诉玩家错几处，不告诉哪几处。</summary>
        public DeductionCheck CheckDeduction()
        {
            var d = Deduction;
            var r = new DeductionCheck();
            if (S.Phase != Phase.P3) { r.Message = ""; return r; }
            if (S.Has(d.GivesEvidence)) { r.Complete = r.Correct = true; r.Message = "推断已经记下了。"; return r; }
            r.Complete = d.Blanks.All(b => Filled(b.Key) != null);
            if (!r.Complete) { r.Message = "还有空没填。"; return r; }
            foreach (var b in d.Blanks) if (Filled(b.Key) != b.Answer) r.WrongKeys.Add(b.Key);
            r.Wrong = r.WrongKeys.Count;
            r.Correct = r.Wrong == 0;
            g.ValidAction();
            g.Playtest.Record("deduction_check", r.Correct ? "ok" : "wrong", r.Wrong.ToString());
            if (r.Correct)
            {
                r.Message = "对上了。";
                g.Log("RULE", "推断与变更单、简报、工票一致：一九八七年险情后的正式安全整改，数字化时漏迁。", "ok");
                g.Evidence.Give(d.GivesEvidence);
                g.Dialogue.Say("J5");   // 事实链 §4.5 / 主项目文档：推断完成后周建国说“那年我二十一。”
            }
            else r.Message = r.Wrong <= 2 ? $"有 {r.Wrong} 处不对。" : "还对不上。再看看那几张旧纸。";
            g.Bus.Raise(Ev.DeductionChecked, r.Correct ? "ok" : "wrong", r.Message, r.Wrong);
            return r;
        }

        // =====================================================================
        // 谜题三：数字孪生离线验证
        // =====================================================================
        public bool TwinRestored => S.Flag("twin_restored");
        public bool TwinComplete => TwinRestored && GuardTwinModel.Cases.All(c => S.TwinPassed.Contains(c.Id));
        public bool TwinAvailable => S.Phase == Phase.P3 && S.Rule(RuleIds.JianguoGuard) == RuleStatus.Draft;

        /// <summary>把丢掉的启动条件补回新控制系统（依据：1987 年变更单）。补回之前也能试，用来看见“现在缺了什么”。</summary>
        public MiniResult TwinRestore()
        {
            if (!TwinAvailable) return MiniResult.No("先在三联板上把这一项想清楚。");
            if (TwinRestored) return MiniResult.Yes("启动条件已经补回。");
            g.SetFlag("twin_restored");
            S.TwinPassed.Clear();
            g.ValidAction();
            g.Log("TWIN", "按 1987 年变更单补回：护罩闭合反馈 → 主轴启动许可（离线孪生）。");
            return Feedback(true, "补回了：护罩合上，主轴才许启动。");
        }

        public TwinRun TwinTest(string caseId)
        {
            if (S.Phase != Phase.P3) return null;
            var run = GuardTwinModel.Run(caseId, TwinRestored);
            if (run == null) return null;
            g.ValidAction();
            g.Log("TWIN", run.Log, run.Pass ? (TwinRestored ? "ok" : "") : "warn");
            if (TwinRestored && run.Pass) S.TwinPassed.Add(caseId);
            g.Bus.Raise(Ev.TwinTest, caseId, run.SpindleRuns ? "runs" : "stops", run.Pass ? 1 : 0);
            g.Playtest.Record("twin", caseId, (TwinRestored ? "restored:" : "current:") + (run.SpindleRuns ? "runs" : "stops"));
            if (TwinComplete) { g.Log("TWIN", "两种情况都符合要求。可以提交待验证。", "ok"); g.Hints.OnProgress(); }
            return run;
        }

        // =====================================================================
        // 谜题四：天车试吊
        // =====================================================================
        void FirstTrial()
        {
            var t = CraneZvModel.Simulate(CraneZvModel.DefaultRope, 1f);
            LastTrial = t;
            g.Bus.Raise(Ev.CraneTrial, t.Outcome.ToString(), "auto", 0, t.Residual);
            g.Log("CR01", "自动试吊：" + t.ToLog(), "warn");
            g.Caption("〔吊钩停车时一晃，缸里的水洒了出来〕");
            g.Evidence.Give("E4_1");
        }

        public float SetT2(float beats)
        {
            if (S.Phase != Phase.P4) return S.CraneT2;
            float v = CraneZvModel.Snap(beats);
            if (System.Math.Abs(v - S.CraneT2) > 1e-4f) { S.CraneT2 = v; g.Hints.OnValidAction(); }
            return v;
        }

        /// <summary>玩家按“试吊”。结果完全由仿真决定。</summary>
        public CraneTrial Trial()
        {
            if (S.Phase != Phase.P4) return null;
            var t = CraneZvModel.Simulate(CraneZvModel.DefaultRope, S.CraneT2);
            LastTrial = t;
            g.ValidAction();
            g.Bus.Raise(Ev.CraneTrial, t.Outcome.ToString(), "player", 0, t.Residual);
            g.Playtest.Record("crane_trial", S.CraneT2.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), t.Outcome.ToString());
            switch (t.Outcome)
            {
                case CraneOutcome.Spill:
                    g.Log("CR01", "试吊：" + t.ToLog() + "水洒出。", "warn");
                    g.Caption("〔水洒了〕");
                    break;
                case CraneOutcome.Shake:
                    // 事实链 §5 操作：偏 0.05–0.10 拍 = 水面轻晃、未洒，可以确认，衡提示可再精调
                    g.Log("CR01", "试吊：" + t.ToLog() + "水面轻晃，未洒出。可以确认，也可以再精调。");
                    g.Caption("〔水面轻轻晃了几下，没洒〕");
                    if (S.Has("E4_2")) g.Evidence.Give("E4_TUNE");
                    else g.Log("CR01", "结果还行，但没有依据。先找到这个时刻是从哪儿来的。");
                    break;
                case CraneOutcome.Still:
                    g.Log("CR01", "试吊：" + t.ToLog() + "吊钩停稳，水面静止。", "ok");
                    g.Caption("〔吊钩停住了。缸里的水，一动不动〕");
                    if (S.Has("E4_2")) g.Evidence.Give("E4_TUNE");
                    else g.Log("CR01", "结果稳定，但没有依据。先找到这个时刻是从哪儿来的。");
                    break;
            }
            return t;
        }

        /// <summary>磁带开始播放：返回要依次播的台词。</summary>
        public IReadOnlyList<string> PlayTape()
        {
            if (S.Phase != Phase.P4 || !S.Flag("tape_taken")) return new string[0];
            g.SetFlag("tape_played");
            g.ValidAction();
            return g.Content.TapeScript;
        }

        /// <summary>磁带放完（表现层在最后一句播完后调用）：一九九八年的铃声显影，衡把铃点标到时间轴上。</summary>
        public void TapeFinished()
        {
            if (S.Phase != Phase.P4 || !S.Flag("tape_played")) return;
            foreach (var id in g.Content.TapeScript) if (!g.Dialogue.Spoken.Contains(id)) g.Dialogue.Say(id);
            g.Evidence.Give("E4_2");
            if (!S.Has("E4_3"))
            {
                g.Bus.Raise(Ev.MemoryDevelop, "m1998");
                g.Playtest.Record("memory", "m1998");
                g.Evidence.Give("E4_3");
                float half = CraneZvModel.Period(CraneZvModel.DefaultRope) / 2f;
                g.Log("CR01", $"磁带铃声标记：间隔约 {half:0.0}s，等于当前摆动周期的一半。");
                g.Evidence.Give("E4_4");
            }
        }
    }
}

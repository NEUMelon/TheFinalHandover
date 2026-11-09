// 《最后一次交班》Demo α —— 谜题四：天车停车时的吊钩摆动（2D 平面单摆）+ 两段式整形（ZV）
// 规则：两段等幅的收车指令，第二段相对第一段推迟 t2（单位：拍 = 当前摆动周期 T）。
//   线性化后残余摆动比 = |cos(π·t2)|：t2 = 1.00 拍 → 100%（原设置，水洒）；t2 = 0.50 拍 → 0（水面静止）。
//   这就是“比钩子早半拍”：把第二段从一整拍挪到半拍。
// 这里不用公式直接出结果，而是真的积分摆的运动方程，所以玩家改时刻 → 摆的轨迹和结果真的跟着变。
// ⚠ 仿真参数为【占位】：绳长 4.0 m、运行速度 0.6 m/s、减速度 0.6 m/s²，只用于教学演示。
using System;
using System.Collections.Generic;

namespace LSB.Core
{
    public enum CraneOutcome { Still, Shake, Spill }

    public sealed class CraneTrial
    {
        public float RopeLength;
        public float Period;        // 摆动周期（秒）
        public float T2Beats;       // 第二段指令时刻（拍）
        public float T2Seconds;
        public float Residual;      // 残余摆动比（相对不整形时）
        public float ResidualAmp;   // 残余摆幅（米，吊钩相对小车的水平偏移）
        public CraneOutcome Outcome;
        public float SampleDt;
        public float[] Time, Angle, TrolleyX, HookX;
        public float StopTime;      // 两段指令都执行完的时刻
        public string ToLog() => $"绳长 {RopeLength:0.0}m，周期 {Period:0.0}s，第二段 {T2Beats:0.00} 拍（{T2Seconds:0.0}s），残余摆动 {Residual * 100:0}%。";
    }

    public static class CraneZvModel
    {
        public const float G = 9.81f;
        public const float DefaultRope = 4.0f;
        public const float V0 = 0.6f;          // 运行速度 m/s
        public const float Decel = 0.6f;       // 收车减速度 m/s²
        public const float StillMax = 0.16f;   // ≈ |t2−0.5| ≤ 0.05 拍：水面静止
        public const float ShakeMax = 0.35f;   // 水面晃但不洒；再大就洒
        public const float MinBeats = 0.20f, MaxBeats = 1.20f, StepBeats = 0.05f;

        public static float Period(float rope) => (float)(2 * Math.PI * Math.Sqrt(rope / G));

        /// <summary>用固定秒数安排第二段（错误三联 d2 的反例）时，换算成拍。</summary>
        public static float SecondsToBeats(float seconds, float rope) => seconds / Period(rope);

        public static CraneOutcome Classify(float residual) => residual <= StillMax ? CraneOutcome.Still : residual <= ShakeMax ? CraneOutcome.Shake : CraneOutcome.Spill;

        public static float Snap(float beats)
        {
            float b = (float)Math.Round(beats / StepBeats) * StepBeats;
            return Math.Max(MinBeats, Math.Min(MaxBeats, b));
        }

        static readonly Dictionary<float, float> refAmp = new Dictionary<float, float>();

        /// <summary>完整仿真：小车从 V0 分两段收车停下，记录摆角、小车与吊钩的水平位置。</summary>
        public static CraneTrial Simulate(float rope, float t2Beats, float sampleDt = 1f / 30f, float tail = 10f)
        {
            float T = Period(rope);
            float t2 = t2Beats * T;
            var r = Run(rope, t2, sampleDt, tail, true);
            float reference = ReferenceAmp(rope);
            r.Residual = reference <= 1e-6f ? 0 : Math.Min(1.5f, r.ResidualAmp / reference);
            r.Outcome = Classify(r.Residual);
            r.T2Beats = t2Beats;
            return r;
        }

        /// <summary>不整形（两段同时下达）时的残余摆幅，作为 100% 基准。</summary>
        public static float ReferenceAmp(float rope)
        {
            lock (refAmp)
            {
                if (refAmp.TryGetValue(rope, out var a)) return a;
                a = Run(rope, 0f, 1f / 120f, 10f, false).ResidualAmp;
                refAmp[rope] = a;
                return a;
            }
        }

        static CraneTrial Run(float rope, float t2, float sampleDt, float tail, bool record)
        {
            // 每段把速度降 V0/2，需要 (V0/2)/Decel 秒；两段叠加时减速度加倍
            float seg = (V0 / 2f) / Decel;
            float lead = 1.0f;                      // 收车前匀速走 1 秒，方便画面上看出“在走”
            float stop = lead + Math.Max(seg, t2 + seg);
            float total = stop + tail;
            const float h = 1f / 600f;              // 积分步长
            double th = 0, om = 0, x = 0, v = V0;
            var ts = new List<float>(); var ang = new List<float>(); var tx = new List<float>(); var hx = new List<float>();
            double nextSample = 0, maxAfter = 0;
            for (double t = 0; t <= total + 1e-9; t += h)
            {
                double a = 0;
                double tc = t - lead;
                if (tc >= 0 && tc < seg) a -= Decel;
                if (tc >= t2 && tc < t2 + seg) a -= Decel;
                if (v + a * h < 0) a = -v / h;      // 不倒车
                // 半隐式欧拉：θ'' = −(g/L)·sinθ − (a/L)·cosθ
                double alpha = -(G / rope) * Math.Sin(th) - (a / rope) * Math.Cos(th);
                om += alpha * h; th += om * h;
                v += a * h; x += v * h;
                if (t > stop + 0.05) maxAfter = Math.Max(maxAfter, Math.Abs(rope * Math.Sin(th)));
                if (record && t + 1e-9 >= nextSample)
                {
                    ts.Add((float)t); ang.Add((float)th); tx.Add((float)x); hx.Add((float)(x + rope * Math.Sin(th)));
                    nextSample += sampleDt;
                }
            }
            return new CraneTrial
            {
                RopeLength = rope, Period = Period(rope), T2Seconds = t2, ResidualAmp = (float)maxAfter, SampleDt = sampleDt, StopTime = stop,
                Time = ts.ToArray(), Angle = ang.ToArray(), TrolleyX = tx.ToArray(), HookX = hx.ToArray(),
            };
        }

        /// <summary>磁带里的铃点（E4_4）：每半拍一响。返回 [0, duration) 内的铃响时刻（秒）。</summary>
        public static float[] BellTimes(float rope, float duration)
        {
            float half = Period(rope) / 2f;
            var list = new List<float>();
            for (float t = 0.5f; t < duration; t += half) list.Add(t);
            return list.ToArray();
        }
    }
}

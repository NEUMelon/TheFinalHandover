// 《最后一次交班》Demo α —— 听音诊断：信号条（谜题一）与时频图框选 / 边频指示（谜题二）
// 数值沿用 UI 原型 js/data.js 与 js/spectro.js（03_听音诊断原型_v0.1 §3.2 / §4.1）。
// ⚠ 全部为【模拟 / 占位数据】：程序合成，用来教玩家“咔哒在左、嗡在右”的判断方式，不是任何真实设备的实测数据。
using System;
using System.Linq;

namespace LSB.Core
{
    public sealed class SpecBox
    {
        public float T0, T1;   // 秒
        public float F0, F1;   // Hz
        public SpecBox(float t0, float t1, float f0, float f1)
        {
            T0 = Math.Min(t0, t1); T1 = Math.Max(t0, t1);
            F0 = Math.Min(f0, f1); F1 = Math.Max(f0, f1);
        }
    }

    public sealed class SpecJudge
    {
        public bool Ok;
        public int Impacts;
        public string Reason;   // gmf / band / few / ok
        public string Message;  // 衡日志文字
    }

    public static class SpectroModel
    {
        public const string DataLabel = "模拟 / 占位数据";

        // ---- 时频图（L1） ----
        public const float Duration = 6.0f;
        public const float FMax = 8000f;
        public const float BandLo = 2000f, BandHi = 4000f;   // 本机结构共振带：2026 年林小满复测值（B-2），不是 1983 年的记录
        public const float Gmf = 620f;                       // 齿轮啮合线
        public static readonly float[] Impacts =
        {
            0.12f, 0.414f, 0.708f, 1.002f, 1.296f, 1.59f, 1.884f, 2.178f, 2.472f, 2.766f,
            3.06f, 3.354f, 3.648f, 3.942f, 4.236f, 4.53f, 4.824f, 5.118f, 5.412f, 5.706f,
        };
        public const float ImpactInterval = 0.294f;

        // ---- 边频指示（R1） ----
        public const float SidebandIndex = 0.18f;
        public const float SidebandThreshold = 0.35f;
        public const float SidebandMax = 0.60f;

        // ---- 信号条（谜题一 左右对比）：轴承成分 / 齿轮成分 ----
        public static float BearingShare(string point) => point == "L1" ? 0.72f : 0.06f;
        public static float GearShare(string point) => point == "L1" ? 0.28f : 0.94f;

        /// <summary>
        /// 时频图像素亮度（0–1），移植原型 spectro.js build()：啮合线及谐波 + 共振带内的周期冲击 + 噪声。
        /// 不是 FFT，是按 03_听音诊断原型 §3.2 的注释合成的示意图。rnd 为确定性随机数（同一测点每次画出来一样）。
        /// </summary>
        public static float Energy(string point, float t, float f, Func<float> rnd)
        {
            bool l1 = point == "L1";
            var lines = l1 ? new[] { (620f, .38f), (1240f, .2f), (1860f, .1f) } : new[] { (620f, 1f), (1240f, .62f), (1860f, .36f), (2480f, .16f) };
            float impAmp = l1 ? 1f : .07f;
            float imp = ImpactEnvelope(t);
            double v = 0.05 + 0.07 * rnd() + 0.08 * Math.Exp(-f / 1500.0);
            foreach (var (lf, la) in lines) v += la * Math.Exp(-((f - lf) * (f - lf)) / (2.0 * 38 * 38));
            double inBand = f >= BandLo && f <= BandHi ? 1 : Math.Exp(-Math.Pow(f < BandLo ? BandLo - f : f - BandHi, 2) / (2.0 * 260 * 260));
            double tex = .75 + .25 * Math.Sin(f / 90.0 + t * 88) * rnd();
            v += impAmp * imp * (inBand * .95 + .1) * tex;
            return (float)Math.Pow(Math.Min(1, v), 1.25);
        }

        /// <summary>t 秒处离最近一次冲击的包络（冲击前 6ms 上升，之后 16ms 衰减）。</summary>
        public static float ImpactEnvelope(float t)
        {
            float imp = 0;
            foreach (var ti in Impacts)
            {
                float dt = t - ti;
                if (dt >= -0.006f && dt < 0.08f) imp = Math.Max(imp, dt < 0 ? 1 + dt / 0.006f : (float)Math.Exp(-dt / 0.016f));
            }
            return imp;
        }

        /// <summary>播放到 pos 秒时，最近越过的是第几个冲击（-1 = 还没有）。用于“咔哒”字样同步闪。</summary>
        public static int LastImpact(float pos)
        {
            int k = -1;
            for (int i = 0; i < Impacts.Length; i++) if (Impacts[i] <= pos) k = i;
            return k;
        }

        /// <summary>原型同款线性同余随机数（种子 L1 = 11，R1 = 23）。</summary>
        public static Func<float> Seeded(string point)
        {
            long s = point == "L1" ? 11 : 23;
            return () => (s = (s * 16807) % 2147483647) / 2147483647f;
        }

        /// <summary>框选判定（与原型 spectro.js judge() 一致）：不得框到啮合线；与共振带重叠 ≥60%；至少框住 3 个冲击。</summary>
        public static SpecJudge Judge(SpecBox b)
        {
            int n = Impacts.Count(t => t >= b.T0 && t <= b.T1);
            float ov = Math.Max(0, Math.Min(b.F1, BandHi) - Math.Max(b.F0, BandLo));
            float ratio = ov / Math.Max(1f, b.F1 - b.F0);
            if (b.F0 <= Gmf && b.F1 >= Gmf) return new SpecJudge { Reason = "gmf", Impacts = n, Message = "框里混进了下面那条横线（齿轮声）。框小一点。" };
            if (ratio < 0.6f) return new SpecJudge { Reason = "band", Impacts = n, Message = "框的地方没有竖条。往竖条那一带框。" };
            if (n < 3) return new SpecJudge { Reason = "few", Impacts = n, Message = "框住的竖条太少。框长一点。" };
            return new SpecJudge { Ok = true, Reason = "ok", Impacts = n, Message = $"已标记：一下一下的咔哒 ×{n}，间隔 {ImpactInterval:0.00} 秒。" };
        }
    }
}

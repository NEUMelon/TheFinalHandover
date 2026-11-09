// 《最后一次交班》Demo α —— 程序合成的占位音频
// 移植自 UI 原型 js/audio.js（03_听音诊断原型 §3.1 的四段示意声音 + 界面音效），另补车间底噪、汽笛、天车铃、对讲机噪声。
// ⚠【模拟 / 占位】全部为程序生成的示意声音，不是实地录音，也不是任何设备的真实数据；正式版换成声音圣经要求的实地录音与声音设计。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public sealed class ProceduralAudio : MonoBehaviour
    {
        const int SR = 22050;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource ambient, loop, oneShot;
        GameSession g;
        System.Random rng = new System.Random(1956);

        public static ProceduralAudio Instance { get; private set; }
        /// <summary>听音诊断循环当前播到第几秒（时频图播放头用）；没在播返回 -1。</summary>
        public static float LoopPosition => Instance != null && Instance.loop != null && Instance.loop.isPlaying ? Instance.loop.time : -1f;
        public static string LoopId { get; private set; }

        public void Bind(GameSession session)
        {
            Instance = this;
            g = session;
            ambient = gameObject.AddComponent<AudioSource>(); ambient.loop = true; ambient.spatialBlend = 0; ambient.volume = 0.25f;
            loop = gameObject.AddComponent<AudioSource>(); loop.loop = true; loop.spatialBlend = 0;
            oneShot = gameObject.AddComponent<AudioSource>(); oneShot.spatialBlend = 0;
            BuildAll();
            ambient.clip = clips["shop_hum"]; ambient.Play();
            g.Bus.Raised += OnEvent;
        }

        void OnDestroy() { if (g != null) g.Bus.Raised -= OnEvent; }

        /// <summary>记忆显影时把 2026 的声音抽空（环境声淡出、诊断循环停掉、界面音效不响），只放过去的声音（PlayPast）。</summary>
        public bool Hushed { get; private set; }
        const float AmbientVolume = 0.25f;

        public void Hush(bool on)
        {
            Hushed = on;
            if (on) StopLoop();
        }

        void Update()
        {
            if (g == null) return;
            AudioListener.volume = g.State.Settings.Volume;
            // 抽空 0.3 秒淡出；回到 2026 用 1.2 秒淡入（声音圣经 §2.8 溶解段）
            float target = Hushed ? 0f : AmbientVolume;
            float rate = Hushed ? AmbientVolume / 0.3f : AmbientVolume / 1.2f;
            ambient.volume = Mathf.MoveTowards(ambient.volume, target, rate * Time.unscaledDeltaTime);
        }

        void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case Ev.Sfx:
                    if (e.A == "loop_L1") Loop("L1");
                    else if (e.A == "loop_R1") Loop("R1");
                    else if (e.A == "ab_gearWear") StartCoroutine(AB("gearWear", 3f));   // A/B 对比：先停右边的声音，放“齿轮磨坏”的示意，再接回来
                    else Play(e.A, 0.8f);
                    break;
                case Ev.CloseUpChanged: if (e.B == "exit") StopLoop(); break;
                case Ev.CloseView: StopLoop(); break;
                case Ev.Line:
                    if (g.Content.Lines.TryGetValue(e.A, out var l) && l.Via == "对讲机") Play("radio", 0.5f);
                    break;
                case Ev.EvidenceAcquired: Play("flip", 0.5f); break;
                case Ev.TriadConflict:
                    if (!string.IsNullOrEmpty(e.B)) Play(e.B, 0.8f);
                    // 事实链 §4.9：“可以拆掉”的仿真反馈里，压低音量放一下 1987 显影里的撞击声
                    if (e.B == "guardOpenStart") StartCoroutine(Later("clang", 0.3f, 1.1f));
                    break;
                // 记忆显影的声音由 MemoryOverlay 按时序调用 PlayPast（声音先于画面），这里不处理 Ev.MemoryDevelop
                case Ev.CraneTrial: if (e.A == "Spill") Play("splash", 0.6f); break;
                case Ev.EndingStepChanged:
                    var st = (EndingStep)e.N;
                    // 拉闸：主轴降速滑音，节拍（车间底噪）停；接着静默 5 秒（全片第二处全静音）；汽笛；衡启动后只剩设备风扇
                    if (st == EndingStep.LightsOut) { ambient.Stop(); StopLoop(); Play("spin_down", 0.7f); }
                    if (st == EndingStep.Silence) Hush(true);
                    if (st == EndingStep.Siren) Hush(false);
                    if (st == EndingStep.HengBoot) { Play("boot", 0.6f); ambient.clip = clips["fan"]; ambient.volume = 0; ambient.Play(); }
                    break;
                case Ev.PhaseChanged:
                    // 新游戏 / 读档：环境声回到车间底噪（结局把它换成了风扇声）
                    if (e.B == "new" || e.B == "load")
                    {
                        Hush(false);
                        bool after = g.State.Phase == Phase.Ending && g.State.Ending >= EndingStep.LightsOut;
                        ambient.clip = clips[g.State.Phase == Phase.Ending && g.State.Ending >= EndingStep.HengBoot ? "fan" : "shop_hum"];
                        if (after && g.State.Ending < EndingStep.HengBoot) ambient.Stop(); else if (!ambient.isPlaying) ambient.Play();
                    }
                    break;
            }
        }

        public void Play(string id, float gain)
        {
            if (Hushed || gain <= 0 || !clips.TryGetValue(id, out var c)) return;
            oneShot.PlayOneShot(c, gain);
        }

        /// <summary>记忆里的声音（抽空期间也响）。</summary>
        public void PlayPast(string id, float gain)
        {
            if (gain <= 0 || !clips.TryGetValue(id, out var c)) return;
            oneShot.PlayOneShot(c, gain);
        }

        System.Collections.IEnumerator Later(string id, float gain, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Play(id, gain);
        }

        public void Loop(string id)
        {
            if (Hushed) return;
            if (!clips.TryGetValue(id, out var c)) return;
            LoopId = id;
            if (loop.clip == c && loop.isPlaying) return;
            loop.clip = c; loop.volume = 0.9f; loop.Play();
        }

        public void StopLoop() { if (loop != null) loop.Stop(); LoopId = null; }

        System.Collections.IEnumerator AB(string id, float seconds)
        {
            var resume = LoopId;
            if (loop != null) loop.Stop();
            Play(id, 0.9f);
            yield return new WaitForSecondsRealtime(seconds);
            if (resume != null && LoopId == resume) loop.Play();
        }

        // =====================================================================
        // 合成
        // =====================================================================
        float Rand() => (float)(rng.NextDouble() * 2 - 1);

        AudioClip Make(string name, float seconds, System.Action<float[]> fill)
        {
            int n = Mathf.Max(1, (int)(seconds * SR));
            var d = new float[n];
            fill(d);
            float peak = 0; for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            if (peak > 0.95f) for (int i = 0; i < n; i++) d[i] *= 0.95f / peak;
            var c = AudioClip.Create("LSB_占位_" + name, n, 1, SR, false);
            c.SetData(d, 0);
            clips[name] = c;
            return c;
        }

        void Hum(float[] d, float amp, float f0 = 620f, float wobble = 0f)
        {
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR, f = f0 * (1 + wobble * Mathf.Sin(2 * Mathf.PI * 15 * t));
                d[i] += amp * (Mathf.Sin(2 * Mathf.PI * f * t) + .45f * Mathf.Sin(2 * Mathf.PI * 2 * f * t + 1) + .2f * Mathf.Sin(2 * Mathf.PI * 3 * f * t + 2)
                    + .25f * Mathf.Sin(2 * Mathf.PI * 48f / 60f * t * 31));
                d[i] += amp * .08f * Rand();
            }
        }

        void Click(float[] d, float at, float amp)
        {
            int s = (int)(at * SR), len = (int)(0.03f * SR);
            float[] fr = { 2550, 3150, 3720 };
            var ph = new float[3]; for (int j = 0; j < 3; j++) ph[j] = (float)(rng.NextDouble() * 6.28);
            for (int k = 0; k < len && s + k < d.Length; k++)
            {
                float t = k / (float)SR, env = Mathf.Exp(-t / 0.005f), v = 0;
                for (int j = 0; j < 3; j++) v += Mathf.Sin(2 * Mathf.PI * fr[j] * t + ph[j]);
                d[s + k] += amp * env * (v / 3 + .25f * Rand());
            }
        }

        /// <summary>简单的一阶高通（约 300Hz）+ 低通（约 6kHz）。</summary>
        static void BandLimit(float[] d)
        {
            float aH = Mathf.Exp(-2 * Mathf.PI * 300f / SR), aL = 1 - Mathf.Exp(-2 * Mathf.PI * 6000f / SR);
            float hp = 0, prev = 0, lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                hp = aH * (hp + d[i] - prev); prev = d[i];
                lp += aL * (hp - lp);
                d[i] = lp;
            }
        }

        void BuildAll()
        {
            float dur = SpectroModel.Duration; var imp = SpectroModel.Impacts;
            Make("L1", dur, d => { Hum(d, .07f); foreach (var t in imp) Click(d, t, .55f); });
            Make("R1", dur, d => { Hum(d, .3f); foreach (var t in imp) Click(d, t, .025f); });
            Make("humLoud", dur, d => Hum(d, .42f));
            Make("humMask", dur, d => { Hum(d, .35f); foreach (var t in imp) Click(d, t, .05f); });
            Make("gearFixed", dur, d => { Hum(d, .06f, 655); foreach (var t in imp) Click(d, t, .55f); });
            Make("denser", dur, d => { Hum(d, .07f); float t = .1f, gap = .3f; while (t < dur) { Click(d, t, .55f); gap = Mathf.Max(.07f, gap * .9f); t += gap; } });
            Make("gearWear", dur, d => Hum(d, .3f, 620, .03f));
            Make("steady", 3f, d => Hum(d, .2f, 620));
            // 车间底噪：低频电机 + 宽带噪声（只做氛围，不参与判断）
            Make("shop_hum", 4f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .97f * y + .03f * Rand(); d[i] = .25f * Mathf.Sin(2 * Mathf.PI * 50 * t) + .12f * Mathf.Sin(2 * Mathf.PI * 100 * t) + 1.6f * y; } });
            Make("stamp", .4f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = Mathf.Exp(-t / .03f) * (Mathf.Sin(2 * Mathf.PI * 90 * t) * .9f + Rand() * .5f); } });
            Make("flip", .5f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1, t / .45f)), 2); y = .82f * y + .18f * Rand(); d[i] = env * y * .9f; } });
            Make("drawer", .7f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .6f * y + .4f * Rand(); d[i] = Mathf.Sin(Mathf.PI * t / .7f) * (y * .35f + .15f * Mathf.Sin(2 * Mathf.PI * (900 + 300 * Mathf.Sin(t * 30)) * t)); } });
            Make("relay", .3f, d => { Click(d, 0, .8f); Click(d, .012f, .4f); for (int i = 0; i < d.Length; i++) d[i] += Mathf.Exp(-i / (float)SR / .02f) * Mathf.Sin(2 * Mathf.PI * 140 * i / SR) * .4f; });
            Make("tick", .05f, d => Click(d, 0, .25f));
            Make("torch", .08f, d => { Click(d, 0, .3f); });
            Make("switch", .08f, d => { Click(d, 0, .4f); });
            Make("locker", .6f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = Mathf.Exp(-t / .08f) * (Mathf.Sin(2 * Mathf.PI * 160 * t) * .6f + Rand() * .3f) + Mathf.Exp(-Mathf.Abs(t - .35f) / .01f) * Rand() * .3f; } });
            Make("cabinet", .5f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = Mathf.Exp(-t / .06f) * (Mathf.Sin(2 * Mathf.PI * 210 * t) * .5f + Rand() * .4f); } });
            Make("main_switch", 1.2f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = Mathf.Exp(-t / .05f) * (Mathf.Sin(2 * Mathf.PI * 70 * t) + Rand() * .8f) + (t > .1f ? .2f * Mathf.Exp(-(t - .1f) / .5f) * Mathf.Sin(2 * Mathf.PI * 50 * t * (1 - (t - .1f) * .6f)) : 0); } });
            Make("lamp_off", .15f, d => { Click(d, 0, .5f); for (int i = 0; i < d.Length; i++) d[i] += Mathf.Exp(-i / (float)SR / .03f) * Mathf.Sin(2 * Mathf.PI * 1800 * i / SR) * .1f; });
            Make("speaker", 1.5f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { y = .5f * y + .5f * Rand(); d[i] = .25f * y; } });
            Make("radio", .35f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .3f * y + .7f * Rand(); d[i] = .35f * y * Mathf.Exp(-t / .12f); } });
            // 汽笛长鸣（结局）：两个略失谐的锯齿波，起音慢、收尾慢
            Make("siren", 4.5f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; float env = Mathf.Min(1, t / .6f) * Mathf.Min(1, (4.5f - t) / 1.2f); float f = 290 + 25 * Mathf.Min(1, t / .8f); float s = ((t * f) % 1f) * 2 - 1, s2 = ((t * f * 1.007f) % 1f) * 2 - 1; d[i] = env * .35f * (s + s2); } });
            // 天车铃（1998 年记忆显影）：每半拍一响
            Make("bell", 6f, d => { foreach (var bt in CraneZvModel.BellTimes(CraneZvModel.DefaultRope, 6f)) { int s0 = (int)(bt * SR); for (int k = 0; k < SR && s0 + k < d.Length; k++) { float t = k / (float)SR; d[s0 + k] += Mathf.Exp(-t / .35f) * (.5f * Mathf.Sin(2 * Mathf.PI * 1180 * t) + .3f * Mathf.Sin(2 * Mathf.PI * 2950 * t) + .15f * Mathf.Sin(2 * Mathf.PI * 4100 * t)); } } });
            Make("splash", .8f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .7f * y + .3f * Rand(); d[i] = y * Mathf.Exp(-t / .25f) * .8f; } });
            Make("spill", .8f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .7f * y + .3f * Rand(); d[i] = y * Mathf.Exp(-t / .25f) * .8f; } });
            Make("slowCrane", 3f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = .2f * Mathf.Sin(2 * Mathf.PI * 80 * t) * (1 + .3f * Mathf.Sin(2 * Mathf.PI * .5f * t)); } });
            Make("guardOpenStart", 2f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; float f = 40 + 200 * Mathf.Min(1, t / 1.2f); d[i] = .3f * Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Min(1, t / .1f); } });
            // ---- 记忆显影（声音圣经 §2.8）：过去的声音做“年代处理”——带宽约 300Hz–6kHz、轻微抖动 ----
            // 金属撞击：几个不成谐波的分音，衰减不同
            Make("clang", 2.2f, d => { float[] fr = { 523, 1371, 2213, 3418, 4790 }; float[] tau = { .9f, .55f, .35f, .22f, .12f }; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR, w = 1 + .004f * Mathf.Sin(2 * Mathf.PI * 5.3f * t), v = 0; for (int j = 0; j < fr.Length; j++) v += Mathf.Exp(-t / tau[j]) * Mathf.Sin(2 * Mathf.PI * fr[j] * w * t + j); d[i] = .5f * v / fr.Length * 2.2f + (t < .01f ? .5f * Rand() * (1 - t / .01f) : 0); } BandLimit(d); });
            // 一声喊（占位）：短促的“嘿”，不是惨叫（声音圣经 §2.8 禁写）。锯齿波过两个共振峰
            Make("shout", .6f, d => { float b1 = 0, b2 = 0, ph = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR, f0 = 260 - 50 * t; ph += f0 / SR; float saw = (ph % 1f) * 2 - 1; b1 += .18f * (saw - b1); b2 += .45f * (saw - b2); float env = Mathf.Min(1, t / .04f) * Mathf.Exp(-Mathf.Max(0, t - .18f) / .12f); d[i] = env * (.6f * b1 + .4f * (b2 - b1)) * .9f; } BandLimit(d); });
            // 颗粒声（细砂流动），聚合 1.0 秒；溶解时倒放
            Make("grain", 1.0f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = (rng.NextDouble() < .02 ? Rand() : 0) * .5f * Mathf.Sin(Mathf.PI * t); } });
            Make("grain_rev", 1.2f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = (rng.NextDouble() < .02 ? Rand() : 0) * .45f * Mathf.Sin(Mathf.PI * t / 1.2f); } });
            // 停留段的极低钢琴单音（占位：几根衰减的谐波）
            Make("low_note", 3f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR, f = 55f; d[i] = Mathf.Exp(-t / 1.4f) * (.5f * Mathf.Sin(2 * Mathf.PI * f * t) + .3f * Mathf.Sin(2 * Mathf.PI * 2 * f * t) + .12f * Mathf.Sin(2 * Mathf.PI * 3.01f * f * t)) * Mathf.Min(1, t / .01f); } });
            // 结局：主轴降速滑音（频率和音量一起往下掉）
            Make("spin_down", 3.0f, d => { float ph = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR, k = Mathf.Clamp01(1 - t / 3f); ph += (40f + 260f * k * k) / SR; d[i] = .35f * k * (Mathf.Sin(2 * Mathf.PI * ph) + .4f * Mathf.Sin(4 * Mathf.PI * ph)); } });
            // 衡的设备风扇（高、平、细；声音圣经 §2.9 结局后）
            Make("fan", 4f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .9f * y + .1f * Rand(); d[i] = .06f * Mathf.Sin(2 * Mathf.PI * 2400 * t) * (1 + .1f * Mathf.Sin(2 * Mathf.PI * 3 * t)) + .5f * y; } });
            // 磁带底噪 + 轻微抖动（谜题四录音机）
            Make("tape_hiss", 4f, d => { float y = 0; for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; y = .5f * y + .5f * Rand(); d[i] = .12f * y * (1 + .2f * Mathf.Sin(2 * Mathf.PI * .8f * t)) + .03f * Mathf.Sin(2 * Mathf.PI * 60 * t); } });
            Make("boot", 1.2f, d => { for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] = .18f * Mathf.Sin(2 * Mathf.PI * (880 + 440 * t) * t) * Mathf.Exp(-t / .6f); } });
        }
    }
}

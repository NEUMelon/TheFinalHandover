// 《最后一次交班》Demo α —— 记忆显影（覆盖层）
// 时序按声音圣经 §2.8 和美术圣经关键帧 B：
//   预备    2026 的声音抽空（环境声 0.3 秒淡出）；一九八七年凹痕之前再全静音 2 秒（全片两处全静音之一）
//   声音先行 过去的声音先进来（金属撞击、一声喊 / 天车铃），比画面早 0.8 秒
//   聚合    1.0 秒：光锥里的网点一颗颗出现，拼成剪影；颗粒声
//   停留    2.0 秒（一九九八年的铃要多响几下，停 3.6 秒）；极低的单音
//   溶解    1.2 秒：网点散掉，2026 的环境声回来
// 期间世界里不能点、提示计时暂停。点一下或按 Esc 直接跳到溶解。
// 灰盒：剪影是程序画的网点图（几何形状拼的，不画五官，不画伤），正式版换特效小组的网点 Shader。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Unity
{
    public sealed class MemoryOverlay
    {
        public static MemoryOverlay Instance { get; private set; }
        public static bool Active => Instance != null && Instance.mem != null;

        const int Tex = 512, Cell = 8;
        const float Lead = 0.8f, Aggregate = 1.0f, Dissolve = 1.2f;

        readonly GameSession g;
        readonly ProceduralAudio audio;
        readonly VisualElement root, cone, dots, captions;
        readonly Label year, tag;
        readonly Texture2D tex;
        readonly Color32[] px = new Color32[Tex * Tex];
        Color32[] final;
        byte[] thr;

        MemoryDef mem;
        float t0, tSound, tAgg, tHold, tDis, tEnd, lastReveal = -1;
        readonly HashSet<string> fired = new HashSet<string>();

        public MemoryOverlay(GameSession g, UiRoot ui, ProceduralAudio audio)
        {
            Instance = this;
            this.g = g; this.audio = audio;
            root = UiRoot.Box("memory");
            cone = UiRoot.Box("mem-cone");
            dots = UiRoot.Box("mem-dots");
            tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, false) { name = "LSB_占位_记忆显影网点", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            dots.style.backgroundImage = new StyleBackground(tex);
            cone.Add(dots);
            year = UiRoot.L("", "f-serif-b", "mem-year");
            cone.Add(year);
            root.Add(cone);
            tag = UiRoot.L("灰盒示意　正式版换记忆显影特效", "placeholder-tag", "mem-tag");
            root.Add(tag);
            captions = UiRoot.Box("mem-caps");
            captions.pickingMode = PickingMode.Ignore;
            root.Add(captions);
            root.RegisterCallback<PointerDownEvent>(_ => Skip());
            UiRoot.Show(root, false);
            ui.OverlayLayer.Add(root);
            g.Bus.Raised += e => { if (e.Type == Ev.MemoryDevelop) Begin(e.A); };
        }

        void Begin(string id)
        {
            if (!g.Content.Memories.TryGetValue(id ?? "", out var m)) return;
            mem = m;
            fired.Clear();
            t0 = Time.unscaledTime;
            tSound = m.SilenceBefore ? 2.3f : 0.3f;     // 0.3 秒抽空 + 2 秒全静音
            tAgg = tSound + Lead;
            tHold = tAgg + Aggregate;
            tDis = tHold + (m.Id == "m1998" ? 3.6f : 2.0f);
            tEnd = tDis + Dissolve;
            Paint(m.Id);
            Reveal(0);
            year.text = m.Year;
            year.style.opacity = 0;
            captions.Clear();
            root.RemoveFromClassList("in");
            UiRoot.Show(root, true);
            root.schedule.Execute(() => root.AddToClassList("in")).StartingIn(16);
            audio?.Hush(true);
            g.Hints.Paused = true;
        }

        public void Skip()
        {
            if (mem == null) return;
            float t = Time.unscaledTime - t0;
            if (t >= tDis) return;
            t0 = Time.unscaledTime - tDis;
            // 跳过时没看到的字幕一次补齐，信息不丢
            for (int i = 0; i < mem.Captions.Length; i++) Cap(i);
            g.Playtest.Record("memory_skip", mem.Id, t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }

        public void Tick()
        {
            if (mem == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Skip();
            float t = Time.unscaledTime - t0;
            bool z = mem.Id == "m1987";

            // 声音先行
            if (t >= tSound && Once("sound")) audio?.PlayPast(z ? "clang" : "bell", 0.9f);
            if (z && t >= tSound + 0.35f && Once("shout")) audio?.PlayPast("shout", 0.55f);
            if (t >= tSound) Cap(0);
            if (t >= (z ? tSound + 0.45f : tHold)) Cap(1);
            // 聚合
            if (t >= tAgg && Once("grain")) audio?.PlayPast("grain", 0.5f);
            if (t >= tHold && Once("note")) audio?.PlayPast("low_note", 0.5f);
            if (t >= tHold + (z ? 0f : 1.2f)) Cap(2);
            // 溶解
            if (t >= tDis && Once("dis")) { audio?.PlayPast("grain_rev", 0.45f); audio?.Hush(false); }

            float k = t < tAgg ? 0 : t < tHold ? (t - tAgg) / Aggregate : t < tDis ? 1 : 1 - (t - tDis) / Dissolve;
            k = Mathf.Clamp01(k);
            if (Mathf.Abs(k - lastReveal) > 0.02f || (k == 0 || k == 1) && k != lastReveal) Reveal(k);
            year.style.opacity = k;
            // 静音时什么都不出现（只有四周慢慢暗下来）；声音进来后光锥才亮，比剪影早 0.8 秒
            cone.style.opacity = Mathf.Clamp01((t - tSound) / Lead) * (t >= tDis ? 1 - Mathf.Clamp01((t - tDis) / Dissolve) : 1);

            if (t >= tEnd) End();
        }

        bool Once(string key) => fired.Add(key);

        void Cap(int i)
        {
            if (mem == null || i >= mem.Captions.Length || !fired.Add("cap" + i)) return;
            var l = UiRoot.L(mem.Captions[i], "mem-cap");
            l.pickingMode = PickingMode.Ignore;
            captions.Add(l);
        }

        void End()
        {
            g.Playtest.Record("memory_end", mem.Id);
            mem = null;
            UiRoot.Show(root, false);
            root.RemoveFromClassList("in");
            audio?.Hush(false);
            g.Hints.Paused = false;
            g.Hints.OnValidAction();   // 看完显影，提示计时从零开始
        }

        // =====================================================================
        // 网点图
        // =====================================================================
        void Reveal(float k)
        {
            lastReveal = k;
            byte cut = (byte)Mathf.Clamp(Mathf.RoundToInt(k * 255f), 0, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = thr[i] < cut || cut == 255 ? final[i] : clear;
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>按格子画网点：每格一个圆点，半径随剪影浓度变化；每格一个随机门限，聚合 / 溶解时按门限一颗颗出现、消失。</summary>
        void Paint(string id)
        {
            final = new Color32[Tex * Tex];
            thr = new byte[Tex * Tex];
            var rnd = new System.Random(id == "m1987" ? 1987 : 1998);
            var warm = new Color32(232, 180, 90, 255);   // C-Warm：光锥里的暖黄网点
            int n = Tex / Cell;
            for (int cy = 0; cy < n; cy++)
                for (int cx = 0; cx < n; cx++)
                {
                    float u = (cx + 0.5f) / n, v = (cy + 0.5f) / n;
                    float dens = Density(id, u, v);
                    byte th = (byte)rnd.Next(0, 255);
                    float r = Cell * 0.5f * Mathf.Sqrt(dens) * 1.15f;
                    for (int py = 0; py < Cell; py++)
                        for (int pxl = 0; pxl < Cell; pxl++)
                        {
                            float dx = pxl + 0.5f - Cell * 0.5f, dy = py + 0.5f - Cell * 0.5f;
                            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                            int x = cx * Cell + pxl, y = cy * Cell + py;
                            int idx = (Tex - 1 - y) * Tex + x;            // Texture2D 原点在左下
                            final[idx] = new Color32(warm.r, warm.g, warm.b, (byte)(a * 255));
                            thr[idx] = th;
                        }
                }
        }

        /// <summary>剪影浓度（0–1）。u, v 是光锥里的坐标，左上 (0,0)，右下 (1,1)。</summary>
        static float Density(string id, float u, float v)
        {
            float dc = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
            if (dc > 0.5f) return 0;
            float edge = Mathf.Clamp01((0.5f - dc) / 0.08f);       // 光锥边缘渐弱
            float bg = 0.10f * edge;
            float s = id == "m1987" ? Scene1987(u, v) : Scene1998(u, v);
            return Mathf.Max(bg, s * edge);
        }

        /// <summary>一九八七：护罩翻开、工件飞出的轨迹（刀刻线）、一只手把年轻人拽开（两个剪影，不画五官）。</summary>
        static float Scene1987(float u, float v)
        {
            float d = 0;
            d = Mathf.Max(d, Circle(u, v, 0.68f, 0.56f, 0.10f) * 0.85f);                 // 卡盘
            d = Mathf.Max(d, RotRect(u, v, 0.71f, 0.32f, 0.30f, 0.05f, -24f) * 0.85f);    // 翻开的护罩
            d = Mathf.Max(d, Dashed(u, v, 0.62f, 0.50f, 0.30f, 0.22f, 0.007f, 0.035f));   // 工件飞出的轨迹
            d = Mathf.Max(d, RotRect(u, v, 0.29f, 0.21f, 0.04f, 0.03f, 20f));             // 工件
            d = Mathf.Max(d, Circle(u, v, 0.44f, 0.40f, 0.045f));                          // 年轻人：头
            d = Mathf.Max(d, RotRect(u, v, 0.41f, 0.60f, 0.10f, 0.26f, 16f));             // 年轻人：身子往后仰
            d = Mathf.Max(d, Line(u, v, 0.10f, 0.50f, 0.40f, 0.50f, 0.028f));              // 拽开他的那只手
            d = Mathf.Max(d, Circle(u, v, 0.09f, 0.36f, 0.05f));                           // 另一个人：头（画面边缘，只露一半）
            d = Mathf.Max(d, RotRect(u, v, 0.07f, 0.62f, 0.12f, 0.34f, 4f));              // 另一个人：身子
            return d;
        }

        /// <summary>一九九八：天车大梁、小车、钢丝绳、吊钩和吊物，驾驶室里打铃的人（剪影）。</summary>
        static float Scene1998(float u, float v)
        {
            float d = 0;
            d = Mathf.Max(d, RotRect(u, v, 0.5f, 0.13f, 0.90f, 0.05f, 0) * 0.85f);       // 大梁
            d = Mathf.Max(d, RotRect(u, v, 0.55f, 0.18f, 0.12f, 0.05f, 0) * 0.85f);      // 小车
            d = Mathf.Max(d, Line(u, v, 0.55f, 0.20f, 0.60f, 0.62f, 0.006f));             // 钢丝绳（略斜：还在摆）
            d = Mathf.Max(d, RotRect(u, v, 0.60f, 0.64f, 0.05f, 0.04f, 8f));             // 吊钩
            d = Mathf.Max(d, RotRect(u, v, 0.61f, 0.74f, 0.17f, 0.12f, 6f) * 0.9f);      // 吊物
            d = Mathf.Max(d, RotRect(u, v, 0.22f, 0.28f, 0.17f, 0.15f, 0) * 0.6f);       // 驾驶室
            d = Mathf.Max(d, Circle(u, v, 0.21f, 0.25f, 0.035f));                          // 驾驶室里的人：头
            d = Mathf.Max(d, Circle(u, v, 0.30f, 0.31f, 0.018f));                          // 铃
            return d;
        }

        static float Circle(float u, float v, float cx, float cy, float r) => Mathf.Clamp01((r - Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy))) / 0.01f);

        static float RotRect(float u, float v, float cx, float cy, float w, float h, float deg)
        {
            float a = -deg * Mathf.Deg2Rad, x = u - cx, y = v - cy;
            float rx = x * Mathf.Cos(a) - y * Mathf.Sin(a), ry = x * Mathf.Sin(a) + y * Mathf.Cos(a);
            return Mathf.Clamp01(Mathf.Min(w * 0.5f - Mathf.Abs(rx), h * 0.5f - Mathf.Abs(ry)) / 0.008f);
        }

        static float Line(float u, float v, float x0, float y0, float x1, float y1, float th)
        {
            float dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
            float t = Mathf.Clamp01(((u - x0) * dx + (v - y0) * dy) / l2);
            float px = x0 + t * dx - u, py = y0 + t * dy - v;
            return Mathf.Clamp01((th - Mathf.Sqrt(px * px + py * py)) / 0.006f + 0.5f);
        }

        static float Dashed(float u, float v, float x0, float y0, float x1, float y1, float th, float dash)
        {
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01(((u - x0) * dx + (v - y0) * dy) / (len * len));
            return (t * len / dash) % 1f < 0.6f ? Line(u, v, x0, y0, x1, y1, th) : 0;
        }
    }
}

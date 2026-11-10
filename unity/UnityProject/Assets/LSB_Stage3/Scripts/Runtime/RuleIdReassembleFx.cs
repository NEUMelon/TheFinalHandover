// 《最后一次交班》Stage 3 · 手写 → Rule ID 点阵重组（看板 §2 Shot F；主项目文档 §7.3 “手写体笔画拆成点阵重组为经验规则 ID”）
// 不改 Demo α 的任何文件：开局时挂到 GameBootstrap.Booted 上。
// 落在哪一层：Demo α 的 UI 是 UI Toolkit。衡启动后（EndingStep.HengBoot → RulesRoll），EndingOverlay 把衡的日志逐行加进 .cine-log，
//   四条 Rule ID 是带 .cine-rule .f-mono 的 Label，每条后面停 0.8 秒（“停半拍”）。3D 场景里的 MES 屏只是冷白自发光面，没有内容。
//   所以本组件在 UI 层做：盯住新出现的 .cine-rule 标签，先把它的字设成透明（底框、左边线照旧），
//   在 OverlayLayer 最上面加一层只画点的 VisualElement，把这条规则的手写原句（老周体 / 建国体，暖黄 C-Warm）画成点阵，
//   拆开后按字符逐个（80ms 一个，UI-DS §5.4）飞到等宽 Rule ID 的点阵位置，飞行中由暖黄变冷白 C-Sys；停一下后点阵淡出，
//   标签原来的字（Demo α 样式）0.2 秒淡入接手。最终画面与 Demo α 完全一样，不出新 UI 方案。
// 颜色只用 UI-DS §3 的 Token：手写 = C-Warm #E8B45A（主项目文档 §6.2 “手写墨水高光”；UI-DS §3 结局双字体同行例外），Rule ID 点阵 = C-Sys #E6F0F5（冷白只给衡）。
// 点的位置离线算好（RuleIdDotsData.cs，生成脚本 40_特效/stage3_RuleID重组/tools/gen_ruleid_dots.py，用 Demo α 的同一批字体），
//   运行时按标签的字号（em）和内容框摆放，不依赖运行时字形栅格化。
// 时序与网页样片 40_特效/stage3_RuleID重组/LSB_VFX_RuleIdReassemble_LookDev_v01.html 一致。
// 关掉：启动参数 -lsbNoStage3Fx。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）；未在 Unity 里编译运行过。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using LSB.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Stage3
{
    public sealed class RuleIdReassembleFx : MonoBehaviour
    {
        // 时序（秒，从标签出现算起）
        public const float HandIn = 0.12f, Break = 0.42f, CharStep = 0.08f, Jitter = 0.04f, Fly = 0.26f, Hold = 0.25f, FadeOut = 0.2f;
        static readonly Color Warm = new Color32(0xE8, 0xB4, 0x5A, 0xFF);   // C-Warm
        static readonly Color Sys = new Color32(0xE6, 0xF0, 0xF5, 0xFF);    // C-Sys

        static RuleIdReassembleFx inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameBootstrap.Booted -= OnBooted;
            GameBootstrap.Booted += OnBooted;
        }

        static void OnBooted(GameBootstrap b)
        {
            if (System.Environment.CommandLine.Contains("-lsbNoStage3Fx") || inst != null || b.Ui == null || b.Ui.OverlayLayer == null) return;
            var go = new GameObject("LSB_Stage3_RuleID重组");
            inst = go.AddComponent<RuleIdReassembleFx>();
            inst.Bind(b);
        }

        /// <summary>一条规则的点：手写点（em）、Rule ID 点（em）、每个 ID 点从哪个手写点来、哪个字符、起飞时刻。</summary>
        sealed class Plan
        {
            public RuleIdDotsData.Entry E;
            public Vector2[] Hand, Id, Drift;
            public int[] Src;
            public float[] Start;
            public bool[] HandUsed;
            public float End;   // 点阵开始淡出、标签字淡入的时刻
        }

        sealed class Run
        {
            public Label Label;
            public Plan Plan;
            public float T0;
            public bool Released;
        }

        GameSession g;
        VisualElement overlay, layer, logBox;
        readonly Dictionary<string, Plan> plans = new Dictionary<string, Plan>();
        readonly List<Run> runs = new List<Run>();
        readonly HashSet<Label> seen = new HashSet<Label>();

        void Bind(GameBootstrap b)
        {
            g = b.Session;
            overlay = b.Ui.OverlayLayer;
            foreach (var e in RuleIdDotsData.Rules) plans[e.Id] = MakePlan(e);
            layer = new VisualElement { name = "LSB_Stage3_RuleIdDots", pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
            layer.generateVisualContent += Draw;
            overlay.Add(layer);   // 加在 EndingOverlay 之后 = 画在它上面
            g.Bus.Raised += e =>
            {
                if (e.Type == Ev.PhaseChanged && (e.B == "load" || e.B == "new")) StopAll();
            };
        }

        void OnDestroy()
        {
            if (layer != null) { layer.generateVisualContent -= Draw; layer.RemoveFromHierarchy(); }
            if (inst == this) inst = null;
        }

        static Plan MakePlan(RuleIdDotsData.Entry e)
        {
            var p = new Plan { E = e };
            p.Hand = Pairs(e.HandDots);
            p.Id = Pairs(e.IdDots);
            p.HandUsed = new bool[p.Hand.Length];
            p.Drift = new Vector2[p.Id.Length];
            p.Src = new int[p.Id.Length];
            p.Start = new float[p.Id.Length];
            // 从左到右对应：左边的笔画流向左边的字母，读起来是“这句话变成了这个 ID”
            var hs = Enumerable.Range(0, p.Hand.Length).OrderBy(i => p.Hand[i].x).ThenBy(i => p.Hand[i].y).ToArray();
            var ids = Enumerable.Range(0, p.Id.Length).OrderBy(i => p.Id[i].x).ThenBy(i => p.Id[i].y).ToArray();
            float last = 0;
            for (int k = 0; k < ids.Length; k++)
            {
                int i = ids[k];
                int h = hs.Length == 0 ? 0 : hs[Mathf.RoundToInt(k * (hs.Length - 1f) / Mathf.Max(1, ids.Length - 1))];
                p.Src[i] = h;
                if (hs.Length > 0) p.HandUsed[h] = true;
                p.Drift[i] = new Vector2(Hash(i, 1) - 0.5f, Hash(i, 2) - 0.8f) * 0.12f;   // 拆开时略往上散
                p.Start[i] = Break + CharStep * e.IdChar[i] + Jitter * Hash(i, 3);
                last = Mathf.Max(last, p.Start[i]);
            }
            p.End = last + Fly + Hold;
            return p;
        }

        static Vector2[] Pairs(short[] v)
        {
            var r = new Vector2[v.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(v[2 * i], v[2 * i + 1]) / 1000f;
            return r;
        }

        static float Hash(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        // ---------------- 找新出现的 Rule ID 标签 ----------------
        void Update()
        {
            var s = g.State;
            bool window = s.Phase == Phase.Ending && s.Ending >= EndingStep.HengBoot && s.Ending <= EndingStep.UnknownSignal;
            if (window)
            {
                if (logBox == null || logBox.panel == null) logBox = overlay.Q(null, "cine-log");
                if (logBox != null)
                    foreach (var child in logBox.Children())
                        if (child is Label l && l.ClassListContains("cine-rule") && seen.Add(l) && plans.TryGetValue(l.text ?? "", out var plan))
                            Begin(l, plan);
            }

            float now = Time.unscaledTime;
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var r = runs[i];
                float t = now - r.T0;
                if (!r.Released && t >= r.Plan.End) Release(r);
                if (r.Label.panel == null || t >= r.Plan.End + FadeOut) { if (!r.Released) Release(r); runs.RemoveAt(i); }
            }
            if (runs.Count > 0 || layerDirty) { layer.MarkDirtyRepaint(); layerDirty = runs.Count > 0; }
            if (!window && runs.Count == 0 && seen.Count > 0) seen.Clear();
        }

        bool layerDirty;

        void Begin(Label l, Plan plan)
        {
            // 字先透明（同一帧、首次绘制之前），结束时撤掉内联颜色，USS 原色 0.2 秒淡入
            l.style.color = new StyleColor(new Color(Sys.r, Sys.g, Sys.b, 0f));
            l.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { new StylePropertyName("color") });
            l.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(FadeOut, TimeUnit.Second) });
            runs.Add(new Run { Label = l, Plan = plan, T0 = Time.unscaledTime });
            layerDirty = true;
        }

        static void Release(Run r)
        {
            r.Released = true;
            r.Label.style.color = StyleKeyword.Null;
        }

        void StopAll()
        {
            foreach (var r in runs) if (!r.Released) Release(r);
            runs.Clear(); seen.Clear();
            layerDirty = true;
        }

        // ---------------- 画点 ----------------
        readonly List<(Vector2 p, float size, Color c)> quads = new List<(Vector2, float, Color)>();

        void Draw(MeshGenerationContext mgc)
        {
            float now = Time.unscaledTime;
            foreach (var r in runs)
            {
                if (r.Label.panel == null) continue;
                quads.Clear();
                Collect(r, now - r.T0);
                Emit(mgc, quads);
            }
        }

        void Collect(Run r, float t)
        {
            var p = r.Plan;
            float em = r.Label.resolvedStyle.fontSize;
            if (float.IsNaN(em) || em <= 0) em = 34f;
            var box = layer.WorldToLocal(r.Label.LocalToWorld(r.Label.contentRect));
            if (float.IsNaN(box.x) || float.IsNaN(box.y) || box.width <= 0f) return;   // 标签还没排版（出现的第一帧），下一帧再画
            float lineH = RuleIdDotsData.LineH / 1000f;
            float ky = box.height > 1f ? box.height / lineH : em;   // 行框高对齐标签内容框
            Vector2 At(Vector2 e) => new Vector2(box.x + e.x * em, box.y + e.y * ky);

            float handSz = RuleIdDotsData.HandPitch / 1000f * em * 0.95f;
            float idSz = RuleIdDotsData.IdPitch / 1000f * em * 0.78f;
            float fade = t >= p.End ? 1f - Mathf.Clamp01((t - p.End) / FadeOut) : 1f;
            float inA = Mathf.Clamp01(t / HandIn);
            float brk = Mathf.Clamp01((t - HandIn) / (Break - HandIn));

            // 没被选中的手写点：拆开时淡出、缩小
            if (brk < 1f)
                for (int h = 0; h < p.Hand.Length; h++)
                    if (!p.HandUsed[h]) quads.Add((At(p.Hand[h]), handSz * (1f - 0.6f * brk), Fade(Warm, inA * (1f - brk))));

            for (int i = 0; i < p.Id.Length; i++)
            {
                var src = p.Hand.Length > 0 ? p.Hand[p.Src[i]] : p.Id[i];
                var loose = src + p.Drift[i] * Smooth(brk);
                float sz = Mathf.Lerp(handSz, idSz, Smooth(brk));
                float f = (t - p.Start[i]) / Fly;
                if (f <= 0f)
                {
                    float shimmer = t > Break ? 0.85f + 0.15f * Mathf.Sin((t * 9f + Hash(i, 4) * 6.28f)) : 1f;
                    quads.Add((At(loose), sz, Fade(Warm, inA * shimmer * fade)));
                }
                else if (f < 1f)
                {
                    float v = EaseInOut(f);
                    var pos = Vector2.Lerp(loose, p.Id[i], v) + new Vector2(0, -0.35f) * Mathf.Sin(Mathf.PI * v);   // 轻微上弧
                    quads.Add((At(pos), Mathf.Lerp(sz, idSz, v), Fade(Color.Lerp(Warm, Sys, v), fade)));
                }
                else
                {
                    float flash = Mathf.Clamp01(1f - (t - p.Start[i] - Fly) / 0.12f);
                    quads.Add((At(p.Id[i]), idSz * (1f + 0.35f * flash), Fade(Sys, fade)));
                }
            }
        }

        static Color Fade(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }
        static float Smooth(float x) => x * x * (3f - 2f * x);
        static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        static void Emit(MeshGenerationContext mgc, List<(Vector2 p, float size, Color c)> q)
        {
            int n = Mathf.Min(q.Count, 16000);   // 4 顶点 / 点，单次分配不超过 65535 顶点
            if (n == 0) return;
            var md = mgc.Allocate(n * 4, n * 6);
            for (int k = 0; k < n; k++)
            {
                var (p, s, c) = q[k];
                float h = s * 0.5f;
                Color32 c32 = c;
                md.SetNextVertex(new Vertex { position = new Vector3(p.x - h, p.y - h, Vertex.nearZ), tint = c32 });
                md.SetNextVertex(new Vertex { position = new Vector3(p.x + h, p.y - h, Vertex.nearZ), tint = c32 });
                md.SetNextVertex(new Vertex { position = new Vector3(p.x + h, p.y + h, Vertex.nearZ), tint = c32 });
                md.SetNextVertex(new Vertex { position = new Vector3(p.x - h, p.y + h, Vertex.nearZ), tint = c32 });
                ushort b = (ushort)(k * 4);
                // UI Toolkit 要顺时针绕序
                md.SetNextIndex(b); md.SetNextIndex((ushort)(b + 1)); md.SetNextIndex((ushort)(b + 2));
                md.SetNextIndex(b); md.SetNextIndex((ushort)(b + 2)); md.SetNextIndex((ushort)(b + 3));
            }
        }
    }
}

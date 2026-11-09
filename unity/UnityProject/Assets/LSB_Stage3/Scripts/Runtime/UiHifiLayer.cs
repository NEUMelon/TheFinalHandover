// 《最后一次交班》Stage 3 · UI 高保真覆盖层  v02
// 开局时把 Resources/LSB_Stage3/UI/lsb_stage3_hifi.uss 加到 UI 根节点，并给 lsb-root 加上 .lsb-s3 类，让覆盖规则生效。
// v02（2026-10-15）再加三条 USS 做不到、要用代码补的 UI-DS 规定（对齐表 v0.2 §3 的 S-01…S-03），全部只读 / 只加样式，不改 Demo α 的逻辑：
//   S-01 三联板冲突格：UI-DS §5.2“左右抖 2 次 ×4px（D-Fast 120ms）”+“安全黄斜纹边”（§7 色盲模式：冲突靠斜纹、合格靠圆章区分）。
//        斜纹照原型基线 50_游戏交互/prototype/screenshots/04b_三联板_规则冲突.png：画在答案框（.triad-slot）四周，安全黄 / 警示黑相间；
//   S-02 页边提示三级的视觉差：UI-DS §5.8 一级铅笔（C-Ink 60%）、二 / 三级钢笔（晒图蓝）。USS 没有 :nth-child，按顺序给每条批注加 s3-hint-l1…l3；
//   S-03 交班本翻页：UI-DS §5.1“翻页 D-Base”，换页时新的一页 240ms ease-out-cubic 淡入（纸张翻页贴图做好前的近似）。
// 做法：每 16ms 扫一遍 UI 树（Demo α 的面板每次重绘都会新建元素，所以靠类名打标记，不持有旧元素）。
// 不改 Demo α 的 USS 和代码；删掉本文件或那张 USS 就回到原样。启动参数 -lsbNoStage3Ui 关掉。
// 由 Claude 编写（AI 辅助代码，记入 AI 使用日志）；只对 Unity 参考库编译过，未在 Unity 里运行过。
using System.Collections.Generic;
using LSB.Unity;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace LSB.Stage3
{
    public static class UiHifiLayer
    {
        // UI-DS §5 动效 Token
        const int DFast = 120, DBase = 240;
        const float ShakePx = 4f;
        // 斜纹边：答案框四周 8px，45°，安全黄 C-Safety #F2C200 / 警示黑 C-Ink #1A1A1A 相间，7px 一条（原型 04b）
        const int StripeBand = 8, StripeStep = 7;
        static readonly Color32 Safety = new Color32(0xF2, 0xC2, 0x00, 0xFF), Ink = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

        const string Shaken = "s3-shaken", Striped = "s3-striped";
        static readonly Dictionary<long, Texture2D> stripeCache = new Dictionary<long, Texture2D>();
        static VisualElement lastSpread;
        static string lastTab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameBootstrap.Booted -= Apply;
            GameBootstrap.Booted += Apply;
        }

        static void Apply(GameBootstrap b)
        {
            if (System.Environment.CommandLine.Contains("-lsbNoStage3Ui") || b.Ui == null || b.Ui.Root == null) return;
            var ss = Resources.Load<StyleSheet>("LSB_Stage3/UI/lsb_stage3_hifi");
            if (ss == null) return;
            var root = b.Ui.Root;
            root.styleSheets.Add(ss);
            var app = root.Q("lsb-root");
            if (app != null) app.AddToClassList("lsb-s3");
            lastSpread = null; lastTab = null;
            root.schedule.Execute(() => Tick(root)).Every(16);
        }

        static void Tick(VisualElement root)
        {
            TriadConflicts(root);
            HintLevels(root);
            LedgerFlip(root);
        }

        // ---------------- S-01 三联板冲突格 ----------------
        static void TriadConflicts(VisualElement root)
        {
            foreach (var col in root.Query<VisualElement>(null, "triad-col").ToList())
            {
                if (!col.ClassListContains("conflict")) continue;
                if (!col.ClassListContains(Shaken))
                {
                    col.AddToClassList(Shaken);
                    col.experimental.animation.Start(0f, 1f, DFast, (e, v) => e.style.translate = new StyleTranslate(new Translate(ShakePx * Mathf.Sin(v * Mathf.PI * 4f), 0, 0)))
                        .Ease(Easing.Linear)
                        .OnCompleted(() => col.style.translate = new StyleTranslate(StyleKeyword.Null));
                }
                var slot = col.Q(null, "triad-slot");
                if (slot != null && !slot.ClassListContains(Striped))
                {
                    float w = slot.resolvedStyle.width, h = slot.resolvedStyle.height;
                    if (float.IsNaN(w) || float.IsNaN(h) || w < 2 * StripeBand || h < 2 * StripeBand) continue;   // 还没排版，下一帧再来
                    slot.style.backgroundImage = new StyleBackground(StripeFrame(Mathf.RoundToInt(w), Mathf.RoundToInt(h)));
                    slot.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.StretchToFill);
                    slot.AddToClassList(Striped);
                }
            }
        }

        /// <summary>和答案框一样大的贴图：四边 8px 宽的 45° 安全黄 / 警示黑斜纹，中间透明（底色仍是 USS 的 #F7EBC4）。按尺寸缓存。</summary>
        static Texture2D StripeFrame(int w, int h)
        {
            w = Mathf.Clamp(w, 2 * StripeBand, 2048); h = Mathf.Clamp(h, 2 * StripeBand, 2048);
            long key = ((long)w << 16) | (uint)h;
            if (stripeCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "LSB_S3_冲突斜纹", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool band = x < StripeBand || y < StripeBand || x >= w - StripeBand || y >= h - StripeBand;
                    px[y * w + x] = !band ? clear : ((x + y) / StripeStep) % 2 == 0 ? Safety : Ink;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            stripeCache[key] = tex;
            return tex;
        }

        // ---------------- S-02 三级提示：铅笔 / 钢笔 ----------------
        static void HintLevels(VisualElement root)
        {
            foreach (var note in root.Query<VisualElement>(null, "hint-note").ToList())
            {
                int i = 0;
                foreach (var c in note.Children())
                {
                    if (!c.ClassListContains("h")) continue;
                    i++;
                    c.EnableInClassList("s3-hint-l1", i == 1);
                    c.EnableInClassList("s3-hint-l2", i == 2);
                    c.EnableInClassList("s3-hint-l3", i >= 3);
                }
            }
        }

        // ---------------- S-03 交班本换页淡入 ----------------
        static void LedgerFlip(VisualElement root)
        {
            var sp = root.Q(null, "ledger-spread");
            if (sp == null) { lastSpread = null; lastTab = null; return; }
            if (sp == lastSpread) return;
            var sel = root.Q<Button>(null, "ledger-tab", "sel");
            string tab = sel != null ? sel.text : null;
            // 刚打开交班本不淡入（面板自己有 D-Slow 抬起）；同一页重绘（读贴页、标已读）也不淡入
            if (lastSpread != null && tab != lastTab)
            {
                sp.style.opacity = 0f;
                sp.experimental.animation.Start(0f, 1f, DBase, (e, v) => e.style.opacity = v)
                    .Ease(Easing.OutCubic)
                    .OnCompleted(() => sp.style.opacity = new StyleFloat(StyleKeyword.Null));
            }
            lastSpread = sp; lastTab = tab;
        }
    }
}

// 《最后一次交班》Stage 3 · 记忆显影（世界空间网点 Shader，替换 Demo α 的 UI 灰盒网点图）  v02
// 不改 Demo α 的任何文件：开局时挂到 GameBootstrap.Booted 上，听 Ev.MemoryDevelop；
// 时序照抄 MemoryOverlay（D-28）：抽空 0.3 s（一九八七再加全静音 2 s）→ 声音先行 0.8 s → 聚合 1.0 s → 停留 2.0 s（一九九八 3.6 s）→ 溶解 1.2 s。
// 声音、字幕、年份、静音、禁点仍由 MemoryOverlay 负责；本组件只把 UI 里那张灰盒网点图藏起来，换成相机前的网点四边形。
// 遮罩：Resources/LSB_Stage3/MemoryMasks/<记忆 ID>.png（R 剪影 / G 刀刻线）。没有遮罩就不接管，Demo α 原样运行。
// 关掉：启动参数 -lsbNoStage3Fx，或把 Stage3Settings.MemoryFx 设为 false。
// v02（2026-10-15）：
//   ① 四边形铺满视野，光锥位置从 UI 的 .mem-cone 读（原来放在画面正中，和 UI 光锥“中心偏左”、年份字对不上）；
//   ② UI 的 .memory 半透明黑盖在 3D 之上，会把网点压暗 42%：接管期间把它清掉，改由 Shader 的 _Dim 在光锥外压暗（同样 2 s 渐入、0.42）；
//   ③ 跳过（点击 / Esc）时和 MemoryOverlay 一样先补满聚合再溶解（原来跳过发生在聚合前会什么都不出现）。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）；只对 Unity 参考库编译过，未在 Unity 里运行过。
using LSB.Core;
using LSB.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.Stage3
{
    public static class Stage3Settings
    {
        public static bool MemoryFx = !System.Environment.CommandLine.Contains("-lsbNoStage3Fx");
    }

    public sealed class MemoryDevelopFx : MonoBehaviour
    {
        const float Lead = 0.8f, Aggregate = 1.0f, Dissolve = 1.2f, QuadDistance = 1.0f, QuadOverscan = 1.01f;
        // 与 lsb_views.uss 的 .memory.in（rgba(4,8,14,0.42)，transition 2s）一致
        const float DimMax = 0.42f, DimFade = 2.0f;
        // UI 读不到时的后备值：.mem-cone 在 1920×1080 上 left 380 / top 150 / 720×720
        static readonly Vector4 FallbackCone = new Vector4(740f / 1920f, 1f - 510f / 1080f, 720f / 1080f, 16f / 9f);

        static MemoryDevelopFx inst;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameBootstrap.Booted -= OnBooted;
            GameBootstrap.Booted += OnBooted;
        }

        static void OnBooted(GameBootstrap b)
        {
            if (!Stage3Settings.MemoryFx || inst != null) return;
            var shader = Shader.Find("LSB/MemoryDevelop");
            if (shader == null || !shader.isSupported) { Debug.LogWarning("[LSB Stage3] LSB/MemoryDevelop Shader 找不到或当前平台不支持（看 Console 里的 Shader 报错），记忆显影沿用 Demo α 灰盒。"); return; }
            var go = new GameObject("LSB_Stage3_记忆显影");
            inst = go.AddComponent<MemoryDevelopFx>();
            inst.Bind(b, shader);
        }

        GameBootstrap boot;
        Material mat;
        Transform quad;
        string memId;
        float t0, tAgg, tDis, tEnd;
        bool running;
        static readonly int IdMask = Shader.PropertyToID("_Mask"), IdAgg = Shader.PropertyToID("_Agg"), IdDis = Shader.PropertyToID("_Dis"),
                            IdCone = Shader.PropertyToID("_ConeRect"), IdDim = Shader.PropertyToID("_Dim");

        void Bind(GameBootstrap b, Shader shader)
        {
            boot = b;
            mat = new Material(shader) { name = "LSB_Stage3_MemoryDevelop" };
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "记忆显影_网点";
            Destroy(q.GetComponent<Collider>());
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            quad = q.transform;
            quad.SetParent(transform, false);
            q.SetActive(false);
            b.Session.Bus.Raised += e => { if (e.Type == Ev.MemoryDevelop) Begin(e.A); };
        }

        void Begin(string id)
        {
            if (!boot.Session.Content.Memories.TryGetValue(id ?? "", out var m)) return;
            var mask = Resources.Load<Texture2D>("LSB_Stage3/MemoryMasks/" + m.Id);
            if (mask == null) { Debug.Log($"[LSB Stage3] 没有 {m.Id} 的遮罩，沿用 Demo α 灰盒。"); return; }

            memId = m.Id;
            mat.SetTexture(IdMask, mask);
            mat.SetFloat(IdAgg, 0); mat.SetFloat(IdDis, 0); mat.SetFloat(IdDim, 0);
            float tSound = m.SilenceBefore ? 2.3f : 0.3f;
            t0 = Time.unscaledTime;
            tAgg = tSound + Lead;
            tDis = tAgg + Aggregate + (m.Id == "m1998" ? 3.6f : 2.0f);
            tEnd = tDis + Dissolve;
            running = true;
            quad.gameObject.SetActive(true);
            HideGrayboxDots(true);
        }

        void LateUpdate()
        {
            if (!running) return;
            float t = Time.unscaledTime - t0;

            // 跳过：MemoryOverlay 里点一下或 Esc 直接进溶解；覆盖层先结束时也立刻溶解。
            // MemoryOverlay.Skip 把时钟拨到溶解起点（剪影先是满的再散），这里同样先补满聚合。
            bool skip = t > 0.2f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape));   // 0.2 s 内忽略触发显影的那一下点击
            if ((skip || !MemoryOverlay.Active) && t < tDis) { tAgg = Mathf.Min(tAgg, t - Aggregate); tDis = t; tEnd = t + Dissolve; }

            mat.SetFloat(IdAgg, Mathf.Clamp01((t - tAgg) / Aggregate));
            mat.SetFloat(IdDis, Mathf.Clamp01((t - tDis) / Dissolve));
            mat.SetFloat(IdDim, DimMax * Mathf.Clamp01(t / DimFade));
            PlaceInFrontOfCamera();

            if (t >= tEnd)
            {
                running = false;
                quad.gameObject.SetActive(false);
                HideGrayboxDots(false);
            }
        }

        void PlaceInFrontOfCamera()
        {
            var cam = boot.Rig != null ? boot.Rig.Cam : Camera.main;
            if (cam == null) return;
            var ct = cam.transform;
            float h = 2f * QuadDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * QuadOverscan;
            quad.position = ct.position + ct.forward * QuadDistance;
            quad.rotation = ct.rotation;                 // 内置 Quad 在本地 XY 平面、法线 -Z：取相机朝向即正对相机，uv 不镜像
            quad.localScale = new Vector3(h * cam.aspect, h, 1f);

            // 光锥：视野 uv 坐标（左下为 0）→ 四边形 uv（多铺了 QuadOverscan，往中心收一点）
            var c = ConeInView(cam);
            mat.SetVector(IdCone, new Vector4(0.5f + (c.x - 0.5f) / QuadOverscan, 0.5f + (c.y - 0.5f) / QuadOverscan, c.z / QuadOverscan, cam.aspect));
        }

        /// <summary>UI 光锥 .mem-cone 在画面里的位置：(中心 u, 中心 v, 直径 / 画面高, 宽高比)。面板铺满全屏，所以按面板比例换算。</summary>
        Vector4 ConeInView(Camera cam)
        {
            var root = boot.Ui != null ? boot.Ui.Root : null;
            var cone = boot.Ui != null && boot.Ui.OverlayLayer != null ? boot.Ui.OverlayLayer.Q(null, "mem-cone") : null;
            if (root == null || cone == null) return FallbackCone;
            Rect rb = root.worldBound, cb = cone.worldBound;
            if (!(rb.width > 1f && rb.height > 1f && cb.height > 1f) || float.IsNaN(cb.x) || float.IsNaN(cb.y)) return FallbackCone;
            return new Vector4((cb.center.x - rb.x) / rb.width, 1f - (cb.center.y - rb.y) / rb.height, cb.height / rb.height, cam.aspect);
        }

        void HideGrayboxDots(bool hide)
        {
            var layer = boot.Ui != null ? boot.Ui.OverlayLayer : null;
            if (layer == null) return;
            var root = layer.Q(null, "memory");
            var dots = layer.Q(null, "mem-dots");
            var cone = layer.Q(null, "mem-cone");
            var tag = layer.Q<Label>(null, "mem-tag");
            // 压暗改由 Shader 画在网点下面；UI 的半透明黑接管期间清掉，结束后还给 USS
            if (root != null) root.style.backgroundColor = hide ? new StyleColor(Color.clear) : new StyleColor(StyleKeyword.Null);
            if (dots != null) dots.style.visibility = hide ? new StyleEnum<Visibility>(Visibility.Hidden) : new StyleEnum<Visibility>(StyleKeyword.Null);
            if (cone != null) cone.style.backgroundColor = hide ? new StyleColor(Color.clear) : new StyleColor(StyleKeyword.Null);
            if (tag != null && hide) tag.text = $"Stage 3 记忆显影 v02（{memId} 遮罩为占位）";
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (inst == this) inst = null;
        }
    }
}

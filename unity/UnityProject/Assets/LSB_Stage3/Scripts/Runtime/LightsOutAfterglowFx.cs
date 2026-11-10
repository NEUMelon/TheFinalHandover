// 《最后一次交班》Stage 3 · 交接灯熄灭 + 逐排熄灯余辉（看板 §2 Shot E）
// 不改 Demo α 的任何文件：开局时挂到 GameBootstrap.Booted 上。熄灯本身仍由 WorldPresenter 做（SetLamp / SetRow / SwitchOff，
// X22 → X2 每排 0.6 秒），这里只在它关灯之后叠一层“灯丝冷却”：
//   · 交接状态板（Ev.LampOff，规则确认同一帧）：2700K → 暗，0.6 秒（UI-DS §5.4 1400ms 行）。
//   · 结局逐排熄灯（EndingStep.LightsOut 期间，检测每排 Light 由开到关）：2700K → 暗红，1.5 秒（看板 §2 Shot E、美术圣经关键帧 C ②）。
//   · 静默（EndingStep.Silence）到汽笛（Siren）：西大门上方一盏很弱的冷绿白应急灯（美术圣经 §7 灯光表“应急灯”、关键帧 C ③）〔派生：位置〕。
// 余辉怎么叠，才不和别人打架：
//   1. 外壳：给熄灭的渲染体套一个放大 1.5% 的同网格子物体，材质是 LSB/AfterglowShell（加色、不写深度），颜色走 MaterialPropertyBlock。
//      原渲染体的 sharedMaterial 一个字也不碰 —— WorldPresenter 每次 SetRow / SetLamp 换材质、Stage3ArtBinder 每帧给模型交接灯切亮 / 灭材质都照常。
//      只给“当时正在显示”的渲染体套外壳：灰盒交接灯被 Stage3ArtBinder 隐藏时，余辉落在模型部件 HandoverLamp_1..4 上。
//   2. 一盏临时点光源：在灯排（或交接灯）位置，颜色、强度随余辉曲线衰减，照到地面 / 柜门上。
//   以后换正式灯具模型：调 RegisterRowRenderer / RegisterHandoverRenderer 把灯泡部件加进来即可。
// 余辉曲线与网页样片 40_特效/stage3_交接灯熄灭/LSB_VFX_LightsOut_LookDev_v01.html 一致：
//   u = t / 时长；色温 K = 800 + 1900·e^(−3.2u)；亮度 b = 0.6·e^(−6u) + 0.4·(1−u)^1.5；颜色 = 黑体近似（Tanner Helland）。
// 关掉：启动参数 -lsbNoStage3Fx。读档 / 新游戏不放余辉（直接是 WorldPresenter 摆好的样子）。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）；未在 Unity 里编译运行过。
using System.Collections.Generic;
using System.Linq;
using LSB.Core;
using LSB.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace LSB.Stage3
{
    public sealed class LightsOutAfterglowFx : MonoBehaviour
    {
        public const float RowSeconds = 1.5f, LampSeconds = 0.6f;
        const float ShellScale = 1.015f;
        const float RowEmission = 0.9f;        // ≈ WorldPresenter 亮灯灯罩自发光 (1, .85, .6)·0.8 的亮度
        const float LampEmission = 1.5f;       // ≈ 交接灯亮时 LampOn·1.5
        const float RowLightShare = 0.55f;     // 余辉灯初始强度 = 该排灯强度 × 此值
        static readonly Vector3 EmergencyPos = new Vector3(0.8f, 4.8f, 8.5f);   // 西大门（X 0，Z 6.5–10.5，门高 4.5）门楣上方、离墙 0.8 m〔派生〕
        static readonly Color EmergencyColor = new Color(0.78f, 0.93f, 0.86f);    // 冷绿白（降饱和）〔派生〕

        static LightsOutAfterglowFx inst;
        static readonly List<Renderer>[] extraRows = Enumerable.Range(0, 6).Select(_ => new List<Renderer>()).ToArray();
        static readonly List<Renderer>[] extraLamps = Enumerable.Range(0, 4).Select(_ => new List<Renderer>()).ToArray();

        /// <summary>正式灯具模型接进来后，把每排的灯泡 / 灯罩渲染体登记进来（可在 Booted 前后任意时刻调用）。</summary>
        public static void RegisterRowRenderer(int row, Renderer r) { if (row >= 0 && row < 6 && r != null && !extraRows[row].Contains(r)) extraRows[row].Add(r); }

        /// <summary>交接状态板的模型灯（Stage3ArtBinder 已按 HandoverLamp_1..4 自动找，一般不用手动登记）。</summary>
        public static void RegisterHandoverRenderer(int lamp, Renderer r) { if (lamp >= 0 && lamp < 4 && r != null && !extraLamps[lamp].Contains(r)) extraLamps[lamp].Add(r); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameBootstrap.Booted -= OnBooted;
            GameBootstrap.Booted += OnBooted;
        }

        static void OnBooted(GameBootstrap b)
        {
            if (System.Environment.CommandLine.Contains("-lsbNoStage3Fx") || inst != null || b.World == null) return;
            var go = new GameObject("LSB_Stage3_熄灯余辉");
            inst = go.AddComponent<LightsOutAfterglowFx>();
            inst.Bind(b);
        }

        sealed class Glow
        {
            public float T0, Seconds, Emission, LightI0;
            public readonly List<Renderer> Shells = new List<Renderer>();
            public Light Light;
            public int Row = -1, Lamp = -1;
        }

        GameSession g;
        WorkshopRefs w;
        Material shellMat;
        MaterialPropertyBlock mpb;
        readonly Dictionary<Renderer, Renderer> shells = new Dictionary<Renderer, Renderer>();
        readonly List<Glow> glows = new List<Glow>();
        readonly Stack<Light> lightPool = new Stack<Light>();
        readonly bool[] rowOn = new bool[6];
        bool resync = true;
        Light emergency;
        float emergencyTarget, emergencyLevel;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Bind(GameBootstrap b)
        {
            g = b.Session; w = b.World;
            var sh = Shader.Find("LSB/AfterglowShell");
            if (sh != null) shellMat = new Material(sh) { name = "LSB_Stage3_AfterglowShell" };
            else Debug.LogWarning("[LSB Stage3] 找不到 LSB/AfterglowShell Shader：熄灯余辉只用点光源，不做灯罩发光。");
            mpb = new MaterialPropertyBlock();
            g.Bus.Raised += OnEvent;

            emergency = new GameObject("应急灯_西大门〔派生〕").AddComponent<Light>();
            emergency.transform.SetParent(transform, false);
            emergency.transform.position = EmergencyPos;
            emergency.type = LightType.Point;
            emergency.range = 6f;
            emergency.color = EmergencyColor;
            emergency.shadows = LightShadows.None;
            emergency.intensity = 0;
            emergency.enabled = false;
            SyncEmergency(true);
        }

        void OnDestroy()
        {
            if (g != null) g.Bus.Raised -= OnEvent;
            if (shellMat != null) Destroy(shellMat);
            if (inst == this) inst = null;
        }

        void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case Ev.LampOff:
                    StartLamp(e.N);
                    break;
                case Ev.PhaseChanged:
                    if (e.B == "load" || e.B == "new") { ClearAll(); resync = true; SyncEmergency(true); }
                    break;
                case Ev.EndingStepChanged:
                    SyncEmergency(false);
                    break;
            }
        }

        // ---------------- 检测 WorldPresenter 关掉了哪一排 ----------------
        void LateUpdate()
        {
            bool window = g.State.Phase == Phase.Ending && g.State.Ending >= EndingStep.LightsOut && g.State.Ending <= EndingStep.Siren;
            for (int r = 0; r < 6; r++)
            {
                bool on = RowLit(r);
                if (!resync && window && rowOn[r] && !on) StartRow(r);
                rowOn[r] = on;
            }
            resync = false;

            float now = Time.unscaledTime;
            for (int i = glows.Count - 1; i >= 0; i--)
            {
                var gl = glows[i];
                float u = (now - gl.T0) / gl.Seconds;
                if (u >= 1f) { EndGlow(gl); glows.RemoveAt(i); continue; }
                // 熄灭的那一排如果又被点亮（衡启动时 WorldPresenter 把灯排换成冷白），余辉立刻让位
                if (gl.Row >= 0 && RowLit(gl.Row)) { EndGlow(gl); glows.RemoveAt(i); continue; }
                var c = Afterglow(u, out float b);
                mpb.SetColor(ColorId, c * (b * gl.Emission));
                foreach (var s in gl.Shells) if (s != null) s.SetPropertyBlock(mpb);
                if (gl.Light != null) { gl.Light.color = c; gl.Light.intensity = gl.LightI0 * Mathf.Pow(b, 1.5f); }
            }

            emergencyLevel = Mathf.MoveTowards(emergencyLevel, emergencyTarget, Time.unscaledDeltaTime / 0.25f);
            emergency.enabled = emergencyLevel > 0.001f;
            emergency.intensity = 0.2f * emergencyLevel;
        }

        bool RowLit(int r)
        {
            var row = w.LampRows[r];
            if (row == null) return false;
            foreach (var l in row) if (l != null && l.enabled) return true;
            return false;
        }

        // ---------------- 开始一段余辉 ----------------
        void StartRow(int r)
        {
            var gl = new Glow { T0 = Time.unscaledTime, Seconds = RowSeconds, Emission = RowEmission, Row = r };
            var targets = new List<Renderer>();
            if (w.LampShades[r] != null) targets.AddRange(w.LampShades[r]);
            targets.AddRange(extraRows[r]);
            AddShells(gl, targets);
            var src = w.LampRows[r]?.FirstOrDefault(l => l != null);
            if (src != null) gl.Light = TakeLight(src.transform.position, src.range * 0.6f, gl.LightI0 = src.intensity * RowLightShare);
            glows.Add(gl);
        }

        void StartLamp(int i)
        {
            if (i < 0 || i >= 4) return;
            glows.RemoveAll(x => { if (x.Lamp == i) { EndGlow(x); return true; } return false; });
            var gl = new Glow { T0 = Time.unscaledTime, Seconds = LampSeconds, Emission = LampEmission, Lamp = i };
            var targets = new List<Renderer>();
            if (w.HandoverLamps != null && i < w.HandoverLamps.Length && w.HandoverLamps[i] != null) targets.Add(w.HandoverLamps[i]);
            var art = GameObject.Find(Stage3ArtBinder.HolderName);
            if (art != null)
                targets.AddRange(art.GetComponentsInChildren<Transform>(true).Where(t => t.name == $"HandoverLamp_{i + 1}").Select(t => t.GetComponent<Renderer>()).Where(x => x != null));
            targets.AddRange(extraLamps[i]);
            AddShells(gl, targets);
            var shown = targets.FirstOrDefault(Visible);
            if (shown != null) gl.Light = TakeLight(shown.bounds.center, 0.6f, gl.LightI0 = 0.6f);
            glows.Add(gl);
        }

        static bool Visible(Renderer r) => r != null && r.enabled && r.gameObject.activeInHierarchy;

        void AddShells(Glow gl, IEnumerable<Renderer> targets)
        {
            if (shellMat == null) return;
            foreach (var t in targets.Distinct())
            {
                if (!Visible(t)) continue;   // 被 Stage3ArtBinder 隐藏的灰盒不套外壳
                var s = Shell(t);
                if (s == null) continue;
                s.gameObject.SetActive(true);
                gl.Shells.Add(s);
            }
        }

        /// <summary>同网格、放大 1.5%（以网格包围盒中心为准）的加色外壳；第一次用到时建，之后复用。</summary>
        Renderer Shell(Renderer target)
        {
            if (shells.TryGetValue(target, out var s) && s != null) return s;
            var mf = target.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            var go = new GameObject("余辉外壳");
            go.layer = target.gameObject.layer;
            go.transform.SetParent(target.transform, false);
            var c = mf.sharedMesh.bounds.center;
            go.transform.localPosition = c - c * ShellScale;
            go.transform.localScale = Vector3.one * ShellScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Enumerable.Repeat(shellMat, Mathf.Max(1, mf.sharedMesh.subMeshCount)).ToArray();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            shells[target] = mr;
            return mr;
        }

        Light TakeLight(Vector3 pos, float range, float intensity)
        {
            var l = lightPool.Count > 0 ? lightPool.Pop() : null;
            if (l == null)
            {
                l = new GameObject("余辉灯").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
            }
            l.transform.position = pos;
            l.range = range;
            l.intensity = intensity;
            l.enabled = true;
            return l;
        }

        void EndGlow(Glow gl)
        {
            foreach (var s in gl.Shells)
                if (s != null && !glows.Any(o => o != gl && o.Shells.Contains(s))) s.gameObject.SetActive(false);
            if (gl.Light != null) { gl.Light.enabled = false; lightPool.Push(gl.Light); gl.Light = null; }
        }

        void ClearAll()
        {
            foreach (var gl in glows) EndGlow(gl);
            glows.Clear();
        }

        // ---------------- 应急灯：静默 + 汽笛（衡启动后让位给冷白） ----------------
        void SyncEmergency(bool snap)
        {
            var s = g.State;
            bool on = s.Phase == Phase.Ending && (s.Ending == EndingStep.Silence || s.Ending == EndingStep.Siren);
            emergencyTarget = on ? 1f : 0f;
            if (snap) emergencyLevel = emergencyTarget;
        }

        // ---------------- 余辉曲线（与网页样片同一套） ----------------
        /// <summary>u = 0..1。返回 sRGB 颜色（最大分量 = 1），亮度 b 另给。</summary>
        public static Color Afterglow(float u, out float brightness)
        {
            u = Mathf.Clamp01(u);
            float kelvin = 800f + 1900f * Mathf.Exp(-3.2f * u);
            brightness = 0.6f * Mathf.Exp(-6f * u) + 0.4f * Mathf.Pow(1f - u, 1.5f);
            return Kelvin(kelvin);
        }

        /// <summary>黑体颜色近似（Tanner Helland），800–6600K 段够用。</summary>
        public static Color Kelvin(float k)
        {
            float t = k / 100f;
            float r = t <= 66f ? 255f : 329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f);
            float gg = t <= 66f ? 99.4708025861f * Mathf.Log(t) - 161.1195681661f : 288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f);
            float b = t >= 66f ? 255f : t <= 19f ? 0f : 138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f;
            var c = new Color(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(gg, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m > 0 ? c / m : c;
        }
    }
}

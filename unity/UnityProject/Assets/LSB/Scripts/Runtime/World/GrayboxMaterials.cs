// 《最后一次交班》Demo α —— 灰盒材质（占位）
// 全部是纯色占位材质，不是最终美术。颜色只用来区分体块和三区，参照美术圣经的色板方向取近似值。
// 优先用 URP Lit；工程没装 URP 时退回 Standard，保证灰盒在任何管线下都能看。
using System.Collections.Generic;
using UnityEngine;

namespace LSB.Unity
{
    public static class GrayboxMaterials
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static readonly Color Floor = new Color(0.56f, 0.55f, 0.52f);      // 水磨石
        public static readonly Color Wall = new Color(0.78f, 0.76f, 0.70f);       // 白灰墙
        public static readonly Color Wainscot = new Color(0.33f, 0.48f, 0.40f);   // 墙裙绿
        public static readonly Color Concrete = new Color(0.62f, 0.61f, 0.58f);
        public static readonly Color Steel = new Color(0.36f, 0.38f, 0.40f);
        public static readonly Color MachineGreen = new Color(0.32f, 0.43f, 0.40f);  // 车床漆
        public static readonly Color MachineHero = new Color(0.30f, 0.45f, 0.42f);
        public static readonly Color Cabinet = new Color(0.47f, 0.52f, 0.48f);
        public static readonly Color Wood = new Color(0.50f, 0.36f, 0.24f);
        public static readonly Color Safety = new Color(0.88f, 0.70f, 0.18f);     // 安全黄线
        public static readonly Color Paint = new Color(0.70f, 0.22f, 0.18f);      // 老周漆圈（褪色红）
        public static readonly Color RedTape = new Color(0.78f, 0.15f, 0.12f);
        public static readonly Color WhiteTape = new Color(0.92f, 0.92f, 0.88f);
        public static readonly Color Glass = new Color(0.55f, 0.65f, 0.68f);
        public static readonly Color Screen = new Color(0.86f, 0.93f, 0.97f);     // 衡：冷白
        public static readonly Color LampOn = new Color(1.00f, 0.66f, 0.30f);     // 交接灯：暖橙
        public static readonly Color Enamel = new Color(0.86f, 0.86f, 0.80f);     // 搪瓷
        public static readonly Color Water = new Color(0.40f, 0.62f, 0.72f);
        public static readonly Color Dark = new Color(0.15f, 0.15f, 0.16f);
        public static readonly Color Hover = new Color(1.00f, 0.78f, 0.35f);

        /// <summary>编辑器生成场景时把材质存成资源（否则存场景后材质丢失）。运行时为空。</summary>
        public static System.Func<Material, Material> Persist;

        public static void ClearCache() => cache.Clear();

        static Shader lit;
        public static bool UsingUrp { get; private set; }

        static Shader Lit()
        {
            if (lit != null) return lit;
            lit = Shader.Find("Universal Render Pipeline/Lit");
            UsingUrp = lit != null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (!UsingUrp) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Diffuse");
            return lit;
        }

        public static Material Get(Color c, float smooth = 0.2f, float metal = 0f, Color? emission = null)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00") + metal.ToString("0.00") + (emission.HasValue ? ColorUtility.ToHtmlStringRGB(emission.Value) : "");
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit()) { name = "LSB_Graybox_" + key };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
            }
            if (Persist != null) m = Persist(m);
            cache[key] = m;
            return m;
        }

        /// <summary>悬停 / 按住空格时的高亮：复制一份带自发光的材质。</summary>
        public static Material Highlight(Material src)
        {
            var m = new Material(src) { name = src.name + "_hl" };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Hover * 0.35f);
            return m;
        }
    }
}

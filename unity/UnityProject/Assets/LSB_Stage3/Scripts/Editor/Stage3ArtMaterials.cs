// 《最后一次交班》Stage 3 · 3D 资产材质自动建立
// 菜单 LSB/Stage3/建立 3D 资产材质：扫描 Assets/LSB_Art/Env/ 下的 FBX，按材质名建 URP Lit 材质（.mat 放在 FBX 同目录的 Materials/），
// 再用 ModelImporter.AddRemap 把 FBX 里的同名材质换掉。
//   M_<资产>_Atlas：贴图集材质，读 Assets/LSB_Art/Env/Textures/<资产>_Atlas_BaseColor / _Normal / _MS
//   其余 M_*：纯色材质，颜色取自 FBX 自带材质（有同名贴花贴图 Textures/<名>_BaseColor.png 时改用贴图）；名字含 “_On” 或 “StatusLED” 的开自发光（交接灯亮、2025 冷白小灯）。
// 贴图导入设置由 ArtTextureImporter 按文件名后缀自动设：_Normal → 法线贴图；_MS / _ORM → 线性。
// 由 Claude 编写（AI 辅助代码，记入 AI 使用日志）。
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LSB.Stage3.EditorTools
{
    public static class Stage3ArtMaterials
    {
        const string EnvRoot = "Assets/LSB_Art/Env";
        const string TexDir = EnvRoot + "/Textures";

        [MenuItem("LSB/Stage3/建立 3D 资产材质", priority = 110)]
        public static void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { EditorUtility.DisplayDialog("LSB Stage3", "找不到 URP Lit Shader。工程应使用 URP 14。", "好"); return; }
            if (!AssetDatabase.IsValidFolder(EnvRoot)) { EditorUtility.DisplayDialog("LSB Stage3", "没有 " + EnvRoot + "，先把 FBX 放进来。", "好"); return; }
            int made = 0, models = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { EnvRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                var matDir = Path.Combine(Path.GetDirectoryName(path), "Materials").Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), "Materials");
                var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
                foreach (var src in embedded)
                {
                    var matPath = $"{matDir}/{src.name}.mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, matPath); made++; }
                    else m.shader = lit;
                    if (src.name.EndsWith("_Atlas")) SetupAtlas(m, src.name.Substring(2, src.name.Length - 2 - "_Atlas".Length));
                    else SetupFlat(m, src);
                    EditorUtility.SetDirty(m);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
                }
                importer.SaveAndReimport();
                models++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[LSB Stage3] 材质：新建 {made} 个，处理模型 {models} 个。");
        }

        static void SetupAtlas(Material m, string asset)
        {
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{asset}_Atlas_BaseColor.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{asset}_Atlas_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{asset}_Atlas_MS.png");
            if (bc == null) Debug.LogWarning($"[LSB Stage3] 缺 {TexDir}/{asset}_Atlas_BaseColor.png");
            m.SetTexture("_BaseMap", bc);
            m.SetColor("_BaseColor", Color.white);
            if (nm != null) { m.SetTexture("_BumpMap", nm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.SetFloat("_Smoothness", 1f);                 // 有贴图时作为光滑度倍数
                m.SetFloat("_SmoothnessTextureChannel", 0f);   // 光滑度取 _MS 的 A 通道
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
        }

        static void SetupFlat(Material m, Material src)
        {
            var c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.gray;
            // 贴花：Textures/<材质名去掉 M_>_BaseColor.png 存在就当底色贴图（铭牌、胶布手写字）
            var decal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{src.name.Substring(src.name.StartsWith("M_") ? 2 : 0)}_BaseColor.png");
            if (decal != null) { m.SetTexture("_BaseMap", decal); c = Color.white; }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f);
            m.SetFloat("_Smoothness", src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness") : 0.4f);
            if (src.name.Contains("Glass"))
            {
                // 玻璃（交班桌玻璃板等）：URP Lit 透明，否则会挡住压在下面的点检单
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.12f));
                m.SetFloat("_Smoothness", 0.9f);
            }
            bool emissive = src.name.Contains("_On") || src.name.Contains("StatusLED");
            if (emissive)
            {
                // 交接灯“亮” = 暖橙 #F0A040；2025 状态灯 = 冷白 #E6F0F5（UI-DS：冷白只给衡）
                // 吊灯（Glow）= 钨丝 2700K 暖白 #FFD9A0 ×2.5
                bool tungsten = src.name.Contains("Glow");
                var e = src.name.Contains("StatusLED") ? new Color(0.90f, 0.94f, 0.96f) : tungsten ? new Color(1f, 0.85f, 0.63f) : new Color(0.94f, 0.63f, 0.25f);
                m.SetColor("_EmissionColor", e * (tungsten ? 2.5f : 4f));
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }
    }

    /// <summary>LSB_Art 贴图按文件名后缀设导入参数。</summary>
    sealed class ArtTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/LSB_Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 4096;
            if (p.EndsWith("_Normal.png")) { ti.textureType = TextureImporterType.NormalMap; }
            else if (p.EndsWith("_MS.png") || p.EndsWith("_ORM.png") || p.EndsWith("_Mask.png")) { ti.sRGBTexture = false; }
        }
    }
}

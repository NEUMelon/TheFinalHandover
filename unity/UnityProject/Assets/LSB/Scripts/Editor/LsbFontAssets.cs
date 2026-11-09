// 《最后一次交班》Demo α —— 把 Fonts/*.ttf 转成 UI Toolkit 用的字体资源（TextCore FontAsset，动态图集）
// 为什么要转：2A 的 USS 直接引用 .ttf，Unity 2022.3 在运行时临时把 TTF 转成字体资源，
// 进出 Play 模式后这个临时资源的图集贴图被销毁，Console 刷“MissingReferenceException: The variable m_AtlasTextures of FontAsset doesn't exist anymore”。
// 现在改成：工程打开（脚本编译完）时自动生成 Assets/LSB/Fonts/SDF/<字体名>.asset（图集和材质作为子资源一起存盘），USS 引用这些 .asset。
// TTF 换过（重新裁子集）时按文件内容的 MD5 自动重建，避免图集里留着旧字形。
// API 用反射调用：FontAsset.CreateFontAsset 在 2022.3 是公开的，但云端检查用的参考库更旧，没有这个方法。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LSB.EditorTools
{
    [InitializeOnLoad]
    public static class LsbFontAssets
    {
        public const string FontDir = "Assets/LSB/Fonts";
        public const string SdfDir = FontDir + "/SDF";
        static readonly string[] StyleSheets =
        {
            "Assets/LSB/Resources/LSB/UI/lsb_tokens.uss", "Assets/LSB/Resources/LSB/UI/lsb_hud.uss",
            "Assets/LSB/Resources/LSB/UI/lsb_views.uss", "Assets/LSB/Resources/LSB/UI/LSB_Theme.tss",
        };

        static LsbFontAssets()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Ensure(false);
            };
        }

        [MenuItem("LSB/6 重建字体资源", priority = 41)]
        public static void RebuildMenu() => Ensure(true);

        /// <summary>缺了或 TTF 变了就生成；force = 全部重建。返回是否有改动。</summary>
        public static bool Ensure(bool force)
        {
            var faType = Type.GetType("UnityEngine.TextCore.Text.FontAsset, UnityEngine.TextCoreTextEngineModule");
            if (faType == null) { Debug.LogError("[LSB] 没找到 UnityEngine.TextCore.Text.FontAsset（需要 Unity 2022.3 或更新）。界面会退回默认字体，中文可能显示成方框。"); return false; }
            if (!Directory.Exists(FontDir)) return false;
            Directory.CreateDirectory(SdfDir);
            bool changed = false;
            foreach (var ttf in Directory.GetFiles(FontDir, "*.ttf").Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var name = Path.GetFileNameWithoutExtension(ttf);
                var assetPath = $"{SdfDir}/{name}.asset";
                var stampPath = $"{SdfDir}/{name}.src.txt";
                var md5 = Md5(ttf);
                bool upToDate = File.Exists(assetPath) && File.Exists(stampPath) && File.ReadAllText(stampPath).Trim() == md5;
                if (upToDate && !force) continue;
                try
                {
                    if (Create(faType, ttf, assetPath, name)) { File.WriteAllText(stampPath, md5); changed = true; }
                }
                catch (Exception e) { Debug.LogError($"[LSB] 生成字体资源失败：{name}：{e.GetBaseException().Message}"); }
            }
            if (changed)
            {
                SetMonoFallback(faType);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                // USS 里的 url 指向这些 .asset；资源刚生成，USS 要重新导入一次才能解析到
                foreach (var ss in StyleSheets) if (File.Exists(ss)) AssetDatabase.ImportAsset(ss, ImportAssetOptions.ForceUpdate);
                Debug.Log("[LSB] 已生成 UI 字体资源：" + SdfDir);
            }
            return changed;
        }

        static bool Create(Type faType, string ttfPath, string assetPath, string name)
        {
            AssetDatabase.ImportAsset(ttfPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogWarning("[LSB] 读不到字体：" + ttfPath); return false; }

            var methods = faType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "CreateFontAsset").ToList();
            object fa = null;
            var full = methods.FirstOrDefault(m => { var p = m.GetParameters(); return p.Length >= 6 && p[0].ParameterType == typeof(Font) && p[1].ParameterType == typeof(int); });
            if (full != null)
            {
                var ps = full.GetParameters();
                var args = new object[ps.Length];
                args[0] = font; args[1] = 64; args[2] = 6;
                args[3] = Enum.Parse(ps[3].ParameterType, "SDFAA");
                args[4] = 2048; args[5] = 2048;
                for (int i = 6; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, "Dynamic");
                    else if (t == typeof(bool)) args[i] = true;
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (t.IsValueType ? Activator.CreateInstance(t) : null);
                }
                fa = full.Invoke(null, args);
            }
            else
            {
                var simple = methods.FirstOrDefault(m => { var p = m.GetParameters(); return p.Length == 1 && p[0].ParameterType == typeof(Font); });
                if (simple != null) fa = simple.Invoke(null, new object[] { font });
            }
            if (!(fa is ScriptableObject so)) { Debug.LogError("[LSB] FontAsset.CreateFontAsset 不可用：" + name); return false; }

            if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            so.name = name;
            AssetDatabase.CreateAsset(so, assetPath);

            // 图集贴图、材质存成子资源（否则进出 Play 模式后丢失，就是 2A 的那个报错）
            if (faType.GetProperty("atlasTextures")?.GetValue(fa) is Texture2D[] texs)
                for (int i = 0; i < texs.Length; i++)
                    if (texs[i] != null && !AssetDatabase.Contains(texs[i])) { texs[i].name = $"{name} Atlas {i}"; AssetDatabase.AddObjectToAsset(texs[i], so); }
            if (faType.GetProperty("material")?.GetValue(fa) is Material mat && !AssetDatabase.Contains(mat)) { mat.name = name + " Material"; AssetDatabase.AddObjectToAsset(mat, so); }

            // 动态图集要记住源字体（打包后运行时按需补字形）
            var ser = new SerializedObject(so);
            var guid = AssetDatabase.AssetPathToGUID(ttfPath);
            Set(ser, "m_SourceFontFileGUID", p => p.stringValue = guid);
            Set(ser, "m_SourceFontFile_EditorRef", p => p.objectReferenceValue = font);
            Set(ser, "m_SourceFontFile", p => p.objectReferenceValue = font);
            Set(ser, "m_ClearDynamicDataOnBuild", p => p.boolValue = false);
            ser.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);
            return true;
        }

        /// <summary>等宽字体（JetBrains Mono）没有中文：缺字时退到思源黑体。</summary>
        static void SetMonoFallback(Type faType)
        {
            var sans = AssetDatabase.LoadAssetAtPath(SdfDir + "/NotoSansSC-Regular.asset", faType);
            var prop = faType.GetProperty("fallbackFontAssetTable");
            if (sans == null || prop == null) return;
            foreach (var mono in new[] { "JetBrainsMono-Regular", "JetBrainsMono-Bold" })
            {
                var fa = AssetDatabase.LoadAssetAtPath(SdfDir + "/" + mono + ".asset", faType);
                if (fa == null) continue;
                var list = Activator.CreateInstance(prop.PropertyType) as System.Collections.IList;
                if (list == null) continue;
                list.Add(sans);
                prop.SetValue(fa, list);
                EditorUtility.SetDirty(fa);
            }
        }

        static void Set(SerializedObject so, string prop, Action<SerializedProperty> set)
        {
            var p = so.FindProperty(prop);
            if (p != null) set(p);
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(f)).Replace("-", "");
        }
    }
}

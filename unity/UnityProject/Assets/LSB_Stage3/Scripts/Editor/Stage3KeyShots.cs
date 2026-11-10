// 《最后一次交班》Stage 3 · 6 个关键截图机位 + 遮罩导入设置
// 菜单 LSB/Stage3/截 6 个关键机位：按 03_阶段3主产/Stage3_制作看板.md §2 的 Shot A–F，各截一张 1920×1080 和一张 3840×2160，
// 存到工程根目录 Screenshots_Stage3/<日期时间>/。每张前按机位切一次光照（日景 / 夜景 / 熄灯中 / 熄灯后），截完恢复。
// 编辑器里截的是静帧，不含运行时特效与 UI（记忆显影、熄灯过程、衡日志要在 Play 里录）。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）；未在 Unity 里编译运行过。
using System;
using System.Collections.Generic;
using System.IO;
using LSB.Core;
using LSB.Unity;
using UnityEditor;
using UnityEngine;

namespace LSB.Stage3.EditorTools
{
    public static class Stage3KeyShots
    {
        enum Light4 { Day, Night, LampsHalfOut, AllOut }

        sealed class Shot
        {
            public string Id, Name;
            public Vector3 Pos;
            public float Yaw, Pitch, Focal;
            public Light4 Light;
        }

        static List<Shot> Shots(ContentDB c)
        {
            var list = new List<Shot>
            {
                // A：白盒 SH-A，北侧朝西南逆光看 LT01 与高侧窗光柱
                new Shot { Id = "A", Name = "16点机加工区与LT01", Pos = new Vector3(14.5f, 1.6f, 15.5f), Yaw = 220, Pitch = 3, Focal = 32, Light = Light4.Day },
                // B：N1 原俯 15° 时交班本压在画面下沿（道具模型预览 ShiftDesk/预览/）；截图机位改俯 35°，游戏内节点不变
                Pitch(FromNode(c, "N1", "B", "交班本与车间背景", Light4.Day), 35),
                FromCloseUp(c, "CU2", "C", "1987凹痕记忆显影_静帧", Light4.Night),
                // D：原定 N8 仰 20° 时吊盘挡住搪瓷缸；10-15 改用 CR01 模型候选机位 4（东北 5m、眼高 1.6），
                //    竖向视角与 Blender 预览 40mm/24mm 底片一致 → 本工程 36mm 横向底片约 34mm。水面在缸口下 5mm，从地面看不到（几何限制）。
                new Shot { Id = "D", Name = "天车下提前半拍", Pos = new Vector3(9.2f, 1.6f, 7.8f), Yaw = 217.3f, Pitch = -25.8f, Focal = 34, Light = Light4.Night },
                FromNode(c, "N9", "E", "逐排熄灯_中段", Light4.LampsHalfOut),
                FromNode(c, "N7", "F", "衡启动与RuleID_静帧", Light4.AllOut),
            };
            return list;
        }

        static Shot Pitch(Shot s, float pitch) { s.Pitch = pitch; return s; }

        static Shot FromNode(ContentDB c, string node, string id, string name, Light4 light)
        {
            var n = c.Node(node);
            return new Shot { Id = id, Name = name, Pos = WorkshopRefs.V(n.Pos), Yaw = n.Yaw, Pitch = n.Pitch, Focal = n.FocalMm, Light = light };
        }

        static Shot FromCloseUp(ContentDB c, string cu, string id, string name, Light4 light)
        {
            var d = c.CloseUp(cu);
            var rot = NodeCameraRig.Rot(d.BaseYaw, d.BasePitch);
            return new Shot { Id = id, Name = name, Pos = WorkshopRefs.V(d.Pivot) - rot * Vector3.forward * d.Distance, Yaw = d.BaseYaw, Pitch = d.BasePitch, Focal = d.FocalMm, Light = light };
        }

        [MenuItem("LSB/Stage3/截 6 个关键机位", priority = 100)]
        public static void Capture()
        {
            var root = GameObject.Find(WorkshopGraybox.RootName);
            if (root == null) { EditorUtility.DisplayDialog("LSB Stage3", "场景里没有灰盒。先用 LSB → 1 一键生成 Demo 场景，打开 DemoAlpha 场景再截。", "好"); return; }
            var content = new ContentDB();
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots_Stage3", DateTime.Now.ToString("yyyyMMdd_HHmm"));
            Directory.CreateDirectory(dir);

            var lights = root.GetComponentsInChildren<Light>(true);
            var saved = new Dictionary<Light, bool>();
            foreach (var l in lights) saved[l] = l.enabled;

            var go = new GameObject("LSB_Stage3_ShotCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 120f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
                int count = 0;
                foreach (var s in Shots(content))
                {
                    ApplyLight(lights, saved, s.Light);
                    cam.transform.position = s.Pos;
                    cam.transform.rotation = NodeCameraRig.Rot(s.Yaw, s.Pitch);
                    cam.fieldOfView = NodeCameraRig.VerticalFov(s.Focal, 16f / 9f);
                    foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160) })
                    {
                        Render(cam, size.x, size.y, Path.Combine(dir, $"Shot{s.Id}_{s.Name}_{size.x}x{size.y}.png"));
                        count++;
                    }
                }
                File.WriteAllText(Path.Combine(dir, "说明.txt"),
                    "Stage 3 VS 关键机位静帧（编辑器渲染，不含运行时特效与 UI）。\n" +
                    "这是 VS 内部母机位；场景 / 特效 / 交互的交付成品须另设机位重出（主文档 §7.4）。\n" +
                    "灰盒期截图只用于对比进度，不上展板。\n");
                Debug.Log($"[LSB Stage3] 已截 {count} 张：{dir}");
                EditorUtility.RevealInFinder(dir);
            }
            finally
            {
                foreach (var kv in saved) if (kv.Key != null) kv.Key.enabled = kv.Value;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // 灯名由 WorkshopGraybox 生成：“太阳_16点_西南”、“灯排_{1..6}_X{2..22}”
        static void ApplyLight(Light[] lights, Dictionary<Light, bool> saved, Light4 mode)
        {
            foreach (var l in lights)
            {
                bool on = saved[l];
                string n = l.gameObject.name;
                if (n.StartsWith("太阳")) on = mode == Light4.Day;
                else if (n.StartsWith("灯排_"))
                {
                    int row = RowOf(n);                       // 1 = X2 … 6 = X22；结局由 X22 → X2 熄灭
                    if (mode == Light4.AllOut) on = false;
                    else if (mode == Light4.LampsHalfOut) on = row <= 3;   // X22、X18、X14 已熄，X10 以西还亮
                    else on = true;
                }
                l.enabled = on;
            }
        }

        static int RowOf(string name)
        {
            var parts = name.Split('_');
            return parts.Length > 1 && int.TryParse(parts[1], out var r) ? r : 0;
        }

        static void Render(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = prevTarget;
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    /// <summary>记忆显影遮罩是数据贴图（R 剪影 / G 刀刻线）：关掉 sRGB 和 Mipmap，防止浓度被伽马改掉。</summary>
    sealed class MemoryMaskImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("LSB_Stage3/Resources/LSB_Stage3/MemoryMasks/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.isReadable = false;
        }
    }
}

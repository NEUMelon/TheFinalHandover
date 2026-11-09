// 《最后一次交班》Demo α —— 编辑器菜单（Unity 菜单栏 “LSB”）
//   LSB/1 一键生成 Demo 场景      ：设置 URP（如已安装）→ 生成 UI 面板设置 → 按白盒规格生成三号车间灰盒 → 存为 Scenes/DemoAlpha.unity 并加入 Build Settings
//   LSB/2 只重新生成灰盒          ：在当前场景里重建灰盒（白盒规格改了之后用）
//   LSB/3 节点截图（截图测试）    ：10 个节点 + 5 个特写 + 总闸机位各截一张，存到工程根目录 Screenshots_DemoAlpha/
//   LSB/4 逻辑层自检（无头通关）  ：在编辑器里从新游戏自动走到结局，结果打印到 Console
//   LSB/5 设置 URP 管线           ：单独设置渲染管线
//   LSB/7 打试玩包（非开发构建）  ：2F 可用性测试用。不带开发菜单，打到工程根目录 Builds/DemoAlpha_试玩/
using System;
using System.IO;
using System.Linq;
using LSB.Core;
using LSB.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LSB.EditorTools
{
    public static class LsbEditorMenu
    {
        const string Root = "Assets/LSB";
        const string ScenePath = Root + "/Scenes/DemoAlpha.unity";
        const string MatDir = Root + "/Graybox/Materials";
        const string PanelPath = Root + "/Resources/LSB/UI/LSB_Panel.asset";
        const string ThemePath = Root + "/Resources/LSB/UI/LSB_Theme.tss";

        [MenuItem("LSB/1 一键生成 Demo 场景", priority = 1)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupUrp(false);
            LsbFontAssets.Ensure(false);
            CreatePanelSettings();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<NodeCameraRig>();
            var content = new ContentDB();
            var n1 = content.Node("N1");
            camGo.transform.position = WorkshopRefs.V(n1.Pos);
            camGo.transform.rotation = NodeCameraRig.Rot(n1.Yaw, n1.Pitch);
            cam.fieldOfView = NodeCameraRig.VerticalFov(n1.FocalMm, 16f / 9f);

            BuildGraybox(content);

            new GameObject("LSB_Game").AddComponent<GameBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[LSB] 已生成 {ScenePath}（灰盒：{content.Nodes.Count} 个节点、{content.CloseUps.Count} 个特写、{content.Interactables.Count} 个交互物）。按 Play 运行。");
        }

        [MenuItem("LSB/2 只重新生成灰盒", priority = 2)]
        public static void RebuildGrayboxOnly()
        {
            BuildGraybox(new ContentDB());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void BuildGraybox(ContentDB content)
        {
            Directory.CreateDirectory(MatDir);
            GrayboxMaterials.ClearCache();
            GrayboxMaterials.Persist = m =>
            {
                var path = $"{MatDir}/{m.name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(existing); return existing; }
                AssetDatabase.CreateAsset(m, path);
                return m;
            };
            try { WorkshopGraybox.Build(content); }
            finally { GrayboxMaterials.Persist = null; GrayboxMaterials.ClearCache(); AssetDatabase.SaveAssets(); }
        }

        // =====================================================================
        [MenuItem("LSB/3 节点截图（截图测试）", priority = 20)]
        public static void Screenshots()
        {
            var content = new ContentDB();
            var root = GameObject.Find(WorkshopGraybox.RootName);
            if (root == null) { BuildGraybox(content); }
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots_DemoAlpha");
            Directory.CreateDirectory(dir);
            var go = new GameObject("LSB_ScreenshotCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 120;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
                var rt = new RenderTexture(1920, 1080, 24);
                cam.targetTexture = rt;
                cam.aspect = 16f / 9f;
                int count = 0;
                foreach (var n in content.Nodes) { Pose(cam, WorkshopRefs.V(n.Pos), n.Yaw, n.Pitch, n.FocalMm); Save(cam, rt, Path.Combine(dir, $"{n.Id}_{n.Name}.png")); count++; }
                foreach (var c in content.CloseUps)
                {
                    var rot = NodeCameraRig.Rot(c.BaseYaw, c.BasePitch);
                    var pos = WorkshopRefs.V(c.Pivot) - rot * Vector3.forward * c.Distance;
                    Pose(cam, pos, c.BaseYaw, c.BasePitch, c.FocalMm); Save(cam, rt, Path.Combine(dir, $"{c.Id}_{c.Name}.png")); count++;
                }
                var sp = content.SwitchPose;
                Pose(cam, WorkshopRefs.V(sp.Pos), sp.Yaw, sp.Pitch, sp.FocalMm); Save(cam, rt, Path.Combine(dir, "N9_拉总闸机位.png")); count++;
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt);
                Debug.Log($"[LSB] 已截 {count} 张：{dir}");
                EditorUtility.RevealInFinder(dir);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Pose(Camera cam, Vector3 pos, float yaw, float pitch, float focal)
        {
            cam.transform.position = pos;
            cam.transform.rotation = NodeCameraRig.Rot(yaw, pitch);
            cam.fieldOfView = NodeCameraRig.VerticalFov(focal, 16f / 9f);
        }

        static void Save(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // =====================================================================
        [MenuItem("LSB/4 逻辑层自检（无头通关）", priority = 21)]
        public static void SelfCheck()
        {
            var g = new GameSession();
            var a = new AutoPlayer(g);
            try
            {
                g.Flow.NewGame();
                for (int i = 0; i < 8 && g.State.Phase != Phase.Done; i++) a.SolveCurrent();
                bool ok = g.State.Phase == Phase.Done && g.State.LampsOff == 4;
                var msg = $"[LSB] 逻辑层自检：{(ok ? "通过" : "未通过")}。阶段 {g.State.Phase}，交接 {g.State.LampsOff}/4，等级 {g.Rules.LevelName}，必要证据 {g.Content.Evidence.Count(e => e.Grade == EvidenceGrade.Necessary && g.State.Has(e.Id))}/{g.Content.Evidence.Count(e => e.Grade == EvidenceGrade.Necessary)}。（自动通关只证明流程能走通，不是试玩数据）";
                if (ok) Debug.Log(msg); else Debug.LogError(msg + "\n" + string.Join("\n", a.Trace));
            }
            catch (Exception e) { Debug.LogError("[LSB] 逻辑层自检出错：" + e.Message); }
        }

        // =====================================================================
        /// <summary>
        /// 2F 可用性测试用的构建：非开发构建（Debug.isDebugBuild = false），开发菜单不会挂上来。
        /// 公司名 / 产品名固定成英文，这样试玩记录的位置固定：
        ///   Windows  %USERPROFILE%\AppData\LocalLow\LSB\LastShift_DemoAlpha\playtest\
        ///   macOS    ~/Library/Application Support/LSB/LastShift_DemoAlpha/playtest/
        /// </summary>
        [MenuItem("LSB/7 打试玩包（非开发构建）", priority = 30)]
        public static void BuildPlaytest()
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("[LSB] 还没有 Demo 场景：先运行 LSB/1 一键生成 Demo 场景。"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerSettings.companyName = "LSB";
            PlayerSettings.productName = "LastShift_DemoAlpha";
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
                target = Application.platform == RuntimePlatform.OSXEditor ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64;
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Builds", "DemoAlpha_试玩");
            var name = "LastShift_DemoAlpha" + (target == BuildTarget.StandaloneOSX ? ".app" : target == BuildTarget.StandaloneLinux64 ? "" : ".exe");
            var path = Path.Combine(dir, name);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None });
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[LSB] 试玩包已打好（非开发构建，没有开发菜单）：{path}。试玩记录在每局结束或退出时导出到 Application.persistentDataPath/playtest/。");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError($"[LSB] 试玩包没打成：{report.summary.result}，错误 {report.summary.totalErrors} 个。看 Console 里上面的红字。");
        }

        // =====================================================================
        [MenuItem("LSB/5 设置 URP 管线", priority = 40)]
        public static void SetupUrpMenu() => SetupUrp(true);

        /// <summary>
        /// 通过反射创建 URP 资源并设为默认管线（工程不直接引用 URP 程序集，没装 URP 时灰盒退回 Built-in + Standard 材质照样能看）。
        /// </summary>
        static void SetupUrp(bool verbose)
        {
            try
            {
                if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null) { if (verbose) Debug.Log("[LSB] 已有渲染管线：" + UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name); return; }
                var assetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
                var dataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
                if (assetType == null || dataType == null) { Debug.LogWarning("[LSB] 没找到 URP 包（com.unity.render-pipelines.universal）。灰盒会用 Built-in 管线和 Standard 材质显示。"); return; }
                Directory.CreateDirectory(Root + "/Settings");
                var data = (ScriptableObject)ScriptableObject.CreateInstance(dataType);
                AssetDatabase.CreateAsset(data, Root + "/Settings/LSB_URP_Renderer.asset");
                var create = assetType.GetMethods().FirstOrDefault(m => m.Name == "Create" && m.IsStatic && m.GetParameters().Length == 1);
                var asset = (UnityEngine.Rendering.RenderPipelineAsset)(create != null ? create.Invoke(null, new object[] { data }) : ScriptableObject.CreateInstance(assetType));
                AssetDatabase.CreateAsset(asset, Root + "/Settings/LSB_URP.asset");
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                AssetDatabase.SaveAssets();
                Debug.Log("[LSB] 已创建并启用 URP 管线：" + Root + "/Settings/LSB_URP.asset");
            }
            catch (Exception e) { Debug.LogWarning("[LSB] 自动设置 URP 失败（不影响灰盒显示，可在 Project Settings > Graphics 手动指定）：" + e.Message); }
        }

        static void CreatePanelSettings()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, PanelPath);
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            if (theme != null) ps.themeStyleSheet = theme;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
        }
    }
}

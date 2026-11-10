// 《最后一次交班》Stage 3 · 把 3D 资产摆进当前场景（看效果用）
// 菜单 LSB/Stage3/摆放 3D 资产：把 Assets/LSB_Art/Env 下最新版的 LT01 车床、继电器旧柜放到白盒规格的位置
//   LT01：局部原点 = 床身西端底面中心 → 世界 (8.0, 0, 11.0)，主轴箱在西、操作面朝南
//   旧柜：局部原点 = 柜背底边中点 → 世界 (26.1, 0, 0.0)，柜门朝北（白盒：控制间南墙 X 24.6–27.6，柜深 0.6）
// FBX 从 Blender 来，轴向换算在不同导出设置下会差 90° / 180°，所以这里不信导入角度，而是按部件实际位置自动校正：
//   LT01：上 = 床脚 → 床身，东 = 主轴箱 → 尾座；旧柜：上 = 柜体 → 交接状态板，北 = 柜体 → 两扇柜门。
// 菜单 LSB/Stage3/切换灰盒（被 3D 资产替换的部分）：只隐藏 / 显示被替换的灰盒方块（车床、旧柜外壳 + 各 manifest 的 hide_graybox），方便看新模型；交互、线索、碰撞都不动。
//   运行时由 Stage3ArtBinder 接玩法（卡盘转、柜门开、交接灯灭都跟灰盒同步），Play 时会自动隐藏被替换的灰盒，这个开关只管编辑器里看。
//   注意：如果删掉 LSB_Stage3_美术资产，记得把灰盒外壳切回显示，否则车床和旧柜会变成看不见。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）。
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LSB.Stage3.EditorTools
{
    public static class Stage3PlaceArt
    {
        const string EnvRoot = "Assets/LSB_Art/Env";
        const string HolderName = "LSB_Stage3_美术资产";

        [MenuItem("LSB/Stage3/摆放 3D 资产", priority = 111)]
        public static void Place()
        {
            var holder = GameObject.Find(HolderName);
            if (holder != null) Undo.DestroyObjectImmediate(holder);
            holder = new GameObject(HolderName);
            Undo.RegisterCreatedObjectUndo(holder, "摆放 3D 资产");

            int n = 0;
            var lathe = Spawn("LSB_ENV_MCH_Lathe_LT01", holder.transform);
            if (lathe != null)
            {
                Orient(lathe, up: ("Legs", "Bed"), horiz: ("Headstock", "Tailstock"), horizTarget: Vector3.right,
                       check: ("Bed", "Apron", Vector3.back), new Vector3(8.0f, 0f, 11.0f));
                n++;
            }
            var cab = Spawn("LSB_ENV_CTL_RelayCabinet_Old", holder.transform);
            if (cab != null)
            {
                Orient(cab, up: ("Body", "HandoverBoard"), horiz: ("Body", "Door_L+Door_R"), horizTarget: Vector3.forward,
                       check: ("Body", "HandoverLamp_1", Vector3.zero), new Vector3(26.1f, 0f, 0.0f));
                n++;
            }
            // 其余资产：读 Assets/LSB_Art/Env/**/unity_manifest.json，按 manifest 的实例位置和朝向摆。
            // 所有 FBX 用同一套导出设置（Blender +X 东、+Y 北、+Z 上），所以借 LT01 自动扶正得到的旋转当“轴向校正”；没有 LT01 就借旧柜。
            Quaternion? axisFix = null;
            foreach (var w in new[] { lathe, cab })
                if (w != null && w.transform.localScale.x > 0 && w.transform.localScale.z > 0) { axisFix = w.transform.GetChild(0).rotation; break; }
            if (axisFix == null) Debug.LogWarning("[LSB Stage3] 没有 LT01 / 旧柜可借轴向校正，其余资产按 FBX 导入角度摆放，朝向可能不对。");
            foreach (var guid in AssetDatabase.FindAssets("unity_manifest t:TextAsset", new[] { EnvRoot }))
            {
                var mpath = AssetDatabase.GUIDToAssetPath(guid);
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>(mpath).text;
                Stage3Manifest m;
                try { m = JsonUtility.FromJson<Stage3Manifest>(json); } catch (System.Exception e) { Debug.LogWarning($"[LSB Stage3] {mpath} 解析失败：{e.Message}"); continue; }
                if (m == null || string.IsNullOrEmpty(m.fbx) || m.instances == null) continue;
                var fbxName = System.IO.Path.GetFileName(m.fbx);
                var dir = System.IO.Path.GetDirectoryName(mpath).Replace('\\', '/');
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{fbxName}");
                if (model == null) { Debug.LogWarning($"[LSB Stage3] {mpath}：同目录下找不到 {fbxName}"); continue; }
                foreach (var inst in m.instances)
                {
                    var wrap = new GameObject(string.IsNullOrEmpty(inst.name) ? m.asset : inst.name).transform;
                    wrap.SetParent(holder.transform, false);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(model, wrap);
                    go.transform.localPosition = Vector3.zero;
                    if (axisFix != null) go.transform.localRotation = axisFix.Value;
                    wrap.rotation = Quaternion.Euler(0, inst.yaw, 0);
                    if (inst.pos != null && inst.pos.Length == 3) wrap.position = new Vector3(inst.pos[0], inst.pos[1], inst.pos[2]);
                    var tag = wrap.gameObject.AddComponent<Stage3ArtInstance>();
                    tag.Asset = m.asset; tag.InstanceName = inst.name; tag.ManifestJson = json;
                    n++;
                }
            }
            EditorSceneManager.MarkSceneDirty(holder.scene);
            Selection.activeGameObject = lathe != null ? lathe : holder;
            if (SceneView.lastActiveSceneView != null && lathe != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log($"[LSB Stage3] 已摆放 {n} 个 3D 资产（在 {HolderName} 下）。");
        }

        static GameObject Spawn(string asset, Transform parent)
        {
            var path = AssetDatabase.FindAssets("t:Model", new[] { $"{EnvRoot}/{asset}" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).LastOrDefault();   // 文件名带 _v##，取最新
            if (path == null) { Debug.LogWarning($"[LSB Stage3] 找不到 {EnvRoot}/{asset} 里的 FBX"); return null; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var wrap = new GameObject(asset).transform; wrap.SetParent(parent, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, wrap);
            go.transform.localPosition = Vector3.zero;
            Debug.Log($"[LSB Stage3] 摆放 {path}");
            return wrap.gameObject;
        }

        /// <summary>部件（含子部件）的世界包围盒；part 可写成 "A+B" 取并集。</summary>
        static Bounds B(GameObject root, string part)
        {
            Bounds? acc = null;
            foreach (var name in part.Split('+'))
            {
                var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (t == null) { Debug.LogWarning($"[LSB Stage3] {root.name} 里没有部件 {name}"); continue; }
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (acc == null) acc = r.bounds; else { var b = acc.Value; b.Encapsulate(r.bounds); acc = b; }
                }
            }
            return acc ?? new Bounds(root.transform.position, Vector3.zero);
        }

        static Vector3 Snap(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }

        /// <summary>wrap 下面是模型：先把“上”转到 +Y，再把水平参考方向转到 horizTarget，最后放到 pos。
        /// check：(A, B, 期望方向)——转完后 B 应在 A 的期望方向一侧，否则说明导入是镜像的，用 wrap 的负缩放修正。</summary>
        static void Orient(GameObject wrap, (string a, string b) up, (string a, string b) horiz, Vector3 horizTarget,
                           (string a, string b, Vector3 dir) check, Vector3 pos)
        {
            var model = wrap.transform.GetChild(0);
            wrap.transform.position = Vector3.zero; wrap.transform.rotation = Quaternion.identity; wrap.transform.localScale = Vector3.one;
            var u = Snap(B(wrap, up.b).center - B(wrap, up.a).center);
            model.rotation = Quaternion.FromToRotation(u, Vector3.up) * model.rotation;
            var h = B(wrap, horiz.b).center - B(wrap, horiz.a).center; h.y = 0;
            var hs = Snap(h);
            var turn = Vector3.Dot(hs, horizTarget) < -0.5f ? Quaternion.AngleAxis(180f, Vector3.up) : Quaternion.FromToRotation(hs, horizTarget);  // 反向时只绕竖轴转，别翻倒
            model.rotation = turn * model.rotation;
            if (check.dir != Vector3.zero)
            {
                var d = B(wrap, check.b).center - B(wrap, check.a).center;
                if (Vector3.Dot(d, check.dir) < 0)
                {
                    var s = Vector3.one; if (check.dir.x != 0) s.x = -1; if (check.dir.z != 0) s.z = -1;
                    wrap.transform.localScale = s;
                    Debug.LogWarning($"[LSB Stage3] {wrap.name} 导入后是镜像的，已用负缩放临时修正；请改导出脚本。");
                }
            }
            wrap.transform.position = pos;
            Debug.Log($"[LSB Stage3] {wrap.name}：模型旋转 {model.localEulerAngles}，位置 {pos}");
        }

        // ---------------------------------------------------------------- 灰盒外壳显示切换
        static readonly string[] LatheShell = { "LT01_床脚_西", "LT01_床脚_东", "LT01_床身", "LT01_导轨", "LT01_主轴箱", "LT01_挂轮罩", "LT01_卡盘", "LT01_尾座", "LT01_刀架", "LT01_溜板箱", "LT01_变速手柄" };
        static readonly string[] CabinetShell = { "继电器柜_2_旧柜", "旧柜_柜门" };

        [MenuItem("LSB/Stage3/切换灰盒（被 3D 资产替换的部分）", priority = 112)]
        public static void ToggleGrayboxShell()
        {
            var root = GameObject.Find(LSB.Unity.WorkshopGraybox.RootName);
            if (root == null) { EditorUtility.DisplayDialog("LSB Stage3", "场景里没有灰盒。", "好"); return; }
            var names = LatheShell.Concat(CabinetShell).ToHashSet();
            var pats = new System.Collections.Generic.List<string>();
            foreach (var inst in Object.FindObjectsOfType<Stage3ArtInstance>(true)) { var m = inst.Parse(); if (m?.hide_graybox != null) pats.AddRange(m.hide_graybox); }
            bool Hit(Transform t)
            {
                for (var p = t; p != null && p != root.transform; p = p.parent)
                {
                    if (names.Contains(p.name) || pats.Any(x => x.EndsWith("*") ? p.name.StartsWith(x.Substring(0, x.Length - 1)) : !x.EndsWith("!") && p.name == x)) return true;
                    if (p == t && pats.Any(x => x.EndsWith("!") && p.name == x.Substring(0, x.Length - 1))) return true;   // “名字!” 只算物体自己
                }
                return false;
            }
            var rs = root.GetComponentsInChildren<Renderer>(true).Where(r => Hit(r.transform)).ToArray();
            if (rs.Length == 0) { Debug.LogWarning("[LSB Stage3] 没找到灰盒外壳方块。"); return; }
            bool show = !rs[0].enabled;
            Undo.RecordObjects(rs, "切换灰盒外壳");
            foreach (var r in rs) r.enabled = show;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[LSB Stage3] 灰盒外壳 {rs.Length} 块：{(show ? "显示" : "隐藏")}（只影响编辑器里的显示；Play 时有 3D 资产就自动隐藏）。");
        }
    }
}

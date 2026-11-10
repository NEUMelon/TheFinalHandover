// 《最后一次交班》Stage 3 · 把 3D 资产接进玩法（不改 Demo α 的任何文件）
// 开局时挂到 GameBootstrap.Booted 上：场景里有 “LSB_Stage3_美术资产”（菜单 LSB/Stage3/摆放 3D 资产 生成）就接线，没有就什么都不做。
//   1. 隐藏被替换的灰盒：LT01 整组（含护罩、卡盘、L1、R1）、旧柜外壳、柜门（含交接状态板和 4 灯）、柜内继电器和两条胶布、“LT01 灰盒占位”标签。
//      只关渲染，碰撞盒、交互、线索判定都还走灰盒，逻辑层一行不动。
//   2. 会动的部件跟着灰盒走：WorldPresenter 照旧转灰盒卡盘、开灰盒柜门、换灰盒交接灯材质；
//      这里每帧把灰盒的转动量复制到模型部件上（绕模型自己的枢轴转），交接灯按灰盒灯是否发光切换亮 / 灭材质。
//      旧柜是两扇门：铰链在西侧的门和灰盒同向转，铰链在东侧的门镜像转，两扇一起开。
//   3. 交互高亮：把模型部件加进对应交互物的高亮目标（灰盒被隐藏后，高亮要落在模型上才看得见）。
//   4. 其余资产（天车、道具、灯具、ENV、LT02–04）按实例上的 Stage3ArtInstance（unity_manifest.json）接：hide_graybox / follow / mirror_emission。
// 由 AI 辅助工具编写（AI 辅助代码，记入 AI 使用日志）。
using System.Collections.Generic;
using System.Linq;
using LSB.Unity;
using UnityEngine;

namespace LSB.Stage3
{
    public sealed class Stage3ArtBinder : MonoBehaviour
    {
        public const string HolderName = "LSB_Stage3_美术资产";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameBootstrap.Booted -= OnBooted;
            GameBootstrap.Booted += OnBooted;
        }

        static void OnBooted(GameBootstrap b)
        {
            var holder = GameObject.Find(HolderName);
            if (holder == null || b.World == null) return;
            var binder = holder.GetComponent<Stage3ArtBinder>();
            if (binder == null) binder = holder.AddComponent<Stage3ArtBinder>();
            binder.Bind(b.World);
        }

        /// <summary>灰盒部件 → 模型部件：记下两边的初始世界旋转，之后复制灰盒的旋转增量。</summary>
        sealed class Follow
        {
            public Transform Gray, Art;
            public Quaternion Gray0, Art0;
            public bool MirrorYaw;   // 东侧铰链的柜门：绕竖轴的角度取反
            public bool Rigid;       // true = 整体跟灰盒走（位移 + 绕灰盒枢轴转）；false = 只复制旋转增量、绕模型自己的枢轴转
            public Vector3 Gray0Pos, Art0Pos;
        }

        sealed class Lamp
        {
            public Renderer Gray, Art;
            public Material On, Off;
        }

        readonly List<Follow> follows = new List<Follow>();
        readonly List<Lamp> lamps = new List<Lamp>();
        bool lockerItemsMoved;
        WorkshopRefs world;

        void Bind(WorkshopRefs w)
        {
            world = w;
            follows.Clear(); lamps.Clear(); lockerItemsMoved = false;
            var lathe = transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("LSB_ENV_MCH_Lathe_LT01"));
            var cab = transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("LSB_ENV_CTL_RelayCabinet_Old"));
            int hidden = 0;

            if (lathe != null)
            {
                var g = Child(w.transform, "LT01");
                if (g != null) hidden += Hide(g);
                hidden += HideNamed(w.transform, n => n.StartsWith("标签_LT01"), true);
                AddFollow(w.Spindle, Part(lathe, "Spindle"), false);
                AddFollow(w.GuardHinge, Part(lathe, "Guard"), false);
                AddTargets(w, "guard", Part(lathe, "Guard"));
                AddTargets(w, "l1_mark", Part(lathe, "L1_Mark_Ring"), Part(lathe, "L1_Mark_Inner"), Part(lathe, "Headstock"));
                AddTargets(w, "r1_sensor", Part(lathe, "Sensor_R1"));
            }

            if (cab != null)
            {
                hidden += HideNamed(w.transform, n => n == "继电器柜_2_旧柜" || n.StartsWith("继电器_") || n == "红胶布_护罩87" || n == "白胶布_保留08", false);
                if (w.CabinetDoor != null) hidden += Hide(w.CabinetDoor);
                foreach (var side in new[] { "Door_L", "Door_R" })
                {
                    var door = Part(cab, side);
                    if (door == null) continue;
                    // 门板在枢轴东侧 = 铰链在西侧，和灰盒柜门同向
                    var r = door.GetComponentsInChildren<Renderer>();
                    bool westHinge = r.Length == 0 || Enclose(r).center.x >= door.position.x;
                    AddFollow(w.CabinetDoor, door, !westHinge);
                }
                AddTargets(w, "cabinet_door", Part(cab, "Door_L"), Part(cab, "Door_R"));
                AddTargets(w, "relay_guard", Part(cab, "Relay_R2C3"));
                AddTargets(w, "handover_board", Part(cab, "HandoverBoard"));
                for (int i = 0; i < 4; i++)
                {
                    var a = Part(cab, $"HandoverLamp_{i + 1}");
                    var gr = w.HandoverLamps != null && i < w.HandoverLamps.Length ? w.HandoverLamps[i] : null;
                    var ar = a != null ? a.GetComponent<Renderer>() : null;
                    if (gr == null || ar == null) continue;
                    // 灭 = 深灰 #3A3A38（与 Blender 源文件 M_CAB_HandoverLamp_Off 一致）
                    lamps.Add(new Lamp { Gray = gr, Art = ar, On = ar.sharedMaterial, Off = MakeOff(ar.sharedMaterial, new Color(0.23f, 0.23f, 0.22f)) });
                }
            }

            // 其余资产：按各实例上的 manifest 接线（天车、道具、灯具、ENV、LT02–04 等）
            var hiddenSets = new HashSet<string>();
            foreach (var inst in GetComponentsInChildren<Stage3ArtInstance>(true))
            {
                var m = inst.Parse();
                if (m == null) continue;
                if (m.hide_graybox != null && hiddenSets.Add(m.asset))
                    foreach (var pat in m.hide_graybox)
                    {
                        // 末尾 “!” = 只关这个物体自己的渲染体，不递归（如“更衣柜!”：同名父组下还挂着手套、工牌套等线索道具）
                        if (pat.EndsWith("!")) { var exact = pat.Substring(0, pat.Length - 1); foreach (var t in w.GetComponentsInChildren<Transform>(true)) if (t.name == exact) { var r = t.GetComponent<Renderer>(); if (r != null && r.enabled) { r.enabled = false; hidden++; } } }
                        else hidden += HideNamed(w.transform, Matcher(pat), false);
                    }
                if (m.follow != null)
                    foreach (var f in m.follow)
                    {
                        var gray = ResolveGray(w, f.gray);
                        var art = Part(inst.transform, f.part);
                        if (gray == null || art == null) { Debug.LogWarning($"[LSB Stage3] {inst.name}：跟随 {f.part} → {f.gray} 找不到对象，跳过"); continue; }
                        AddFollow(gray, art, false, f.mode == "rigid");
                    }
                if (m.asset == "LSB_ENV_PRP_Lockers" && w.LockerDoor != null && hiddenSets.Contains(m.asset) && !lockerItemsMoved)
                {
                    // 灰盒的工牌套、建线合影按“门开着（105°）”摆在门内侧；有了实体柜门后关门时会穿出门外。
                    // 把它们挂到老周柜门铰链下，相对位置按门开 105° 时计算，之后跟门一起转。
                    lockerItemsMoved = true;
                    var hinge = w.LockerDoor;
                    var openRot = (hinge.parent != null ? hinge.parent.rotation : Quaternion.identity) * Quaternion.Euler(0, 105f, 0);
                    foreach (var n in new[] { "工牌套（夹着工作照）", "建线合影" })
                    {
                        var t = Child(w.transform, n);
                        if (t == null) continue;
                        var lp = Quaternion.Inverse(openRot) * (t.position - hinge.position);
                        var lr = Quaternion.Inverse(openRot) * t.rotation;
                        var sc = t.lossyScale;
                        t.SetParent(hinge, false);
                        t.localPosition = lp; t.localRotation = lr; t.localScale = sc;
                    }
                }
                if (m.highlight != null)
                    foreach (var h in m.highlight)
                        if (string.IsNullOrEmpty(h.instance) || h.instance == inst.InstanceName) AddTargets(w, h.id, Part(inst.transform, h.part));
                if (m.mirror_emission != null)
                    foreach (var e in m.mirror_emission)
                    {
                        if (!string.IsNullOrEmpty(e.instance) && e.instance != inst.InstanceName) continue;
                        var gt = Child(w.transform, e.gray);
                        var gr = gt != null ? gt.GetComponent<Renderer>() : null;
                        var at = Part(inst.transform, e.part);
                        var ar = at != null ? at.GetComponent<Renderer>() : null;
                        if (gr == null || ar == null) { Debug.LogWarning($"[LSB Stage3] {inst.name}：发光 {e.part} → {e.gray} 找不到对象，跳过"); continue; }
                        // 灯具发光体灭了是白搪瓷色 #E8E4D8，不用交接灯的深灰
                        lamps.Add(new Lamp { Gray = gr, Art = ar, On = ar.sharedMaterial, Off = MakeOff(ar.sharedMaterial, new Color(0.91f, 0.89f, 0.85f)) });
                    }
            }
            LateUpdate();
            Debug.Log($"[LSB Stage3] 3D 资产已接进玩法：隐藏灰盒渲染 {hidden} 个，跟随部件 {follows.Count} 个，发光部件 {lamps.Count} 个。");
        }

        void LateUpdate()
        {
            foreach (var f in follows)
            {
                if (f.Gray == null || f.Art == null) continue;
                if (f.Rigid)
                {
                    // 灰盒物体被 SetActive(false)（如洒水后的 MugWater）时模型部件跟着隐藏
                    bool on = f.Gray.gameObject.activeInHierarchy;
                    if (f.Art.gameObject.activeSelf != on) f.Art.gameObject.SetActive(on);
                    var inv = Quaternion.Inverse(f.Gray0);
                    f.Art.SetPositionAndRotation(f.Gray.position + f.Gray.rotation * (inv * (f.Art0Pos - f.Gray0Pos)), f.Gray.rotation * inv * f.Art0);
                    continue;
                }
                var d = f.Gray.rotation * Quaternion.Inverse(f.Gray0);
                if (f.MirrorYaw)
                {
                    d.ToAngleAxis(out var ang, out var axis);
                    if (ang > 180f) ang -= 360f;
                    d = Quaternion.AngleAxis(-ang * Mathf.Sign(Vector3.Dot(axis, Vector3.up)), Vector3.up);
                }
                f.Art.rotation = d * f.Art0;
            }
            foreach (var l in lamps)
            {
                if (l.Gray == null || l.Art == null) continue;
                var m = l.Gray.sharedMaterial;
                int lit = m != null && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.01f ? 1 : 0;
                var want = lit == 1 ? l.On : l.Off;
                if (l.Art.sharedMaterial != want) l.Art.sharedMaterial = want;   // 柜门高亮复原时可能写回旧材质，每帧对齐
            }
        }

        void AddFollow(Transform gray, Transform art, bool mirrorYaw, bool rigid = false)
        {
            if (gray == null || art == null) return;
            follows.Add(new Follow { Gray = gray, Art = art, Gray0 = rigid ? gray.rotation : GrayRest(gray), Art0 = art.rotation, MirrorYaw = mirrorYaw,
                                     Rigid = rigid, Gray0Pos = gray.position, Art0Pos = art.position });
        }

        static Material MakeOff(Material on, Color? baseColor)
        {
            if (on == null) return null;
            var off = new Material(on) { name = on.name.Replace("_On", "") + "_Off（运行时）" };
            if (baseColor.HasValue) off.SetColor("_BaseColor", baseColor.Value);
            off.SetColor("_EmissionColor", Color.black);
            off.DisableKeyword("_EMISSION");
            return off;
        }

        /// <summary>manifest 里的灰盒名：先当 WorkshopRefs 的 Transform 字段名（如 CraneHook），找不到再按物体名找。</summary>
        static Transform ResolveGray(WorkshopRefs w, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var fi = typeof(WorkshopRefs).GetField(key);
            if (fi != null && typeof(Transform).IsAssignableFrom(fi.FieldType)) return fi.GetValue(w) as Transform;
            return Child(w.transform, key);
        }

        static System.Func<string, bool> Matcher(string pat)
        {
            if (pat.EndsWith("*")) { var p = pat.Substring(0, pat.Length - 1); return n => n.StartsWith(p); }
            return n => n == pat;
        }

        /// <summary>灰盒部件的“静止”世界旋转。读档时柜门可能已经开着，卡盘也可能已经转过：铰链取 localRotation = 0 时的姿态。</summary>
        Quaternion GrayRest(Transform gray)
        {
            if (gray == world.CabinetDoor || gray == world.GuardHinge)
                return gray.parent != null ? gray.parent.rotation : Quaternion.identity;
            return gray.rotation;   // 卡盘：转角无所谓
        }

        static void AddTargets(WorkshopRefs w, string id, params Transform[] parts)
        {
            if (!w.Interactables.TryGetValue(id, out var tag)) return;
            var list = new List<Renderer>(tag.Targets ?? new Renderer[0]);
            foreach (var p in parts) if (p != null) list.AddRange(p.GetComponentsInChildren<Renderer>(true));
            tag.Targets = list.Distinct().ToArray();
        }

        static Transform Part(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static Transform Child(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static int Hide(Transform t)
        {
            int n = 0;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { r.enabled = false; n++; }
            return n;
        }

        static int HideNamed(Transform root, System.Func<string, bool> match, bool wholeObject)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => match(t.name)).ToArray())
            {
                if (wholeObject) { t.gameObject.SetActive(false); n++; }
                else n += Hide(t);
            }
            return n;
        }

        static Bounds Enclose(Renderer[] rs)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}

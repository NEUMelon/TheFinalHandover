// 《最后一次交班》Demo α —— 三号车间灰盒（程序化生成）
// 唯一依据：02_母项目VS/02_车间白盒规格_v0.1.md §1–§4、§7。坐标一律用规格坐标（米；X 东、Y 上、Z 北；原点西南角）。
// 编辑器菜单 “LSB/生成三号车间灰盒场景” 和运行时 GameBootstrap 都调用这里，保证场景和逻辑层用同一份数据。
// ⚠ 全部是灰盒体块 + 纯色占位材质，不是最终模型。场景里的“灰盒 / 占位”标签可以在开发菜单里关掉。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public static class WorkshopGraybox
    {
        public const string RootName = "LSB_三号车间_灰盒";

        sealed class Ctx
        {
            public Transform Root, Structure, Equipment, Props, Lights, Interact, Labels;
            public WorkshopRefs Refs;
            public readonly Dictionary<string, List<Renderer>> Visuals = new Dictionary<string, List<Renderer>>();
            public void Vis(string id, GameObject go) { if (!Visuals.TryGetValue(id, out var l)) Visuals[id] = l = new List<Renderer>(); l.AddRange(go.GetComponentsInChildren<Renderer>()); }
        }

        public static WorkshopRefs Build(ContentDB content, Transform parent = null)
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var c = new Ctx();
            var root = new GameObject(RootName);
            if (parent != null) root.transform.SetParent(parent, false);
            c.Root = root.transform;
            c.Refs = root.AddComponent<WorkshopRefs>();
            c.Structure = Group(c.Root, "结构");
            c.Equipment = Group(c.Root, "设备");
            c.Props = Group(c.Root, "道具");
            c.Lights = Group(c.Root, "灯光");
            c.Interact = Group(c.Root, "交互物");
            c.Labels = Group(c.Root, "灰盒标签");
            c.Refs.LabelsRoot = c.Labels.gameObject;

            BuildStructure(c);
            BuildMachining(c);
            BuildCrane(c);
            BuildControlRoom(c);
            BuildDeskArea(c);
            BuildAnnex(c);
            BuildLights(c);
            BuildInteractables(c, content);
            BuildLabels(c, content);
            return c.Refs;
        }

        // =====================================================================
        // 基本体
        // =====================================================================
        static Transform Group(Transform p, string name) { var g = new GameObject(name).transform; g.SetParent(p, false); return g; }

        /// <summary>按规格中心 + 尺寸放一个方块。blocker = 保留碰撞体（墙、地面挡住射线，防止隔墙点到东西）。</summary>
        public static GameObject Box(Transform p, string name, Vector3 center, Vector3 size, Color col, bool blocker = false, float smooth = 0.2f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(col, smooth);
            if (!blocker) Strip(go);
            return go;
        }

        /// <summary>按两个角点放方块（墙体用）。</summary>
        static GameObject Span(Transform p, string name, float x0, float y0, float z0, float x1, float y1, float z1, Color col, bool blocker = true)
            => Box(p, name, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)), col, blocker);

        /// <summary>圆柱：axis = 'x' / 'y' / 'z'。</summary>
        public static GameObject Cyl(Transform p, string name, Vector3 center, float radius, float length, char axis, Color col, float smooth = 0.3f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);
            if (axis == 'x') go.transform.localRotation = Quaternion.Euler(0, 0, 90);
            if (axis == 'z') go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(col, smooth);
            Strip(go);
            return go;
        }

        static void Strip(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
        }

        // =====================================================================
        // §2 柱网与结构
        // =====================================================================
        static void BuildStructure(Ctx c)
        {
            var s = c.Structure;
            var wall = GrayboxMaterials.Wall; var wain = GrayboxMaterials.Wainscot;
            const float H = 9.0f, T = 0.25f;

            // 地面：水磨石；1m 分格只画主通道和试吊区的安全线
            Span(s, "地面_水磨石", 0, -0.1f, 0, 36, 0, 18, GrayboxMaterials.Floor);
            var lines = Group(s, "安全线");
            Span(lines, "主通道_南线", 0, 0.002f, 6.95f, 24, 0.006f, 7.05f, GrayboxMaterials.Safety, false);
            Span(lines, "主通道_北线", 0, 0.002f, 8.95f, 24, 0.006f, 9.05f, GrayboxMaterials.Safety, false);
            Span(lines, "试吊区_西", 2.95f, 0.002f, 2, 3.05f, 0.006f, 6, GrayboxMaterials.Safety, false);
            Span(lines, "试吊区_东", 8.95f, 0.002f, 2, 9.05f, 0.006f, 6, GrayboxMaterials.Safety, false);
            Span(lines, "试吊区_南", 3, 0.002f, 1.95f, 9, 0.006f, 2.05f, GrayboxMaterials.Safety, false);
            Span(lines, "试吊区_北", 3, 0.002f, 5.95f, 9, 0.006f, 6.05f, GrayboxMaterials.Safety, false);

            // 南墙 Ⓐ（Z=0）：X 0–24 每柱距一樘高侧窗（窗台 3.5，窗顶 6.0）；X 24–30 控制间外墙
            var south = Group(s, "南墙");
            Span(south, "南墙_下", 0, 0, -T, 30, 3.5f, 0, wall);
            Span(south, "南墙_上", 0, 6.0f, -T, 30, H, 0, wall);
            for (int i = 0; i < 4; i++)
            {
                float x = i * 6;
                Span(south, $"南墙_窗间墙{i}", x, 3.5f, -T, x + 0.8f, 6.0f, 0, wall);
                Span(south, $"南窗_{i}_窗框", x + 0.8f, 3.5f, -0.08f, x + 6, 3.56f, 0, GrayboxMaterials.Steel, false);
                for (int k = 1; k < 4; k++) Span(south, $"南窗_{i}_棂{k}", x + 0.8f + k * 1.3f - 0.03f, 3.5f, -0.06f, x + 0.8f + k * 1.3f + 0.03f, 6.0f, -0.02f, GrayboxMaterials.Steel, false);
            }
            Span(south, "南墙_控制间段", 24, 3.5f, -T, 30, 6.0f, 0, wall);
            Span(south, "南墙裙", 0, 0, 0, 30, 1.2f, 0.03f, wain, false);

            // 北墙 Ⓑ（Z=18）：同高钢窗，只进漫射光
            var north = Group(s, "北墙");
            Span(north, "北墙_下", 0, 0, 18, 30, 3.5f, 18 + T, wall);
            Span(north, "北墙_上", 0, 6.0f, 18, 30, H, 18 + T, wall);
            for (int i = 0; i < 5; i++) Span(north, $"北墙_窗间墙{i}", i * 6, 3.5f, 18, i * 6 + 0.8f, 6.0f, 18 + T, wall);
            Span(north, "北墙裙", 0, 0, 17.97f, 30, 1.2f, 18, wain, false);

            // 西山墙（X=0）：车间大门 Z 6.5–10.5，推拉钢门宽 4m，门高 4.5〔派生〕
            var west = Group(s, "西山墙");
            Span(west, "西墙_南段", -T, 0, 0, 0, H, 6.5f, wall);
            Span(west, "西墙_北段", -T, 0, 10.5f, 0, H, 18, wall);
            Span(west, "西墙_门上", -T, 4.5f, 6.5f, 0, H, 10.5f, wall);
            Span(west, "西大门_推拉钢门（半开）", -0.12f, 0, 4.4f, -0.04f, 4.5f, 6.6f, GrayboxMaterials.Steel);
            Span(west, "西墙裙_南", 0, 0, 0, 0.03f, 1.2f, 6.5f, wain, false);
            Span(west, "西墙裙_北", 0, 0, 10.5f, 0.03f, 1.2f, 18, wain, false);

            // 主车间与附房之间（X=30）：附房吊顶 3.6；门：更衣室 Z14、休息区 Z5（宽 1.0，高 2.1〔派生〕）
            // 墙厚只做 4cm（X 29.98–30.02）：规格把总闸（东墙内侧）和奖状（附房一侧）都放在 X≈30 线上
            var east = Group(s, "X30隔墙");
            Span(east, "X30_南段", 29.98f, 0, 0, 30.02f, 3.6f, 4.5f, wall);
            Span(east, "X30_中段", 29.98f, 0, 5.5f, 30.02f, 3.6f, 13.5f, wall);
            Span(east, "X30_北段", 29.98f, 0, 14.5f, 30.02f, 3.6f, 18, wall);
            Span(east, "X30_门上_休息区", 29.98f, 2.1f, 4.5f, 30.02f, 3.6f, 5.5f, wall);
            Span(east, "X30_门上_更衣室", 29.98f, 2.1f, 13.5f, 30.02f, 3.6f, 14.5f, wall);
            Span(east, "X30_上部山墙", 29.98f, 3.6f, 0, 30.02f, H, 18, wall);
            Span(east, "X30_墙裙_西面", 29.97f, 0, 8, 30, 1.2f, 13.5f, wain, false);

            // 柱：X = 0, 6, …, 30；Z 两侧；截面 0.4（X）× 0.6（Z），牛腿顶 6.6
            var cols = Group(s, "柱网");
            for (int i = 0; i <= 5; i++)
            {
                float x = Mathf.Clamp(i * 6, 0.2f, 29.8f);
                foreach (var z in new[] { 0.3f, 17.7f })
                {
                    Box(cols, $"柱_{i + 1}_{(z < 9 ? "A" : "B")}", new Vector3(x, H / 2, z), new Vector3(0.4f, H, 0.6f), GrayboxMaterials.Concrete, true);
                    float dz = z < 9 ? 0.45f : -0.45f;
                    Box(cols, $"牛腿_{i + 1}_{(z < 9 ? "A" : "B")}", new Vector3(x, 6.35f, z + dz), new Vector3(0.4f, 0.5f, 0.3f), GrayboxMaterials.Concrete);
                }
                // 屋架下弦 Y 9.0
                Box(cols, $"屋架下弦_{i + 1}", new Vector3(x, 9.0f, 9), new Vector3(0.25f, 0.3f, 18), GrayboxMaterials.Steel);
                Box(cols, $"屋架上弦_{i + 1}_南", new Vector3(x, 9.75f, 4.5f), new Vector3(0.2f, 0.2f, 9.2f), GrayboxMaterials.Steel).transform.localRotation = Quaternion.Euler(-9.5f, 0, 0);
                Box(cols, $"屋架上弦_{i + 1}_北", new Vector3(x, 9.75f, 13.5f), new Vector3(0.2f, 0.2f, 9.2f), GrayboxMaterials.Steel).transform.localRotation = Quaternion.Euler(9.5f, 0, 0);
            }

            // 吊车梁：沿 Ⓐ、Ⓑ 轴内侧，轨顶 7.0；轨道中心 Z 0.75 / 17.25
            Span(cols, "吊车梁_A", 0, 6.6f, 0.55f, 30, 7.0f, 0.95f, GrayboxMaterials.Concrete, false);
            Span(cols, "吊车梁_B", 0, 6.6f, 17.05f, 30, 7.0f, 17.45f, GrayboxMaterials.Concrete, false);

            // 屋面：天窗（monitor）X 0–24、Z 6–12 抬高到 11.7，侧窗 10.5–11.7 敞开进漫射光
            var roof = Group(s, "屋面");
            Span(roof, "屋面_南", 0, 10.4f, 0, 30, 10.6f, 6, GrayboxMaterials.Concrete);
            Span(roof, "屋面_北", 0, 10.4f, 12, 30, 10.6f, 18, GrayboxMaterials.Concrete);
            Span(roof, "屋面_控制区上方", 24, 10.4f, 6, 30, 10.6f, 12, GrayboxMaterials.Concrete);
            Span(roof, "天窗_顶", 0, 11.7f, 5.8f, 24, 11.9f, 12.2f, GrayboxMaterials.Concrete);
            for (int i = 0; i <= 4; i++) { Span(roof, $"天窗_立柱南{i}", i * 6, 10.6f, 5.9f, i * 6 + 0.12f, 11.7f, 6.0f, GrayboxMaterials.Steel, false); Span(roof, $"天窗_立柱北{i}", i * 6, 10.6f, 12.0f, i * 6 + 0.12f, 11.7f, 12.1f, GrayboxMaterials.Steel, false); }
        }

        // =====================================================================
        // 机加工区：4 台车床、立钻、砂轮机、工件架
        // =====================================================================
        static void BuildMachining(Ctx c)
        {
            Lathe(c, "LT02", 3.4f, false);
            Lathe(c, "LT01", 9.4f, true);
            Lathe(c, "LT03", 15.4f, false);
            Lathe(c, "LT04", 21.4f, false);

            var e = c.Equipment;
            var drill = Group(e, "立钻");
            Box(drill, "立钻_底座", new Vector3(16, 0.1f, 3), new Vector3(0.8f, 0.2f, 0.6f), GrayboxMaterials.MachineGreen);
            Box(drill, "立钻_立柱", new Vector3(16, 1.0f, 3.2f), new Vector3(0.18f, 1.8f, 0.18f), GrayboxMaterials.MachineGreen);
            Box(drill, "立钻_主轴箱", new Vector3(16, 1.75f, 3.0f), new Vector3(0.45f, 0.5f, 0.5f), GrayboxMaterials.MachineGreen);

            var gr = Group(e, "砂轮机");
            Box(gr, "砂轮机_座", new Vector3(19, 0.45f, 2.5f), new Vector3(0.3f, 0.9f, 0.3f), GrayboxMaterials.MachineGreen);
            Box(gr, "砂轮机_电机", new Vector3(19, 1.0f, 2.5f), new Vector3(0.3f, 0.2f, 0.25f), GrayboxMaterials.MachineGreen);
            c.Vis("grinder", Cyl(gr, "砂轮_左", new Vector3(18.75f, 1.0f, 2.5f), 0.12f, 0.04f, 'x', GrayboxMaterials.Concrete));
            c.Vis("grinder", Cyl(gr, "砂轮_右", new Vector3(19.25f, 1.0f, 2.5f), 0.12f, 0.04f, 'x', GrayboxMaterials.Concrete));

            var rack = Group(c.Props, "工件架");
            Box(rack, "工件架_框", new Vector3(2, 0.9f, 16), new Vector3(3.0f, 1.8f, 0.8f), GrayboxMaterials.Steel).transform.localScale = new Vector3(3.0f, 0.06f, 0.8f);
            for (int k = 0; k < 3; k++) Box(rack, $"工件架_层{k}", new Vector3(2, 0.4f + k * 0.6f, 16), new Vector3(3.0f, 0.05f, 0.8f), GrayboxMaterials.Steel);
            for (int k = 0; k < 6; k++) c.Vis("shaft_rack", Cyl(rack, $"半成品轴{k}", new Vector3(2, 1.08f, 15.7f + k * 0.12f), 0.04f, 2.6f, 'x', GrayboxMaterials.Concrete, 0.6f));

            // 广播喇叭（南墙高处）
            c.Vis("speaker", Cyl(c.Props, "广播喇叭", new Vector3(12, 5.0f, 0.45f), 0.18f, 0.35f, 'z', GrayboxMaterials.Dark));
            // 安全标语牌（西山墙内侧）
            c.Vis("slogan", Box(c.Props, "安全标语牌", new Vector3(0.06f, 2.6f, 12.6f), new Vector3(0.02f, 0.6f, 2.4f), GrayboxMaterials.Paint));
        }

        /// <summary>车床：主轴轴线沿 X，主轴箱在西端；操作面朝南。</summary>
        static void Lathe(Ctx c, string id, float cx, bool hero)
        {
            var g = Group(c.Equipment, id);
            var col = hero ? GrayboxMaterials.MachineHero : GrayboxMaterials.MachineGreen;
            float x0 = cx - 1.4f;      // 西端
            // 床身 + 床脚
            Box(g, id + "_床脚_西", new Vector3(x0 + 0.35f, 0.35f, 11.0f), new Vector3(0.6f, 0.7f, 0.8f), col);
            Box(g, id + "_床脚_东", new Vector3(x0 + 2.45f, 0.35f, 11.0f), new Vector3(0.6f, 0.7f, 0.8f), col);
            Box(g, id + "_床身", new Vector3(cx, 0.85f, 11.0f), new Vector3(2.8f, 0.3f, 0.6f), col);
            Box(g, id + "_导轨", new Vector3(cx + 0.3f, 1.02f, 11.0f), new Vector3(2.2f, 0.04f, 0.4f), GrayboxMaterials.Steel, false, 0.7f);
            // 主轴箱 X 8.0–8.9（LT01），顶面 Y 1.3
            var head = Box(g, id + "_主轴箱", new Vector3(x0 + 0.45f, 1.05f, 11.0f), new Vector3(0.9f, 0.5f, 0.7f), col);
            // 挂轮罩：主轴箱西端面下部
            Box(g, id + "_挂轮罩", new Vector3(x0 - 0.06f, 0.95f, 11.0f), new Vector3(0.12f, 0.35f, 0.55f), col);
            // 卡盘 + 主轴
            var chuck = Cyl(g, id + "_卡盘", new Vector3(x0 + 1.05f, 1.15f, 11.0f), 0.13f, 0.12f, 'x', GrayboxMaterials.Steel);
            // 尾座、刀架、溜板箱
            Box(g, id + "_尾座", new Vector3(x0 + 2.55f, 1.15f, 11.0f), new Vector3(0.35f, 0.3f, 0.35f), col);
            Box(g, id + "_刀架", new Vector3(x0 + 1.6f, 1.17f, 10.85f), new Vector3(0.25f, 0.15f, 0.25f), GrayboxMaterials.Steel);
            Box(g, id + "_溜板箱", new Vector3(x0 + 1.6f, 0.82f, 10.6f), new Vector3(0.5f, 0.35f, 0.12f), col);
            Box(g, id + "_变速手柄", new Vector3(x0 + 0.75f, 1.05f, 10.6f), new Vector3(0.03f, 0.2f, 0.03f), GrayboxMaterials.Dark);

            if (!hero) return;
            c.Refs.Spindle = chuck.transform;
            c.Vis("l1_mark", head);
            // L1：主轴箱顶面左端的样冲眼 + Ø60mm 褪色漆圈（手电可显）
            c.Vis("l1_mark", Cyl(g, "L1_漆圈", new Vector3(8.15f, 1.302f, 11.05f), 0.03f, 0.004f, 'y', GrayboxMaterials.Paint));
            Cyl(g, "L1_样冲眼", new Vector3(8.15f, 1.305f, 11.05f), 0.002f, 0.004f, 'y', GrayboxMaterials.Dark);
            // R1：主轴箱前面右侧、变速手柄旁的临时传感器 + 新线
            c.Vis("r1_sensor", Box(g, "R1_传感器", new Vector3(8.80f, 1.15f, 10.63f), new Vector3(0.03f, 0.03f, 0.03f), GrayboxMaterials.Dark));
            Box(g, "R1_新线", new Vector3(8.85f, 0.9f, 10.62f), new Vector3(0.008f, 0.5f, 0.008f), GrayboxMaterials.Safety);
            // 护罩：半圆罩，旋转铰链在后沿（北侧），凹痕在外表面中部
            var hinge = new GameObject("护罩_铰链").transform;
            hinge.SetParent(g, false);
            hinge.localPosition = new Vector3(9.05f, 1.32f, 10.88f);
            c.Refs.GuardHinge = hinge;
            var guard = Box(hinge, "护罩", new Vector3(0, -0.12f, -0.18f), new Vector3(0.5f, 0.24f, 0.04f), GrayboxMaterials.Safety);
            var guardTop = Box(hinge, "护罩_顶", new Vector3(0, 0, -0.09f), new Vector3(0.5f, 0.04f, 0.2f), GrayboxMaterials.Safety);
            Box(hinge, "护罩_凹痕", new Vector3(0, -0.12f, -0.205f), new Vector3(0.1f, 0.07f, 0.01f), GrayboxMaterials.Dark);
            c.Vis("guard", guard); c.Vis("guard", guardTop);
        }

        // =====================================================================
        // 天车 CR01：桥架停在 X 6.0；滑轮组 Y 7.0，绳长 4.0，钩底 Y 3.0；司机室在南端 Z 2.0、Y 5.0
        // =====================================================================
        static void BuildCrane(Ctx c)
        {
            var g = Group(c.Equipment, "CR01_天车");
            var bridge = new GameObject("CR01_桥架").transform; bridge.SetParent(g, false); bridge.localPosition = new Vector3(6.0f, 0, 0);
            c.Refs.CraneBridge = bridge;
            Box(bridge, "桥架_主梁南", new Vector3(0, 7.6f, 9.0f), new Vector3(0.35f, 1.2f, 16.5f), GrayboxMaterials.Safety);
            Box(bridge, "桥架_主梁北", new Vector3(0.9f, 7.6f, 9.0f), new Vector3(0.35f, 1.2f, 16.5f), GrayboxMaterials.Safety).transform.localPosition = new Vector3(-0.9f, 7.6f, 9.0f);
            Box(bridge, "端梁_南", new Vector3(-0.45f, 7.2f, 0.75f), new Vector3(1.6f, 0.4f, 0.4f), GrayboxMaterials.Steel);
            Box(bridge, "端梁_北", new Vector3(-0.45f, 7.2f, 17.25f), new Vector3(1.6f, 0.4f, 0.4f), GrayboxMaterials.Steel);
            var trolley = new GameObject("CR01_小车").transform; trolley.SetParent(bridge, false); trolley.localPosition = new Vector3(0, 0, 4.0f);
            c.Refs.CraneTrolley = trolley;
            Box(trolley, "小车", new Vector3(-0.45f, 8.35f, 0), new Vector3(1.3f, 0.5f, 1.2f), GrayboxMaterials.Steel);
            Cyl(trolley, "滑轮组", new Vector3(0, 7.0f, 0), 0.15f, 0.12f, 'x', GrayboxMaterials.Dark);
            // 绳 + 吊钩 + 搪瓷缸：绕滑轮组（Y 7.0）摆动，摆动轴为 X 方向
            var pivot = new GameObject("吊钩_摆动轴").transform; pivot.SetParent(trolley, false); pivot.localPosition = new Vector3(0, 7.0f, 0);
            c.Refs.CraneRope = pivot;
            Box(pivot, "钢丝绳", new Vector3(0, -2.0f, 0), new Vector3(0.02f, 4.0f, 0.02f), GrayboxMaterials.Dark);
            var hook = new GameObject("吊钩").transform; hook.SetParent(pivot, false); hook.localPosition = new Vector3(0, -4.0f, 0);
            c.Refs.CraneHook = hook;
            Box(hook, "吊钩_钩体", new Vector3(0, 0.1f, 0), new Vector3(0.12f, 0.25f, 0.08f), GrayboxMaterials.Steel);
            Box(hook, "吊盘", new Vector3(0, -0.06f, 0), new Vector3(0.45f, 0.03f, 0.45f), GrayboxMaterials.Steel);
            var mug = Cyl(hook, "搪瓷缸", new Vector3(0, 0.02f, 0), 0.05f, 0.12f, 'y', GrayboxMaterials.Enamel, 0.6f);
            mug.transform.localPosition = new Vector3(0, 0.02f, 0);
            c.Refs.CraneMug = mug.transform;
            var water = Cyl(hook, "缸里的水", new Vector3(0, 0.07f, 0), 0.045f, 0.004f, 'y', GrayboxMaterials.Water, 0.9f);
            c.Refs.MugWater = water.transform;
            // 司机室
            var cab = Box(bridge, "司机室", new Vector3(0.2f, 5.6f, 2.0f), new Vector3(1.4f, 1.8f, 1.4f), GrayboxMaterials.Safety);
            Box(bridge, "司机室_吊杆", new Vector3(0.2f, 6.8f, 2.0f), new Vector3(0.12f, 0.8f, 0.12f), GrayboxMaterials.Steel);
            c.Vis("cab_mug", Cyl(bridge, "司机室_旧搪瓷缸", new Vector3(0, 5.25f, 2.0f), 0.05f, 0.12f, 'y', GrayboxMaterials.Enamel));
            // 衡的调试平板（地上的支架）
            Box(c.Props, "调试平板_支架", new Vector3(7.0f, 0.45f, 5.95f), new Vector3(0.04f, 0.9f, 0.04f), GrayboxMaterials.Steel);
            c.Vis("crane_tablet", Box(c.Props, "调试平板", new Vector3(7.0f, 1.0f, 5.9f), new Vector3(0.3f, 0.22f, 0.02f), GrayboxMaterials.Dark));
        }

        // =====================================================================
        // 控制间（X 24–30，Z 0–8）：玻璃隔断，门在 X 27、Z 8；继电器柜 ×3、衡 MES、总闸
        // =====================================================================
        static void BuildControlRoom(Ctx c)
        {
            var g = Group(c.Equipment, "控制间");
            // 玻璃隔断：灰盒只做框和半透明板的位置；高 3.0〔派生〕
            Span(g, "隔断_Z8_西段", 24, 0, 7.97f, 26.5f, 1.0f, 8.03f, GrayboxMaterials.Wall);
            Span(g, "隔断_Z8_东段", 27.5f, 0, 7.97f, 30, 1.0f, 8.03f, GrayboxMaterials.Wall);
            Span(g, "隔断_Z8_上框", 24, 2.95f, 7.97f, 30, 3.05f, 8.03f, GrayboxMaterials.Steel);
            Span(g, "隔断_X24_下", 23.97f, 0, 0, 24.03f, 1.0f, 8, GrayboxMaterials.Wall);
            Span(g, "隔断_X24_上框", 23.97f, 2.95f, 0, 24.03f, 3.05f, 8, GrayboxMaterials.Steel);
            for (float x = 24; x <= 30.01f; x += 1.5f) if (Mathf.Abs(x - 27) > 0.6f) Span(g, $"隔断_竖框_{x:0.0}", x - 0.03f, 1.0f, 7.97f, x + 0.03f, 2.95f, 8.03f, GrayboxMaterials.Steel, false);
            for (float z = 0; z <= 8.01f; z += 2f) Span(g, $"隔断_X24竖框_{z:0}", 23.97f, 1.0f, z - 0.03f, 24.03f, 2.95f, z + 0.03f, GrayboxMaterials.Steel, false);
            Span(g, "控制间_门框上", 26.5f, 2.1f, 7.97f, 27.5f, 2.95f, 8.03f, GrayboxMaterials.Wall);

            // 继电器柜 ×3：X 24.6–27.6，Z 0.3；中间一台是旧柜
            for (int i = 0; i < 3; i++)
            {
                float x = 25.1f + i;
                var body = Box(g, $"继电器柜_{i + 1}{(i == 1 ? "_旧柜" : "")}", new Vector3(x, 1.0f, 0.3f), new Vector3(0.98f, 2.0f, 0.6f), GrayboxMaterials.Cabinet);
                if (i != 1) continue;
                // 旧柜：柜门（铰链在西侧）+ 柜内继电器排 + 交接状态板（4 灯，Y 1.6）
                var hinge = new GameObject("旧柜_柜门铰链").transform; hinge.SetParent(g, false); hinge.localPosition = new Vector3(x - 0.49f, 1.0f, 0.62f);
                c.Refs.CabinetDoor = hinge;
                var door = Box(hinge, "旧柜_柜门", new Vector3(0.49f, 0, 0), new Vector3(0.96f, 1.94f, 0.03f), GrayboxMaterials.Cabinet);
                c.Vis("cabinet_door", door);
                for (int row = 0; row < 4; row++)
                    for (int k = 0; k < 6; k++)
                    {
                        var relay = Box(g, $"继电器_{row + 1}_{k + 1}", new Vector3(x - 0.35f + k * 0.14f, 1.75f - row * 0.35f, 0.48f), new Vector3(0.1f, 0.12f, 0.1f), GrayboxMaterials.Dark);
                        if (row == 1 && k == 2)
                        {
                            relay.name = "继电器_护罩联锁（图纸上没有）";
                            c.Vis("relay_guard", relay);
                            c.Vis("relay_guard", Box(g, "红胶布_护罩87", new Vector3(relay.transform.localPosition.x, 1.43f, 0.535f), new Vector3(0.1f, 0.03f, 0.005f), GrayboxMaterials.RedTape));
                            c.Vis("relay_guard", Box(g, "白胶布_保留08", new Vector3(relay.transform.localPosition.x, 1.36f, 0.535f), new Vector3(0.1f, 0.025f, 0.005f), GrayboxMaterials.WhiteTape));
                        }
                    }
                var board = Box(door.transform.parent, "交接状态板", new Vector3(0.49f, 0.6f, 0.03f), new Vector3(0.5f, 0.35f, 0.02f), GrayboxMaterials.Dark);
                c.Vis("handover_board", board);
                for (int l = 0; l < 4; l++)
                {
                    var lamp = Cyl(door.transform.parent, $"交接灯_{l + 1}", new Vector3(0.31f + l * 0.12f, 0.6f, 0.045f), 0.03f, 0.02f, 'z', GrayboxMaterials.LampOn);
                    lamp.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.LampOn, 0.5f, 0, GrayboxMaterials.LampOn * 1.5f);
                    c.Refs.HandoverLamps[l] = lamp.GetComponent<Renderer>();
                    c.Vis("handover_board", lamp);
                }
            }

            // 衡 MES：双屏，桌面高 0.75，冷白自发光
            Box(g, "MES_桌", new Vector3(28.5f, 0.375f, 4.3f), new Vector3(1.6f, 0.75f, 0.7f), GrayboxMaterials.Steel);
            var screens = new List<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                var sc = Box(g, $"MES_屏{i + 1}", new Vector3(28.1f + i * 0.8f, 1.10f, 4.0f), new Vector3(0.6f, 0.38f, 0.03f), GrayboxMaterials.Dark);
                sc.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.Screen, 0.8f, 0, GrayboxMaterials.Screen * 0.9f);
                screens.Add(sc.GetComponent<Renderer>());
                c.Vis("mes", sc);
            }
            c.Refs.MesScreens = screens.ToArray();

            // 总闸：东墙（X 30 内侧）刀闸箱 0.6×0.3×0.8（规格写作 L×W×H），Y 1.4
            var sw = Box(g, "总闸_刀闸箱", new Vector3(29.85f, 1.4f, 1.5f), new Vector3(0.3f, 0.8f, 0.6f), GrayboxMaterials.Steel);
            c.Vis("main_switch", sw);
            var handlePivot = new GameObject("总闸_手柄转轴").transform; handlePivot.SetParent(g, false); handlePivot.localPosition = new Vector3(29.68f, 1.55f, 1.5f);
            c.Refs.MainSwitchHandle = handlePivot;
            c.Vis("main_switch", Box(handlePivot, "总闸_手柄", new Vector3(0, 0.18f, 0), new Vector3(0.05f, 0.36f, 0.05f), GrayboxMaterials.Paint));
        }

        // =====================================================================
        // 交班区：交班桌（面向西）、点检单、抽屉、磁带、录音机、台灯、对讲机、工具箱
        // =====================================================================
        static void BuildDeskArea(Ctx c)
        {
            var g = Group(c.Props, "交班桌");
            Box(g, "交班桌_桌面", new Vector3(26.5f, 0.74f, 13.0f), new Vector3(0.8f, 0.04f, 1.6f), GrayboxMaterials.Wood);
            foreach (var dz in new[] { -0.72f, 0.72f }) foreach (var dx in new[] { -0.35f, 0.35f })
                Box(g, "桌腿", new Vector3(26.5f + dx, 0.36f, 13.0f + dz), new Vector3(0.05f, 0.72f, 0.05f), GrayboxMaterials.Wood);
            c.Vis("checklist", Box(g, "玻璃板下的点检单", new Vector3(26.3f, 0.765f, 12.75f), new Vector3(0.3f, 0.004f, 0.21f), GrayboxMaterials.WhiteTape));
            c.Vis("ledger", Box(g, "交班本", new Vector3(26.5f, 0.785f, 13.0f), new Vector3(0.3f, 0.05f, 0.22f), new Color(0.18f, 0.28f, 0.22f)));
            // 抽屉在右侧（面西坐时的右手 = 北）
            var drawer = new GameObject("抽屉").transform; drawer.SetParent(g, false); drawer.localPosition = new Vector3(26.55f, 0.62f, 13.55f);
            c.Refs.DrawerBox = drawer;
            c.Vis("drawer", Box(drawer, "抽屉_盒", Vector3.zero, new Vector3(0.45f, 0.14f, 0.35f), GrayboxMaterials.Wood));
            c.Vis("tape", Box(drawer, "盒式磁带", new Vector3(-0.05f, 0.04f, 0.05f), new Vector3(0.1f, 0.015f, 0.065f), GrayboxMaterials.Dark));
            c.Vis("recorder", Box(drawer, "单卡录音机", new Vector3(0.05f, 0.06f, -0.1f), new Vector3(0.28f, 0.08f, 0.12f), GrayboxMaterials.Steel));
            Box(g, "台灯_座", new Vector3(25.95f, 0.78f, 13.45f), new Vector3(0.14f, 0.03f, 0.14f), GrayboxMaterials.Dark);
            c.Vis("desk_lamp", Box(g, "台灯_杆", new Vector3(25.95f, 0.98f, 13.45f), new Vector3(0.02f, 0.4f, 0.02f), GrayboxMaterials.Dark));
            c.Vis("walkie", Box(g, "对讲机", new Vector3(26.0f, 0.86f, 12.7f), new Vector3(0.06f, 0.18f, 0.04f), GrayboxMaterials.Dark));
            c.Vis("toolbox_desk", Box(g, "工具箱", new Vector3(27.3f, 0.13f, 12.3f), new Vector3(0.5f, 0.25f, 0.25f), GrayboxMaterials.Paint));
            Box(g, "椅子", new Vector3(27.2f, 0.45f, 13.0f), new Vector3(0.45f, 0.06f, 0.45f), GrayboxMaterials.Wood);
        }

        // =====================================================================
        // 附房（X 30–36）：更衣室（北，Z 10–18）+ 休息区（南，Z 0–10），吊顶 3.6
        // =====================================================================
        static void BuildAnnex(Ctx c)
        {
            var s = c.Structure;
            var wall = GrayboxMaterials.Wall; var wain = GrayboxMaterials.Wainscot;
            Span(s, "附房_东墙", 36, 0, 0, 36.25f, 3.6f, 18, wall);
            Span(s, "附房_北墙", 30, 0, 18, 36, 3.6f, 18.25f, wall);
            Span(s, "附房_南墙", 30, 0, -0.25f, 36, 3.6f, 0, wall);
            Span(s, "附房_吊顶", 30, 3.6f, 0, 36.25f, 3.75f, 18.25f, GrayboxMaterials.Wall);
            // 更衣室 / 休息区之间的隔墙（门在 X 33，宽 1.0〔派生〕）
            Span(s, "附房_Z10隔墙_西", 30, 0, 9.9f, 32.5f, 3.6f, 10.1f, wall);
            Span(s, "附房_Z10隔墙_东", 33.5f, 0, 9.9f, 36, 3.6f, 10.1f, wall);
            Span(s, "附房_Z10门上", 32.5f, 2.1f, 9.9f, 33.5f, 3.6f, 10.1f, wall);
            Span(s, "更衣室墙裙", 30.25f, 0, 17.97f, 36, 1.2f, 18, wain, false);

            // 更衣柜 ×6：老周的是东数第 2 个（X 34.1）
            var lk = Group(c.Props, "更衣柜");
            float[] xs = { 30.9f, 31.7f, 32.5f, 33.3f, 34.1f, 34.9f };
            foreach (var x in xs)
            {
                bool zhou = Mathf.Abs(x - 34.1f) < 0.01f;
                var body = Box(lk, zhou ? "更衣柜_老周" : "更衣柜", new Vector3(x, 0.9f, 17.6f), new Vector3(0.6f, 1.8f, 0.5f), new Color(0.45f, 0.52f, 0.55f));
                if (!zhou) { Box(lk, "柜门", new Vector3(x, 0.9f, 17.345f), new Vector3(0.56f, 1.74f, 0.01f), new Color(0.42f, 0.49f, 0.52f)); continue; }
                c.Vis("locker_zhou", body);
                var hinge = new GameObject("老周柜门_铰链").transform; hinge.SetParent(lk, false); hinge.localPosition = new Vector3(x - 0.29f, 0.9f, 17.34f);
                c.Refs.LockerDoor = hinge;
                c.Vis("locker_zhou", Box(hinge, "老周柜门", new Vector3(0.28f, 0, 0), new Vector3(0.56f, 1.74f, 0.015f), new Color(0.42f, 0.49f, 0.52f)));
                // 柜内：三副手套、工牌套（门内侧）、合影、饭盒
                c.Vis("gloves", Box(lk, "劳保手套×3", new Vector3(34.1f, 1.25f, 17.45f), new Vector3(0.3f, 0.06f, 0.18f), new Color(0.85f, 0.82f, 0.72f)));
                c.Vis("badge_photo", Box(lk, "工牌套（夹着工作照）", new Vector3(33.86f, 1.45f, 17.2f), new Vector3(0.09f, 0.12f, 0.005f), new Color(0.75f, 0.2f, 0.18f)));
                c.Vis("photo1956", Box(lk, "建线合影", new Vector3(33.82f, 1.55f, 17.3f), new Vector3(0.005f, 0.18f, 0.25f), new Color(0.6f, 0.58f, 0.52f)));
                c.Vis("lunchbox", Box(lk, "搪瓷饭盒", new Vector3(34.25f, 1.62f, 17.5f), new Vector3(0.18f, 0.08f, 0.12f), GrayboxMaterials.Enamel));
            }
            Box(lk, "更衣凳", new Vector3(33, 0.22f, 16.6f), new Vector3(3.0f, 0.06f, 0.3f), GrayboxMaterials.Wood);

            // 休息区：长桌、条凳、暖水瓶架、奖状
            var rest = Group(c.Props, "休息区");
            Box(rest, "长桌", new Vector3(33.2f, 0.74f, 4.5f), new Vector3(2.4f, 0.04f, 0.8f), GrayboxMaterials.Wood);
            Box(rest, "长桌_腿", new Vector3(33.2f, 0.36f, 4.5f), new Vector3(2.2f, 0.72f, 0.06f), GrayboxMaterials.Wood);
            Box(rest, "条凳_南", new Vector3(33.2f, 0.22f, 3.8f), new Vector3(2.2f, 0.05f, 0.25f), GrayboxMaterials.Wood);
            Box(rest, "条凳_北", new Vector3(33.2f, 0.22f, 5.2f), new Vector3(2.2f, 0.05f, 0.25f), GrayboxMaterials.Wood);
            Box(rest, "暖水瓶架", new Vector3(31.0f, 0.45f, 2.6f), new Vector3(0.45f, 0.9f, 0.25f), GrayboxMaterials.Steel);
            c.Vis("thermos", Cyl(rest, "暖水瓶", new Vector3(31.0f, 1.08f, 2.6f), 0.06f, 0.36f, 'y', GrayboxMaterials.Paint));
            c.Vis("award", Box(rest, "“先进生产者”奖状", new Vector3(30.27f, 1.9f, 4.0f), new Vector3(0.02f, 0.45f, 0.6f), new Color(0.82f, 0.68f, 0.40f)));
        }

        // =====================================================================
        // 灯光：太阳（16:00 西南，高度角约 18°）、6 排 × 3 盏搪瓷灯罩（每排一盏实时点光源，控制灯数）
        // =====================================================================
        static void BuildLights(Ctx c)
        {
            var sun = new GameObject("太阳_16点_西南").AddComponent<Light>();
            sun.transform.SetParent(c.Lights, false);
            sun.type = LightType.Directional;
            sun.transform.localRotation = Quaternion.Euler(18f, 55f, 0f);   // 光从 235°（西南）射向东北
            sun.color = new Color(1.0f, 0.82f, 0.58f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            c.Refs.SunLight = sun;

            float[] xs = { 2, 6, 10, 14, 18, 22 };
            float[] zs = { 4.5f, 9.0f, 13.5f };
            for (int r = 0; r < 6; r++)
            {
                c.Refs.LampRows[r] = new List<Light>();
                c.Refs.LampShades[r] = new List<Renderer>();
                foreach (var z in zs)
                {
                    var shade = Cyl(c.Lights, $"灯罩_X{xs[r]:0}_Z{z:0.0}", new Vector3(xs[r], 5.5f, z), 0.225f, 0.18f, 'y', GrayboxMaterials.Enamel);
                    shade.GetComponent<Renderer>().sharedMaterial = GrayboxMaterials.Get(GrayboxMaterials.Enamel, 0.6f, 0, new Color(1f, 0.85f, 0.6f) * 0.8f);
                    Box(c.Lights, "吊杆", new Vector3(xs[r], 7.25f, z), new Vector3(0.02f, 3.5f, 0.02f), GrayboxMaterials.Dark);
                    c.Refs.LampShades[r].Add(shade.GetComponent<Renderer>());
                }
                var pl = new GameObject($"灯排_{r + 1}_X{xs[r]:0}").AddComponent<Light>();
                pl.transform.SetParent(c.Lights, false);
                pl.transform.localPosition = new Vector3(xs[r], 5.2f, 9.0f);
                pl.type = LightType.Point;
                pl.range = 12f;
                pl.intensity = 1.2f;
                pl.color = new Color(1f, 0.84f, 0.62f);
                pl.shadows = LightShadows.None;
                c.Refs.LampRows[r].Add(pl);
            }
            // 控制间与附房顶灯
            foreach (var p in new[] { new Vector3(27, 2.9f, 4), new Vector3(27, 4.5f, 13), new Vector3(33, 3.4f, 14), new Vector3(33, 3.4f, 5) })
            {
                var l = new GameObject("室内顶灯").AddComponent<Light>();
                l.transform.SetParent(c.Lights, false); l.transform.localPosition = p;
                l.type = LightType.Point; l.range = 8; l.intensity = 0.9f; l.color = new Color(1f, 0.9f, 0.75f);
            }
        }

        // =====================================================================
        // §7 交互物：每个一个碰撞盒 + InteractableTag，ID 与逻辑层一致
        // =====================================================================
        static void BuildInteractables(Ctx c, ContentDB content)
        {
            foreach (var d in content.Interactables)
            {
                var go = new GameObject("I_" + d.Id);
                go.transform.SetParent(c.Interact, false);
                go.transform.localPosition = WorkshopRefs.V(d.Pos);
                var box = go.AddComponent<BoxCollider>();
                // 太小的东西把碰撞盒放大到至少 8cm，方便点
                box.size = new Vector3(Mathf.Max(0.08f, d.Size.x), Mathf.Max(0.08f, d.Size.y), Mathf.Max(0.08f, d.Size.z));
                var tag = go.AddComponent<InteractableTag>();
                tag.Id = d.Id; tag.DisplayName = d.Name; tag.SpecNo = d.SpecNo; tag.PuzzleRelevant = d.PuzzleRelevant;
                if (c.Visuals.TryGetValue(d.Id, out var vis)) tag.Targets = vis.ToArray();
                c.Refs.Register(tag);
            }
            // 会动的部件跟着碰撞盒走：柜门、护罩的碰撞盒挂到铰链下面
            Reparent(c, "guard", c.Refs.GuardHinge);
            Reparent(c, "cabinet_door", c.Refs.CabinetDoor);
            Reparent(c, "tape", c.Refs.DrawerBox);
            Reparent(c, "recorder", c.Refs.DrawerBox);
            Reparent(c, "cab_mug", c.Refs.CraneBridge);
        }

        static void Reparent(Ctx c, string id, Transform to)
        {
            if (to != null && c.Refs.Interactables.TryGetValue(id, out var t)) t.transform.SetParent(to, true);
        }

        // =====================================================================
        // 灰盒标签：标明这是占位
        // =====================================================================
        static void BuildLabels(Ctx c, ContentDB content)
        {
            Label(c, "机加工区（灰盒）", new Vector3(12, 4.2f, 9), 0.5f);
            Label(c, "控制间（灰盒）", new Vector3(27, 3.4f, 4), 0.25f);
            Label(c, "交班区（灰盒）", new Vector3(27, 3.4f, 13), 0.25f);
            Label(c, "更衣室（灰盒）", new Vector3(33, 2.8f, 14), 0.2f);
            Label(c, "休息区（灰盒）", new Vector3(33, 2.8f, 5), 0.2f);
            Label(c, "LT01 灰盒占位（Hero 模型待制作）", new Vector3(9.4f, 2.0f, 11.0f), 0.12f);
            Label(c, "CR01 天车 灰盒占位", new Vector3(6.0f, 6.4f, 9.0f), 0.2f);
            Label(c, "试吊区", new Vector3(6, 0.3f, 2.2f), 0.15f);
        }

        static void Label(Ctx c, string text, Vector3 pos, float size)
        {
            var go = new GameObject("标签_" + text);
            go.transform.SetParent(c.Labels, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = size;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 1f, 1f, 0.55f);
            var f = BuiltinFont();
            if (f != null) { tm.font = f; go.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
            go.AddComponent<FaceCamera>();
        }

        public static Font BuiltinFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return f;
        }
    }

    /// <summary>标签始终朝向相机。</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}

// 《最后一次交班》Demo α —— 空间数据：节点、设备特写、可交互物
// 唯一依据：02_母项目VS/02_车间白盒规格_v0.1.md（§4 摆放、§5 节点、§6 特写、§7 交互物）。改坐标先改白盒规格。
using System.Collections.Generic;
using System.Linq;

namespace LSB.Core
{
    public sealed partial class ContentDB
    {
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<CloseUpDef> CloseUps = new List<CloseUpDef>();
        public readonly List<InteractableDef> Interactables = new List<InteractableDef>();

        /// <summary>
        /// 拉总闸时的机位。规格 §8 写“N9 转身 180°面向总闸”，但从 N9 (27.0, 8.6) 看总闸 (29.75, 1.5) 的方位约 159°，
        /// 转 180°（朝东 90°）是对不上的；Demo 按实际方位朝向总闸（D-04）。
        /// </summary>
        public NodeDef SwitchPose;

        public NodeDef Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
        public CloseUpDef CloseUp(string id) => CloseUps.FirstOrDefault(c => c.Id == id);
        public InteractableDef Interactable(string id) => Interactables.FirstOrDefault(i => i.Id == id);

        // 区域常量
        public const string ZoneMachining = "机加工区";
        public const string ZoneControl = "控制间与交班区";
        public const string ZoneLocker = "更衣休息室";

        void BuildWorld()
        {
            // ---------- §5 十个固定节点（视高 1.6m，N8 例外） ----------
            N("N1", "交班桌", 27.6f, 1.6f, 13.0f, 270, 15, 32, ZoneControl, new[] { "N2", "N3", "N5", "N9" }, new[] { "CU5" });
            N("N2", "老周的更衣柜", 34.1f, 1.6f, 16.2f, 0, 0, 30, ZoneLocker, new[] { "N1", "N10" });
            N("N3", "LT01 车床正面", 9.4f, 1.6f, 9.6f, 0, 10, 32, ZoneMachining, new[] { "N4", "N5", "N8" }, new[] { "CU1", "CU2" });
            N("N4", "主轴箱左端", 7.0f, 1.6f, 10.2f, 60, 0, 35, ZoneMachining, new[] { "N3" }, new[] { "CU1" });
            N("N5", "主通道", 14.0f, 1.6f, 8.0f, 290, 0, 28, ZoneMachining, new[] { "N1", "N3", "N8", "N9" });
            N("N6", "继电器柜", 26.1f, 1.6f, 1.8f, 180, 0, 30, ZoneControl, new[] { "N7", "N9" }, new[] { "CU3" });
            N("N7", "衡的屏幕", 28.5f, 1.6f, 5.2f, 180, 0, 35, ZoneControl, new[] { "N6", "N9" });
            N("N8", "天车试吊区", 7.5f, 0.9f, 6.5f, 225, -20, 28, ZoneMachining, new[] { "N3", "N5" }, new[] { "CU4" });
            N("N9", "控制间门口", 27.0f, 1.6f, 8.6f, 270, 0, 28, ZoneControl, new[] { "N1", "N5", "N6", "N7" });
            N("N10", "休息区", 33.0f, 1.6f, 4.0f, 270, 0, 30, ZoneLocker, new[] { "N2" });

            // 规格 §5 的“可达节点”表不对称（例如 N1 列了 N3，N3 没列 N1），§8 动线还用到表里没有的 N5↔N9。
            // Demo 统一为双向可达，并补上 N5↔N9（登记于 口径与待回写项_DemoAlpha.md D-03）。
            foreach (var a in Nodes)
                foreach (var b in a.Neighbors)
                {
                    var nb = Nodes.First(n => n.Id == b);
                    if (!nb.Neighbors.Contains(a.Id)) nb.Neighbors = nb.Neighbors.Concat(new[] { a.Id }).ToArray();
                }

            {
                var n9 = Nodes.First(n => n.Id == "N9");
                float dx = 29.75f - n9.Pos.x, dz = 1.5f - n9.Pos.z;
                float yaw = (float)(System.Math.Atan2(dx, dz) * 180.0 / System.Math.PI);
                if (yaw < 0) yaw += 360;
                SwitchPose = new NodeDef { Id = "N9_SWITCH", Name = "总闸", Pos = n9.Pos, Yaw = (float)System.Math.Round(yaw), Pitch = 5, FocalMm = 32, Neighbors = new string[0], CloseUps = new string[0], Zone = ZoneControl };
            }

            // ---------- §6 五个设备特写（可旋转 3D 视图，50–85mm） ----------
            CloseUps.Add(new CloseUpDef { Id = "CU1", Name = "LT01 主轴箱", Pivot = new V3(8.45f, 1.18f, 10.85f), Distance = 1.5f, BaseYaw = 15, BasePitch = 22, YawRange = 60, PitchMin = -10, PitchMax = 40, FocalMm = 55, EntryNodes = new[] { "N3", "N4" }, Panel = "diag" });
            CloseUps.Add(new CloseUpDef { Id = "CU2", Name = "LT01 护罩", Pivot = new V3(9.05f, 1.20f, 10.70f), Distance = 1.0f, BaseYaw = 30, BasePitch = 12, YawRange = 45, PitchMin = 0, PitchMax = 30, FocalMm = 60, EntryNodes = new[] { "N3" } });
            CloseUps.Add(new CloseUpDef { Id = "CU3", Name = "继电器柜", Pivot = new V3(26.1f, 1.35f, 0.62f), Distance = 1.3f, BaseYaw = 180, BasePitch = 5, YawRange = 30, PitchMin = -5, PitchMax = 20, FocalMm = 50, EntryNodes = new[] { "N6" } });
            CloseUps.Add(new CloseUpDef { Id = "CU4", Name = "天车防摇曲线", Pivot = new V3(6.0f, 3.2f, 4.0f), Distance = 5.0f, BaseYaw = 215, BasePitch = -12, YawRange = 0, PitchMin = -12, PitchMax = -12, FocalMm = 35, EntryNodes = new[] { "N8" }, Panel = "crane" });
            CloseUps.Add(new CloseUpDef { Id = "CU5", Name = "抽屉与录音机", Pivot = new V3(26.55f, 0.62f, 13.55f), Distance = 0.9f, BaseYaw = 270, BasePitch = 40, YawRange = 30, PitchMin = 25, PitchMax = 55, FocalMm = 50, EntryNodes = new[] { "N1" }, Panel = "tape" });

            // ---------- §7 交互物（规格 26 项；同一项里拆开的子物件共用 SpecNo） ----------
            I(1, "ledger", "交班本", 26.5f, 0.80f, 13.0f, 0.4f, 0.06f, 0.3f, new[] { "N1" }, "全部", true);
            I(2, "mes", "衡的屏幕", 28.5f, 1.10f, 4.0f, 1.2f, 0.5f, 0.2f, new[] { "N7", "N9" }, "全部", true);
            I(3, "locker_zhou", "老周的更衣柜", 34.1f, 0.90f, 17.6f, 0.6f, 1.8f, 0.5f, new[] { "N2" }, "P1", true);
            I(3, "gloves", "劳保手套", 34.1f, 1.25f, 17.45f, 0.35f, 0.25f, 0.2f, new[] { "N2" }, "P1", true);
            I(4, "badge_photo", "工牌套", 33.86f, 1.45f, 17.2f, 0.12f, 0.16f, 0.06f, new[] { "N2" }, "P1", true);
            I(5, "l1_mark", "主轴箱左端", 8.15f, 1.30f, 11.05f, 0.25f, 0.12f, 0.25f, new[] { "N4", "CU1" }, "P1", true);
            I(6, "r1_sensor", "右端传感器", 8.80f, 1.15f, 10.55f, 0.18f, 0.18f, 0.12f, new[] { "CU1" }, "P1", true);
            I(7, "guard", "卡盘护罩", 9.05f, 1.20f, 10.70f, 0.5f, 0.4f, 0.3f, new[] { "N3", "CU2" }, "P3", true);
            I(8, "cabinet_door", "继电器柜柜门", 26.1f, 1.00f, 0.62f, 1.0f, 2.0f, 0.08f, new[] { "N6", "CU3" }, "P3", true);
            I(8, "relay_guard", "贴红胶布的继电器", 26.0f, 1.40f, 0.48f, 0.12f, 0.12f, 0.1f, new[] { "CU3" }, "P3", true);
            I(9, "checklist", "点检单", 26.3f, 0.77f, 12.75f, 0.3f, 0.02f, 0.21f, new[] { "N1" }, "P3", true);
            I(10, "drawer", "抽屉", 26.55f, 0.62f, 13.55f, 0.45f, 0.14f, 0.35f, new[] { "N1", "CU5" }, "P4", true);
            I(10, "tape", "磁带", 26.5f, 0.66f, 13.6f, 0.1f, 0.02f, 0.07f, new[] { "CU5" }, "P4", true);
            I(11, "recorder", "录音机", 26.6f, 0.68f, 13.45f, 0.3f, 0.1f, 0.12f, new[] { "CU5" }, "P4", true);
            I(12, "crane_tablet", "衡的调试平板", 7.0f, 1.00f, 5.9f, 0.3f, 0.25f, 0.06f, new[] { "N8" }, "P4", true);
            I(13, "handover_board", "交接状态板", 26.1f, 1.60f, 0.92f, 0.5f, 0.35f, 0.04f, new[] { "N6", "N9" }, "反馈", false);
            I(14, "main_switch", "总闸", 29.75f, 1.40f, 1.5f, 0.3f, 0.8f, 0.6f, new[] { "N9", "N6" }, "结局", true);
            I(15, "toolbox_desk", "工具箱", 27.3f, 0.25f, 12.3f, 0.5f, 0.25f, 0.25f, new[] { "N1" }, "教学", false);
            I(16, "walkie", "对讲机", 26.0f, 0.82f, 12.7f, 0.07f, 0.2f, 0.05f, new[] { "N1" }, "全部", false);
            I(17, "photo1956", "建线合影", 33.82f, 1.55f, 17.3f, 0.02f, 0.2f, 0.28f, new[] { "N2" }, "氛围", false);
            I(18, "lunchbox", "搪瓷饭盒", 34.25f, 1.62f, 17.5f, 0.2f, 0.1f, 0.14f, new[] { "N2" }, "氛围", false);
            I(19, "cab_mug", "司机室里的搪瓷缸", 6.0f, 5.25f, 2.0f, 0.4f, 0.4f, 0.4f, new[] { "N8" }, "氛围", false);
            I(20, "award", "“先进生产者”奖状", 30.06f, 1.9f, 4.0f, 0.02f, 0.45f, 0.6f, new[] { "N10" }, "氛围", false);
            I(21, "thermos", "暖水瓶", 31.0f, 0.95f, 2.6f, 0.4f, 0.45f, 0.2f, new[] { "N10" }, "氛围", false);
            I(22, "shaft_rack", "工件架上的半成品轴", 2.0f, 1.0f, 16.0f, 3.0f, 1.8f, 0.8f, new[] { "N5" }, "氛围", false);
            I(23, "speaker", "车间广播喇叭", 12.0f, 5.0f, 0.45f, 0.4f, 0.4f, 0.4f, new[] { "N5", "N8" }, "氛围", false);
            I(24, "grinder", "砂轮机", 19.0f, 0.9f, 2.5f, 0.6f, 1.2f, 0.5f, new[] { "N5" }, "氛围", false);
            I(25, "slogan", "安全标语牌", 0.06f, 2.6f, 12.6f, 0.02f, 0.6f, 2.4f, new[] { "N5" }, "氛围", false);
            I(26, "desk_lamp", "台灯", 25.95f, 1.0f, 13.45f, 0.2f, 0.45f, 0.2f, new[] { "N1" }, "氛围", false);
        }

        void N(string id, string name, float x, float y, float z, float yaw, float pitch, float focal, string zone, string[] nb, string[] cu = null)
            => Nodes.Add(new NodeDef { Id = id, Name = name, Pos = new V3(x, y, z), Yaw = yaw, Pitch = pitch, FocalMm = focal, Zone = zone, Neighbors = nb, CloseUps = cu ?? new string[0] });

        void I(int specNo, string id, string name, float x, float y, float z, float sx, float sy, float sz, string[] from, string puzzle, bool relevant)
            => Interactables.Add(new InteractableDef { SpecNo = specNo, Id = id, Name = name, Pos = new V3(x, y, z), Size = new V3(sx, sy, sz), VisibleFrom = from, Puzzle = puzzle, PuzzleRelevant = relevant });
    }
}

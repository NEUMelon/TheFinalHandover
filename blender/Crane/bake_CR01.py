# 天车组 CR01：在 build_CR01.py 的 .blend 上按磨损动线烘焙 3 张贴图集（lsb_bake 流水线），存成 v01 .blend。由 Claude 编写。
# 运行：python3 bake_CR01.py <build 输出目录> [主贴图集边长=2048]
#   CR01      2048：桥架 / 小车 / 司机室（Mid）—— 安全色漆面掉漆露铁红底漆再露铁；手常碰处（门把、控制器手柄、座椅、走台扶手）磨亮
#   CR01Hook  2048：钩头 / 钩 / 吊环 / 吊索 / 吊盘 / 下段钢丝绳（Hero 小件）—— 钢丝绳油污（深色油泥成片、局部露钢）、钩座被吊环磨亮
#   CR01Mug   1024：两只搪瓷缸（Hero 道具）—— 白搪瓷掉瓷露黑底釉 / 铁；司机室旧缸“口沿磕掉一块瓷”（ContentDB.Puzzles.cs:83）
# 搪瓷是釉面：lsb_bake 的漆面粗糙度固定 0.55，烘完后把 CR01Mug 未掉瓷处的粗糙度改成 0.12（光滑度 0.88）。
import bpy, sys, os
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, "..", "Textures"))
import lsb_bake
P, Tl = lsb_bake.Painted, lsb_bake.Tiled
src = os.path.abspath(sys.argv[1]); RES = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
ASSET = "LSB_ENV_CRN_Crane_CR01_v01"
TEX = os.path.join(HERE, "Textures")
bpy.ops.wm.open_mainfile(filepath=os.path.join(src, f"{ASSET}_build.blend"))

PRIMER = "#7A4A3A"       # 铁红底漆（1950–80 年代国营厂设备常见）
MASK_A, MASK_B, MASK_C = "T_LT01_Paint_1983Green_Mask.png", "T_CAB_Body_1956GreyGreen_Mask.png", "T_LT01_Guard_1956Grey_Mask.png"
# 磨损动线（资产坐标 = 桥架停在试吊位、吊钩静止时的位置）：(x, y, z, 半径, 强度[, 磨亮系数])
TOUCH_CRANE = [
    (0.95, 2.45, 5.60, 0.22, 1.0),          # 司机室东门把手
    (0.92, 2.2, 5.0, 0.45, 0.35, 0.0),      # 门下沿踢碰
    (-0.32, 1.93, 5.55, 0.14, 1.0),         # 左控制器手柄
    (0.45, 1.93, 5.55, 0.14, 1.0),          # 右控制器手柄
    (0.12, 1.57, 5.17, 0.25, 0.7),          # 座椅
    (0.12, 1.8, 4.76, 0.55, 0.5),           # 司机室地板（脚下）
    (0.42, 1.62, 5.76, 0.08, 1.0),          # 铃拉绳把手
    (0.35, 2.15, 6.53, 0.3, 0.6),           # 顶部检修口
    (1.52, 2.0, 8.21, 0.7, 0.6),            # 走台扶手（司机室上去的那段）
    (1.52, 9.0, 8.21, 0.9, 0.35),           # 走台扶手跨中（去驱动机构检修）
    (1.15, 9.0, 7.18, 0.9, 0.45, 0.3),      # 走台跨中踩踏
    (1.8, 0.83, 7.43, 0.3, 0.8, 0.0), (-1.8, 0.83, 7.43, 0.3, 0.8, 0.0),       # 南端缓冲器撞击
    (1.8, 17.17, 7.43, 0.3, 0.8, 0.0), (-1.8, 17.17, 7.43, 0.3, 0.8, 0.0),     # 北端缓冲器撞击
    (0.0, 4.0, 8.56, 0.45, 0.35, 0.0),      # 卷筒周围（钢丝绳刮擦）
    (0.0, 4.0, 7.04, 0.25, 0.7, 0.0),       # 导绳器（绳摆动时刮）
]
CR01 = {
    "M_CR01_Paint_GreyGreen": P(MASK_A, "#66746A", PRIMER, TOUCH_CRANE, a1=0.30, a2=0.10, base_w=0.08, edge_k=0.6),
    "M_CR01_Safety_Yellow":   P(MASK_B, "#F2C200", PRIMER, TOUCH_CRANE, a1=0.38, a2=0.12, base_w=0.14, edge_k=0.7),
    "M_CR01_Safety_Black":    P(MASK_B, "#1A1A1A", PRIMER, TOUCH_CRANE, a1=0.30, a2=0.12, base_w=0.12, edge_k=0.7),
    "M_CR01_Machinery":       P(MASK_C, "#4E5852", "#3B3F3A", TOUCH_CRANE, a1=0.25, a2=0.10, base_w=0.08, edge_k=0.6),
    "M_CR01_Wood":            P(MASK_C, "#5A4632", "#3E3024", TOUCH_CRANE, a1=0.35, a2=0.0, base_w=0.1, edge_k=0.5),
    "M_CR01_WireRope_Static": P(MASK_C, "#2F2C26", "#4B463D", TOUCH_CRANE, a1=0.45, a2=0.12, base_w=0.25, edge_k=0.2),
    "M_CR01_Kit2026":         P(MASK_B, "#C9CDCC", "#B0B4B3", [], a1=0.04, a2=0.0, base_w=0.0, edge_k=0.2),   # 2026 新件，几乎无磨损
    "M_CR01_Steel":           Tl("LT01_MachinedSteel"),
    "M_CR01_Bell":            Tl("LT01_WornBright"),
}
HZ = 3.0
TOUCH_HOOK = [
    (0.0, 4.0, HZ + 0.39, 0.07, 1.0, 1.0),     # 钩座：吊环来回磨，露铁磨亮
    (-0.07, 4.0, HZ + 0.52, 0.05, 0.7, 0.0),   # 钩尖磕碰
    (0.0, 4.0, HZ + 1.0, 0.25, 0.4, 0.0),      # 钩头下沿（落地 / 碰工件）
    (0.0, 4.0, HZ - 0.045, 0.07, 0.6, 1.0),    # 吊盘中心：缸底常年放在这
    (0.0, 4.0, HZ + 1.25, 0.2, 0.3, 0.0),      # 动滑轮周围：绳刮
]
HOOK = {
    "M_CR01Hook_Yellow":   P(MASK_B, "#F2C200", PRIMER, TOUCH_HOOK, a1=0.32, a2=0.12, base_w=0.12, edge_k=0.8),
    "M_CR01Hook_Black":    P(MASK_B, "#1A1A1A", PRIMER, TOUCH_HOOK, a1=0.28, a2=0.10, base_w=0.10, edge_k=0.8),
    "M_CR01Hook_Tray":     P(MASK_A, "#5C625E", PRIMER, TOUCH_HOOK, a1=0.30, a2=0.12, base_w=0.10, edge_k=0.7),
    "M_CR01Hook_WireRope": P(MASK_C, "#2F2C26", "#4B463D", TOUCH_HOOK, a1=0.45, a2=0.14, base_w=0.25, edge_k=0.2),
    "M_CR01Hook_Steel":    Tl("LT01_MachinedSteel"),
}
import math
ca = math.radians(60)
TOUCH_MUG = [
    (0.05 * math.cos(ca), 2.0 + 0.05 * math.sin(ca), 5.305, 0.014, 1.0, 0.0),   # 司机室旧缸：口沿磕掉一块瓷
    (0.0, 2.0, 5.185, 0.05, 0.35, 0.0),                                         # 旧缸底圈
    (-0.05, 4.0 - 0.0, HZ - 0.04, 0.025, 0.5, 0.0),                              # 吊钩缸：缸底一处磕点
    (0.035, 4.0 - 0.035, HZ + 0.03, 0.012, 0.8, 0.0),                            # 吊钩缸：缸身一处小掉瓷
]
MUG = {
    "M_CR01Mug_Enamel_White":   P(MASK_B, "#E9E6DC", "#1E2430", TOUCH_MUG, a1=0.10, a2=0.04, base_w=0.03, edge_k=0.5, floor_k=0.0),
    "M_CR01Mug_Enamel_BlueRim": P(MASK_B, "#2C4A7A", "#1E2430", TOUCH_MUG, a1=0.18, a2=0.06, base_w=0.05, edge_k=0.6, floor_k=0.0),
}
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
stats = []
for name, rec, res in (("CR01", CR01, RES), ("CR01Hook", HOOK, RES), ("CR01Mug", MUG, 1024)):
    d = lsb_bake.bake_atlas(objs, name, TEX, rec, res=res)
    stats.append(f"{name}: {res}px density_px_per_m={d:.0f}")
# 搪瓷釉面：未露铁处粗糙度 0.12
ms = np.asarray(Image.open(os.path.join(TEX, "CR01Mug_Atlas_MS.png")).convert("RGBA")).astype(np.float32) / 255
orm = np.asarray(Image.open(os.path.join(TEX, "CR01Mug_Atlas_ORM.png")).convert("RGB")).astype(np.float32) / 255
glaze = ms[..., 0] < 0.05
rough = np.where(glaze, 0.12, orm[..., 1]); orm[..., 1] = rough; ms[..., 3] = 1 - rough
Image.fromarray((ms * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(TEX, "CR01Mug_Atlas_MS.png"))
Image.fromarray((orm * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(TEX, "CR01Mug_Atlas_ORM.png"))
# 水面：不进贴图集（纯色 M_CR01_Water，光滑度高）；状态灯 M_CR01_StatusLED_2026：Unity 端按名字开冷白自发光
for im in bpy.data.images:
    if im.filepath: im.reload()
open(os.path.join(src, "bake_stats.txt"), "w").write("\n".join(stats) + "\n")
print("[bake] " + " | ".join(stats))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(src, f"{ASSET}.blend"))

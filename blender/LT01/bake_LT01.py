# LT01 v02：在 build_LT01.py 产出的 .blend 上按磨损动线烘焙贴图集，替换材质并重新导出 FBX。由 Claude 编写。
# 运行：python3 bake_LT01.py <build 输出目录> [贴图集边长=4096]
import bpy, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, "..", "Textures"))
import lsb_bake
src = os.path.abspath(sys.argv[1]); RES = int(sys.argv[2]) if len(sys.argv) > 2 else 4096
bpy.ops.wm.open_mainfile(filepath=os.path.join(src, "LSB_ENV_MCH_Lathe_LT01_v02.blend"))
M = bpy.data.materials
# 手常碰的位置（局部 = 世界，米）：操作者站在南侧，右手摇溜板手轮、左手伸向主轴箱（规格 §2.4 磨损动线）
TOUCH = [
    (1.56, -0.40, 0.78, 0.30, 1.0),    # 溜板箱手轮：几十年右手进刀，最重
    (1.65, -0.43, 1.105, 0.18, 0.8),   # 中拖板手轮
    (1.65, -0.45, 1.06, 0.30, 0.5),    # 溜板前沿（手腕靠着）
    (0.65, -0.49, 1.20, 0.18, 0.9),    # 变速手柄
    (0.75, -0.47, 1.30, 0.30, 0.7),    # 主轴箱前上沿（左手扶）
    (0.15, 0.05, 1.318, 0.10, 1.0),    # L1 测点周围
    (0.33, -0.41, 0.83, 0.25, 0.6),    # 进给箱手柄
    (2.40, -0.06, 1.25, 0.15, 0.4),    # 尾座锁紧手柄（尾座端最少）
    (1.045, -0.180, 1.267, 0.07, 1.0, 0.0), # 1987 凹痕：撞击处漆皮崩裂、露铁，不磨亮
    (1.05, -0.19, 1.05, 0.15, 0.6),    # 护罩把手
]
RECIPES = {
    "M_LT01_Paint_1983Green": lsb_bake.Painted("T_LT01_Paint_1983Green_Mask.png", "#5F7F72", "#8A968C", TOUCH, a1=0.30, a2=0.10, base_w=0.06, edge_k=0.7),
    "M_LT01_Guard_1956Grey": lsb_bake.Painted("T_LT01_Guard_1956Grey_Mask.png", "#6E7A72", "#4A564F", TOUCH, a1=0.40, a2=0.14, base_w=0.12, edge_k=0.7),
    "M_LT01_CastIron": lsb_bake.Tiled("LT01_CastIron"),
    "M_LT01_ChipTray": lsb_bake.Tiled("LT01_CastIron"),
    "M_LT01_MachinedSteel": lsb_bake.Tiled("LT01_MachinedSteel"),
    "M_LT01_WornBright": lsb_bake.Tiled("LT01_WornBright"),
}
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
d = lsb_bake.bake_atlas(objs, "LT01", os.path.join(HERE, "..", "Textures", "Atlas"), RECIPES, res=RES)
open(os.path.join(src, "bake_stats.txt"), "w").write(f"atlas={RES}\ndensity_px_per_m={d:.0f}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(src, "LSB_ENV_MCH_Lathe_LT01_v02.blend"))
bpy.ops.export_scene.fbx(filepath=os.path.join(src, "LSB_ENV_MCH_Lathe_LT01_v02.fbx"),
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, add_leaf_bones=False)

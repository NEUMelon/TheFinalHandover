# 继电器柜 v02：在 build_RelayCabinet.py 产出的 .blend 上按磨损动线烘焙贴图集，替换材质并重新导出 FBX。由 AI 辅助工具编写。
# 运行：python3 bake_RelayCabinet.py <build 输出目录> [贴图集边长=4096]
import bpy, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, "..", "Textures"))
import lsb_bake
src = os.path.abspath(sys.argv[1]); RES = int(sys.argv[2]) if len(sys.argv) > 2 else 4096
bpy.ops.wm.open_mainfile(filepath=os.path.join(src, "LSB_ENV_CTL_RelayCabinet_Old_v02.blend"))
# 柜门朝 +Y；门关着烘。手常碰：门中缝把手和一字锁、推门的中缝两侧；柜内基本不碰
TOUCH = [
    (-0.03, 0.63, 1.07, 0.22, 1.0),    # 右门把手
    (0.03, 0.63, 1.07, 0.18, 0.8),     # 左门一字锁（螺丝刀常刮）
    (0.0, 0.63, 1.30, 0.40, 0.4),      # 中缝两侧推门
    (0.0, 0.63, 0.25, 0.45, 0.4),      # 门下沿踢门
]
RECIPES = {
    "M_CAB_Body_1956GreyGreen": lsb_bake.Painted("T_CAB_Body_1956GreyGreen_Mask.png", "#6E7A72", "#59645D", TOUCH, a1=0.28, a2=0.08, base_w=0.05, edge_k=0.6),
    "M_CAB_Plinth": lsb_bake.Tiled("LT01_CastIron"),
    "M_CAB_DinRail": lsb_bake.Tiled("LT01_MachinedSteel"),
    "M_CAB_Lock": lsb_bake.Tiled("LT01_WornBright"),
}
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
d = lsb_bake.bake_atlas(objs, "CAB", os.path.join(HERE, "..", "Textures", "Atlas"), RECIPES, res=RES)
open(os.path.join(src, "bake_stats.txt"), "w").write(f"atlas={RES}\ndensity_px_per_m={d:.0f}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(src, "LSB_ENV_CTL_RelayCabinet_Old_v02.blend"))
bpy.ops.export_scene.fbx(filepath=os.path.join(src, "LSB_ENV_CTL_RelayCabinet_Old_v02.fbx"),
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, add_leaf_bones=False)

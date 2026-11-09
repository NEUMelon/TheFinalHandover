# 天车组 CR01 导出 Unity FBX（由 Claude 编写）：python3 export_CR01_fbx.py <v01.blend> <输出.fbx>
# 复用 20_场景地编/stage3/export_fbx_unity.py 的 flatten()（按部件合并，部件只有位置、没有旋转），只额外登记本资产的嵌套部件名。
import bpy, sys, os
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(HERE, ".."))
import export_fbx_unity as E
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, fbx = os.path.abspath(args[0]), os.path.abspath(args[1])
PARTS = {"CR01_Bridge", "CR01_Trolley", "CR01_Rope", "CR01_Hook", "CR01_Mug", "CR01_MugWater", "CR01_CabMug"}
E.KEEP |= PARTS
bpy.ops.wm.open_mainfile(filepath=blend)
root = bpy.data.objects["LSB_ENV_CRN_Crane_CR01_v01"]
objs = E.flatten(root)
tris = 0
for o in objs:
    o.data.calc_loop_triangles(); n = len(o.data.loop_triangles); tris += n
    print(f"[part] {o.name} parent={o.parent.name if o.parent else '-'} pivot={tuple(round(v, 4) for v in o.matrix_world.translation)} tris={n} mats={[m.name for m in o.data.materials]}")
print(f"parts={len(objs)} tris={tris}")
bpy.ops.export_scene.fbx(filepath=fbx, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=False, add_leaf_bones=False)
print("exported", fbx)

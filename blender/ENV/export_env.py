# ENV 资产导出 FBX（复用 ../export_fbx_unity.flatten，不改那个文件）。由 AI 辅助工具编写（AI 辅助，已登记 AI 使用日志）。
# 运行：python3 export_env.py <blend> <fbx> [--single 部件名]
#   --single：把整个资产合成一个部件（背景车床用：没有会动的部件，合成一个省 Draw Call）；此时清空 KEEP，免得 LT01 的部件名被拆出来。
import bpy, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import export_fbx_unity as E
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, fbx = args[0], args[1]
single = args[args.index("--single") + 1] if "--single" in args else None
bpy.ops.wm.open_mainfile(filepath=os.path.abspath(blend))
scn = bpy.context.scene
root = next(o for o in scn.objects if o.parent is None and o.type == 'EMPTY' and o.name.startswith("LSB_ENV_"))
if single:
    E.KEEP = set()
    holder = bpy.data.objects.new(single, None); scn.collection.objects.link(holder); holder.parent = root
    for o in list(root.children):
        if o is holder: continue
        mw = o.matrix_world.copy(); o.parent = holder; o.matrix_world = mw
objs = E.flatten(root)
tris = 0
for o in objs: o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
print(f"parts={len(objs)} tris={tris} names={[o.name for o in objs]}")
bpy.ops.export_scene.fbx(filepath=os.path.abspath(fbx), apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=False, add_leaf_bones=False)
print("exported", fbx)

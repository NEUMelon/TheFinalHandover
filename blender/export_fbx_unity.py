# 把 v02 .blend 导出成给 Unity 的 FBX（由 Claude 编写）。运行：python3 export_fbx_unity.py <blend> <fbx>
# v02 第一次导出（多层空物体 + 旋转父级 + 实验性 bake_space_transform）在 Unity 里整体躺倒、手轮脱位（10-15 引擎内截图）。
# 现在的做法：
#   1. 按“部件”合并网格：根下的每个直接子物体是一个部件；KEEP 里的名字（凹痕、手轮、柜门、交接灯、联锁继电器、胶布等）单独成部件。
#   2. 每个部件只有位置（原点 = 原来的枢轴，如护罩铰链、柜门铰链、卡盘轴线），没有旋转和缩放；层级只保留部件之间的父子关系。
#   3. 用 Blender 默认轴向换算导出（不用 bake_space_transform）；Unity 端由“摆放 3D 资产”菜单按部件位置自动校正朝向。
import bpy, sys
from mathutils import Matrix

KEEP = {
    # LT01
    "Spindle", "Guard", "Guard_Dent", "Carriage", "CrossSlide", "ToolPost", "Apron", "Apron_Handwheel",
    "CrossSlide_Handwheel", "Tailstock", "Tailstock_Handwheel", "Headstock", "Legs", "Bed", "ChipTray", "ChangeGearCover",
    "Sensor_R1", "Cable_2025", "L1_Mark_Ring", "L1_Mark_Inner", "Nameplate", "WorkLamp",
    # 继电器柜
    "Body", "Relays", "Wiring", "Door_L", "Door_R", "HandoverBoard",
    "HandoverLamp_1", "HandoverLamp_2", "HandoverLamp_3", "HandoverLamp_4",
    "Relay_R2C3", "Relay_R2C3_RedTape_1987", "Relay_R2C3_WhiteTape_2008", "Wire_2025_GuardInterlock",
}

def flatten(root):
    scn = bpy.context.scene
    parts = list(dict.fromkeys(list(root.children) + [o for o in scn.objects if o.name in KEEP and o is not root]))
    part_set = set(parts)
    def owner(o):
        p = o
        while p is not None and p is not root:
            if p in part_set: return p
            p = p.parent
        return None
    built = {}
    for P in parts:
        meshes = [o for o in scn.objects if o.type == 'MESH' and owner(o) is P]
        if not meshes: continue
        pivot = P.matrix_world.translation.copy()
        dups = []
        for m in meshes:
            d = m.copy(); d.data = m.data.copy(); scn.collection.objects.link(d)
            mw = m.matrix_world.copy(); d.parent = None; d.matrix_world = mw
            dups.append(d)
        bpy.ops.object.select_all(action='DESELECT')
        for d in dups: d.select_set(True)
        bpy.context.view_layer.objects.active = dups[0]
        if len(dups) > 1: bpy.ops.object.join()
        j = bpy.context.view_layer.objects.active
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        j.data.transform(Matrix.Translation(-pivot))
        j.location = pivot
        built[P] = (j, pivot, P.name)
    def parent_part(P):
        p = P.parent
        while p is not None and p is not root:
            if p in built: return p
            p = p.parent
        return None
    # 原物体删掉后 P 会失效，先全部换成名字
    items = [(name, j, pivot, (built[rel].__getitem__(2) if (rel := parent_part(P)) is not None else None)) for P, (j, pivot, name) in built.items()]
    keep_objs = {j for _, j, _, _ in items}
    for o in list(scn.objects):
        if o not in keep_objs: bpy.data.objects.remove(o, do_unlink=True)
    byname = {name: (j, pivot) for name, j, pivot, _ in items}
    for name, j, pivot, pname in items:
        if pname is not None:
            pj, ppiv = byname[pname]
            j.parent = pj; j.matrix_parent_inverse = Matrix.Identity(4); j.location = pivot - ppiv
    for name, j, _, _ in items:
        j.name = name; j.data.name = name
    built = {name: (j, None, name) for name, j, _, _ in items}
    return [b[0] for b in built.values()]

if __name__ == "__main__":
    blend, fbx = sys.argv[-2], sys.argv[-1]
    bpy.ops.wm.open_mainfile(filepath=blend)
    root = next(o for o in bpy.context.scene.objects if o.parent is None and o.type == 'EMPTY' and o.name.startswith("LSB_ENV_"))
    objs = flatten(root)
    tris = 0
    for o in objs: o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
    print(f"parts={len(objs)} tris={tris}")
    bpy.ops.export_scene.fbx(filepath=fbx, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=False, add_leaf_bones=False)
    print("exported", fbx)

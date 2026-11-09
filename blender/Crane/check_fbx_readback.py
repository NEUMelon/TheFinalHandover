import bpy, sys
from mathutils import Vector
blend, fbx = sys.argv[1], sys.argv[2]
def centers():
    d = {}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        d[o.name] = (sum(bb, Vector()) / 8, o.matrix_world.translation.copy(), o.parent.name if o.parent else '-')
    return d
bpy.ops.wm.open_mainfile(filepath=blend)
# .blend：按部件汇总的世界包围盒中心（逐网格合并）
def owner(o):
    p = o.parent
    while p is not None and p.type != 'EMPTY': p = p.parent
    return p
groups = {}
for o in bpy.context.scene.objects:
    if o.type == 'MESH':
        groups.setdefault(owner(o).name, []).extend([o.matrix_world @ Vector(c) for c in o.bound_box])
ref = {k: (Vector((min(p.x for p in v), min(p.y for p in v), min(p.z for p in v))) + Vector((max(p.x for p in v), max(p.y for p in v), max(p.z for p in v)))) / 2 for k, v in groups.items()}
piv = {o.name: o.matrix_world.translation.copy() for o in bpy.context.scene.objects if o.type == 'EMPTY'}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
bpy.context.view_layer.update()
worst = 0
for o in bpy.context.scene.objects:
    if o.type != 'MESH': continue
    me = o.data; mw = o.matrix_world
    pts = [mw @ v.co for v in me.vertices]
    c = (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))) + Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))) / 2
    dc = (c - ref[o.name]).length; dp = (mw.translation - piv[o.name]).length
    worst = max(worst, dc, dp)
    print(f"{o.name:14s} parent={o.parent.name if o.parent else '-':13s} origin={tuple(round(v,3) for v in mw.translation)} (blend pivot {tuple(round(v,3) for v in piv[o.name])}) bbox_center_err={dc*1000:.2f}mm pivot_err={dp*1000:.2f}mm rot={tuple(round(v,4) for v in mw.to_euler())} tris={sum(len(p.vertices)-2 for p in me.polygons)}")
print(f"WORST_ERR_MM={worst*1000:.3f}")

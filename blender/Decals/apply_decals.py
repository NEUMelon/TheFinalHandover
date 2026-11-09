# 给 LT01 铭牌、联锁继电器两条胶布的正面铺 UV 并挂贴花贴图，另存 v04 .blend（v02 不动）。由 Claude 编写，记入 AI 使用日志。
# 运行：python3 apply_decals.py <src.blend> <dst.blend>
# Unity 端：Stage3ArtMaterials 发现 Textures/<材质名去掉 M_>_BaseColor.png 就把它当 _BaseMap（M_LT01_Nameplate → LT01_Nameplate_BaseColor.png）。
import bpy, sys, os
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__))
# 物体名 → (贴图, 正面法线方向, 观众看过去时 u 增大的世界方向)
SPECS = {
    "Nameplate": ("LT01_Nameplate_BaseColor.png", Vector((0, -1, 0)), Vector((1, 0, 0))),          # 车床操作面朝 −Y
    "Relay_R2C3_RedTape_1987": ("CAB_RedTape_1987_BaseColor.png", Vector((0, 1, 0)), Vector((-1, 0, 0))),   # 柜门朝 +Y，观众右手 = −X
    "Relay_R2C3_WhiteTape_2008": ("CAB_WhiteTape_2008_BaseColor.png", Vector((0, 1, 0)), Vector((-1, 0, 0))),
}
src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=src)
done = 0
for name, (img, front, right) in SPECS.items():
    o = bpy.data.objects.get(name)
    if o is None: continue
    me = o.data; mw = o.matrix_world; nm = mw.to_3x3().inverted().transposed()
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    pts = [mw @ v.co for v in me.vertices]
    fr = [p for f in me.polygons if (nm @ f.normal).normalized().dot(front) > 0.7 for p in (pts[i] for i in f.vertices)]
    us = [p.dot(right) for p in fr]; vs = [p.z for p in fr]
    u0, u1, v0, v1 = min(us), max(us), min(vs), max(vs)
    for f in me.polygons:
        is_front = (nm @ f.normal).normalized().dot(front) > 0.7
        for li in f.loop_indices:
            p = pts[me.loops[li].vertex_index]
            uv[li].uv = ((p.dot(right) - u0) / (u1 - u0), (p.z - v0) / (v1 - v0)) if is_front else (0.01, 0.5)
    mat = me.materials[0]
    mat.use_nodes = True
    nt = mat.node_tree; bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(os.path.join(HERE, img))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    done += 1
    print("decal", name, mat.name, f"front {len(fr)} verts")
bpy.ops.wm.save_as_mainfile(filepath=dst)
print("saved", dst, done)

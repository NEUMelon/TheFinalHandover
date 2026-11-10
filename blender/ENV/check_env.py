# ENV FBX 回读检查（由 AI 辅助工具编写）：python3 check_env.py <blend> <fbx> [--colliders]
# 1. Blender 重新导入 FBX，逐部件比较世界包围盒（中心、尺寸）与 .blend 里的同名部件；列材质、UV 层。
# 2. --colliders：取 WorkshopGraybox.cs 里所有 blocker 方块（墙 / 地面 / 柱 / 屋面 / 隔断下段，坐标抄自源码），
#    在每个方块露出的表面上撒点，量到模型表面的最近距离——模型外形应与碰撞体重合（距离 ≈ 0）。
#    跳过：地面以下的点、落在别的 blocker 里面的点（两块相接处）、墙在 Z 9.0 的顶面（模型故意封到 10.6）。
import bpy, sys, os, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, fbx = os.path.abspath(args[0]), os.path.abspath(args[1])
bpy.ops.wm.open_mainfile(filepath=blend)
def bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return lo, hi
# blend 里的部件 = 根的直接子物体（及其子孙合起来）
root = next(o for o in bpy.context.scene.objects if o.parent is None and o.type == 'EMPTY' and o.name.startswith("LSB_ENV_"))
ref = {}
def acc(o, key):
    if o.type == 'MESH':
        lo, hi = bounds(o)
        if key in ref: a, b = ref[key]; ref[key] = (Vector(map(min, a, lo)), Vector(map(max, b, hi)))
        else: ref[key] = (lo, hi)
    for c in o.children: acc(c, key)
kids = list(root.children)
if len(kids) > 1 or "--single" not in args:
    for c in kids: acc(c, c.name)
if "--single" in args:
    ref = {}; [acc(c, args[args.index("--single") + 1]) for c in kids]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
worst = 0.0; tris = 0
for o in sorted(meshes, key=lambda o: o.name):
    lo, hi = bounds(o); o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
    r = ref.get(o.name)
    if r is None: print(f"  {o.name}: 不在 blend 里？"); continue
    d = max((lo - r[0]).length, (hi - r[1]).length); worst = max(worst, d)
    c = (lo + hi) / 2
    print(f"  {o.name:22s} center=({c.x:7.3f},{c.y:7.3f},{c.z:7.3f}) size=({hi.x-lo.x:6.3f},{hi.y-lo.y:6.3f},{hi.z-lo.z:6.3f}) Δ={d*1000:.2f}mm rot={tuple(round(math.degrees(a),1) for a in o.rotation_euler)} scale={tuple(round(s,3) for s in o.scale)} uv={[u.name for u in o.data.uv_layers]} mats={[m.name for m in o.data.materials]}")
print(f"[check] parts={len(meshes)} tris={tris} 最大包围盒偏差={worst*1000:.2f} mm（相对 .blend）")
if "--colliders" not in args: sys.exit(0)
for o in meshes: o.data.transform(o.matrix_world); o.matrix_world = o.matrix_world.Identity(4)
bpy.context.view_layer.update()
verts, polys = [], []
for o in meshes:
    base = len(verts); verts += [tuple(v.co) for v in o.data.vertices]
    polys += [tuple(base + i for i in p.vertices) for p in o.data.polygons]
bvh = BVHTree.FromPolygons(verts, polys)
B = {}   # 名字 → (x0,x1, y0,y1, z0,z1)，Blender 坐标（y = Unity Z）
B["地面_水磨石"] = (0, 36, 0, 18, -0.1, 0)
B["南墙_下"] = (0, 30, -0.25, 0, 0, 3.5); B["南墙_上"] = (0, 30, -0.25, 0, 6, 9)
for i in range(4): B[f"南墙_窗间墙{i}"] = (i * 6, i * 6 + 0.8, -0.25, 0, 3.5, 6)
B["南墙_控制间段"] = (24, 30, -0.25, 0, 3.5, 6)
B["北墙_下"] = (0, 30, 18, 18.25, 0, 3.5); B["北墙_上"] = (0, 30, 18, 18.25, 6, 9)
for i in range(5): B[f"北墙_窗间墙{i}"] = (i * 6, i * 6 + 0.8, 18, 18.25, 3.5, 6)
B["西墙_南段"] = (-0.25, 0, 0, 6.5, 0, 9); B["西墙_北段"] = (-0.25, 0, 10.5, 18, 0, 9); B["西墙_门上"] = (-0.25, 0, 6.5, 10.5, 4.5, 9)
B["西大门_推拉钢门（半开）"] = (-0.12, -0.04, 4.4, 6.6, 0, 4.5)
B["X30_南段"] = (29.98, 30.02, 0, 4.5, 0, 3.6); B["X30_中段"] = (29.98, 30.02, 5.5, 13.5, 0, 3.6); B["X30_北段"] = (29.98, 30.02, 14.5, 18, 0, 3.6)
B["X30_门上_休息区"] = (29.98, 30.02, 4.5, 5.5, 2.1, 3.6); B["X30_门上_更衣室"] = (29.98, 30.02, 13.5, 14.5, 2.1, 3.6); B["X30_上部山墙"] = (29.98, 30.02, 0, 18, 3.6, 9)
for i in range(6):
    x = min(max(i * 6, 0.2), 29.8)
    for yc, s in ((0.3, "A"), (17.7, "B")): B[f"柱_{i+1}_{s}"] = (x - 0.2, x + 0.2, yc - 0.3, yc + 0.3, 0, 9)
B["屋面_南"] = (0, 30, 0, 6, 10.4, 10.6); B["屋面_北"] = (0, 30, 12, 18, 10.4, 10.6); B["屋面_控制区上方"] = (24, 30, 6, 12, 10.4, 10.6); B["天窗_顶"] = (0, 24, 5.8, 12.2, 11.7, 11.9)
B["附房_东墙"] = (36, 36.25, 0, 18, 0, 3.6); B["附房_北墙"] = (30, 36, 18, 18.25, 0, 3.6); B["附房_南墙"] = (30, 36, -0.25, 0, 0, 3.6); B["附房_吊顶"] = (30, 36.25, 0, 18.25, 3.6, 3.75)
B["附房_Z10隔墙_西"] = (30, 32.5, 9.9, 10.1, 0, 3.6); B["附房_Z10隔墙_东"] = (33.5, 36, 9.9, 10.1, 0, 3.6); B["附房_Z10门上"] = (32.5, 33.5, 9.9, 10.1, 2.1, 3.6)
B["隔断_Z8_西段"] = (24, 26.5, 7.97, 8.03, 0, 1); B["隔断_Z8_东段"] = (27.5, 30, 7.97, 8.03, 0, 1); B["隔断_Z8_上框"] = (24, 30, 7.97, 8.03, 2.95, 3.05)
B["隔断_X24_下"] = (23.97, 24.03, 0, 8, 0, 1); B["隔断_X24_上框"] = (23.97, 24.03, 0, 8, 2.95, 3.05); B["控制间_门框上"] = (26.5, 27.5, 7.97, 8.03, 2.1, 2.95)
def inside(p, b, e=0.02): return b[0] - e < p[0] < b[1] + e and b[2] - e < p[1] < b[3] + e and b[4] - e < p[2] < b[5] + e
def samples(b, step=0.25):
    x0, x1, y0, y1, z0, z1 = b
    def rng(a, c): n = max(1, int((c - a) / step)); return [a + (c - a) * (k + 0.5) / n for k in range(n)]
    for x in rng(x0, x1):
        for y in rng(y0, y1): yield (x, y, z0), "z"; yield (x, y, z1), "z+"
    for x in rng(x0, x1):
        for z in rng(z0, z1): yield (x, y0, z), "y"; yield (x, y1, z), "y"
    for y in rng(y0, y1):
        for z in rng(z0, z1): yield (x0, y, z), "x"; yield (x1, y, z), "x"
allmax = 0; bad = []
for name, b in B.items():
    ds = []
    for p, face in samples(b):
        if p[2] < -1e-4: continue
        if face == "z+" and abs(p[2] - 9.0) < 1e-6 and ("墙" in name or "山墙" in name): continue
        if any(inside(p, o) for k, o in B.items() if k != name): continue
        loc, nrm, idx, dist = bvh.find_nearest(p)
        ds.append(dist)
    if not ds: continue
    mx = max(ds); allmax = max(allmax, mx)
    if mx > 0.01: bad.append((name, mx, sum(d > 0.01 for d in ds), len(ds)))
print(f"[colliders] blocker 方块 {len(B)} 个；露出表面采样点到模型表面的最大距离 = {allmax*1000:.1f} mm")
for n, mx, k, t in bad: print(f"  超过 10 mm：{n}  max={mx*1000:.0f} mm（{k}/{t} 点）")

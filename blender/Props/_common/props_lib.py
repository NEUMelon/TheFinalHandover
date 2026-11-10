# 《最后一次交班》Stage 3 · 道具组共用：建模小工具、烘焙（lsb_bake）、贴字 / 贴花、扁平化导出（export_fbx_unity.flatten）、回读检查、预览。
# 由 AI 辅助工具编写（AI 编写脚本程序化生成，已登记 80_合规/AI使用日志）。
# 轴向：Blender +X = 世界东，+Y = 世界北，+Z = 上；单位米。每个资产的根是 EMPTY “LSB_ENV_…_v01”，根下每个直接子物体 = 一个部件（原点 = 枢轴）。
import bpy, bmesh, math, os, sys, json
import numpy as np
from mathutils import Vector, Matrix
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROPS = os.path.dirname(HERE)
STAGE3 = os.path.dirname(PROPS)
PROJ = os.path.dirname(os.path.dirname(STAGE3))
sys.path.insert(0, os.path.join(STAGE3, "Textures")); sys.path.insert(0, STAGE3)
import lsb_bake
import export_fbx_unity as E
PTEX = os.path.join(PROPS, "_Textures")
FONT = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"

# lsb_bake 只在 stage3/Textures 找平铺贴图；道具补的贴图在 Props/_Textures，先找这里
_orig_load = lsb_bake._load
def _load(name):
    p = os.path.join(PTEX, name)
    if os.path.exists(p):
        return np.asarray(Image.open(p).convert("RGB"), np.float32)[::-1] / 255.0
    return _orig_load(name)
lsb_bake._load = _load

def hexc(h): return tuple(int(h[i:i+2], 16) / 255 for i in (1, 3, 5))

class Asset:
    def __init__(s, name):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        s.scn = bpy.context.scene
        s.name = name
        s.root = bpy.data.objects.new(name, None); s.scn.collection.objects.link(s.root)
        s.M = {}; s.labels = []; s.parts = {}

    # ---------------- 材质
    def mat(s, key, name, hx, rough=0.6, metal=0.0, emit=None, emit_str=0.0, alpha=1.0):
        m = bpy.data.materials.new(name); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (*[c ** 2.2 for c in hexc(hx)], 1)
        b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
        if emit:
            b.inputs["Emission Color"].default_value = (*[c ** 2.2 for c in hexc(emit)], 1)
            b.inputs["Emission Strength"].default_value = emit_str
        if alpha < 1: b.inputs["Alpha"].default_value = alpha
        s.M[key] = m; return m

    # ---------------- 部件与网格
    def part(s, name, loc=(0, 0, 0), parent=None):
        o = bpy.data.objects.new(name, None); s.scn.collection.objects.link(o)
        o.parent = parent or s.root; o.location = Vector(loc) - (parent.matrix_world.translation if parent else Vector())
        bpy.context.view_layer.update()
        s.parts[name] = o; return o
    def _obj(s, name, bm, parent, material):
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new(name, me); s.scn.collection.objects.link(o)
        o.parent = parent; o.matrix_parent_inverse = parent.matrix_world.inverted()
        if material: me.materials.append(s.M[material])
        return o
    def box(s, name, c, size, material, parent, bevel=0.0, seg=1):
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=size, verts=bm.verts); bmesh.ops.translate(bm, vec=c, verts=bm.verts)
        o = s._obj(name, bm, parent, material)
        if bevel: s.bevel(o, bevel, seg)
        return o
    def bevel(s, o, w, seg=1, angle=35):
        b = o.modifiers.new("Bevel", 'BEVEL'); b.width = w; b.segments = seg
        b.limit_method = 'ANGLE'; b.angle_limit = math.radians(angle); b.harden_normals = False
    def cyl(s, name, c, r, length, axis, material, parent, seg=16, r2=None, cap=True):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=length)
        rot = {'x': Matrix.Rotation(math.pi / 2, 4, 'Y'), 'y': Matrix.Rotation(-math.pi / 2, 4, 'X'), 'z': Matrix.Identity(4)}[axis]
        bmesh.ops.transform(bm, matrix=rot, verts=bm.verts); bmesh.ops.translate(bm, vec=c, verts=bm.verts)
        return s._obj(name, bm, parent, material)
    def lathe(s, name, profile, c, material, parent, seg=24, axis='z', close_top=False, normals=None):
        """profile：[(r, h), …] 绕竖轴旋成壳（开口），r=0 的点自动收口。"""
        bm = bmesh.new()
        rings = []
        for r, h in profile:
            ring = []
            for i in range(seg if r > 1e-6 else 1):
                a = 2 * math.pi * i / seg
                ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), h)))
            rings.append(ring)
        for A, B in zip(rings, rings[1:]):
            if len(A) == 1 and len(B) == 1: continue
            if len(A) == 1: [bm.faces.new((A[0], B[i], B[(i + 1) % seg])) for i in range(seg)]
            elif len(B) == 1: [bm.faces.new((A[i], A[(i + 1) % seg], B[0])) for i in range(seg)]
            else: [bm.faces.new((A[i], A[(i + 1) % seg], B[(i + 1) % seg], B[i])) for i in range(seg)]
        if close_top and len(rings[-1]) > 1: bm.faces.new(rings[-1][::-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if normals in ("in", "out"):
            # 开口壳：按法线是否背离旋转轴统一朝向（in = 朝轴 / 朝罩内，out = 朝外）
            bm.normal_update()
            f0 = max(bm.faces, key=lambda f: f.calc_area())
            cc = f0.calc_center_median(); radial = Vector((cc.x, cc.y, 0))
            outward = f0.normal.dot(radial) > 0
            if outward != (normals == "out"): bmesh.ops.reverse_faces(bm, faces=bm.faces)
        if axis == 'x': bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.pi / 2, 4, 'Y'), verts=bm.verts)
        if axis == 'y': bmesh.ops.transform(bm, matrix=Matrix.Rotation(-math.pi / 2, 4, 'X'), verts=bm.verts)
        bmesh.ops.translate(bm, vec=c, verts=bm.verts)
        return s._obj(name, bm, parent, material)
    def poly(s, name, verts, faces, material, parent):
        bm = bmesh.new(); vs = [bm.verts.new(v) for v in verts]
        for f in faces: bm.faces.new([vs[i] for i in f])
        return s._obj(name, bm, parent, material)
    def tube(s, name, pts, r, material, parent, seg=8):
        """沿折线的圆管（转角处简单相接）。"""
        bm = bmesh.new(); rings = []
        P = [Vector(p) for p in pts]
        for i, p in enumerate(P):
            d = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
            up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
            a = d.cross(up).normalized(); b = d.cross(a)
            rings.append([bm.verts.new(p + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
        for A, B in zip(rings, rings[1:]):
            for k in range(seg): bm.faces.new((A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k]))
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return s._obj(name, bm, parent, material)
    def solidify(s, o, t):
        m = o.modifiers.new("Sheet", 'SOLIDIFY'); m.thickness = t; m.offset = 0

    def label(s, name, corners, img, material, parent, base_tex_alpha=True):
        """贴字 / 贴花：corners = 左下、右下、右上、左上（米，世界坐标）；img = PIL RGBA，烘焙后合成进贴图集的这块 UV。"""
        o = s.poly(name, corners, [(0, 1, 2, 3)], material, parent)
        o["lsb_label"] = len(s.labels); s.labels.append((name, [tuple(c) for c in corners], img)); return o

    # ---------------- 收尾
    def meshes(s): return [o for o in s.scn.objects if o.type == 'MESH']
    def finalize(s, sharp_deg=40):
        bpy.ops.object.select_all(action='DESELECT')
        for o in s.meshes():
            bpy.context.view_layer.objects.active = o; o.select_set(True)
            for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
            o.select_set(False)
        for o in s.meshes():
            o.data.shade_smooth(); o.data.set_sharp_from_angle(angle=math.radians(sharp_deg))
        return s.tris()
    def tris(s, objs=None):
        t = 0
        for o in objs or s.meshes(): o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
        return t
    def part_tris(s):
        out = {}
        for o in s.meshes():
            p = o
            while p.parent is not None and p.parent != s.root: p = p.parent
            o.data.calc_loop_triangles(); out[p.name] = out.get(p.name, 0) + len(o.data.loop_triangles)
        return out

    # ---------------- 烘焙（lsb_bake 流水线）+ 贴字
    def bake(s, key, recipes, res, out_dir):
        d = lsb_bake.bake_atlas(s.meshes(), key, out_dir, recipes, res=res, margin=8)
        if s.labels: s._paint_labels(key, out_dir, res)
        for im in bpy.data.images:
            if im.filepath: im.reload()
        return d
    def _paint_labels(s, key, out_dir, res):
        p = os.path.join(out_dir, f"{key}_Atlas_BaseColor.png")
        A = np.asarray(Image.open(p).convert("RGB"), np.float32) / 255.0
        for o in s.meshes():
            if "lsb_label" not in o: continue
            if not o.data.materials or o.data.materials[0].name != f"M_{key}_Atlas": continue
            name, corners, img = s.labels[o["lsb_label"]]
            me = o.data; uv = me.uv_layers["UVMap"].data; poly = me.polygons[0]
            mw = o.matrix_world
            cuv = []
            for c in corners:   # 按世界坐标对上四个角
                best = min(poly.loop_indices, key=lambda li: ((mw @ me.vertices[me.loops[li].vertex_index].co) - Vector(c)).length)
                cuv.append(Vector(uv[best].uv))
            c0, c1, c3 = cuv[0], cuv[1], cuv[3]
            px = lambda q: (q.x * res, (1 - q.y) * res)
            P0, P1, P3 = [np.array(px(q)) for q in (c0, c1, c3)]
            allp = np.array([px(q) for q in cuv])
            x0, y0 = np.floor(allp.min(0)).astype(int) - 2; x1, y1 = np.ceil(allp.max(0)).astype(int) + 2
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, res - 1), min(y1, res - 1)
            yy, xx = np.mgrid[y0:y1 + 1, x0:x1 + 1] + 0.5
            Mx = np.array([P1 - P0, P3 - P0]).T; Mi = np.linalg.inv(Mx)
            st = np.einsum('ij,jhw->ihw', Mi, np.stack([xx - P0[0], yy - P0[1]]))
            sv, tv = st[0], st[1]
            inside = (sv > -0.03) & (sv < 1.03) & (tv > -0.03) & (tv < 1.03)
            im = np.asarray(img.convert("RGBA"), np.float32) / 255.0; H, W = im.shape[:2]
            ix = np.clip((sv * W).astype(int), 0, W - 1); iy = np.clip(((1 - tv) * H).astype(int), 0, H - 1)
            smp = im[iy, ix]; a = smp[..., 3:4] * inside[..., None]
            reg = A[y0:y1 + 1, x0:x1 + 1]
            A[y0:y1 + 1, x0:x1 + 1] = reg * (1 - a) + smp[..., :3] * a
        Image.fromarray((np.clip(A, 0, 1) * 255 + 0.5).astype(np.uint8)).save(p)
        print(f"[label] {len(s.labels)} labels painted into {p}", flush=True)

    # ---------------- 保存 / 导出 / 回读
    def save(s, path): bpy.ops.wm.save_as_mainfile(filepath=path)
    def export(s, fbx_path, keep=()):
        E.KEEP |= set(keep)
        objs = E.flatten(s.root)
        bpy.context.view_layer.update()
        info = {}
        for o in objs:
            bb = [o.matrix_world @ Vector(v) for v in o.bound_box]
            info[o.name] = {"center": [round(sum(v[i] for v in bb) / 8, 4) for i in range(3)], "pivot": [round(x, 4) for x in o.matrix_world.translation],
                            "tris": (o.data.calc_loop_triangles(), len(o.data.loop_triangles))[1], "materials": [m.name for m in o.data.materials if m]}
        bpy.ops.export_scene.fbx(filepath=fbx_path, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                                 object_types={'MESH'}, mesh_smooth_type='FACE', bake_space_transform=False, add_leaf_bones=False)
        return info

def readback(fbx_path, info):
    """重新导入 FBX，对比每个部件的世界包围盒中心 / 枢轴（导入器把 Y 上换回 Z 上）。返回 (最大误差 m, 行列表)。"""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path, axis_forward='-Z', axis_up='Y')
    bpy.context.view_layer.update()
    rows, worst = [], 0.0
    got = {o.name: o for o in bpy.context.scene.objects if o.type == 'MESH'}
    for name, d in info.items():
        o = got.get(name)
        if o is None: rows.append(f"{name}: 缺失！"); worst = 9e9; continue
        bb = [o.matrix_world @ Vector(v) for v in o.bound_box]
        c = [sum(v[i] for v in bb) / 8 for i in range(3)]; pv = list(o.matrix_world.translation)
        e = max(max(abs(c[i] - d["center"][i]) for i in range(3)), max(abs(pv[i] - d["pivot"][i]) for i in range(3)))
        worst = max(worst, e)
        o.data.calc_loop_triangles()
        rows.append(f"{name}: 中心 {['%.3f' % x for x in c]} 枢轴 {['%.3f' % x for x in pv]} 误差 {e * 1000:.2f} mm 三角 {len(o.data.loop_triangles)} 材质 {[m.name for m in o.data.materials if m]}")
    extra = set(got) - set(info)
    if extra: rows.append(f"多出的物体：{sorted(extra)}")
    return worst, rows

def text_img(lines, size=(512, 256), bg=(222, 210, 180, 255), ink=(40, 45, 70, 255), font_px=90, rule=False, rot=0.0, jitter=0):
    """纸签 + 手写感文字（字体为文泉驿正黑，略倾斜、墨色不匀）。lines = [(文字, 相对字号, y 位置 0–1)]。"""
    im = Image.new("RGBA", size, bg); dr = ImageDraw.Draw(im)
    W, H = size
    if rule:
        for y in range(int(H * 0.2), H, int(H * 0.18)): dr.line([(int(W * 0.05), y), (int(W * 0.95), y)], fill=(150, 160, 175, 160), width=2)
    rng = np.random.default_rng(len(lines) * 7 + W)
    for txt, k, yp in lines:
        f = ImageFont.truetype(FONT, int(font_px * k))
        layer = Image.new("RGBA", size, (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
        tw = ld.textlength(txt, font=f); x = (W - tw) / 2
        for ch in txt:
            dy = rng.normal(0, jitter) if jitter else 0
            ld.text((x, H * yp - font_px * k / 2 + dy), ch, font=f, fill=ink); x += ld.textlength(ch, font=f)
        if rot: layer = layer.rotate(rot, resample=Image.BICUBIC, center=(W / 2, H * yp))
        im.alpha_composite(layer)
    # 纸边泛黄
    a = np.asarray(im, np.float32); yy, xx = np.mgrid[0:H, 0:W]
    e = np.minimum(np.minimum(xx, W - 1 - xx) / W, np.minimum(yy, H - 1 - yy) / H)
    k = np.clip(1 - e / 0.08, 0, 1)[..., None] * 0.25
    a[..., :3] = a[..., :3] * (1 - k) + np.array([150, 120, 70]) * k
    return Image.fromarray(a.astype(np.uint8), "RGBA")

def render_setup(res=(960, 540), samples=32, world=(0.42, 0.47, 0.52), strength=0.5):
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = samples; scn.cycles.use_denoising = True
    scn.render.resolution_x, scn.render.resolution_y = res; scn.render.resolution_percentage = 100
    scn.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (*world, 1); bg.inputs[1].default_value = strength
    cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
    return scn, cam, bg
def floor(z=0.0, size=10, loc=(0, 0), hx="#7E7A72"):
    bpy.ops.mesh.primitive_plane_add(size=size, location=(loc[0], loc[1], z)); g = bpy.context.active_object
    m = bpy.data.materials.new("Floor"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*[c ** 2.2 for c in hexc(hx)], 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    g.data.materials.append(m); return g
def light(name, kind, loc, target, energy, size=1.0, color=(1, 1, 1)):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == 'AREA': l.size = size
    o = bpy.data.objects.new(name, l); bpy.context.scene.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); return o
def shoot(cam, path, loc, target=None, lens=35, rot=None, ortho=None):
    cam.location = loc
    if rot is not None: cam.rotation_euler = rot
    else: cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if ortho: cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else: cam.data.type = 'PERSP'; cam.data.lens = lens
    cam.data.sensor_width = 36
    bpy.context.scene.render.filepath = path; bpy.ops.render.render(write_still=True)

def pipeline(A, key, recipes, res, out_dir, keep=(), budget=None):
    """build 之后：修改器落地 → 烘焙贴图集（+贴字）→ 存 .blend → flatten 导出 FBX → 回读检查 → 写 build_stats.txt。"""
    os.makedirs(out_dir, exist_ok=True)
    tris = A.finalize()
    per = A.part_tris()
    # key 可以是列表：[(key, recipes, res), …] → 同一模型烘几张贴图集（每张一个 M_<key>_Atlas 材质）
    bakes = key if isinstance(key, list) else [(key, recipes, res)]
    dens = [A.bake(k, r, rs, out_dir) for k, r, rs in bakes]
    key = " + ".join(f"{k} {rs}px≈{d:.0f}px/m" for (k, r, rs), d in zip(bakes, dens)); res = ""; dens = 0
    blend = os.path.join(out_dir, f"{A.name}.blend"); A.save(blend)
    fbx = os.path.join(out_dir, f"{A.name}.fbx")
    info = A.export(fbx, keep)
    worst, rows = readback(fbx, info)
    lines = [f"asset={A.name}", f"tris_total={tris}", f"budget={budget}", f"atlas={key}",
             "parts:"] + [f"  {k}: {v}" for k, v in sorted(per.items())] + [f"readback_worst_mm={worst * 1000:.3f}"] + ["readback:"] + ["  " + r for r in rows]
    open(os.path.join(out_dir, "build_stats.txt"), "w").write("\n".join(lines) + "\n")
    print("\n".join(lines), flush=True)
    return tris, worst

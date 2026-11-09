# 《最后一次交班》LT01 车床（虚构 HC-620 型）Hero 模型 · 程序化建模脚本
# 由 Claude 编写（AI 制作，已登记 80_合规/AI使用日志；2026-10-14 用户放开 3D 口径）。
# 尺寸依据：20_场景地编/stage3/01_Hero资产与场景主产规格_v0.1.md §2 与三视图参考 v01。
# 运行：python3 build_LT01.py <输出目录>（需要 pip 包 bpy 5.1；也可在 Blender 5.x 的 Scripting 里运行）
# 坐标：Blender X = 东（主轴轴线），Y = 北，Z = 上；局部原点 = 床身西端底面中心；操作面朝南（−Y）。
# 导出 FBX 时转为 Unity 的 Y-up：Unity X = 东，Unity Z = 北，摆到世界 (8.0, 0, 11.0)。
import bpy, bmesh, math, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Textures'))
import lsb_textures
from mathutils import Vector, Matrix

OUT = sys.argv[-1] if len(sys.argv) > 1 and os.path.isdir(sys.argv[-1]) else os.path.dirname(os.path.abspath(__file__))
MM = 0.001
VER = "v02"

# ---------------------------------------------------------------- 场景
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
scn.unit_settings.system = 'METRIC'
scn.unit_settings.scale_length = 1.0
root = bpy.data.objects.new(f"LSB_ENV_MCH_Lathe_LT01_{VER}", None)
scn.collection.objects.link(root)

# ---------------------------------------------------------------- 材质（年代叠层的基色；正式贴图在 Substance / 程序贴图阶段替换）
def mat(name, rgb, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*[c ** 2.2 for c in rgb], 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    return m

def hexc(h): return tuple(int(h[i:i+2], 16) / 255 for i in (1, 3, 5))

M = {
    "paint": mat("M_LT01_Paint_1983Green", hexc("#5F7F72"), 0.55),        # L2 1983 墙裙绿降饱和
    "cast":  mat("M_LT01_CastIron", hexc("#3A3D3B"), 0.7, 0.6),           # L0 铸铁
    "steel": mat("M_LT01_MachinedSteel", hexc("#9EA3A3"), 0.32, 1.0),     # 导轨面、卡盘、丝杠
    "worn":  mat("M_LT01_WornBright", hexc("#C9C6BC"), 0.18, 1.0),        # 手轮把手、手柄磨亮
    "grip":  mat("M_LT01_BakeliteGrip", hexc("#2A211C"), 0.35),           # 胶木把手
    "black": mat("M_LT01_Sensor2025", hexc("#141516"), 0.45),             # L7 2025 传感器
    "cable": mat("M_LT01_Cable2025", hexc("#1E2022"), 0.5),
    "tie":   mat("M_LT01_ZipTie", hexc("#E8E6DE"), 0.5),
    "ring":  mat("M_LT01_L1Ring_1983", hexc("#E9DFB8"), 0.5),             # L1 黄白漆圈
    "plate": mat("M_LT01_Nameplate", hexc("#B8A57A"), 0.4, 0.8),
    "guard": mat("M_LT01_Guard_1956Grey", hexc("#6E7A72"), 0.6),          # L1 1956 苏式灰绿
    "chip":  mat("M_LT01_ChipTray", hexc("#4A4A44"), 0.8, 0.4),
    "lamp":  mat("M_LT01_WorkLamp_Enamel", hexc("#2F4A3E"), 0.4),
}

# ---------------------------------------------------------------- 建模工具
def link(obj, parent=root, material=None):
    scn.collection.objects.link(obj)
    obj.parent = parent
    if material is not None:
        obj.data.materials.append(M[material] if isinstance(material, str) else material)
    return obj

def new_obj(name, bm, parent=root, material=None, origin=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    link(o, parent, material)
    if origin != (0, 0, 0):
        o_mm = Vector(origin) * MM
        me.transform(Matrix.Translation(-o_mm))
        o.location = o_mm - (parent.matrix_world.translation if parent is not root else Vector())
    return o

def box(name, x0, x1, y0, y1, z0, z1, material="paint", bevel=0.0, parent=root, seg=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=((x1 - x0) * MM, (y1 - y0) * MM, (z1 - z0) * MM), verts=bm.verts)
    bmesh.ops.translate(bm, vec=((x0 + x1) / 2 * MM, (y0 + y1) / 2 * MM, (z0 + z1) / 2 * MM), verts=bm.verts)
    o = new_obj(name, bm, parent, material)
    if bevel > 0: add_bevel(o, bevel, seg)
    return o

def cyl(name, axis, c, r, length, material="steel", seg=48, bevel=0.0, parent=root, start=None, cap=True):
    """axis: 'X'/'Y'/'Z'；c = 轴线上起点（start=True）或中心点。"""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r * MM, radius2=r * MM, depth=length * MM)
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'))
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'X'))
    off = Vector(c) * MM
    if start:
        d = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}[axis] * (start * length / 2 * MM)
        off += d
    bmesh.ops.translate(bm, vec=off, verts=bm.verts)
    o = new_obj(name, bm, parent, material)
    if bevel > 0: add_bevel(o, bevel, 2)
    return o

def add_bevel(o, w_mm, seg=3):
    b = o.modifiers.new("Bevel", 'BEVEL')
    b.width = w_mm * MM; b.segments = seg; b.limit_method = 'ANGLE'; b.angle_limit = math.radians(35)
    b.harden_normals = True

def tube_along(name, pts_mm, r, material="cable", parent=root, res=8):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts_mm) - 1)
    for bp, p in zip(sp.bezier_points, pts_mm):
        bp.co = Vector(p) * MM; bp.handle_left_type = bp.handle_right_type = 'AUTO'
    cu.bevel_depth = r * MM; cu.bevel_resolution = 2; cu.resolution_u = res
    o = bpy.data.objects.new(name, cu); link(o, parent, material)
    return o

def handwheel(name, center, axis, R, spokes=3, rim=11, handle=True, parent=root, crank_ang=45):
    """铸铁手轮：轮缘 + 辐条 + 轮毂 + 磨亮把手。在局部 Z 轴上建，再把整组转到 axis（'X' 朝东 / 'Y' 朝南即操作者）。"""
    grp = bpy.data.objects.new(name, None); link(grp, parent)
    pw = parent.matrix_world.translation if parent is not root else Vector()
    grp.location = Vector(center) * MM - pw
    grp.rotation_euler = (0, math.pi / 2, 0) if axis == 'X' else (math.pi / 2, 0, 0)   # 局部 +Z → +X / −Y
    bpy.ops.mesh.primitive_torus_add(major_radius=R * MM, minor_radius=rim * MM, major_segments=48, minor_segments=12)
    rimo = bpy.context.active_object; rimo.name = name + "_Rim"
    for c in list(rimo.users_collection): c.objects.unlink(rimo)
    link(rimo, grp, "cast")
    cyl(name + "_Hub", 'Z', (0, 0, -15), R * 0.22, 60, "cast", 32, 3, grp)
    for i in range(spokes):
        a = 2 * math.pi * i / spokes
        sp = cyl(f"{name}_Spoke{i+1}", 'X', (0, 0, 0), rim * 0.75, R - 8, "cast", 12, 0, grp, start=1)
        sp.data.transform(Matrix.Rotation(a, 4, 'Z'))
    if handle:
        a = math.radians(crank_ang)
        px, py = R * 0.85 * math.cos(a), R * 0.85 * math.sin(a)
        cyl(name + "_HandlePin", 'Z', (px, py, 0), 6, 30, "steel", 12, 0, grp, start=1)
        cyl(name + "_HandleGrip", 'Z', (px, py, 25), 14, 95, "worn", 24, 3, grp, start=1)
    return grp

# ---------------------------------------------------------------- 部件（§2.2 部件名 = 引擎子物体名）
# Legs 床脚
legs = bpy.data.objects.new("Legs", None); link(legs)
box("Leg_West", 0, 700, -350, 350, 0, 700, "paint", 18, legs)
box("Leg_West_MotorDoor", 60, 640, -356, -350, 140, 520, "paint", 4, legs)
cyl("Leg_West_DoorLatch", 'Y', (590, -362, 330), 10, 14, "steel", 16, 0, legs)
for i in range(6):   # 散热百叶
    box(f"Leg_West_Louver{i+1}", 150, 550, -360, -356, 430 + i * 14, 436 + i * 14, "paint", 0, legs)
box("Leg_East", 2100, 2650, -330, 330, 0, 700, "paint", 18, legs)
box("Leg_West_Plinth", -10, 710, -360, 360, 0, 40, "cast", 6, legs)
box("Leg_East_Plinth", 2090, 2660, -340, 340, 0, 40, "cast", 6, legs)

# Bed 床身 + 导轨（铸件圆角放大）
bed = box("Bed", 0, 2650, -200, 200, 700, 920, "paint", 22)
box("Bed_Way_Front", 700, 2650, -190, -120, 920, 950, "steel", 4, bed)
box("Bed_Way_Rear", 700, 2650, 120, 190, 920, 950, "steel", 4, bed)
box("Bed_Rack", 700, 2600, -212, -200, 860, 900, "steel", 1, bed)
for i in range(9):   # 床身筋板（从导轨缝隙看得见）
    box(f"Bed_Rib{i+1}", 760 + i * 210, 790 + i * 210, -120, 120, 860, 915, "paint", 0, bed)

# ChipTray 接屑盘
ct = box("ChipTray", 700, 2100, -350, 350, 640, 655, "chip", 2)
for nm, a in (("S", (700, 2100, -350, -340)), ("N", (700, 2100, 340, 350)), ("W", (700, 710, -350, 350)), ("E", (2090, 2100, -350, 350))):
    box(f"ChipTray_Lip{nm}", a[0], a[1], a[2], a[3], 655, 700, "chip", 1, ct)

# Headstock 主轴箱 + 进给箱
hs = box("Headstock", 0, 900, -470, 470, 950, 1300, "paint", 25)
box("Headstock_Lid", 20, 880, -450, 450, 1300, 1318, "paint", 6, hs)
box("FeedBox", 0, 700, -400, 300, 700, 950, "paint", 18, hs)
for i, x in enumerate((180, 330, 480)):   # 进给手柄
    cyl(f"FeedBox_Knob{i+1}", 'Y', (x, -410, 830), 22, 20, "cast", 24, 2, hs, start=-1)
    cyl(f"FeedBox_Lever{i+1}", 'Y', (x, -430, 830), 6, 50, "steel", 12, 0, hs, start=-1)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.013, segments=16, ring_count=8, location=(x * MM, -0.482, 0.83))
    k = bpy.context.active_object; k.name = f"FeedBox_Ball{i+1}"
    for c in list(k.users_collection): c.objects.unlink(k)
    link(k, hs, "grip")
# 变速手柄（④ 球头包浆）
cyl("Headstock_ShiftLever_Boss", 'Y', (650, -470, 1150), 30, 25, "cast", 32, 3, hs, start=-1)
cyl("Headstock_ShiftLever", 'Z', (650, -490, 1150), 8, 95, "steel", 16, 0, hs, start=1)
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.016, segments=24, ring_count=12, location=(0.65, -0.49, 1.255))
b = bpy.context.active_object; b.name = "Headstock_ShiftLever_Ball"
for c in list(b.users_collection): c.objects.unlink(b)
link(b, hs, "grip")
for i, x in enumerate((150, 420)):   # 主轴箱前面两个选速旋钮
    cyl(f"Headstock_SpeedDial{i+1}", 'Y', (x, -470, 1050), 45, 22, "cast", 40, 3, hs, start=-1)
    box(f"Headstock_SpeedDial{i+1}_Pointer", x - 6, x + 6, -496, -492, 1050, 1095, "worn", 0, hs)
# 主轴箱东端面：主轴鼻端
cyl("Headstock_SpindleNose", 'X', (900, 0, 1150), 95, 30, "steel", 48, 2, hs, start=1)
# 主轴箱顶面油窗、吊环
cyl("Headstock_OilSight", 'Y', (780, -470, 1240), 18, 6, "steel", 24, 0, hs, start=-1)
cyl("Headstock_OilSight_Glass", 'Y', (780, -476, 1240), 13, 2, "black", 24, 0, hs, start=-1)

# ChangeGearCover 挂轮罩（西端面下部）
cg = box("ChangeGearCover", -60, 0, -300, 300, 700, 1080, "paint", 20)
for i, (y, z) in enumerate(((-250, 750), (250, 750), (-250, 1030), (250, 1030))):
    cyl(f"ChangeGearCover_Screw{i+1}", 'X', (-60, y, z), 7, 5, "steel", 12, 0, cg, start=-1)

# Spindle + Chuck 卡盘（独立：结局降速旋转；原点在主轴轴线）
chuck_root = bpy.data.objects.new("Spindle", None); link(chuck_root)
chuck_root.location = Vector((900, 0, 1150)) * MM
ch = cyl("Chuck", 'X', (30, 0, 0), 125, 120, "steel", 64, 4, chuck_root, start=1)
cyl("Chuck_Face", 'X', (150, 0, 0), 110, 4, "cast", 64, 0, chuck_root, start=1)
for i in range(3):
    a = 2 * math.pi * i / 3
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(0.04, 0.028, 0.07), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0.172, 0, 0.065), verts=bm.verts)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, 'X'))
    jo = new_obj(f"Chuck_Jaw{i+1}", bm, chuck_root, "steel"); add_bevel(jo, 2, 2)
    cyl(f"Chuck_KeyHole{i+1}", 'Y', (0, 0, 0), 7, 4, "black", 12, 0, chuck_root).matrix_basis = Matrix.Rotation(a + math.pi / 3, 4, 'X') @ Matrix.Translation((0.09, 0, 0.125))
cyl("Workpiece", 'X', (150, 0, 0), 30, 180, "steel", 32, 1, chuck_root, start=1)

# Guard 护罩（独立，铰链沿 X，原点在铰链轴）+ Guard_Dent 凹痕（独立网格，深 10 mm，待核 E-03）
GR = 215          # 护罩半径（卡盘 Ø250 + 爪）
GX0, GX1 = 800, 1300
PH0, PH1 = -20, 160   # φ：0 = 正上方，90 = 正前方（南），沿弧向前下包
DENT_X = (1000, 1100); DENT_PH = (45, 70); DEPTH = 10
def guard_pt(x, ph, inward=0.0):
    r = GR - inward
    return Vector((x * MM, -r * math.sin(math.radians(ph)) * MM, (1150 + r * math.cos(math.radians(ph))) * MM))
hinge = guard_pt(GX0, PH0)
guard_root = bpy.data.objects.new("Guard", None); link(guard_root)
guard_root.location = Vector((0, hinge.y, hinge.z))
def guard_grid(name, xs, phs, skip=None, depth_fn=None, rim=True, parent=guard_root, t=3.0):
    """护罩薄板：外表面 + 内表面（向轴线偏 t mm），只在外轮廓补侧边；开孔边与凹痕片不补，二者在孔边严丝合缝。"""
    bm = bmesh.new(); vo, vi = {}, {}
    hz = Vector((0, hinge.y, hinge.z))
    for i, x in enumerate(xs):
        for j, ph in enumerate(phs):
            d = depth_fn(x, ph) if depth_fn else 0.0
            vo[i, j] = bm.verts.new(guard_pt(x, ph, d) - hz)
            vi[i, j] = bm.verts.new(guard_pt(x, ph, d + t) - hz)
    nx, ny = len(xs) - 1, len(phs) - 1
    for i in range(nx):
        for j in range(ny):
            if skip and skip(xs[i], xs[i + 1], phs[j], phs[j + 1]): continue
            bm.faces.new((vo[i, j], vo[i, j + 1], vo[i + 1, j + 1], vo[i + 1, j]))   # 外表面法线朝外
            bm.faces.new((vi[i, j], vi[i + 1, j], vi[i + 1, j + 1], vi[i, j + 1]))
    if rim:
        for i in range(nx):
            bm.faces.new((vo[i, 0], vo[i + 1, 0], vi[i + 1, 0], vi[i, 0]))
            bm.faces.new((vo[i, ny], vi[i, ny], vi[i + 1, ny], vo[i + 1, ny]))
        for j in range(ny):
            bm.faces.new((vo[0, j], vi[0, j], vi[0, j + 1], vo[0, j + 1]))
            bm.faces.new((vo[nx, j], vo[nx, j + 1], vi[nx, j + 1], vi[nx, j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj(name, bm, parent, "guard")
    o["smooth_all"] = True
    return o
# 凹痕：工件飞出撞击的椭圆坑，中心偏下、带一道向东的拖痕
def dent_depth(x, ph):
    cx, cp = 1045, 57
    u = (x - cx) / 22.0; v = (ph - cp) / 5.0
    rr = u * u + v * v
    core = math.exp(-rr * 1.2)
    drag = math.exp(-(((x - 1075) / 22.0) ** 2) * 1.4 - (((ph - 54) / 3.0) ** 2)) * 0.35   # 向东的拖痕
    e = max(min((x - DENT_X[0]) / 25, (DENT_X[1] - x) / 25, (ph - DENT_PH[0]) / 6, (DENT_PH[1] - ph) / 6, 1.0), 0.0)
    return DEPTH * max(core, drag) * e * e * (3 - 2 * e)      # smoothstep：孔边深度和坡度都为 0
# 护罩网格线 = 粗网格 ∪ 凹痕细网格：护罩开孔边与凹痕片共用同一排顶点，没有 T 形接缝；smoothstep 让孔边坡度为 0，法线连续
dxs = [DENT_X[0] + 2.5 * i for i in range(41)]
dphs = [DENT_PH[0] + 0.625 * i for i in range(41)]
xs = sorted(set([GX0 + 25 * i for i in range((GX1 - GX0) // 25 + 1)] + dxs))
phs = sorted(set([PH0 + 5 * i for i in range((PH1 - PH0) // 5 + 1)] + dphs))
guard_grid("Guard_Shell", xs, phs, skip=lambda xa, xb, pa, pb: DENT_X[0] <= xa and xb <= DENT_X[1] and DENT_PH[0] <= pa and pb <= DENT_PH[1])
guard_grid("Guard_Dent", dxs, dphs, depth_fn=dent_depth, rim=False)
# 护罩端板、铰链、把手
for nm, x in (("W", GX0), ("E", GX1)):
    bm = bmesh.new()
    pts = [guard_pt(x, ph) for ph in phs] + [Vector((x * MM, 0, 1150 * MM))]
    vv = [bm.verts.new(p - Vector((0, hinge.y, hinge.z))) for p in pts]
    bm.faces.new(vv)
    o = new_obj(f"Guard_EndPlate{nm}", bm, guard_root, "guard")
    s = o.modifiers.new("Sheet", 'SOLIDIFY'); s.thickness = 3 * MM; s.offset = 1 if nm == "E" else -1
cyl("Guard_HingePin", 'X', (GX0 - 10, 0, 0), 9, GX1 - GX0 + 20, "steel", 16, 0, guard_root, start=1)
hp = guard_pt(1050, 118) - Vector((0, hinge.y, hinge.z))
cyl("Guard_Handle", 'X', (hp.x / MM - 60, hp.y / MM - 25, hp.z / MM), 8, 120, "worn", 16, 2, guard_root, start=1)

# Carriage / CrossSlide / ToolPost
car = box("Carriage", 1400, 1900, -450, 450, 950, 1060, "paint", 12)
box("CrossSlide", 1550, 1750, -300, 200, 1060, 1150, "paint", 8, car)
box("CrossSlide_Dovetail", 1560, 1740, -300, 200, 1060, 1068, "steel", 1, car)
tp = box("ToolPost", 1600, 1710, -100, 10, 1150, 1240, "steel", 4, car)
cyl("ToolPost_Bolt", 'Z', (1655, -45, 1240), 14, 30, "steel", 16, 1, tp, start=1)
box("ToolPost_Tool", 1640, 1665, -230, -95, 1175, 1200, "steel", 1, tp)
for i, x in enumerate((1615, 1695)):
    cyl(f"ToolPost_Screw{i+1}", 'Z', (x, -45, 1240), 7, 18, "steel", 12, 0, tp, start=1)
handwheel("CrossSlide_Handwheel", (1650, -360, 1105), 'Y', 75, 3, 8, True, car, 30)
cyl("CrossSlide_Screw", 'Y', (1650, -360, 1105), 10, 60, "steel", 16, 0, car, start=1)

# Apron + Handwheel 溜板箱 + 手轮（⑤ 把手磨亮最重）
ap = box("Apron", 1420, 1880, -330, -200, 600, 950, "paint", 14, car)
handwheel("Apron_Handwheel", (1560, -345, 780), 'Y', 100, 3, 11, True, ap, 45)
cyl("Apron_HalfNutLever_Boss", 'Y', (1800, -330, 700), 20, 15, "cast", 24, 2, ap, start=-1)
cyl("Apron_HalfNutLever", 'Z', (1800, -350, 700), 7, 90, "steel", 12, 0, ap, start=-1)
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.014, segments=16, ring_count=8, location=(1.8, -0.35, 0.61))
b = bpy.context.active_object; b.name = "Apron_HalfNutLever_Ball"
for c in list(b.users_collection): c.objects.unlink(b)
link(b, ap, "grip")

# Leadscrew / FeedRod 丝杠光杠
cyl("Leadscrew", 'X', (700, -230, 820), 18, 1900, "steel", 24, 0, start=1)
cyl("FeedRod", 'X', (700, -230, 760), 13, 1900, "steel", 20, 0, start=1)
box("Leadscrew_EndBracket", 2580, 2650, -260, -200, 720, 860, "paint", 6)

# Tailstock 尾座 + 手轮（⑥）
ts = box("Tailstock", 2200, 2600, -200, 200, 950, 1100, "paint", 18)
box("Tailstock_Body", 2230, 2580, -150, 150, 1100, 1240, "paint", 20, ts)
cyl("Tailstock_Barrel", 'X', (2200, 0, 1150), 55, 400, "paint", 40, 4, ts, start=1)
cyl("Tailstock_Quill", 'X', (2000, 0, 1150), 30, 200, "steel", 32, 1, ts, start=1)
cyl("Tailstock_Centre", 'X', (2000, 0, 1150), 15, 40, "steel", 24, 0, ts, start=-1)
cyl("Tailstock_HandwheelShaft", 'X', (2600, 0, 1150), 14, 90, "steel", 16, 0, ts, start=1)
handwheel("Tailstock_Handwheel", (2690, 0, 1150), 'X', 90, 3, 9, True, ts, 60)
ll = cyl("Tailstock_LockLever", 'Y', (0, -70, 0), 6, 140, "steel", 12, 0, ts)   # 绕自身根部转 20°，再放到尾座顶面
ll.data.transform(Matrix.Translation(Vector((2400, -60, 1255)) * MM) @ Matrix.Rotation(math.radians(20), 4, 'Z'))

# Sensor_R1 + Cable_2025（L7 2025 年代层：黑色 Ø30 + 新线 + 扎带）
cyl("Sensor_R1", 'Y', (800, -470, 1150), 15, 40, "black", 24, 1, start=-1)
cyl("Sensor_R1_Base", 'Y', (800, -470, 1150), 22, 6, "steel", 24, 0, start=-1)
tube_along("Cable_2025", [(800, -512, 1150), (835, -520, 1060), (760, -480, 960), (700, -420, 880), (680, -410, 700)], 4)
for i, (x, y, z) in enumerate(((812, -514, 1095), (735, -458, 925), (690, -413, 790))):
    cyl(f"Cable_2025_ZipTie{i+1}", 'Z', (x, y, z), 7, 4, "tie", 12, 0)

# L1_Mark 测点（顶面 x150 z+50）：Ø60 漆圈 + Ø2 样冲点（法线细节在贴图里）
cyl("L1_Mark_Ring", 'Z', (150, 50, 1318), 30, 0.6, "ring", 48, 0, start=1)
cyl("L1_Mark_Inner", 'Z', (150, 50, 1318.6), 24, 0.6, "worn", 48, 0, start=1)

# Nameplate 铭牌（虚构 HC-620 型；文字走贴花）
box("Nameplate", 250, 470, -472, -470, 1130, 1220, "plate", 1)
for i, (x, z) in enumerate(((258, 1138), (462, 1138), (258, 1212), (462, 1212))):
    cyl(f"Nameplate_Rivet{i+1}", 'Y', (x, -472, z), 3, 2, "steel", 8, 0, start=-1)

# WorkLamp 工作灯（主轴箱后上方摆臂，可选）
wl = bpy.data.objects.new("WorkLamp", None); link(wl)
cyl("WorkLamp_Base", 'Z', (780, 400, 1318), 35, 30, "cast", 24, 2, wl, start=1)
cyl("WorkLamp_Arm1", 'Z', (780, 400, 1348), 9, 380, "steel", 12, 0, wl, start=1)
tube_along("WorkLamp_Arm2", [(780, 400, 1728), (860, 330, 1800), (980, 220, 1720)], 9, "steel", wl)
bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=False, segments=32, radius1=75 * MM, radius2=28 * MM, depth=110 * MM)
bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-25), 3, 'X'))
bmesh.ops.translate(bm, vec=Vector((990, 205, 1650)) * MM, verts=bm.verts)
sh = new_obj("WorkLamp_Shade", bm, wl, "lamp"); s = sh.modifiers.new("Sheet", 'SOLIDIFY'); s.thickness = 1.5 * MM

# ---------------------------------------------------------------- 收尾：修改器落地、UV、统计
def all_mesh(): return [o for o in scn.objects if o.type in ('MESH', 'CURVE')]
bpy.ops.object.select_all(action='DESELECT')
for o in all_mesh():
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    if o.type == 'CURVE':
        bpy.ops.object.convert(target='MESH')
    else:
        for m in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    o.select_set(False)
for o in all_mesh():
    o.data.shade_smooth()
    if not o.get("smooth_all"):
        o.data.set_sharp_from_angle(angle=math.radians(40))
# UV：每个物体 Smart UV（正式贴图前由 Substance / 程序贴图沿用）
for o in all_mesh():
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT'); o.select_set(False)

# 年代叠层可平铺贴图（Textures/gen_textures.py 生成）
TEX = {"paint": "LT01_Paint_1983Green", "guard": "LT01_Guard_1956Grey", "cast": "LT01_CastIron", "chip": "LT01_CastIron", "steel": "LT01_MachinedSteel", "worn": "LT01_WornBright"}
for k, t in TEX.items(): lsb_textures.hook(M[k], t)
lsb_textures.world_uv(all_mesh(), {M[k] for k in TEX})

tris = 0
for o in all_mesh():
    o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
print(f"[LT01] objects={len(all_mesh())} tris={tris}")

blend = os.path.join(OUT, f"LSB_ENV_MCH_Lathe_LT01_{VER}.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"LSB_ENV_MCH_Lathe_LT01_{VER}.fbx"),
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'EMPTY', 'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                         bake_space_transform=True, add_leaf_bones=False, path_mode='COPY')
open(os.path.join(OUT, "build_stats.txt"), "w").write(f"objects={len(all_mesh())}\ntris={tris}\n")

# 《最后一次交班》天车组 CR01（桥架 + 小车 + 司机室 + 吊钩 / 钢丝绳 / 吊盘 + 搪瓷缸 ×2）· 程序化建模脚本
# 由 Claude 编写（AI 编写脚本程序化生成，已登记 80_合规/AI使用日志；用户 2026-10 批准由 Claude 做 3D）。
# 依据：02_母项目VS/02_车间白盒规格_v0.1.md §2/§3（轨顶 Y 7.0、轨道 Z 0.75/17.25、跨度 16.5、屋架下弦 Y 9.0、柱内侧 Z 0.6）；
#       WorkshopGraybox.cs BuildCrane()（桥架 X 6.0、小车 Z 4.0、摆动轴 Y 7.0、绳长 4.0、司机室 (0.2,5.6,2.0) 1.4×1.8×1.4、
#       司机室旧缸 (0,5.25,2.0)、吊钩 / 吊盘 / 缸 / 水 的局部位置）；01_圣经/02_美术圣经_v0.1.md §2（安全黄只给吊钩和梁警示斜纹）、§6。
# 运行：python3 build_CR01.py <输出目录>
# 坐标：Blender +X = 世界东，+Y = 世界北，+Z = 上。资产原点 = 灰盒 CR01_桥架 的枢轴（Unity 世界 (6.0, 0, 0)，桥架停在试吊位）。
# Unity 灰盒坐标 (x, y, z) 在这里写成 (x, z, y)。
# 部件（空物体，原点 = 灰盒对应物体的枢轴；导出时按部件合并）：
#   CR01_Bridge   ← CraneBridge（原点 (0,0,0)）
#     CR01_Trolley  ← CraneTrolley（(0,4,0)）
#       CR01_Rope     ← CraneRope 吊钩_摆动轴（(0,4,7.0)）
#         CR01_Hook     ← CraneHook（(0,4,3.0)）：钩头（动滑轮组）+ 钩 + 吊环 + 4 根吊索 + 吊盘
#           CR01_Mug      ← CraneMug（(0,4,3.02)）
#           CR01_MugWater ← MugWater（(0,4,3.07)，水面圆心）
#     CR01_CabMug   ← 司机室_旧搪瓷缸（(0,2.0,5.25)）
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

OUT = sys.argv[-1] if len(sys.argv) > 1 and os.path.isdir(sys.argv[-1]) else os.path.dirname(os.path.abspath(__file__))
ASSET = "LSB_ENV_CRN_Crane_CR01_v01"

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
root = bpy.data.objects.new(ASSET, None); scn.collection.objects.link(root)

def hexc(h): return tuple(int(h[i:i+2], 16) / 255 for i in (1, 3, 5))
def mat(name, hx, rough=0.6, metal=0.0, emit=None, emit_str=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*[c ** 2.2 for c in hexc(hx)], 1)
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*[c ** 2.2 for c in hexc(emit)], 1)
        b.inputs["Emission Strength"].default_value = emit_str
    return m
# 贴图集分三组（bake_CR01.py）：CR01 = 桥架 / 小车 / 司机室；CR01Hook = 吊钩 / 绳 / 吊盘；CR01Mug = 两只搪瓷缸
M = {
    # --- CR01
    "paint":  mat("M_CR01_Paint_GreyGreen", "#66746A", 0.55),     # 设备漆：墙裙绿降饱和（美术圣经 §2 #2）
    "yel":    mat("M_CR01_Safety_Yellow", "#F2C200", 0.5),        # 警示斜纹黄
    "blk":    mat("M_CR01_Safety_Black", "#1A1A1A", 0.5),         # 警示斜纹黑、缓冲器、电木手柄
    "steel":  mat("M_CR01_Steel", "#9A9C98", 0.4, 1.0),           # 车轮踏面、轨、轴
    "mach":   mat("M_CR01_Machinery", "#4E5852", 0.6),            # 电机 / 减速器 / 制动器（深灰绿漆）
    "wood":   mat("M_CR01_Wood", "#5A4632", 0.6),                 # 司机座、铃拉绳手柄
    "bronze": mat("M_CR01_Bell", "#A88A55", 0.35, 1.0),           # 天车铃
    "ropeA":  mat("M_CR01_WireRope_Static", "#3A372F", 0.5, 0.6), # 卷筒上 / 卷筒到摆动轴的钢丝绳
    "kit":    mat("M_CR01_Kit2026", "#C9CDCC", 0.45),             # 2026 自动化套件外壳（浅灰塑料）
    # --- CR01Hook
    "hyel":   mat("M_CR01Hook_Yellow", "#F2C200", 0.5),
    "hblk":   mat("M_CR01Hook_Black", "#1A1A1A", 0.5),
    "hsteel": mat("M_CR01Hook_Steel", "#9A9C98", 0.4, 1.0),
    "rope":   mat("M_CR01Hook_WireRope", "#3A372F", 0.5, 0.6),
    "tray":   mat("M_CR01Hook_Tray", "#5C625E", 0.6),
    # --- CR01Mug
    "enamel": mat("M_CR01Mug_Enamel_White", "#E9E6DC", 0.2),
    "rim":    mat("M_CR01Mug_Enamel_BlueRim", "#2C4A7A", 0.2),
    # --- 纯色（不进贴图集）
    "water":  mat("M_CR01_Water", "#3E4A50", 0.03),
    "led":    mat("M_CR01_StatusLED_2026", "#E6F0F5", 0.2, 0, "#E6F0F5", 3.0),   # 名字含 StatusLED → Unity 端冷白自发光
}

def part(name, loc, parent):
    e = bpy.data.objects.new(name, None); scn.collection.objects.link(e)
    e.parent = parent; e.matrix_parent_inverse = Matrix.Identity(4)
    e.location = Vector(loc) - parent.matrix_world.translation
    bpy.context.view_layer.update()
    return e
def new_obj(name, bm, parent, material):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); scn.collection.objects.link(o)
    # 网格顶点用世界坐标写，挂到部件下时保持世界位置
    o.parent = parent; o.matrix_parent_inverse = parent.matrix_world.inverted()
    if isinstance(material, (list, tuple)):
        for m in material: o.data.materials.append(M[m])
    else: o.data.materials.append(M[material])
    return o
def bevel_bm(bm, w, seg=1):
    if w > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=w, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
def box(name, x0, x1, y0, y1, z0, z1, material, parent, bev=0.0, seg=1):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), verts=bm.verts)
    bevel_bm(bm, bev, seg)
    return new_obj(name, bm, parent, material)
def _orient(bm, axis):
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'))
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'X'))
def cyl(name, axis, c, r, length, material, parent, seg=16, r2=None, bev=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=length)
    if bev:   # 只倒两端圆周棱
        cap = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-7]
        bmesh.ops.bevel(bm, geom=cap, offset=bev, segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    for f in bm.faces: f.smooth = abs(f.normal.z) < 0.5
    _orient(bm, axis)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    return new_obj(name, bm, parent, material)
def prism(name, poly, axis, a0, a1, material, parent, bev=0.0, seg=1):
    """poly：垂直于 axis 的平面内的二维多边形（axis='X' → (y,z)，'Y' → (x,z)，'Z' → (x,y)），沿 axis 从 a0 拉到 a1。"""
    bm = bmesh.new()
    def P(p, a):
        if axis == 'X': return (a, p[0], p[1])
        if axis == 'Y': return (p[0], a, p[1])
        return (p[0], p[1], a)
    v0 = [bm.verts.new(P(p, a0)) for p in poly]; v1 = [bm.verts.new(P(p, a1)) for p in poly]
    bm.faces.new(v0); bm.faces.new(list(reversed(v1)))
    n = len(poly)
    for i in range(n): bm.faces.new((v0[i], v0[(i + 1) % n], v1[(i + 1) % n], v1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bevel_bm(bm, bev, seg)
    return new_obj(name, bm, parent, material)
def torus(name, c, R, r, normal_axis, material, parent, seg=16, rseg=6, arc=2 * math.pi):
    bm = bmesh.new(); rings = []
    closed = arc >= 2 * math.pi - 1e-6
    nU = seg if closed else seg + 1
    for i in range(nU):
        a = arc * i / seg
        ring = []
        for j in range(rseg):
            b = 2 * math.pi * j / rseg
            x = (R + r * math.cos(b)) * math.cos(a); y = (R + r * math.cos(b)) * math.sin(a); z = r * math.sin(b)
            ring.append(bm.verts.new((x, y, z)))
        rings.append(ring)
    for i in range(nU if closed else nU - 1):
        A, B = rings[i], rings[(i + 1) % nU]
        for j in range(rseg): bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    if not closed:
        bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces: f.smooth = True
    _orient(bm, normal_axis)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    return new_obj(name, bm, parent, material)
def sweep(name, pts, section, material, parent, up=None, twist=0.0, caps=True, smooth=True):
    """pts：中心线；section(t) → [(u, v)]（u 沿“侧向”N，v 沿 B）；twist：每米扭转角（钢丝绳股）。"""
    bm = bmesh.new(); pts = [Vector(p) for p in pts]; n = len(pts)
    L = [0.0]
    for i in range(1, n): L.append(L[-1] + (pts[i] - pts[i - 1]).length)
    rings = []
    prevN = None
    for i, p in enumerate(pts):
        T = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        if up is not None: Nn = Vector(up).cross(T).normalized()
        elif prevN is None:
            ref = Vector((0, 0, 1)) if abs(T.z) < 0.9 else Vector((1, 0, 0))
            Nn = ref.cross(T).normalized()
        else: Nn = (prevN - T * prevN.dot(T)).normalized()
        prevN = Nn; B = T.cross(Nn)
        ang = twist * L[i]
        Nr = Nn * math.cos(ang) + B * math.sin(ang); Br = T.cross(Nr)
        t = L[i] / L[-1]
        rings.append([bm.verts.new(p + Nr * u + Br * v) for (u, v) in section(t)])
    k = len(rings[0])
    for i in range(n - 1):
        A, Bq = rings[i], rings[i + 1]
        for j in range(k):
            f = bm.faces.new((A[j], A[(j + 1) % k], Bq[(j + 1) % k], Bq[j])); f.smooth = smooth
    if caps:
        bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj(name, bm, parent, material)
def circ(r, k, phase=0.0): return lambda t: [(r * math.cos(2 * math.pi * j / k + phase), r * math.sin(2 * math.pi * j / k + phase)) for j in range(k)]
def wire_rope(name, p0, p1, r, material, parent, step=0.05):
    """钢丝绳：扭转的六边形截面（平直着色 → 斜向股纹），每米扭 1 圈半（捻距约 0.11 m / 股）。"""
    p0, p1 = Vector(p0), Vector(p1); n = max(2, int((p1 - p0).length / step) + 1)
    pts = [p0.lerp(p1, i / (n - 1)) for i in range(n)]
    return sweep(name, pts, circ(r, 6), material, parent, twist=2 * math.pi * 9.0, smooth=False)
def stripes(name, origin, U, V, L, H, parent, w=0.10, off=0.003, mats=("yel", "blk")):
    """45° 黄黑警示斜纹贴板（单面，离表面 off）：原点 + U·[0,L] + V·[0,H]，法线 = U×V。"""
    origin, U, V = Vector(origin), Vector(U).normalized(), Vector(V).normalized()
    Nn = U.cross(V); bm = bmesh.new(); mi = []
    def clip(poly, f):
        out = []
        for i in range(len(poly)):
            a, b = poly[i - 1], poly[i]; fa, fb = f(a), f(b)
            if fb >= 0:
                if fa < 0: out.append(a + (b - a) * (fa / (fa - fb)))
                out.append(b)
            elif fa >= 0: out.append(a + (b - a) * (fa / (fa - fb)))
        return out
    k0, k1 = int(math.floor(-H / w)) - 1, int(math.ceil(L / w)) + 1
    from mathutils import Vector as V2
    for k in range(k0, k1):
        poly = [V2((0, 0)), V2((L, 0)), V2((L, H)), V2((0, H))]
        poly = clip(poly, lambda p, k=k: (p.x + p.y) - k * w)
        poly = clip(poly, lambda p, k=k: (k + 1) * w - (p.x + p.y))
        if len(poly) < 3: continue
        f = bm.faces.new([bm.verts.new(origin + U * p.x + V * p.y + Nn * off) for p in poly])
        f.material_index = k % 2
    return new_obj(name, bm, parent, list(mats))

# =====================================================================================
# 部件空物体
# =====================================================================================
Bridge = part("CR01_Bridge", (0, 0, 0), root)
Trolley = part("CR01_Trolley", (0, 4.0, 0), Bridge)
Rope = part("CR01_Rope", (0, 4.0, 7.0), Trolley)
Hook = part("CR01_Hook", (0, 4.0, 3.0), Rope)
Mug = part("CR01_Mug", (0, 4.0, 3.02), Hook)
Water = part("CR01_MugWater", (0, 4.0, 3.07), Hook)
CabMug = part("CR01_CabMug", (0, 2.0, 5.25), Bridge)

# =====================================================================================
# 桥架：两根箱形主梁（X ±0.55，两端变截面搭在端梁上），端梁（车轮压轨顶 Z 7.0），走台 + 栏杆，集中驱动 + 传动长轴
# 灰盒主梁在 X 0 / −0.9；摆动轴在 X 0，真天车的绳从两梁中间下来，所以模型主梁对称放在 X ±0.55（整体东移 0.45 m）。
# =====================================================================================
GX, GW = 0.55, 0.18                 # 主梁中心、半宽（腹板外皮）
Y0, Y1 = 0.66, 17.34                # 主梁两端（柱内侧 Y 0.6 外留 6 cm）
EB_TOP = 7.62                       # 端梁顶 = 主梁端部底
TOP = 8.20                          # 主梁顶（小车轨底）
BOT = 7.12                          # 跨中主梁底
TAPER = (1.10, 2.60)                # 变截面起止（离端）
for s, gx in (("E", GX), ("W", -GX)):
    prof = [(Y0, TOP), (Y0, EB_TOP), (Y0 + TAPER[0], EB_TOP), (Y0 + TAPER[1], BOT), (Y1 - TAPER[1], BOT),
            (Y1 - TAPER[0], EB_TOP), (Y1, EB_TOP), (Y1, TOP)]
    prism(f"Girder_{s}_Box", prof, 'X', gx - GW, gx + GW, "paint", Bridge, bev=0.012, seg=2)
    box(f"Girder_{s}_TopFlange", gx - GW - 0.035, gx + GW + 0.035, Y0, Y1, TOP, TOP + 0.022, "paint", Bridge, bev=0.006)
    bot = [(Y0 + TAPER[0], EB_TOP), (Y0 + TAPER[1], BOT), (Y1 - TAPER[1], BOT), (Y1 - TAPER[0], EB_TOP),
           (Y1 - TAPER[0], EB_TOP - 0.02), (Y1 - TAPER[1], BOT - 0.02), (Y0 + TAPER[1], BOT - 0.02), (Y0 + TAPER[0], EB_TOP - 0.02)]
    prism(f"Girder_{s}_BotFlange", bot, 'X', gx - GW - 0.035, gx + GW + 0.035, "paint", Bridge, bev=0.005)
    # 小车轨（方钢）
    box(f"TrolleyRail_{s}", gx - 0.03, gx + 0.03, Y0 + 0.1, Y1 - 0.1, TOP + 0.022, TOP + 0.075, "steel", Bridge, bev=0.006)
    # 腹板外侧竖向加劲板（每 1.5 m）
    sx = 1 if gx > 0 else -1
    for i in range(10):
        y = Y0 + TAPER[1] + 0.45 + i * 1.32
        if y > Y1 - TAPER[1] - 0.2: break
        box(f"Girder_{s}_Stiff{i}", gx + sx * GW, gx + sx * (GW + 0.06), y - 0.006, y + 0.006, BOT + 0.03, TOP - 0.02, "paint", Bridge)
    # 跨中两侧警示斜纹（外腹板，长 1.4 m）
    xo = gx + sx * (GW + 0.0)
    for k, yc in enumerate((7.6, 10.4)):
        if sx > 0: stripes(f"Girder_{s}_Stripe{k}", (xo, yc - 0.7, BOT + 0.25), (0, 1, 0), (0, 0, 1), 1.4, 0.6, Bridge)
        else: stripes(f"Girder_{s}_Stripe{k}", (xo, yc + 0.7, BOT + 0.25), (0, -1, 0), (0, 0, 1), 1.4, 0.6, Bridge)
    # 底面斜纹（仰拍能看到）：变截面段下方各一块
    for k, (ya, yb) in enumerate(((Y0 + TAPER[1], Y0 + TAPER[1] + 1.2), (Y1 - TAPER[1] - 1.2, Y1 - TAPER[1]))):
        stripes(f"Girder_{s}_StripeUnder{k}", (gx - GW, ya, BOT), (0, 1, 0), (1, 0, 0), yb - ya, 2 * GW, Bridge)
# 铆钉（1950 年代铆接箱梁的特征；半圆头 Ø22，8 边 2 圈）：按 (位置, 法线) 批量生成到一个物体里
def rivets(name, items, parent, r=0.011, material="paint"):
    bm = bmesh.new()
    for (p, nrm) in items:
        p, nrm = Vector(p), Vector(nrm).normalized()
        t1 = nrm.cross(Vector((0, 0, 1)) if abs(nrm.z) < 0.9 else Vector((1, 0, 0))).normalized(); t2 = nrm.cross(t1)
        base = [bm.verts.new(p + (t1 * math.cos(a) + t2 * math.sin(a)) * r) for a in [2 * math.pi * j / 8 for j in range(8)]]
        mid = [bm.verts.new(p + (t1 * math.cos(a) + t2 * math.sin(a)) * r * 0.72 + nrm * r * 0.42) for a in [2 * math.pi * j / 8 for j in range(8)]]
        tip = bm.verts.new(p + nrm * r * 0.6)
        for j in range(8):
            bm.faces.new((base[j], base[(j + 1) % 8], mid[(j + 1) % 8], mid[j])).smooth = True
            bm.faces.new((mid[j], mid[(j + 1) % 8], tip)).smooth = True
    return new_obj(name, bm, parent, material)
for s, gx in (("E", GX), ("W", -GX)):
    sx = 1 if gx > 0 else -1; xw = gx + sx * GW; items = []
    y = Y0 + 0.08
    while y < Y1 - 0.05:
        if y < Y0 + TAPER[0] or y > Y1 - TAPER[0]: zb = EB_TOP
        elif y < Y0 + TAPER[1]: zb = EB_TOP + (BOT - EB_TOP) * (y - Y0 - TAPER[0]) / (TAPER[1] - TAPER[0])
        elif y > Y1 - TAPER[1]: zb = EB_TOP + (BOT - EB_TOP) * (Y1 - TAPER[0] - y) / (TAPER[1] - TAPER[0])
        else: zb = BOT
        for zz in (zb + 0.045, TOP - 0.045):
            items.append(((xw, y, zz), (sx, 0, 0)))
        y += 0.16
    # 端部搭接板（腹板落在端梁上的那一段）：竖向 3 列
    for ye in (Y0 + 0.15, Y0 + 0.35, Y0 + 0.55, Y1 - 0.15, Y1 - 0.35, Y1 - 0.55):
        for zz in (EB_TOP + 0.15, EB_TOP + 0.3, EB_TOP + 0.45):
            items.append(((xw, ye, zz), (sx, 0, 0)))
    rivets(f"Girder_{s}_Rivets", items, Bridge)
for s, ya, yb in (("S", 0.62, 1.05), ("N", 16.95, 17.38)):
    items = []
    for face_y, ny in ((ya, -1), (yb, 1)):
        x = -1.6
        while x <= 1.6:
            for zz in (7.3, 7.56): items.append(((x, face_y, zz), (0, ny, 0)))
            x += 0.12
    rivets(f"EndBeam_{s}_Rivets", items, Bridge)

# 主梁之间的连接横撑（端部和跨中）
for i, y in enumerate((Y0 + 0.4, 6.0, 12.0, Y1 - 0.4)):
    z0 = EB_TOP + 0.15 if (y < 2 or y > 16) else TOP - 0.45
    box(f"CrossTie_{i}", -GX + GW, GX - GW, y - 0.06, y + 0.06, z0, z0 + 0.14, "paint", Bridge, bev=0.008)

# 端梁（南 / 北）：车轮中心在轨道 Y 0.75 / 17.25
for s, yc, yi in (("S", 0.75, 1.0), ("N", 17.25, -1.0)):
    ya, yb = (0.62, 1.05) if s == "S" else (16.95, 17.38)
    box(f"EndBeam_{s}", -1.65, 1.65, ya, yb, 7.24, EB_TOP, "paint", Bridge, bev=0.015, seg=2)
    box(f"EndBeam_{s}_CapPlate", -1.68, 1.68, ya - 0.01, yb + 0.01, EB_TOP - 0.02, EB_TOP + 0.006, "paint", Bridge, bev=0.004)
    for xw in (-1.3, 1.3):
        cyl(f"EndBeam_{s}_Wheel{xw:+.0f}", 'Y', (xw, yc, 7.2), 0.2, 0.09, "steel", Bridge, seg=24)
        cyl(f"EndBeam_{s}_WheelFlange{xw:+.0f}", 'Y', (xw, yc - 0.055 * yi * -1, 7.2), 0.225, 0.02, "steel", Bridge, seg=24)
        box(f"EndBeam_{s}_Bearing{xw:+.0f}", xw - 0.13, xw + 0.13, ya - 0.05, yb + 0.05, 7.12, 7.3, "mach", Bridge, bev=0.012)
        cyl(f"EndBeam_{s}_Axle{xw:+.0f}", 'Y', (xw, yc, 7.2), 0.05, (yb - ya) + 0.16, "steel", Bridge, seg=10)
    # 缓冲器（橡胶 + 座）：两端
    for sx in (-1, 1):
        box(f"EndBeam_{s}_BufferSeat{sx:+d}", sx * 1.65, sx * 1.70, ya + 0.04, yb - 0.04, 7.28, 7.58, "paint", Bridge, bev=0.006)
        cyl(f"EndBeam_{s}_Buffer{sx:+d}", 'X', (sx * 1.80, yc + 0.08 * yi, 7.43), 0.085, 0.2, "blk", Bridge, seg=14)
        stripes(f"EndBeam_{s}_StripeEnd{sx:+d}", (sx * 1.703, ya + 0.04 if sx > 0 else yb - 0.04, 7.28),
                (0, 1 if sx > 0 else -1, 0), (0, 0, 1), (yb - ya) - 0.08, 0.30, Bridge)
    # 内侧面通长斜纹（朝跨内，仰拍可见）
    yf = yb if s == "S" else ya
    if s == "S": stripes(f"EndBeam_{s}_Stripe", (1.6, yf, 7.26), (-1, 0, 0), (0, 0, 1), 3.2, 0.34, Bridge)
    else: stripes(f"EndBeam_{s}_Stripe", (-1.6, yf, 7.26), (1, 0, 0), (0, 0, 1), 3.2, 0.34, Bridge)
# 2026 自动化套件：南端梁东车轮轴端编码器 + 线缆（美术圣经 §6，待核 K-05）
cyl("Kit2026_Encoder", 'Y', (1.3, 0.50, 7.2), 0.045, 0.07, "kit", Bridge, seg=14)
box("Kit2026_EncoderBracket", 1.24, 1.36, 0.53, 0.57, 7.12, 7.28, "kit", Bridge)
sweep("Kit2026_EncoderCable", [(1.3, 0.47, 7.2), (1.3, 0.45, 7.3), (1.1, 0.58, 7.6), (0.95, 0.9, 7.66), (0.9, 2.0, 7.4)], circ(0.008, 6), "blk", Bridge)

# 走台（东主梁外侧）+ 栏杆 + 集中驱动（跨中）+ 传动长轴
WX0, WX1, WZ = GX + GW + 0.02, 1.55, 7.16
box("Walkway_Deck", WX0, WX1, 1.2, 16.8, WZ, WZ + 0.012, "paint", Bridge, bev=0.004)
box("Walkway_Toe", WX1 - 0.006, WX1, 1.2, 16.8, WZ, WZ + 0.12, "paint", Bridge)
for i in range(12):
    y = 1.3 + i * 1.4
    box(f"Walkway_Bracket{i}", WX0, WX1 - 0.02, y - 0.02, y + 0.02, WZ - 0.22, WZ, "paint", Bridge)
    cyl(f"Railing_Post{i}", 'Z', (WX1 - 0.03, y, WZ + 0.53), 0.018, 1.05, "paint", Bridge, seg=6)
cyl("Railing_Top", 'Y', (WX1 - 0.03, 9.0, WZ + 1.05), 0.022, 15.6, "paint", Bridge, seg=8)
cyl("Railing_Mid", 'Y', (WX1 - 0.03, 9.0, WZ + 0.55), 0.016, 15.6, "paint", Bridge, seg=6)
box("Drive_MotorBase", 0.85, 1.45, 8.4, 9.6, WZ + 0.012, WZ + 0.08, "mach", Bridge, bev=0.006)
cyl("Drive_Motor", 'Y', (1.15, 8.75, WZ + 0.27), 0.18, 0.5, "mach", Bridge, seg=20, bev=0.01)
for i in range(6):
    a = 2 * math.pi * i / 6
    box(f"Drive_MotorFin{i}", 1.15 + 0.19 * math.cos(a) - 0.01, 1.15 + 0.19 * math.cos(a) + 0.01, 8.55, 8.95,
        WZ + 0.27 + 0.19 * math.sin(a) - 0.01, WZ + 0.27 + 0.19 * math.sin(a) + 0.01, "mach", Bridge)
cyl("Drive_BrakeDrum", 'Y', (1.15, 9.08, WZ + 0.27), 0.12, 0.08, "steel", Bridge, seg=18)
box("Drive_BrakeShoe", 1.0, 1.3, 9.03, 9.13, WZ + 0.08, WZ + 0.44, "mach", Bridge, bev=0.01)
box("Drive_Gearbox", 0.92, 1.38, 9.2, 9.55, WZ + 0.08, WZ + 0.5, "mach", Bridge, bev=0.015, seg=2)
cyl("Drive_LineShaft", 'Y', (1.15, 9.0, WZ + 0.27), 0.03, 16.0, "steel", Bridge, seg=8)
for i, y in enumerate((2.0, 3.6, 5.2, 6.8, 11.2, 12.8, 14.4, 16.0)):
    box(f"Drive_ShaftBearing{i}", 1.07, 1.23, y - 0.06, y + 0.06, WZ + 0.012, WZ + 0.31, "mach", Bridge, bev=0.006)
# 传动轴到端梁的减速箱
for s, y in (("S", 1.12), ("N", 16.88)):
    box(f"Drive_EndReducer_{s}", 1.0, 1.45, y - 0.12 if s == "S" else y - 0.12, y + 0.12, WZ + 0.0, WZ + 0.45, "mach", Bridge, bev=0.012)

# 司机室吊挂（桁架式吊杆）+ 司机室（南端 Y 1.3–2.7，X −0.5–0.9，Z 4.7–6.5）
CX0, CX1, CY0, CY1, CZ0, CZ1 = -0.5, 0.9, 1.3, 2.7, 4.7, 6.5
def girder_bottom(y):
    if y < Y0 + TAPER[0]: return EB_TOP
    if y < Y0 + TAPER[1]: return EB_TOP + (BOT - EB_TOP) * (y - Y0 - TAPER[0]) / (TAPER[1] - TAPER[0])
    return BOT
for i, (x, y) in enumerate(((-0.45, 1.4), (0.55, 1.4), (-0.45, 2.6), (0.55, 2.6))):
    zt = girder_bottom(y) - 0.02
    box(f"Cab_Hanger{i}", x - 0.03, x + 0.03, y - 0.03, y + 0.03, CZ1, zt, "paint", Bridge, bev=0.005)
    box(f"Cab_HangerClamp{i}", x - 0.07, x + 0.07, y - 0.07, y + 0.07, zt - 0.03, zt + 0.02, "paint", Bridge, bev=0.005)
for i, y in enumerate((1.4, 2.6)):
    sweep(f"Cab_HangerBrace{i}", [(-0.45, y, CZ1 + 0.05), (0.55, y, girder_bottom(y) - 0.1)], circ(0.018, 6), "paint", Bridge, smooth=False)
box("Cab_Floor_S", CX0, CX1, CY0, 2.15, CZ0, CZ0 + 0.05, "paint", Bridge, bev=0.008)
box("Cab_Floor_W", CX0, -0.12, 2.15, CY1, CZ0, CZ0 + 0.05, "paint", Bridge, bev=0.008)
box("Cab_Floor_E", 0.58, CX1, 2.15, CY1, CZ0, CZ0 + 0.05, "paint", Bridge, bev=0.008)
# 地板观察窗（无玻璃，四边角钢框）—— 从 N8 仰视看见司机室里的缸要经过这里
box("Cab_FloorWindow_FrameN", -0.12, 0.58, CY1 - 0.04, CY1, CZ0 - 0.03, CZ0 + 0.05, "paint", Bridge)
box("Cab_FloorWindow_FrameS", -0.12, 0.58, 2.15, 2.19, CZ0 - 0.03, CZ0 + 0.05, "paint", Bridge)
for i, (x, y) in enumerate(((CX0, CY0), (CX1, CY0), (CX0, CY1), (CX1, CY1))):
    box(f"Cab_Post{i}", x - 0.03, x + 0.03, y - 0.03, y + 0.03, CZ0, CZ1, "paint", Bridge, bev=0.006)
SILL, HEAD, T = 5.45, 6.36, 0.02
box("Cab_Roof", CX0 - 0.06, CX1 + 0.06, CY0 - 0.06, CY1 + 0.06, CZ1 - 0.04, CZ1 + 0.02, "paint", Bridge, bev=0.012)
box("Cab_RoofHatch", 0.1, 0.6, 1.9, 2.4, CZ1 + 0.02, CZ1 + 0.05, "paint", Bridge, bev=0.008)
# 四面下墙板（东面开门）+ 窗台 + 窗楣 + 竖框
box("Cab_Wall_S", CX0, CX1, CY0 - T, CY0, CZ0 + 0.05, SILL, "paint", Bridge, bev=0.006)
box("Cab_Wall_N", CX0, CX1, CY1, CY1 + T, CZ0 + 0.05, SILL - 0.3, "paint", Bridge, bev=0.006)   # 北面（正面）矮墙 + 大窗
box("Cab_Wall_W", CX0 - T, CX0, CY0, CY1, CZ0 + 0.05, SILL, "paint", Bridge, bev=0.006)
box("Cab_Wall_E_S", CX1, CX1 + T, CY0, 1.75, CZ0 + 0.05, SILL, "paint", Bridge, bev=0.006)
for nm, x0, x1, y0, y1, z in (("S", CX0, CX1, CY0 - 0.05, CY0, SILL), ("W", CX0 - 0.05, CX0, CY0, CY1, SILL),
                               ("Ehead", CX1, CX1 + 0.05, CY0, CY1, HEAD), ("Shead", CX0, CX1, CY0 - 0.05, CY0, HEAD),
                               ("Whead", CX0 - 0.05, CX0, CY0, CY1, HEAD), ("Nhead", CX0, CX1, CY1, CY1 + 0.05, HEAD),
                               ("N", CX0, CX1, CY1, CY1 + 0.05, SILL - 0.3), ("E", CX1, CX1 + 0.05, CY0, 1.75, SILL)):
    box(f"Cab_Rail_{nm}", x0, x1, y0, y1, z - 0.025, z + 0.025, "paint", Bridge, bev=0.006)
for i, x in enumerate((-0.03, 0.43)):
    box(f"Cab_Mullion_S{i}", x - 0.02, x + 0.02, CY0 - 0.04, CY0, SILL, HEAD, "paint", Bridge)
    box(f"Cab_Mullion_N{i}", x - 0.02, x + 0.02, CY1, CY1 + 0.04, SILL - 0.3, HEAD, "paint", Bridge)
box("Cab_Mullion_W", CX0 - 0.04, CX0, 1.98, 2.02, SILL, HEAD, "paint", Bridge)
# 东门（Y 1.75–2.65）：门框 + 门板下半 + 把手
box("Cab_Door_Frame", CX1, CX1 + 0.04, 1.75, 1.79, CZ0 + 0.05, HEAD, "paint", Bridge)
box("Cab_Door_FrameN", CX1, CX1 + 0.04, 2.63, 2.67, CZ0 + 0.05, HEAD, "paint", Bridge)
box("Cab_Door_Lower", CX1 + 0.005, CX1 + 0.025, 1.79, 2.63, CZ0 + 0.08, SILL, "paint", Bridge, bev=0.004)
box("Cab_Door_Rail", CX1 + 0.005, CX1 + 0.045, 1.79, 2.63, SILL - 0.02, SILL + 0.02, "paint", Bridge)
cyl("Cab_Door_Handle", 'Y', (CX1 + 0.06, 2.45, 5.6), 0.012, 0.14, "steel", Bridge, seg=8)
for k, y in enumerate((2.39, 2.51)): cyl(f"Cab_Door_HandlePost{k}", 'X', (CX1 + 0.04, y, 5.6), 0.008, 0.04, "steel", Bridge, seg=6)
# 司机室内：木座椅（Y 1.55，面朝北）、两台凸轮控制器（左右）、放缸的小圆台（缸底 Z 5.185）、铃拉绳
box("Cab_Seat", -0.05, 0.3, 1.42, 1.72, 5.12, 5.17, "wood", Bridge, bev=0.012)
box("Cab_SeatBack", -0.05, 0.3, 1.38, 1.42, 5.17, 5.6, "wood", Bridge, bev=0.01)
cyl("Cab_SeatPost", 'Z', (0.125, 1.57, 4.93), 0.03, 0.4, "mach", Bridge, seg=10)
for k, x in (("L", -0.32), ("R", 0.45)):
    box(f"Cab_Controller{k}", x - 0.12, x + 0.12, 1.85, 2.13, CZ0 + 0.05, 5.32, "mach", Bridge, bev=0.015, seg=2)
    box(f"Cab_Controller{k}_Top", x - 0.13, x + 0.13, 1.84, 2.14, 5.32, 5.34, "mach", Bridge, bev=0.006)
    sweep(f"Cab_Controller{k}_Lever", [(x, 1.99, 5.34), (x, 1.93, 5.52)], circ(0.011, 8), "steel", Bridge)
    torus(f"Cab_Controller{k}_Knob", (x, 1.93, 5.555), 0.012, 0.025, 'Z', "blk", Bridge, seg=10, rseg=6)
    box(f"Cab_Controller{k}_Plate", x - 0.06, x + 0.06, 2.135, 2.138, 5.15, 5.22, "steel", Bridge)
# 放缸的小台：圆盘 Ø0.14 + 立柱（缸底贴台面 Z 5.185）
cyl("Cab_MugStand_Top", 'Z', (0, 2.0, 5.17), 0.07, 0.03, "paint", Bridge, seg=16, bev=0.004)
cyl("Cab_MugStand_Post", 'Z', (0, 2.0, (CZ0 + 0.05 + 5.155) / 2), 0.022, 5.155 - CZ0 - 0.05, "paint", Bridge, seg=8)
cyl("Cab_MugStand_Foot", 'Z', (0, 2.0, CZ0 + 0.06), 0.06, 0.02, "paint", Bridge, seg=12)
# 天车铃：司机室底下东南角，铜铃 + 拉绳穿地板到座椅右手边的木把手
torus("Bell_Lip", (0.62, 1.55, 4.47), 0.12, 0.012, 'Z', "bronze", Bridge, seg=20, rseg=6)
bm_pts = [(0.62, 1.55, 4.47), (0.62, 1.55, 4.6)]
sweep("Bell_Body", [(0.62, 1.55, 4.465), (0.62, 1.55, 4.52), (0.62, 1.55, 4.58), (0.62, 1.55, 4.62)],
      lambda t: [((0.12 - 0.06 * t ** 1.3) * math.cos(a), (0.12 - 0.06 * t ** 1.3) * math.sin(a)) for a in [2 * math.pi * j / 20 for j in range(20)]],
      "bronze", Bridge, up=(1, 0, 0))
box("Bell_Bracket", 0.58, 0.66, 1.51, 1.59, 4.62, CZ0, "mach", Bridge)
sweep("Bell_PullCord", [(0.66, 1.55, 4.55), (0.66, 1.55, CZ0 + 0.05), (0.5, 1.6, 5.45), (0.42, 1.62, 5.75)], circ(0.005, 5), "ropeA", Bridge)
cyl("Bell_PullHandle", 'X', (0.42, 1.62, 5.76), 0.015, 0.1, "wood", Bridge, seg=8)
# 天车铭牌
box("Bridge_Nameplate", GX + GW + 0.002, GX + GW + 0.006, 8.6, 9.4, 7.6, 7.95, "steel", Bridge)

# =====================================================================================
# 小车（原点 (0,4,0)）：车架、4 轮、起升卷筒（轴沿 Y，两段绳在 Y ±0.13 从卷筒底下垂）、电机 / 减速器 / 制动器、
# 导绳器（灰盒“滑轮组” Z 7.0）、2026 冷白状态灯
# =====================================================================================
TY = 4.0; FZ0, FZ1 = 8.33, 8.50
for s, x in (("E", GX), ("W", -GX)):
    box(f"Trolley_SideBeam_{s}", x - 0.09, x + 0.09, TY - 0.75, TY + 0.75, FZ0, FZ1, "paint", Trolley, bev=0.01, seg=2)
    for yy in (-0.58, 0.58):
        cyl(f"Trolley_Wheel_{s}{yy:+.1f}", 'X', (x, TY + yy, 8.385), 0.11, 0.07, "steel", Trolley, seg=20)
        cyl(f"Trolley_WheelFlange_{s}{yy:+.1f}", 'X', (x - 0.04 * (1 if x > 0 else -1), TY + yy, 8.385), 0.125, 0.015, "steel", Trolley, seg=20)
        box(f"Trolley_Bearing_{s}{yy:+.1f}", x - 0.13, x + 0.13, TY + yy - 0.08, TY + yy + 0.08, 8.33, 8.45, "mach", Trolley, bev=0.008)
for k, yy in enumerate((-0.72, 0.72)):
    box(f"Trolley_EndBeam{k}", -GX - 0.09, GX + 0.09, TY + yy - 0.05, TY + yy + 0.05, FZ0, FZ1, "paint", Trolley, bev=0.01)
    stripes(f"Trolley_EndStripe{k}", (-GX - 0.09 if yy < 0 else GX + 0.09, TY + yy + (-0.053 if yy < 0 else 0.053), FZ0),
            (1 if yy < 0 else -1, 0, 0), (0, 0, 1), 2 * (GX + 0.09), FZ1 - FZ0, Trolley, w=0.07)
# 卷筒：轴沿 Y，中心 Z 8.56，半径 0.17（底 8.39）；中段缠绳
DZ, DR = 8.56, 0.17
cyl("Trolley_Drum", 'Y', (0, TY, DZ), DR, 0.84, "mach", Trolley, seg=28)
cyl("Trolley_DrumRopeWrap", 'Y', (0, TY - 0.2, DZ), DR + 0.012, 0.32, "ropeA", Trolley, seg=28)
cyl("Trolley_DrumRopeWrap2", 'Y', (0, TY + 0.2, DZ), DR + 0.012, 0.32, "ropeA", Trolley, seg=28)
for k, yy in enumerate((-0.43, 0.43)):
    cyl(f"Trolley_DrumFlange{k}", 'Y', (0, TY + yy, DZ), DR + 0.04, 0.02, "mach", Trolley, seg=28)
    box(f"Trolley_DrumStand{k}", -0.12, 0.12, TY + yy * 1.4 - 0.05, TY + yy * 1.4 + 0.05, FZ0, DZ + 0.08, "mach", Trolley, bev=0.01)
    box(f"Trolley_CrossBeam{k}", -GX, GX, TY + yy * 1.4 - 0.06, TY + yy * 1.4 + 0.06, FZ0, FZ0 + 0.1, "paint", Trolley, bev=0.008)
cyl("Trolley_DrumShaft", 'Y', (0, TY, DZ), 0.04, 1.34, "steel", Trolley, seg=10)
# 起升减速器 + 电机 + 制动器（北端，沿 X 排）
box("Trolley_HoistGearbox", -0.32, 0.3, TY + 0.66, TY + 0.95, FZ0, 8.78, "mach", Trolley, bev=0.015, seg=2)
cyl("Trolley_HoistMotor", 'X', (-0.55, TY + 0.81, 8.6), 0.13, 0.42, "mach", Trolley, seg=20, bev=0.008)
cyl("Trolley_HoistBrake", 'X', (-0.27, TY + 0.81, 8.6), 0.1, 0.06, "steel", Trolley, seg=18)
box("Trolley_HoistBrakeShoe", -0.31, -0.23, TY + 0.68, TY + 0.94, 8.47, 8.74, "mach", Trolley, bev=0.006)
box("Trolley_Deck_N", -GX, GX, TY + 0.6, TY + 1.0, FZ0 - 0.012, FZ0, "paint", Trolley)
# 小车运行电机（南端西侧）
cyl("Trolley_TravelMotor", 'X', (-0.35, TY - 0.86, 8.52), 0.09, 0.3, "mach", Trolley, seg=16)
box("Trolley_TravelGear", -0.15, 0.08, TY - 0.95, TY - 0.76, FZ0, 8.64, "mach", Trolley, bev=0.01)
box("Trolley_Deck_S", -GX, GX, TY - 1.0, TY - 0.6, FZ0 - 0.012, FZ0, "paint", Trolley)
# 卷筒到摆动轴的两段绳（静止，Y ±0.13）
for k, yy in enumerate((-0.13, 0.13)):
    wire_rope(f"Trolley_RopeDown{k}", (0, TY + yy, DZ - DR - 0.005), (0, TY + yy, 7.0), 0.0095, "ropeA", Trolley)
# 导绳器（灰盒“滑轮组” Z 7.0）：两条吊板从车架挂下来，夹住两根绳
for k, xx in enumerate((-0.2, 0.2)):
    box(f"Trolley_GuideStrap{k}", xx - 0.02, xx + 0.02, TY - 0.03, TY + 0.03, 7.05, FZ0, "paint", Trolley, bev=0.004)
box("Trolley_RopeGuide", -0.22, 0.22, TY - 0.2, TY + 0.2, 6.99, 7.09, "paint", Trolley, bev=0.012, seg=2)
for k, yy in enumerate((-0.13, 0.13)):
    torus(f"Trolley_RopeGuideBush{k}", (0, TY + yy, 6.985), 0.022, 0.008, 'Z', "steel", Trolley, seg=12, rseg=5)
stripes("Trolley_RopeGuideStripeE", (0.223, TY - 0.2, 6.99), (0, 1, 0), (0, 0, 1), 0.4, 0.1, Trolley, w=0.05)
stripes("Trolley_RopeGuideStripeW", (-0.223, TY + 0.2, 6.99), (0, -1, 0), (0, 0, 1), 0.4, 0.1, Trolley, w=0.05)
# 2026 自动化套件：小车东南角状态灯（冷白）+ 编码器
box("Kit2026_LampBox", GX - 0.06, GX + 0.06, TY - 0.86, TY - 0.76, FZ1, FZ1 + 0.08, "kit", Trolley, bev=0.006)
sweep("Kit2026_LampLens", [(GX, TY - 0.81, FZ1 + 0.08), (GX, TY - 0.81, FZ1 + 0.105), (GX, TY - 0.81, FZ1 + 0.12)],
      lambda t: [((0.03 - 0.022 * t * t) * math.cos(2 * math.pi * j / 12), (0.03 - 0.022 * t * t) * math.sin(2 * math.pi * j / 12)) for j in range(12)],
      "led", Trolley, up=(1, 0, 0))
cyl("Kit2026_DrumEncoder", 'Y', (0, TY - 0.72, DZ), 0.04, 0.06, "kit", Trolley, seg=14)
sweep("Kit2026_Cable", [(0, TY - 0.75, DZ), (0.05, TY - 0.8, 8.52), (0.3, TY - 0.82, 8.52), (GX, TY - 0.81, FZ1 + 0.04)], circ(0.006, 5), "blk", Trolley)

# =====================================================================================
# 钢丝绳（原点 = 摆动轴 (0,4,7.0)）：两段绳从 Z 7.0 到钩头动滑轮切点（钩局部 Z 1.25 → 世界 Z 4.25）
# 灰盒绳长 4.0 是摆长（摆动轴 → 吊钩原点），模型绳只画到钩头顶，钩头 + 钩占下面 1.25 m。
# =====================================================================================
HZ = 3.0
SH_Z, SH_R = HZ + 1.25, 0.13
for k, yy in enumerate((-SH_R, SH_R)):
    wire_rope(f"Rope_Fall{k}", (0, TY + yy, 7.0), (0, TY + yy, SH_Z), 0.0095, "rope", Rope)

# =====================================================================================
# 吊钩（原点 (0,4,3.0)）：钩头（两侧板黄黑斜纹 + 动滑轮）、横梁、螺母、钩柄、钩身（锻钩，安全黄）、吊环、4 根吊索、吊盘
# =====================================================================================
# 钩头侧板：轮廓 = 下方矩形 + 上方半圆（绕 X 轴的滑轮），板厚 0.025，X ±0.06
plate = [(-0.21, HZ + 0.98), (0.21, HZ + 0.98)] + [(0.21 * math.cos(a), SH_Z + 0.21 * math.sin(a)) for a in [math.pi * i / 12 for i in range(13)]][0:]
plate = [(-0.21, HZ + 0.98), (0.21, HZ + 0.98)] + [(0.21 * math.cos(math.pi * i / 12), SH_Z + 0.21 * math.sin(math.pi * i / 12)) for i in range(13)]
plate = [(y + TY, z) for (y, z) in plate]
for s, x in (("E", 0.06), ("W", -0.06)):
    prism(f"HookBlock_Plate_{s}", plate, 'X', x - 0.0125, x + 0.0125, "hyel", Hook, bev=0.006, seg=2)
    sx = 1 if x > 0 else -1
    if sx > 0: stripes(f"HookBlock_Stripe_{s}", (x + 0.0125, TY - 0.2, HZ + 0.99), (0, 1, 0), (0, 0, 1), 0.4, 0.22, Hook, w=0.06, off=0.002, mats=("hyel", "hblk"))
    else: stripes(f"HookBlock_Stripe_{s}", (x - 0.0125, TY + 0.2, HZ + 0.99), (0, -1, 0), (0, 0, 1), 0.4, 0.22, Hook, w=0.06, off=0.002, mats=("hyel", "hblk"))
    cyl(f"HookBlock_PinNut_{s}", 'X', (x + sx * 0.02, TY, SH_Z), 0.035, 0.02, "hsteel", Hook, seg=6)
    for yy in (-0.15, 0.15):
        cyl(f"HookBlock_Bolt_{s}{yy:+.2f}", 'X', (x + sx * 0.016, TY + yy, HZ + 1.05), 0.012, 0.012, "hsteel", Hook, seg=6)
# 动滑轮（带绳槽）+ 绳绕滑轮下半圈
sweep("HookBlock_Sheave", [(-0.022, TY, SH_Z), (-0.012, TY, SH_Z), (0.0, TY, SH_Z), (0.012, TY, SH_Z), (0.022, TY, SH_Z)],
      lambda t: [((SH_R + 0.012 - 0.018 * math.sin(math.pi * t)) * math.cos(2 * math.pi * j / 24), (SH_R + 0.012 - 0.018 * math.sin(math.pi * t)) * math.sin(2 * math.pi * j / 24)) for j in range(24)],
      "hsteel", Hook, up=(0, 0, 1))
torus("HookBlock_RopeWrap", (0, TY, SH_Z), SH_R, 0.0095, 'X', "rope", Hook, seg=16, rseg=6, arc=math.pi)
# 绳绕下半圈：torus 默认在 XY 平面，转到 YZ 平面后弧从 +Y 经 −Z ... 用 arc=π 后再修正方向（见下）
o = bpy.data.objects["HookBlock_RopeWrap"]
o.data.transform(Matrix.Translation((0, TY, SH_Z)) @ Matrix.Rotation(math.pi, 4, 'X') @ Matrix.Translation((0, -TY, -SH_Z)))
cyl("HookBlock_Pin", 'X', (0, TY, SH_Z), 0.025, 0.16, "hsteel", Hook, seg=10)
# 横梁 + 螺母 + 钩柄
box("HookBlock_Crosshead", -0.08, 0.08, TY - 0.16, TY + 0.16, HZ + 0.9, HZ + 0.98, "hsteel", Hook, bev=0.01, seg=2)
cyl("Hook_Nut", 'Z', (0, TY, HZ + 0.995), 0.05, 0.035, "hsteel", Hook, seg=6)
cyl("Hook_Shank", 'Z', (0, TY, HZ + 0.8), 0.035, 0.24, "hyel", Hook, seg=16)
cyl("Hook_ShankThread", 'Z', (0, TY, HZ + 0.93), 0.03, 0.06, "hsteel", Hook, seg=16)
# 钩身：中心线在 XZ 平面；钩座（承载点）在 Z HZ+0.40、正对钩柄轴线下方
RC, CZ = 0.092, HZ + 0.455        # 中心线圆半径、圆心
cl = [(0.0, HZ + 0.69), (0.0, HZ + 0.64), (0.03, HZ + 0.6)]
for i in range(0, 23):
    a = math.radians(-10 - 10 * i) if False else math.radians(10 - 10.5 * i)    # 从右上（+X 侧）顺时针绕到左上
    cl.append((RC * math.cos(a), CZ + RC * math.sin(a)))
cl = [(x, TY, z) for (x, z) in cl]
def hook_sec(t):
    # 截面：从钩柄的圆形过渡到梯形（内侧 = 朝圆心，宽），尖端收细
    if t < 0.12: rr = 0.033; return [(rr * math.cos(2 * math.pi * j / 10), rr * math.sin(2 * math.pi * j / 10)) for j in range(10)]
    s = min(1.0, (t - 0.12) / 0.15)
    taper = 1.0 - 0.55 * max(0.0, (t - 0.55) / 0.45) ** 1.2
    h = (0.033 + 0.004 * s) * taper                       # 半高（径向）
    wi, wo = (0.03 + 0.002 * s) * taper, (0.03 - 0.014 * s) * taper   # 内侧 / 外侧半宽
    pts = []
    for j in range(10):
        a = 2 * math.pi * j / 10; u = h * math.cos(a); w = wi if u < 0 else wo
        pts.append((u, w * math.sin(a) * (1 - 0.15 * abs(math.cos(a)))))
    return pts
sweep("Hook_Body", cl, hook_sec, "hyel", Hook, up=(0, -1, 0))
# 吊环（YZ 平面，穿过钩座）+ 4 根吊索 + 吊盘
RING_C = HZ + 0.455 - 0.092 + 0.033 + 0.012 - 0.05
torus("Hook_Ring", (0, TY, RING_C), 0.05, 0.012, 'X', "hsteel", Hook, seg=16, rseg=6)
TRAY_TOP = HZ - 0.045
# 吊盘：圆盘 Ø0.22（灰盒是 0.45 方板；N8 仰角约 36°，方板会把缸整个挡住，所以收小成圆盘，缸口和蓝口沿从 N8 能露出来）
TR = 0.11
cyl("Tray_Plate", 'Z', (0, TY, (HZ - 0.068 + TRAY_TOP) / 2), TR, TRAY_TOP - (HZ - 0.068), "tray", Hook, seg=32, bev=0.004)
torus("Tray_Lip", (0, TY, TRAY_TOP + 0.008), TR - 0.006, 0.008, 'Z', "tray", Hook, seg=32, rseg=6)
for k, ang in enumerate((0.0, 120.0, 240.0)):        # 三根吊索；0° / 120° 分在 N8 视线两侧，240° 在缸背后
    a = math.radians(ang); c, s_ = math.cos(a), math.sin(a)
    lug = Vector((c * (TR - 0.004), TY + s_ * (TR - 0.004), TRAY_TOP + 0.04))
    prism(f"Tray_Lug{k}", [(-0.012, TRAY_TOP), (0.012, TRAY_TOP), (0.012, TRAY_TOP + 0.03), (0.0, TRAY_TOP + 0.05), (-0.012, TRAY_TOP + 0.03)],
          'X', -0.004, 0.004, "tray", Hook)
    o = bpy.data.objects[f"Tray_Lug{k}"]
    o.data.transform(Matrix.Translation((lug.x, lug.y, 0)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
    top = Vector((0, TY, RING_C - 0.05)) + Vector((c, s_, 0)) * 0.006
    wire_rope(f"Sling{k}", top, lug, 0.005, "rope", Hook, step=0.04)
    torus(f"Sling{k}_Eye", (lug.x, lug.y, lug.z), 0.012, 0.0035, 'Z', "hsteel", Hook, seg=10, rseg=4)
    bpy.data.objects[f"Sling{k}_Eye"].data.transform(Matrix.Translation((lug.x, lug.y, lug.z)) @ Matrix.Rotation(math.pi / 2, 4, Vector((-s_, c, 0))) @ Matrix.Translation((-lug.x, -lug.y, -lug.z)))

# =====================================================================================
# 搪瓷缸：白搪瓷 + 蓝口沿 + 卷边；外径 Ø0.10、高 0.12；部件原点在灰盒“搪瓷缸”中心（缸底在原点下 0.065）
# 把手朝东南（-31°），从 N8 看是侧影。司机室旧缸复用同一个造型。水面是独立部件（圆心 = 缸口下 0.5 cm）。
# =====================================================================================
def build_mug(prefix, origin, parent, handle_deg=-31.0):
    ox, oy, oz = origin
    R, H0, H1, WALL = 0.05, -0.065, 0.055, 0.0035
    # 回转轮廓 (r, z)：外底 → 外壁 → 卷边 → 内壁 → 内底（蓝口沿 = 卷边 + 外壁顶 6 mm）
    prof_white = [(0.0, H0), (R - 0.006, H0), (R - 0.002, H0 + 0.003), (R, H0 + 0.008), (R, H1 - 0.012)]
    prof_rim = [(R, H1 - 0.012), (R, H1 - 0.006), (R + 0.0015, H1 - 0.002), (R + 0.0005, H1 + 0.0015),
                (R - 0.002, H1 + 0.0022), (R - WALL, H1 - 0.001), (R - WALL, H1 - 0.008)]
    prof_in = [(R - WALL, H1 - 0.008), (R - WALL, H0 + 0.012), (R - WALL - 0.004, H0 + WALL + 0.001), (0.0, H0 + WALL)]
    SEG = 32
    def revolve(name, prof, material, close_axis):
        bm = bmesh.new(); rings = []
        for (r, z) in prof:
            if r < 1e-6: rings.append([bm.verts.new((ox, oy, oz + z))]); continue
            rings.append([bm.verts.new((ox + r * math.cos(2 * math.pi * j / SEG), oy + r * math.sin(2 * math.pi * j / SEG), oz + z)) for j in range(SEG)])
        for A, B in zip(rings, rings[1:]):
            for j in range(SEG):
                if len(A) == 1: f = bm.faces.new((A[0], B[(j + 1) % SEG], B[j]))
                elif len(B) == 1: f = bm.faces.new((A[j], A[(j + 1) % SEG], B[0]))
                else: f = bm.faces.new((A[j], A[(j + 1) % SEG], B[(j + 1) % SEG], B[j]))
                f.smooth = True
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return new_obj(name, bm, parent, material)
    revolve(prefix + "_Body", prof_white, "enamel", True)
    revolve(prefix + "_Rim", prof_rim, "rim", False)
    revolve(prefix + "_Inner", prof_in, "enamel", True)
    # 把手：C 形扁带（截面 8×3 mm），上端接卷边下 1.5 cm，下端接缸底上 2.5 cm
    a = math.radians(handle_deg); d = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-d.y, d.x, 0))
    pts = []
    for i in range(13):
        t = i / 12; ang = math.pi * t
        rr = 0.003 + 0.036 * math.sin(ang) ** 0.8
        z = (H1 - 0.017) * (1 - t) + (H0 + 0.027) * t + 0.006 * math.sin(ang)
        pts.append(Vector((ox, oy, oz)) + d * (R - 0.002 + rr) + Vector((0, 0, z)))
    sweep(prefix + "_Handle", pts, lambda t: [(0.0016 * math.cos(2 * math.pi * j / 8) / 1.0, 0.0045 * math.sin(2 * math.pi * j / 8)) for j in range(8)],
          "enamel", parent, up=tuple(side))
build_mug("Mug", (0, TY, HZ + 0.02), Mug)
build_mug("CabMug", (0, 2.0, 5.25), CabMug, handle_deg=150.0)
# 水面：独立部件，圆心 (0,4,3.07)；半径 = 缸内壁 0.0465，带 1 mm 弯月面
bm = bmesh.new(); c0 = bm.verts.new((0, TY, HZ + 0.07 - 0.0006)); SEG = 32
ring1 = [bm.verts.new((0.040 * math.cos(2 * math.pi * j / SEG), TY + 0.040 * math.sin(2 * math.pi * j / SEG), HZ + 0.07 - 0.0004)) for j in range(SEG)]
ring2 = [bm.verts.new((0.0464 * math.cos(2 * math.pi * j / SEG), TY + 0.0464 * math.sin(2 * math.pi * j / SEG), HZ + 0.07 + 0.0008)) for j in range(SEG)]
for j in range(SEG):
    bm.faces.new((c0, ring1[j], ring1[(j + 1) % SEG])).smooth = True
    bm.faces.new((ring1[j], ring2[j], ring2[(j + 1) % SEG], ring1[(j + 1) % SEG])).smooth = True
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
new_obj("MugWater_Surface", bm, Water, "water")

# =====================================================================================
# 平滑 / 统计 / 保存
# =====================================================================================
meshes = [o for o in scn.objects if o.type == 'MESH']
for o in meshes:
    smooth_faces = [p.use_smooth for p in o.data.polygons]
    if not any(smooth_faces):
        o.data.shade_smooth(); o.data.set_sharp_from_angle(angle=math.radians(40))
    else:
        o.data.set_sharp_from_angle(angle=math.radians(50))
def tri_count(objs):
    t = 0
    for o in objs: o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
    return t
def owner(o):
    p = o.parent
    while p is not None and p.type != 'EMPTY': p = p.parent
    return p.name if p else "?"
groups = {}
for o in meshes: groups.setdefault(owner(o), []).append(o)
lines = [f"objects={len(meshes)}", f"tris_total={tri_count(meshes)}"]
for k in sorted(groups): lines.append(f"tris_{k}={tri_count(groups[k])}")
print("[CR01] " + " ".join(lines))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f"{ASSET}_build.blend"))
open(os.path.join(OUT, "build_stats.txt"), "w").write("\n".join(lines) + "\n")

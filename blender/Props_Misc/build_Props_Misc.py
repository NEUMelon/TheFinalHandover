# 《最后一次交班》Stage 3 · 杂项道具（BG / Mid）程序化建模。由 AI 辅助工具编写（AI 辅助，已登记 AI 使用日志）。
# 运行：python3 build_Props_Misc.py <输出目录> [ENV 贴图目录] [本目录贴图目录]
# 位置 / 尺寸唯一依据：WorkshopGraybox.cs（BuildMachining / BuildControlRoom / BuildDeskArea / BuildAnnex）与白盒规格 §4、§7。
# 整批一个资产，原点 = 车间西南角 = Unity (0,0,0)。Blender X = 东，Y = 北（Unity Z），Z = 上（Unity Y）。
# 部件：Speaker、Slogan、Drill、Grinder、WorkRack、Shafts、RestTable、RestBenches、ThermosRack、Thermos、MES_Desk、MES_Monitors、
#       MainSwitch_Box、MainSwitch_Handle（原点 = 灰盒“总闸_手柄转轴”(29.68, 1.55, 1.5)，跟 WorkshopRefs.MainSwitchHandle 转）、LockerBench、Chair。
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix
HERE = os.path.dirname(os.path.abspath(__file__)); STAGE3 = os.path.dirname(HERE)
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
ENVTEX = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(STAGE3, "ENV", "Textures")
MYTEX = os.path.abspath(sys.argv[3]) if len(sys.argv) > 3 else os.path.join(HERE, "Textures")
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene

def hexc(h): return tuple((int(h[i:i+2], 16) / 255) ** 2.2 for i in (1, 3, 5))
MATS = {}
def mat_tex(key, tex_name, fallback, texdir):
    m = bpy.data.materials.new(f"M_{tex_name}_Atlas"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*hexc(fallback), 1)
    if os.path.exists(os.path.join(texdir, f"{tex_name}_Atlas_BaseColor.png")):
        def ld(suf, cs):
            im = bpy.data.images.load(os.path.join(texdir, f"{tex_name}_Atlas_{suf}.png"), check_existing=True); im.colorspace_settings.name = cs
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
        t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
        t = ld("MS", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
        nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"]); nt.links.new(sp.outputs[0], b.inputs["Metallic"])
        t = ld("Normal", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    MATS[key] = m
def mat_flat(key, name, col, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*hexc(col), 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    MATS[key] = m
mat_tex("steel", "LSB_ENV_BLD_SteelPaint", "#56615A", ENVTEX)          # 复用 ENV
mat_tex("paint", "LSB_ENV_PRP_MachinePaint", "#5F7F72", MYTEX)         # = LT01 1983 绿漆平铺
mat_tex("iron", "LSB_ENV_PRP_CastIron", "#3A3D3B", MYTEX)
mat_tex("msteel", "LSB_ENV_PRP_MachinedSteel", "#9EA3A3", MYTEX)
mat_tex("worn", "LSB_ENV_PRP_WornBright", "#C9C6BC", MYTEX)
mat_tex("wood", "LSB_ENV_PRP_Wood", "#8E6A42", MYTEX)
mat_tex("slogan", "LSB_ENV_PRP_Slogan", "#E2DBC8", MYTEX)
mat_flat("spk", "M_LSB_ENV_PRP_SpeakerPaint", "#7E847F", 0.55, 0.3)
mat_flat("dark", "M_LSB_ENV_PRP_DarkMetal", "#2E3130", 0.5, 0.6)
mat_flat("bakelite", "M_LSB_ENV_PRP_Bakelite", "#241D19", 0.35)
mat_flat("wheel", "M_LSB_ENV_PRP_GrindingWheel", "#7A776F", 0.9)
mat_flat("thermos", "M_LSB_ENV_PRP_ThermosShell", "#4E7462", 0.35, 0.2)
mat_flat("cork", "M_LSB_ENV_PRP_Cork", "#9A7A55", 0.8)
mat_flat("enamel", "M_LSB_ENV_PRP_EnamelWhite", "#E8E4DA", 0.25)
mat_flat("enrim", "M_LSB_ENV_PRP_EnamelRim", "#1F3A5F", 0.3)
mat_flat("desk", "M_LSB_ENV_PRP_Desk2025_Top", "#B4B6B2", 0.45)
mat_flat("deskframe", "M_LSB_ENV_PRP_Desk2025_Frame", "#4A4D4F", 0.4, 0.6)
mat_flat("monitor", "M_LSB_ENV_PRP_Monitor2025_Housing", "#2A2C2E", 0.45)
mat_flat("yellow", "M_LSB_ENV_PRP_WarnYellow", "#F2C200", 0.5)
mat_flat("black", "M_LSB_ENV_PRP_WarnBlack", "#1A1A1A", 0.5)

root = bpy.data.objects.new("LSB_ENV_PRP_Misc_v01", None); scn.collection.objects.link(root)
class Part:
    def __init__(s, name, pivot=(0, 0, 0)):
        s.name = name; s.pivot = Vector(pivot); s.bm = bmesh.new(); s.uv = s.bm.loops.layers.uv.new("UVMap"); s.mats = []
    def mi(s, key):
        m = MATS[key]
        if m not in s.mats: s.mats.append(m)
        return s.mats.index(m)
PARTS = {}
def P(name, pivot=None):
    if name not in PARTS: PARTS[name] = Part(name, pivot or (0, 0, 0))
    return PARTS[name]

def uv_box(co, n, s=1.0):
    x, y, z = co; ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 0: return (y / s, z / s)
    if ax == 1: return (x / s, z / s)
    return (x / s, y / s)
def add_faces(part, verts, faces, key, uvf=None, smooth=False):
    pt = P(part); bm = pt.bm; vv = [bm.verts.new(v) for v in verts]; idx = pt.mi(key)
    for f in faces:
        face = bm.faces.new([vv[i] for i in f]); face.material_index = idx; face.smooth = smooth
        face.normal_update(); n = face.normal
        for l in face.loops: l[pt.uv].uv = uvf(l.vert.co) if uvf else uv_box(l.vert.co, n)
BOXF = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
def box(part, x0, x1, y0, y1, z0, z1, key):
    x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1)); z0, z1 = sorted((z0, z1))
    add_faces(part, [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], BOXF, key)
def bc(part, c, s, key):      # 中心 + 尺寸
    box(part, c[0] - s[0] / 2, c[0] + s[0] / 2, c[1] - s[1] / 2, c[1] + s[1] / 2, c[2] - s[2] / 2, c[2] + s[2] / 2, key)
def beam(part, a, b, w, h, key, side=(1, 0, 0)):
    a = Vector(a); b = Vector(b); d = (b - a).normalized(); sv = Vector(side)
    if abs(sv.dot(d)) > 0.95: sv = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    sv = (sv - d * sv.dot(d)).normalized(); hv = d.cross(sv).normalized()
    pts = [tuple(p + sv * (w / 2 * i) + hv * (h / 2 * j)) for p in (a, b) for (i, j) in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    add_faces(part, pts, BOXF, key)
def frame_axes(axis):
    a = Vector(axis).normalized()
    u = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    u = (u - a * u.dot(a)).normalized(); v = a.cross(u)
    return a, u, v
def revolve(part, base, axis, prof, key, seg=24, cap0=True, cap1=True, smooth=True, closed=False):
    """prof = [(r, t)]：沿 axis 距 base t 处半径 r。closed=True 时 prof 是闭合截面（壳）。"""
    a, u, v = frame_axes(axis); base = Vector(base)
    rings = []
    for r, t in prof:
        rings.append([tuple(base + a * t + (u * math.cos(2 * math.pi * k / seg) + v * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    verts = [p for ring in rings for p in ring]; faces = []
    nr = len(rings)
    for i in range(nr - 1 + (1 if closed else 0)):
        i2 = (i + 1) % nr
        for k in range(seg):
            k2 = (k + 1) % seg
            faces.append((i * seg + k, i * seg + k2, i2 * seg + k2, i2 * seg + k))
    if not closed:
        if cap0 and prof[0][0] > 1e-6: faces.append(tuple(reversed(range(seg))))
        if cap1 and prof[-1][0] > 1e-6: faces.append(tuple((nr - 1) * seg + k for k in range(seg)))
    pt = P(part); bm = pt.bm; vv = [bm.verts.new(p) for p in verts]; idx = pt.mi(key)
    for f in faces:
        try: face = bm.faces.new([vv[i] for i in f])
        except ValueError: continue
        face.material_index = idx; face.smooth = smooth and len(f) == 4
        face.normal_update()
        for l in face.loops: l[pt.uv].uv = uv_box(l.vert.co, face.normal)
    return a
def cyl(part, c0, c1, r, key, seg=16, smooth=True):
    c0 = Vector(c0); c1 = Vector(c1); L = (c1 - c0).length
    revolve(part, c0, c1 - c0, [(r, 0), (r, L)], key, seg, smooth=smooth)
def fan(part, center, axis_x, r0, r1, a0, a1, x0, x1, key, seg=12):
    """绕 X 轴的扇形挡板 / 弧形罩：角度 φ 从 −Y（南、正面）往上量（度）。生成两侧板 + 外弧带。"""
    cy, cz = center
    def p(x, r, ph): ph = math.radians(ph); return (x, cy - r * math.cos(ph), cz + r * math.sin(ph))
    phs = [a0 + (a1 - a0) * k / seg for k in range(seg + 1)]
    for x in (x0, x1):
        verts = [p(x, r0, ph) for ph in phs] + [p(x, r1, ph) for ph in reversed(phs)]
        add_faces(part, verts, [tuple(range(len(verts))) if x == x1 else tuple(reversed(range(len(verts))))], key)
    verts = [p(x0, r1, ph) for ph in phs] + [p(x1, r1, ph) for ph in phs]
    n = len(phs); add_faces(part, verts, [(k, k + 1, n + k + 1, n + k) for k in range(n - 1)], key)

# =============================================================== 1. 广播喇叭（灰盒 (12, 5.0, 0.45) Ø0.36 × 0.35，柱 ③ 上）
# 柱 ③ 截面 X 11.8–12.2、Z 0–0.6；喇叭装在柱北面（Z 0.6），号筒朝北略下倾 12°（灰盒中心落在柱子里，这里外移到柱面上）
bc("Speaker", (12.0, 0.61, 5.05), (0.16, 0.02, 0.36), "spk")                    # 抱箍底板
for z in (4.92, 5.18): bc("Speaker", (12.0, 0.3, z), (0.44, 0.64, 0.02), "spk")   # 两道抱箍（包住柱）
beam("Speaker", (12.0, 0.62, 5.12), (12.0, 0.72, 5.05), 0.03, 0.03, "spk")
ax = Vector((0, math.cos(math.radians(12)), -math.sin(math.radians(12))))
s0 = Vector((12.0, 0.70, 5.06))
revolve("Speaker", s0, ax, [(0.0, 0.0), (0.075, 0.0), (0.075, 0.12), (0.04, 0.14)], "dark", 20)        # 驱动器
horn = [(0.035, 0.13), (0.05, 0.25), (0.09, 0.38), (0.17, 0.50), (0.185, 0.505),
        (0.18, 0.51), (0.165, 0.50), (0.085, 0.385), (0.046, 0.255), (0.031, 0.135)]
revolve("Speaker", s0, ax, horn, "spk", 24, closed=True)

# =============================================================== 2. 安全标语牌（灰盒 (0.06, 2.6, 12.6)，0.02 × 0.6 × 2.4，西山墙内侧）
y0, y1, z0, z1 = 11.4, 13.8, 2.3, 2.9
uvs = lambda co: ((co[1] - y0) / (y1 - y0), (co[2] - z0) / (z1 - z0))
add_faces("Slogan", [(0.06, y0, z0), (0.06, y1, z0), (0.06, y1, z1), (0.06, y0, z1)], [(0, 1, 2, 3)], "slogan", uvs)   # 正面朝东（+X）
box("Slogan", 0.035, 0.0599, y0, y1, z0, z1, "steel")                                   # 背板
for (a, b, c, d) in ((y0 - 0.02, y1 + 0.02, z0 - 0.02, z0), (y0 - 0.02, y1 + 0.02, z1, z1 + 0.02), (y0 - 0.02, y0, z0, z1), (y1, y1 + 0.02, z0, z1)):
    box("Slogan", 0.03, 0.068, a, b, c, d, "steel")                                    # 角钢边框
for yy in (11.6, 13.6):
    box("Slogan", 0.0, 0.035, yy - 0.02, yy + 0.02, 2.25, 2.95, "steel")                # 墙上挂件

# =============================================================== 3. 立钻（灰盒 底座 (16, 0.1, 3) 0.8×0.2×0.6；立柱 (16, 1.0, 3.2)；主轴箱 (16, 1.75, 3.0) 0.45×0.5×0.5）
box("Drill", 15.6, 16.4, 2.7, 3.3, 0.0, 0.16, "paint")
box("Drill", 15.65, 16.35, 2.72, 3.28, 0.16, 0.2, "msteel")                              # 底座工作面
for xx in (15.8, 16.0, 16.2): box("Drill", xx - 0.01, xx + 0.01, 2.72, 3.1, 0.195, 0.201, "dark")   # T 形槽
cyl("Drill", (16.0, 3.2, 0.2), (16.0, 3.2, 1.9), 0.075, "msteel", 20)
cyl("Drill", (16.0, 3.2, 0.82), (16.0, 3.2, 0.96), 0.11, "paint", 20)                    # 工作台升降套
box("Drill", 15.95, 16.05, 2.95, 3.12, 0.86, 0.94, "paint")
cyl("Drill", (16.0, 2.92, 0.94), (16.0, 2.92, 0.99), 0.19, "paint", 28)                  # 圆工作台
cyl("Drill", (16.0, 2.92, 0.99), (16.0, 2.92, 1.0), 0.18, "msteel", 28)
box("Drill", 15.78, 16.22, 2.75, 3.25, 1.5, 1.92, "paint")                               # 主轴箱
box("Drill", 15.8, 16.2, 2.78, 3.25, 1.92, 2.0, "paint")                                 # 皮带罩
cyl("Drill", (16.0, 3.38, 1.55), (16.0, 3.38, 1.92), 0.085, "paint", 20)                 # 电机（后）
cyl("Drill", (16.0, 2.9, 1.5), (16.0, 2.9, 1.3), 0.035, "msteel", 16)                    # 主轴套筒
revolve("Drill", (16.0, 2.9, 1.3), (0, 0, -1), [(0.03, 0.0), (0.03, 0.04), (0.018, 0.09)], "msteel", 16)   # 钻夹头
cyl("Drill", (16.0, 2.9, 1.21), (16.0, 2.9, 1.08), 0.005, "msteel", 8)
cyl("Drill", (16.22, 2.95, 1.66), (16.28, 2.95, 1.66), 0.035, "paint", 16)               # 进给手柄毂
for k in range(3):
    a = math.radians(90 + 120 * k); e = Vector((16.27, 2.95 + 0.2 * math.cos(a), 1.66 + 0.2 * math.sin(a)))
    cyl("Drill", (16.27, 2.95, 1.66), tuple(e), 0.009, "msteel", 8)
    revolve("Drill", e, (1, 0, 0), [(0.0, -0.025), (0.02, -0.02), (0.022, 0.0), (0.02, 0.02), (0.0, 0.025)], "worn", 10)
box("Drill", 15.82, 15.92, 2.73, 2.75, 1.62, 1.76, "dark")                               # 按钮盒

# =============================================================== 4. 砂轮机（灰盒 座 (19, 0.45, 2.5) 0.3×0.9×0.3；电机 (19, 1.0, 2.5)；砂轮 X 18.75 / 19.25，R 0.12，厚 0.04）
box("Grinder", 18.8, 19.2, 2.3, 2.7, 0.0, 0.04, "iron")
add_faces("Grinder", [(18.86, 2.36, 0.04), (19.14, 2.36, 0.04), (19.14, 2.64, 0.04), (18.86, 2.64, 0.04),
                      (18.92, 2.42, 0.88), (19.08, 2.42, 0.88), (19.08, 2.58, 0.88), (18.92, 2.58, 0.88)], BOXF, "paint")   # 收分立座
box("Grinder", 18.9, 19.1, 2.4, 2.6, 0.88, 0.9, "paint")
cyl("Grinder", (18.86, 2.5, 1.0), (19.14, 2.5, 1.0), 0.1, "paint", 24)                   # 电机
box("Grinder", 18.93, 19.07, 2.42, 2.58, 0.9, 0.93, "paint")
for x0_, x1_ in ((18.82, 18.86), (19.14, 19.18)): cyl("Grinder", (x0_, 2.5, 1.0), (x1_, 2.5, 1.0), 0.075, "paint", 20)
cyl("Grinder", (18.70, 2.5, 1.0), (19.30, 2.5, 1.0), 0.012, "msteel", 8)
for wx in (18.75, 19.25):
    cyl("Grinder", (wx - 0.02, 2.5, 1.0), (wx + 0.02, 2.5, 1.0), 0.12, "wheel", 28)
    cyl("Grinder", (wx - 0.024, 2.5, 1.0), (wx + 0.024, 2.5, 1.0), 0.04, "msteel", 12)    # 法兰
    fan("Grinder", (2.5, 1.0), None, 0.0, 0.14, 55, 300, wx - 0.035, wx + 0.035, "paint")  # 砂轮罩
    box("Grinder", wx - 0.03, wx + 0.03, 2.30, 2.36, 0.965, 0.975, "worn")               # 托架（磨亮）
    beam("Grinder", (wx, 2.33, 0.97), (wx, 2.40, 0.86), 0.02, 0.012, "steel")
    beam("Grinder", (wx - 0.032, 2.36, 1.22), (wx - 0.032, 2.30, 1.14), 0.006, 0.08, "steel", side=(0, 1, 0))  # 护目挡板支杆
box("Grinder", 18.95, 19.05, 2.355, 2.36, 0.62, 0.7, "dark")                             # 开关

# =============================================================== 5. 工件架 + 半成品轴（灰盒 (2, 0.9, 16) 3.0×1.8×0.8；层板 Y 0.4 / 1.0 / 1.6；轴 ×6 R 0.04 L 2.6，Y 1.08，Z 15.7 + 0.12k）
for xx in (0.5, 2.0, 3.5):
    for yy in (15.6, 16.4): bc("WorkRack", (xx + (0.025 if xx < 2 else -0.025 if xx > 2 else 0), yy + (0.025 if yy < 16 else -0.025), 0.9), (0.05, 0.05, 1.8), "steel")
for zz in (0.4, 1.0, 1.6):
    for yy in (15.625, 16.375): box("WorkRack", 0.5, 3.5, yy - 0.025, yy + 0.025, zz - 0.025, zz + 0.025, "steel")
    for xx in (0.525, 2.0, 3.475): box("WorkRack", xx - 0.02, xx + 0.02, 15.6, 16.4, zz - 0.02, zz + 0.02, "steel")
    box("WorkRack", 0.55, 3.45, 15.65, 16.35, zz + 0.019, zz + 0.025, "steel")
for xx in (0.9, 2.0, 3.1): box("WorkRack", xx - 0.04, xx + 0.04, 15.64, 16.36, 1.025, 1.045, "wood")   # 中层木垫块
for k in range(4): cyl("WorkRack", (0.7, 15.75 + 0.15 * k, 0.46), (3.2, 15.75 + 0.15 * k, 0.46), 0.035, "iron", 12)   # 下层圆钢毛坯
for k in range(6):                                                                     # 半成品台阶轴：两端车好，中段还是锻件黑皮
    y = 15.7 + 0.12 * k; zc = 1.085
    steps = [(0.7, 0.95, 0.028, "msteel"), (0.95, 1.6, 0.038, "msteel"), (1.6, 2.4, 0.044, "iron"), (2.4, 3.0, 0.038, "msteel" if k % 3 else "iron"), (3.0, 3.3, 0.026, "msteel" if k % 3 else "iron")]
    for a, b, r, m in steps: cyl("Shafts", (a, y, zc), (b, y, zc), r, m, 14)

# =============================================================== 6. 休息区：长桌 (33.2, 0.74, 4.5) 2.4×0.8；条凳 Z 3.8 / 5.2；暖水瓶架 (31.0, 0.45, 2.6) 0.45×0.9×0.25
box("RestTable", 32.0, 34.4, 4.1, 4.9, 0.72, 0.76, "wood")
for xx in (32.1, 34.3):
    for yy in (4.18, 4.82): bc("RestTable", (xx, yy, 0.36), (0.05, 0.05, 0.72), "wood")
for yy in (4.17, 4.83): box("RestTable", 32.1, 34.3, yy - 0.012, yy + 0.012, 0.62, 0.72, "wood")
for xx in (32.1, 34.3): box("RestTable", xx - 0.012, xx + 0.012, 4.18, 4.82, 0.62, 0.72, "wood")
box("RestTable", 32.1, 34.3, 4.48, 4.52, 0.12, 0.17, "wood")
def bench(part, xa, xb, yc, w=0.25, h=0.45):
    box(part, xa, xb, yc - w / 2, yc + w / 2, h - 0.04, h, "wood")
    for xx in (xa + 0.12, xb - 0.12):
        box(part, xx - 0.02, xx + 0.02, yc - w / 2 + 0.02, yc + w / 2 - 0.02, 0.0, h - 0.04, "wood")
    box(part, xa + 0.12, xb - 0.12, yc - 0.02, yc + 0.02, 0.12, 0.17, "wood")
bench("RestBenches", 32.1, 34.3, 3.8); bench("RestBenches", 32.1, 34.3, 5.2)
for xx in (30.79, 31.21):
    for yy in (2.49, 2.71): bc("ThermosRack", (xx, yy, 0.45), (0.025, 0.025, 0.9), "steel")
for zz in (0.3, 0.9):
    box("ThermosRack", 30.775, 31.225, 2.475, 2.725, zz - 0.02, zz, "steel")
box("ThermosRack", 30.775, 31.225, 2.475, 2.725, 0.06, 0.08, "steel")
def thermos(cx, cy, z0):
    prof = [(0.0, 0.0), (0.058, 0.0), (0.06, 0.01), (0.06, 0.26), (0.05, 0.29), (0.032, 0.31), (0.032, 0.33)]
    revolve("Thermos", (cx, cy, z0), (0, 0, 1), prof, "thermos", 20)
    revolve("Thermos", (cx, cy, z0 + 0.33), (0, 0, 1), [(0.024, 0.0), (0.026, 0.03), (0.0, 0.03)], "cork", 12)
    for t in (0.02, 0.24): revolve("Thermos", (cx, cy, z0 + t), (0, 0, 1), [(0.0615, 0.0), (0.0615, 0.012)], "enrim", 20, cap0=False, cap1=False)
    beam("Thermos", (cx - 0.062, cy, z0 + 0.22), (cx - 0.075, cy, z0 + 0.12), 0.012, 0.008, "dark", side=(0, 1, 0))
thermos(31.0, 2.6, 0.9)                     # 灰盒暖水瓶（交互物 thermos）
thermos(31.08, 2.6, 0.3)                    # 下层第二只
revolve("Thermos", (31.16, 2.64, 0.9), (0, 0, 1), [(0.0, 0.0), (0.042, 0.0), (0.045, 0.085), (0.047, 0.09)], "enamel", 18)   # 茶缸
revolve("Thermos", (31.16, 2.64, 0.9 + 0.084), (0, 0, 1), [(0.047, 0.0), (0.047, 0.007)], "enrim", 18, cap0=False, cap1=False)
beam("Thermos", (31.205, 2.64, 0.97), (31.215, 2.64, 0.92), 0.008, 0.012, "enamel", side=(0, 1, 0))

# =============================================================== 7. 衡 MES 桌（灰盒 MES_桌 (28.5, 0.375, 4.3) 1.6×0.75×0.7）+ 显示器外壳（屏面保留灰盒 MES_屏1/2：(28.1 / 28.9, 1.10, 4.0) 0.6×0.38×0.03）
box("MES_Desk", 27.7, 29.3, 3.95, 4.65, 0.72, 0.75, "desk")
for xx in (27.74, 29.26):
    box("MES_Desk", xx - 0.025, xx + 0.025, 3.99, 4.61, 0.0, 0.72, "deskframe")
    box("MES_Desk", xx - 0.03, xx + 0.03, 3.98, 4.62, 0.0, 0.03, "deskframe")
box("MES_Desk", 27.77, 29.23, 3.97, 3.99, 0.32, 0.70, "deskframe")                        # 挡板
box("MES_Desk", 27.77, 29.23, 3.99, 4.15, 0.62, 0.64, "deskframe")                        # 走线槽
box("MES_Desk", 28.28, 28.72, 4.30, 4.44, 0.75, 0.77, "monitor")                          # 键盘
box("MES_Desk", 28.85, 28.91, 4.33, 4.43, 0.75, 0.77, "monitor")                          # 鼠标
for cx in (28.1, 28.9):
    # 屏幕灰盒：X cx±0.3、Y(上) 0.91–1.29、Z(北) 3.985–4.015，正面朝北。外壳：背壳在屏后，前框压住屏边 1 cm，中间 0.58×0.36 露出灰盒屏面
    box("MES_Monitors", cx - 0.315, cx + 0.315, 3.955, 3.984, 0.895, 1.305, "monitor")
    for (a, b, c, d) in ((cx - 0.315, cx + 0.315, 0.895, 0.92), (cx - 0.315, cx + 0.315, 1.28, 1.305), (cx - 0.315, cx - 0.29, 0.92, 1.28), (cx + 0.29, cx + 0.315, 0.92, 1.28)):
        box("MES_Monitors", a, b, 3.984, 4.022, c, d, "monitor")
    box("MES_Monitors", cx - 0.12, cx + 0.12, 3.925, 3.955, 1.0, 1.2, "monitor")
    box("MES_Monitors", cx - 0.025, cx + 0.025, 3.96, 3.984, 0.75, 0.895, "monitor")
    box("MES_Monitors", cx - 0.12, cx + 0.12, 3.96, 4.15, 0.75, 0.762, "monitor")

# =============================================================== 8. 总闸（灰盒 刀闸箱 (29.85, 1.4, 1.5) 0.3×0.8×0.6；手柄转轴 (29.68, 1.55, 1.5)，手柄向上 0.36）
box("MainSwitch_Box", 29.72, 30.0, 1.2, 1.8, 1.0, 1.8, "steel")
box("MainSwitch_Box", 29.70, 29.72, 1.21, 1.79, 1.01, 1.79, "steel")                      # 箱门
for zz in (1.15, 1.65): cyl("MainSwitch_Box", (29.71, 1.795, zz - 0.04), (29.71, 1.795, zz + 0.04), 0.012, "dark", 8)   # 铰链
cyl("MainSwitch_Box", (29.70, 1.5, 1.55), (29.655, 1.5, 1.55), 0.045, "dark", 16)          # 转轴座
add_faces("MainSwitch_Box", [(29.698, 1.25, 1.62), (29.698, 1.39, 1.62), (29.698, 1.32, 1.74)], [(0, 1, 2)], "black")   # “有电危险”三角牌
add_faces("MainSwitch_Box", [(29.696, 1.265, 1.632), (29.696, 1.375, 1.632), (29.696, 1.32, 1.725)], [(0, 1, 2)], "yellow")
add_faces("MainSwitch_Box", [(29.695, 1.325, 1.715), (29.695, 1.31, 1.67), (29.695, 1.33, 1.672), (29.695, 1.315, 1.64), (29.695, 1.335, 1.68), (29.695, 1.318, 1.678)], [(0, 1, 2, 3, 4, 5)], "black")
PIV = (29.68, 1.5, 1.55)
P("MainSwitch_Handle", PIV)
cyl("MainSwitch_Handle", (29.66, 1.5, 1.55), (29.70, 1.5, 1.55), 0.03, "dark", 16)
box("MainSwitch_Handle", 29.655, 29.705, 1.475, 1.525, 1.55, 1.89, "dark")                # 手柄杆（灰盒 0.05 × 0.36）
cyl("MainSwitch_Handle", (29.68, 1.35, 1.9), (29.68, 1.65, 1.9), 0.02, "bakelite", 14)      # 双手横把

# =============================================================== 9. 更衣凳（灰盒 (33, 0.22, 16.6) 3.0×0.3）、椅子（灰盒 (27.2, 0.45, 13.0) 0.45×0.45，面西对着交班桌）
bench("LockerBench", 31.5, 34.5, 16.6, w=0.3)
box("Chair", 26.975, 27.425, 12.775, 13.225, 0.44, 0.47, "wood")
for xx in (27.0, 27.4):
    for yy in (12.8, 13.2):
        top = 0.95 if xx > 27.2 else 0.44
        box("Chair", xx - 0.018, xx + 0.018, yy - 0.018, yy + 0.018, 0.0, top, "wood")
for zz in (0.68, 0.84): box("Chair", 27.385, 27.415, 12.8, 13.2, zz, zz + 0.08, "wood")      # 靠背横档（东侧）
for xx in (27.0, 27.4): box("Chair", xx - 0.012, xx + 0.012, 12.8, 13.2, 0.15, 0.18, "wood")
box("Chair", 27.0, 27.4, 12.988, 13.012, 0.15, 0.18, "wood")

# =============================================================== 生成物体
def finish(part, parent):
    me = bpy.data.meshes.new(part.name)
    part.bm.to_mesh(me); part.bm.free()
    me.transform(Matrix.Translation(-part.pivot))
    for m in part.mats: me.materials.append(m)
    o = bpy.data.objects.new(part.name, me); scn.collection.objects.link(o); o.parent = parent; o.location = part.pivot
    return o
objs = [finish(p, root) for p in PARTS.values()]
lines = []; tris = 0
for o in objs:
    o.data.calc_loop_triangles(); t = len(o.data.loop_triangles); tris += t
    lines.append(f"{o.name}: tris={t} pivot={tuple(round(v, 3) for v in o.location)}")
print("\n".join(lines)); print(f"[Misc] parts={len(objs)} tris={tris}")
os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, "build_stats.txt"), "w").write("\n".join(lines) + f"\nparts={len(objs)}\ntris={tris}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LSB_ENV_PRP_Misc_v01.blend"))

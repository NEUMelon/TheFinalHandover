# 《最后一次交班》Stage 3 · 三号车间建筑壳（ENV-Kit）程序化建模。由 AI 辅助工具编写（AI 辅助，已登记 AI 使用日志）。
# 运行：python3 build_ENV.py <输出目录> [贴图目录=同目录 Textures/]
# 尺寸唯一依据：02_母项目VS/02_车间白盒规格_v0.1.md §2–§4 与灰盒生成器 WorkshopGraybox.cs（BuildStructure / BuildControlRoom / BuildAnnex）。
# 灰盒里墙、地面、柱、屋面、隔断下段是碰撞体（blocker），模型只替换渲染，所以凡是灰盒有碰撞体的地方，这里的外形按灰盒方块原样做（同一组角点）；
# 灰盒没碰撞体的地方（牛腿、吊车梁、屋架、窗框）按规格细化。〔派生〕的只有：墙顶 9.0 → 10.6 封檐（灰盒在 9.0–10.4 之间留了一道缝）、
# 天窗两端封板、屋架腹杆 / 节点板、屋面板肋、窗的玻璃分格条、门框、线槽走向、斑马线位置。
# 坐标：Blender X = 世界东（Unity X），Blender Y = 世界北（Unity Z），Blender Z = 上（Unity Y）；整栋一个资产，原点 = 车间西南角室内地面 = Unity (0,0,0)。
# 输出：LSB_ENV_BLD_Workshop_v01.blend（建筑壳 + 地面 + 安全线 + 线槽），LSB_ENV_BLD_Glass_v01.blend（可选玻璃，单独资产，默认不摆）。
import bpy, bmesh, math, sys, os
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import env_common as C

OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "Workshop")
TEX = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(HERE, "Textures")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene

# ---------------------------------------------------------------- 材质
def hexc(h): return tuple((int(h[i:i+2], 16) / 255) ** 2.2 for i in (1, 3, 5))
MATS = {}
def mat_tex(key, tex_name, fallback):
    """贴图材质：名字 M_<贴图集>_Atlas（Unity Stage3ArtMaterials 自动接 <贴图集>_Atlas_BaseColor / Normal / MS）。"""
    m = bpy.data.materials.new(f"M_{tex_name}_Atlas"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*hexc(fallback), 1)
    p = os.path.join(TEX, f"{tex_name}_Atlas_BaseColor.png")
    if os.path.exists(p):
        def ld(suf, cs):
            im = bpy.data.images.load(os.path.join(TEX, f"{tex_name}_Atlas_{suf}.png"), check_existing=True); im.colorspace_settings.name = cs
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
        t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
        t = ld("MS", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
        nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"]); nt.links.new(sp.outputs[0], b.inputs["Metallic"])
        t = ld("Normal", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    m["lsb_atlas"] = tex_name
    MATS[key] = m
def mat_flat(key, name, col, rough=0.6, metal=0.0, alpha=1.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*hexc(col), 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if alpha < 1: b.inputs["Alpha"].default_value = alpha
    MATS[key] = m

mat_tex("terrazzo", "LSB_ENV_BLD_Terrazzo", "#B9B4A8")
mat_tex("stain", "LSB_ENV_BLD_FloorStain", "#9A9488")
mat_tex("wain", "LSB_ENV_BLD_Wainscot", "#5E7D6A")
mat_tex("plaster", "LSB_ENV_BLD_Plaster", "#D2C8AF")
mat_tex("concrete", "LSB_ENV_BLD_Concrete", "#A8A59C")
mat_tex("steel", "LSB_ENV_BLD_SteelPaint", "#56615A")
mat_tex("line", "LSB_ENV_BLD_SafetyLine", "#F2C200")
mat_tex("zebra", "LSB_ENV_BLD_Zebra", "#F2C200")
mat_flat("rail", "M_LSB_ENV_BLD_CraneRail", "#4A4B48", 0.45, 0.9)
mat_flat("wood", "M_LSB_ENV_BLD_DoorFrame_Paint", "#5B6A5F", 0.5)            # 附房木门框：刷墙裙绿系深色调和漆
mat_flat("c1998", "M_LSB_ENV_BLD_Conduit1998_PVC", "#A39F92", 0.55)          # L5 1998 灰色 PVC 线槽（泛黄）
mat_flat("c2025", "M_LSB_ENV_BLD_Conduit2025_PVC", "#CDD1D2", 0.35)          # L7 2025 亮灰新线槽
mat_flat("gw2025", "M_LSB_ENV_BLD_Gateway2025", "#3A3E41", 0.4)              # 2025 线槽末端接线盒（无指示灯）
mat_flat("glass_old", "M_LSB_ENV_BLD_GlassOld", "#9FAEA6", 0.15, 0.0, 0.35)  # 旧玻璃：发绿、发雾（需在 Unity 设透明）
mat_flat("glass_new", "M_LSB_ENV_BLD_GlassNew", "#D6DEDF", 0.05, 0.0, 0.15)  # 换过的新玻璃：更透

# ---------------------------------------------------------------- 部件：每个部件一张 bmesh，最后转物体（部件原点 = 世界原点）
root = bpy.data.objects.new("LSB_ENV_BLD_Workshop_v01", None); scn.collection.objects.link(root)
class Part:
    def __init__(s, name):
        s.name = name; s.bm = bmesh.new(); s.uv = s.bm.loops.layers.uv.new("UVMap"); s.mats = []
    def mi(s, key):
        m = MATS[key]
        if m not in s.mats: s.mats.append(m)
        return s.mats.index(m)
PARTS = {}
def P(name):
    if name not in PARTS: PARTS[name] = Part(name)
    return PARTS[name]

def uv_for(mode, co, n):
    """按世界坐标给 UV。box:s = 三向投影（s 米一张）；wain:off = 墙裙（U 沿墙 2 m 一张，V = (z+off)/1.2）。"""
    x, y, z = co; ax = max(range(3), key=lambda i: abs(n[i]))
    if mode.startswith("box"):
        s = float(mode.split(":")[1])
        if ax == 0: return (y / s, z / s)
        if ax == 1: return (x / s, z / s)
        return (x / s, y / s)
    if mode.startswith("wain"):
        off = float(mode.split(":")[1])
        if ax == 0: return (y / 2.0, (z + off) / 1.2)
        if ax == 1: return (x / 2.0, (z + off) / 1.2)
        return (x / 2.0, y / 2.0)
    raise ValueError(mode)

def add_faces(part, verts, faces, key, uvmode):
    bm = part.bm; vv = [bm.verts.new(v) for v in verts]; idx = part.mi(key)
    for f in faces:
        face = bm.faces.new([vv[i] for i in f]); face.material_index = idx; face.smooth = False
        face.normal_update(); n = face.normal
        for l in face.loops: l[part.uv].uv = uv_for(uvmode, l.vert.co, n)

BOX_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
def box(part, x0, x1, y0, y1, z0, z1, key, uvmode="box:2"):
    x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1)); z0, z1 = sorted((z0, z1))
    v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    add_faces(P(part), v, BOX_FACES, key, uvmode)

def beam(part, a, b, w, h, key, uvmode="box:1", side=(1, 0, 0)):
    """两点之间的方截面杆件：w 沿 side 方向，h 沿 (b−a)×side。"""
    a = Vector(a); b = Vector(b); d = (b - a).normalized(); sv = Vector(side)
    sv = (sv - d * sv.dot(d)).normalized(); hv = d.cross(sv).normalized()
    pts = []
    for p in (a, b):
        for (i, j) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            pts.append(tuple(p + sv * (w / 2 * i) + hv * (h / 2 * j)))
    add_faces(P(part), pts, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], key, uvmode)

def prism_x(part, x0, x1, prof, key, uvmode="box:2"):
    """YZ 平面多边形 prof 沿 X 拉伸。"""
    n = len(prof)
    v = [(x0, y, z) for y, z in prof] + [(x1, y, z) for y, z in prof]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    add_faces(P(part), v, faces, key, uvmode)

H_TOP = 10.6       # 墙顶封到屋面顶（灰盒墙只到 9.0，屋面 10.4–10.6，中间是缝）〔派生〕

# ---------------------------------------------------------------- 地面：1 m 分格，每格独立 UV（随机象限 + 旋转）；机床脚下的分格用油污贴图集
fl = P("Floor")
for i in range(36):
    for j in range(18):
        pad = C.pad_of_cell(i, j)
        corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        vv = [fl.bm.verts.new((x, y, 0.0)) for x, y in corners]
        f = fl.bm.faces.new(vv); f.smooth = False
        f.material_index = fl.mi("stain" if pad else "terrazzo")
        for l, (x, y) in zip(f.loops, corners):
            l[fl.uv].uv = C.pad_uv(pad, x, y) if pad else C.cell_tile_uv(i, j, x - i, y - j)

# ---------------------------------------------------------------- 安全线（离地 4 mm 的薄片，只有顶面）
LZ = 0.004
def line_quad(x0, x1, y0, y1, key="line"):
    pt = P("SafetyLines"); bm = pt.bm; idx = pt.mi(key)
    corners = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    f = bm.faces.new([bm.verts.new((x, y, LZ)) for x, y in corners]); f.material_index = idx; f.smooth = False
    along_x = (x1 - x0) >= (y1 - y0)
    for l, (x, y) in zip(f.loops, corners):
        if key == "zebra": l[pt.uv].uv = (x, y)
        elif along_x: l[pt.uv].uv = (x, (y - y0) / (y1 - y0))
        else: l[pt.uv].uv = (y, (x - x0) / (x1 - x0))
# 主通道 Z 7.0–9.0 两侧黄线（灰盒 主通道_南线 / 北线；西端让出大门斑马线）
line_quad(0.6, 24, 6.95, 7.05); line_quad(0.6, 24, 8.95, 9.05)
# 天车试吊区 X 3–9, Z 2–6 黄线围框（灰盒 试吊区_*；拐角不重叠）
line_quad(2.95, 3.05, 1.95, 6.05); line_quad(8.95, 9.05, 1.95, 6.05)
line_quad(3.05, 8.95, 1.95, 2.05); line_quad(3.05, 8.95, 5.95, 6.05)
# 斑马线〔派生〕：西大门门槛内侧、继电器柜前、总闸前（电气设备前禁止堆放区）
line_quad(0.0, 0.6, 6.5, 10.5, "zebra")
line_quad(24.6, 27.6, 0.62, 1.4, "zebra")
line_quad(28.95, 29.95, 1.0, 2.0, "zebra")

# ---------------------------------------------------------------- 墙：墙裙段（0–1.2，墙裙绿 + 上沿 30 mm 深色线）+ 白灰段
def wall(x0, x1, y0, y1, z0, z1, part="Walls", wain_off=0.0, upper="plaster"):
    """z0..z1 跨过 1.2 时拆成墙裙段 + 白灰段（同一组角点，外形与灰盒方块重合）。"""
    if z0 < 1.2 - 1e-6:
        box(part, x0, x1, y0, y1, z0, min(z1, 1.2), "wain", f"wain:{wain_off}")
    if z1 > 1.2 + 1e-6:
        box(part, x0, x1, y0, y1, max(z0, 1.2), z1, upper, "box:2")

T = 0.25
# 南墙 Ⓐ（灰盒 南墙_下 / 南墙_上 / 南墙_窗间墙0–3 / 南墙_控制间段）
wall(0, 30, -T, 0, 0, 3.5)
for i in range(4): wall(i * 6, i * 6 + 0.8, -T, 0, 3.5, 6.0)
wall(24, 30, -T, 0, 3.5, 6.0)
wall(0, 30, -T, 0, 6.0, H_TOP)
# 北墙 Ⓑ（灰盒 北墙_下 / 上 / 窗间墙0–4）
wall(0, 30, 18, 18 + T, 0, 3.5)
for i in range(5): wall(i * 6, i * 6 + 0.8, 18, 18 + T, 3.5, 6.0)
wall(0, 30, 18, 18 + T, 6.0, H_TOP)
# 西山墙（大门 Z 6.5–10.5，门高 4.5）；转角补到 −0.25
wall(-T, 0, -T, 6.5, 0, H_TOP)
wall(-T, 0, 10.5, 18 + T, 0, H_TOP)
wall(-T, 0, 6.5, 10.5, 4.5, H_TOP)
# X30 隔墙（4 cm 厚，门：休息区 Z 4.5–5.5、更衣室 Z 13.5–14.5，高 2.1）+ 上部山墙
for y0, y1 in ((0, 4.5), (5.5, 13.5), (14.5, 18)): wall(29.98, 30.02, y0, y1, 0, 3.6)
for y0, y1 in ((4.5, 5.5), (13.5, 14.5)): wall(29.98, 30.02, y0, y1, 2.1, 3.6)
box("Walls", 29.98, 30.02, 0, 18, 3.6, H_TOP, "plaster")
# 附房（灰盒 BuildAnnex）
wall(36, 36.25, -T, 18 + T, 0, 3.6)
wall(30, 36, 18, 18 + T, 0, 3.6)
wall(30, 36, -T, 0, 0, 3.6)
box("Walls", 30, 36.25, 0, 18.25, 3.6, 3.75, "plaster")
wall(30, 32.5, 9.9, 10.1, 0, 3.6); wall(33.5, 36, 9.9, 10.1, 0, 3.6); wall(32.5, 33.5, 9.9, 10.1, 2.1, 3.6)

# ---------------------------------------------------------------- 柱 + 牛腿（灰盒 柱_i_A/B：X = 0.2, 6, …, 29.8；Z 0.3 / 17.7；0.4 × 0.6 × 9.0）
for i in range(6):
    x = min(max(i * 6, 0.2), 29.8)
    for yc, sgn in ((0.3, 1), (17.7, -1)):
        wall(x - 0.2, x + 0.2, yc - 0.3, yc + 0.3, 0, 9.0, part="Columns", upper="concrete")
        # 牛腿：顶 6.6，挑出 0.3，下斜面〔外形按规格细化，灰盒是 0.4×0.5×0.3 方块，无碰撞体〕
        yf = yc + 0.3 * sgn; ye = yf + 0.3 * sgn
        prof = [(yf, 5.95), (ye, 6.35), (ye, 6.6), (yf, 6.6)]
        if sgn < 0: prof = list(reversed(prof))
        prism_x("Columns", x - 0.2, x + 0.2, prof, "concrete")

# ---------------------------------------------------------------- 吊车梁（T 形混凝土梁，搁在牛腿 6.6 上）+ 钢轨（轨顶 7.0，轨道中心 Z 0.75 / 17.25）
for yc in (0.75, 17.25):
    box("CraneBeams", 0, 30, yc - 0.12, yc + 0.12, 6.6, 6.78, "concrete")
    box("CraneBeams", 0, 30, yc - 0.2, yc + 0.2, 6.78, 6.87, "concrete")
    box("CraneBeams", 0, 30, yc - 0.065, yc + 0.065, 6.87, 6.89, "rail")
    box("CraneBeams", 0, 30, yc - 0.012, yc + 0.012, 6.89, 6.96, "rail")
    box("CraneBeams", 0, 30, yc - 0.035, yc + 0.035, 6.96, 7.0, "rail")
    for k in range(0, 31, 2):   # 轨道压板
        box("CraneBeams", k - 0.04, k + 0.04, yc - 0.1, yc + 0.1, 6.87, 6.9, "rail")

# ---------------------------------------------------------------- 钢屋架（平行弦，下弦中心 9.0，上弦顶 10.4 托屋面板；节间 1.45 m）
ys = [0.3 + 1.45 * k for k in range(13)]
for i in range(6):
    x = min(max(i * 6, 0.2), 29.8)
    beam("RoofTruss", (x, 0.0, 9.0), (x, 18.0, 9.0), 0.18, 0.14, "steel")         # 下弦
    beam("RoofTruss", (x, 0.0, 10.32), (x, 18.0, 10.32), 0.18, 0.14, "steel")      # 上弦
    for k, y in enumerate(ys):
        beam("RoofTruss", (x, y, 9.07), (x, y, 10.25), 0.12, 0.08, "steel")        # 竖杆
        box("RoofTruss", x - 0.006, x + 0.006, y - 0.16, y + 0.16, 8.88, 9.2, "steel", "box:1")    # 节点板
        box("RoofTruss", x - 0.006, x + 0.006, y - 0.16, y + 0.16, 10.18, 10.4, "steel", "box:1")
    for k in range(12):                                                            # 斜腹杆：两半跨向跨中下斜（Pratt）
        ya, yb = ys[k], ys[k + 1]
        if (ya + yb) / 2 < 9: beam("RoofTruss", (x, ya, 10.25), (x, yb, 9.07), 0.1, 0.07, "steel")
        else: beam("RoofTruss", (x, ya, 9.07), (x, yb, 10.25), 0.1, 0.07, "steel")

# ---------------------------------------------------------------- 屋面（灰盒 屋面_南 / 北 / 控制区上方，10.4–10.6）+ 屋面板肋 + 矩形天窗
box("Roof", 0, 30, 0, 6, 10.4, 10.6, "concrete")
box("Roof", 0, 30, 12, 18, 10.4, 10.6, "concrete")
box("Roof", 24, 30, 6, 12, 10.4, 10.6, "concrete")
for k in range(13):                                    # 大型屋面板纵肋（1.5 m 一道），天窗开口处断开
    y = k * 1.5
    if 6 < y < 12: box("Roof", 24, 30, y - 0.04, y + 0.04, 10.22, 10.4, "concrete")
    elif y not in (0.0, 18.0): box("Roof", 0, 30, y - 0.04, y + 0.04, 10.22, 10.4, "concrete")
for i in range(5):                                     # 天窗立柱（灰盒 天窗_立柱南/北i）
    box("Roof", i * 6, i * 6 + 0.12, 5.9, 6.0, 10.6, 11.7, "steel", "box:1")
    box("Roof", i * 6, i * 6 + 0.12, 12.0, 12.1, 10.6, 11.7, "steel", "box:1")
box("Roof", 0, 24, 5.8, 12.2, 11.7, 11.9, "concrete")  # 天窗顶（灰盒 天窗_顶）
box("Roof", 0, 0.12, 6.0, 12.0, 10.6, 11.7, "plaster")         # 天窗两端封板〔派生〕
box("Roof", 23.88, 24.0, 6.0, 12.0, 10.6, 11.7, "plaster")
for y0 in (5.8, 12.2 - 0.06): box("Roof", 0, 24, y0, y0 + 0.06, 11.55, 11.7, "steel", "box:1")   # 天窗挑檐封边

# ---------------------------------------------------------------- 钢窗（高侧窗 窗台 3.5 / 窗顶 6.0；每樘 5.2 m，3 根中梃 1.3 m 一档，2 道横档，小玻璃分格）
def steel_window(x0, x1, z0, z1, yf0, yf1, part="Windows", bays=4, rows=3):
    F = 0.06
    box(part, x0, x1, yf0, yf1, z0, z0 + F, "steel", "box:1"); box(part, x0, x1, yf0, yf1, z1 - F, z1, "steel", "box:1")
    box(part, x0, x0 + F, yf0, yf1, z0, z1, "steel", "box:1"); box(part, x1 - F, x1, yf0, yf1, z0, z1, "steel", "box:1")
    bw = (x1 - x0) / bays; rh = (z1 - z0) / rows
    for k in range(1, bays): xm = x0 + k * bw; box(part, xm - 0.03, xm + 0.03, yf0, yf1, z0, z1, "steel", "box:1")
    for r in range(1, rows): zm = z0 + r * rh; box(part, x0, x1, yf0, yf1, zm - 0.025, zm + 0.025, "steel", "box:1")
    ym0, ym1 = yf0 + 0.015, yf1 - 0.015
    for k in range(bays):
        xm = x0 + (k + 0.5) * bw; box(part, xm - 0.012, xm + 0.012, ym0, ym1, z0, z1, "steel", "box:1")
    for r in range(rows):
        zm = z0 + (r + 0.5) * rh; box(part, x0, x1, ym0, ym1, zm - 0.012, zm + 0.012, "steel", "box:1")
for i in range(4): steel_window(i * 6 + 0.8, i * 6 + 6, 3.5, 6.0, -0.10, -0.04)     # 南窗（灰盒 南窗_i_窗框 / 棂）
for i in range(5): steel_window(i * 6 + 0.8, i * 6 + 6, 3.5, 6.0, 18.04, 18.10)    # 北窗
for i in range(4):                                                                 # 天窗侧窗（南北两面）
    for yf in (5.92, 12.02):
        steel_window(i * 6 + 0.12, (i + 1) * 6, 10.6, 11.7, yf, yf + 0.05, bays=4, rows=2)
# 窗台内侧水泥窗台板（随墙）
for i in range(4): box("Windows", i * 6 + 0.8, i * 6 + 6, -0.04, 0.0, 3.46, 3.5, "concrete")
for i in range(5): box("Windows", i * 6 + 0.8, i * 6 + 6, 18.0, 18.04, 3.46, 3.5, "concrete")

# ---------------------------------------------------------------- 西大门：推拉钢门（半开，灰盒 X −0.12..−0.04, Z 4.4–6.6, 高 4.5）+ 门洞钢包角
wd = "WestDoor"
box(wd, -0.12, -0.04, 4.4, 6.6, 0, 4.5, "steel", "box:1")
for z0, z1 in ((0.0, 0.1), (2.2, 2.3), (4.4, 4.5)): box(wd, -0.04, -0.01, 4.4, 6.6, z0, z1, "steel", "box:1")
for y0, y1 in ((4.4, 4.5), (6.5, 6.6)): box(wd, -0.04, -0.01, y0, y1, 0, 4.5, "steel", "box:1")
beam(wd, (-0.025, 4.5, 0.1), (-0.025, 6.5, 2.2), 0.03, 0.08, "steel", side=(1, 0, 0))
beam(wd, (-0.025, 4.5, 2.3), (-0.025, 6.5, 4.4), 0.03, 0.08, "steel", side=(1, 0, 0))
for y0, y1 in ((6.4, 6.5), (10.5, 10.6)): box(wd, -0.02, 0.03, y0, y1, 0, 4.5, "steel", "box:1")     # 门洞包角
box(wd, -0.02, 0.03, 6.4, 10.6, 4.5, 4.6, "steel", "box:1")

# ---------------------------------------------------------------- 控制间玻璃隔断（灰盒 BuildControlRoom：下段砖墙 0–1.0 + 钢框到 3.05；门在 X 26.5–27.5）
cp = "ControlRoomPartition"
for x0, x1 in ((24, 26.5), (27.5, 30)): wall(x0, x1, 7.97, 8.03, 0, 1.0, part=cp, wain_off=0.2)   # 隔断_Z8_西段 / 东段
wall(23.97, 24.03, 0, 8, 0, 1.0, part=cp, wain_off=0.2)                                            # 隔断_X24_下
box(cp, 24, 30, 7.97, 8.03, 2.95, 3.05, "steel", "box:1")                                          # 隔断_Z8_上框
box(cp, 23.97, 24.03, 0, 8, 2.95, 3.05, "steel", "box:1")                                          # 隔断_X24_上框
box(cp, 26.5, 27.5, 7.97, 8.03, 2.1, 2.95, "plaster")                                               # 控制间_门框上
for x0, x1 in ((24, 26.5), (27.5, 30)):
    box(cp, x0, x1, 7.96, 8.04, 1.0, 1.05, "steel", "box:1")                                       # 窗台压条
    box(cp, x0, x1, 7.975, 8.025, 2.08, 2.12, "steel", "box:1")                                   # 中横档〔派生〕
box(cp, 23.96, 24.04, 0, 8, 1.0, 1.05, "steel", "box:1")
box(cp, 23.975, 24.025, 0, 8, 2.08, 2.12, "steel", "box:1")
for x in (24.0, 25.5, 28.5, 30.0): box(cp, x - 0.03, x + 0.03, 7.97, 8.03, 1.0, 2.95, "steel", "box:1")   # 隔断_竖框
for x in (26.5, 27.5): box(cp, x - 0.04, x + 0.04, 7.96, 8.04, 0, 2.95, "steel", "box:1")               # 门框立梃
for y in (0.0, 2.0, 4.0, 6.0, 8.0): box(cp, 23.97, 24.03, y - 0.03, y + 0.03, 1.0, 2.95, "steel", "box:1")   # 隔断_X24竖框

# ---------------------------------------------------------------- 附房木门框（X30：休息区 Z 4.5–5.5、更衣室 Z 13.5–14.5；Z10 隔墙门 X 32.5–33.5）
df = "DoorFrames"
for y0, y1 in ((4.5, 5.5), (13.5, 14.5)):
    for yy in (y0, y1 - 0.05): box(df, 29.95, 30.05, yy, yy + 0.05, 0, 2.15, "wood", "box:1")
    box(df, 29.95, 30.05, y0, y1, 2.1, 2.15, "wood", "box:1")
for xx in (32.5, 33.45): box(df, xx, xx + 0.05, 9.87, 10.13, 0, 2.15, "wood", "box:1")
box(df, 32.5, 33.5, 9.87, 10.13, 2.1, 2.15, "wood", "box:1")

# ---------------------------------------------------------------- 线槽
# L5 1998 灰色 PVC 线槽（100×60）：控制间南墙 Z 2.3–2.4 → 3 台继电器柜顶下线；沿东侧柱（X 29.8）与 X30 墙到总闸顶
c98 = "Conduit_1998"
def trunk(part, x0, x1, y0, y1, z0, z1, key):
    box(part, x0, x1, y0, y1, z0, z1, key, "box:1")
box_ = trunk
trunk(c98, 24.2, 29.6, 0.0, 0.06, 2.30, 2.40, "c1998")
for x in (25.1, 26.1, 27.1): trunk(c98, x - 0.05, x + 0.05, 0.0, 0.06, 2.0, 2.30, "c1998")
trunk(c98, 29.54, 29.6, 0.0, 0.70, 2.30, 2.40, "c1998")
trunk(c98, 29.54, 29.98, 0.60, 0.70, 2.30, 2.40, "c1998")
trunk(c98, 29.92, 29.98, 0.60, 1.55, 2.30, 2.40, "c1998")
trunk(c98, 29.92, 29.98, 1.45, 1.55, 1.80, 2.30, "c1998")
for x in (24.6, 25.6, 26.6, 27.6, 28.6): trunk(c98, x - 0.06, x + 0.06, 0.055, 0.065, 2.29, 2.41, "c1998")   # 接头盖
# L7 2025 亮灰新线槽（60×40）：旧柜顶 → 南墙 Z 2.52–2.58 → 东侧柱 → X30 墙北行 → 隔断上框顶西行到 X24 → 下到机加工区一侧的接线盒；
#   另一路沿 X30 墙下到衡 MES 桌（桌面 0.75）
c25 = "Conduit_2025"
trunk(c25, 26.3, 26.36, 0.0, 0.04, 2.0, 2.58, "c2025")
trunk(c25, 26.3, 29.6, 0.0, 0.04, 2.52, 2.58, "c2025")
trunk(c25, 29.56, 29.6, 0.0, 0.66, 2.52, 2.58, "c2025")
trunk(c25, 29.56, 29.98, 0.60, 0.66, 2.52, 2.58, "c2025")
trunk(c25, 29.94, 29.98, 0.60, 7.91, 2.52, 2.58, "c2025")
trunk(c25, 29.92, 29.98, 7.85, 7.91, 2.52, 3.09, "c2025")
trunk(c25, 29.92, 29.98, 7.85, 8.03, 3.05, 3.09, "c2025")
trunk(c25, 24.0, 29.98, 7.97, 8.03, 3.05, 3.09, "c2025")
trunk(c25, 23.91, 23.97, 7.97, 8.09, 3.03, 3.09, "c2025")
trunk(c25, 23.91, 23.97, 8.03, 8.09, 1.32, 3.09, "c2025")
box(c25, 23.83, 23.97, 7.98, 8.22, 1.05, 1.32, "gw2025", "box:1")           # 接线盒（无灯）
trunk(c25, 29.94, 29.98, 4.27, 4.33, 0.70, 2.55, "c2025")
trunk(c25, 29.3, 29.98, 4.27, 4.33, 0.66, 0.70, "c2025")
for y in (2.0, 4.0, 6.0): trunk(c25, 29.93, 29.99, y - 0.03, y + 0.03, 2.51, 2.59, "c2025")       # 卡扣
for x in (25.0, 26.5, 28.0): trunk(c25, x - 0.03, x + 0.03, 7.96, 8.04, 3.04, 3.10, "c2025")

# ---------------------------------------------------------------- 生成物体
def finish(part, parent):
    me = bpy.data.meshes.new(part.name); part.bm.to_mesh(me); part.bm.free()
    for m in part.mats: me.materials.append(m)
    o = bpy.data.objects.new(part.name, me); scn.collection.objects.link(o); o.parent = parent
    return o
objs = [finish(p, root) for p in PARTS.values()]
tris = 0; lines = []
for o in objs:
    o.data.calc_loop_triangles(); t = len(o.data.loop_triangles); tris += t
    lines.append(f"{o.name}: tris={t} mats={[m.name for m in o.data.materials]}")
print("\n".join(lines)); print(f"[ENV] parts={len(objs)} tris={tris}")
open(os.path.join(OUT, "build_stats.txt"), "w").write("\n".join(lines) + f"\nparts={len(objs)}\ntris={tris}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LSB_ENV_BLD_Workshop_v01.blend"))

# ---------------------------------------------------------------- 可选玻璃（单独资产：材质需在 Unity 设透明 + 关投影，否则会挡住光柱；默认不摆）
bpy.ops.wm.read_factory_settings(use_empty=True); scn = bpy.context.scene
MATS.clear()
mat_flat("glass_old", "M_LSB_ENV_BLD_GlassOld", "#9FAEA6", 0.15, 0.0, 0.35)
mat_flat("glass_new", "M_LSB_ENV_BLD_GlassNew", "#D6DEDF", 0.05, 0.0, 0.15)
PARTS.clear()
groot = bpy.data.objects.new("LSB_ENV_BLD_Glass_v01", None); scn.collection.objects.link(groot)
import random
rnd = random.Random(1987)
def pane_x(part, x0, x1, y, z0, z1):     # 沿 X 的竖直玻璃（双面）
    k = "glass_new" if rnd.random() < 0.3 else "glass_old"
    for f in ((0, 1, 2, 3), (3, 2, 1, 0)): add_faces(P(part), [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], [f], k, "box:1")
def pane_y(part, x, y0, y1, z0, z1):
    k = "glass_new" if rnd.random() < 0.3 else "glass_old"
    for f in ((0, 1, 2, 3), (3, 2, 1, 0)): add_faces(P(part), [(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], [f], k, "box:1")
for i in range(4):
    for k in range(4): pane_x("Glass_SouthWindows", i * 6 + 0.8 + k * 1.3, i * 6 + 0.8 + (k + 1) * 1.3, -0.07, 3.5, 6.0)
for i in range(5):
    for k in range(4): pane_x("Glass_NorthWindows", i * 6 + 0.8 + k * 1.3, i * 6 + 0.8 + (k + 1) * 1.3, 18.07, 3.5, 6.0)
for i in range(4):
    for yf in (5.945, 12.045):
        for k in range(4): pane_x("Glass_Monitor", i * 6 + 0.12 + k * 1.47, i * 6 + 0.12 + (k + 1) * 1.47, yf, 10.6, 11.7)
for x0, x1 in ((24, 25.5), (25.5, 26.5), (27.5, 28.5), (28.5, 30)): pane_x("Glass_ControlRoom", x0, x1, 8.0, 1.05, 2.95)
for y0 in (0, 2, 4, 6): pane_y("Glass_ControlRoom", 24.0, y0, y0 + 2, 1.05, 2.95)
gobjs = [finish(p, groot) for p in PARTS.values()]
gt = 0
for o in gobjs: o.data.calc_loop_triangles(); gt += len(o.data.loop_triangles)
print(f"[Glass] parts={len(gobjs)} tris={gt}")
open(os.path.join(OUT, "build_stats.txt"), "a").write(f"glass_parts={len(gobjs)}\nglass_tris={gt}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LSB_ENV_BLD_Glass_v01.blend"))

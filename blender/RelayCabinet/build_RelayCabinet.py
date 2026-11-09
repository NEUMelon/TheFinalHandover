# 《最后一次交班》继电器控制柜（旧柜）Hero 模型 · 程序化建模脚本
# 由 Claude 编写（AI 制作，已登记 80_合规/AI使用日志；2026-10-14 用户放开 3D 口径）。
# 尺寸依据：20_场景地编/stage3/01_Hero资产与场景主产规格_v0.1.md §3 与三视图参考 v01。
# 运行：python3 build_RelayCabinet.py <输出目录>（pip 包 bpy 5.1，或 Blender 5.x Scripting）
# 坐标：柜门朝 +Y，Z = 上；局部原点 = 柜底面中心（底座下沿）。站在柜前看，左手 = +X（三视图“左门”“左数第 3 个”都按人站在柜前说）。
# 摆放：控制间南墙、柜门朝北时，局部 +Y 对世界北，无需旋转。
# 两扇门各自原点在铰链轴上，绕 Z 转即开门；交接灯 HandoverLamp_1..4、护罩联锁继电器 Relay_R2C3 为独立子物体，供程序控制。
import bpy, bmesh, math, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Textures'))
import lsb_textures
from mathutils import Vector, Matrix

OUT = sys.argv[-1] if len(sys.argv) > 1 and os.path.isdir(sys.argv[-1]) else os.path.dirname(os.path.abspath(__file__))
MM = 0.001
VER = "v02"
W, D, H, BASE = 1000, 600, 2000, 100
T = 20            # 柜体钢板（含折边）视觉厚度
DOOR_T = 25

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
root = bpy.data.objects.new(f"LSB_ENV_CTL_RelayCabinet_Old_{VER}", None)
scn.collection.objects.link(root)

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
M = {
    "body":   mat("M_CAB_Body_1956GreyGreen", "#6E7A72", 0.55, 0.2),
    "base":   mat("M_CAB_Plinth", "#2E302E", 0.7, 0.3),
    "inner":  mat("M_CAB_MountPlate", "#8C918A", 0.6, 0.3),
    "rail":   mat("M_CAB_DinRail", "#A9ADAD", 0.35, 1.0),
    "relay":  mat("M_CAB_RelayCover", "#EDE6D6", 0.4),
    "relayb": mat("M_CAB_RelayBase", "#2B2826", 0.5),
    "red":    mat("M_CAB_RedTape_1987", "#A8322D", 0.5),
    "white":  mat("M_CAB_WhiteTape_2008", "#E9E2C8", 0.55),
    "term":   mat("M_CAB_TerminalBlock", "#3C3F44", 0.5),
    "duct98": mat("M_CAB_Duct_1998Grey", "#B9B4A8", 0.6),
    "duct25": mat("M_CAB_Duct_2025", "#D9DEE0", 0.45),
    "wire":   mat("M_CAB_Wire_Old", "#7A2E26", 0.5),
    "wireb":  mat("M_CAB_Wire_Old_Black", "#1E1E1E", 0.5),
    "wire25": mat("M_CAB_Wire_2025", "#3B6E9E", 0.45),
    "tie":    mat("M_CAB_ZipTie", "#E8E6DE", 0.5),
    "qr":     mat("M_CAB_QRLabel", "#F4F4F0", 0.5),
    "board":  mat("M_CAB_HandoverBoard", "#EDE6D6", 0.5),
    "lampon": mat("M_CAB_HandoverLamp_On", "#E8B45A", 0.3, 0, "#F0A040", 6.0),   # 亮 = 暖橙（旧方式承担）
    "lampoff":mat("M_CAB_HandoverLamp_Off", "#3A3A38", 0.3),                     # 灭 = 深灰（衡接手）
    "bezel":  mat("M_CAB_LampBezel", "#9EA3A3", 0.3, 1.0),
    "led":    mat("M_CAB_StatusLED_2025", "#E6F0F5", 0.2, 0, "#E6F0F5", 3.0),
    "lock":   mat("M_CAB_Lock", "#B8B4A8", 0.3, 1.0),
    "plate":  mat("M_CAB_Nameplate", "#B8A57A", 0.4, 0.8),
}

def link(o, parent=root, material=None):
    scn.collection.objects.link(o); o.parent = parent
    if material: o.data.materials.append(M[material])
    return o
def new_obj(name, bm, parent=root, material=None):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return link(bpy.data.objects.new(name, me), parent, material)
def add_bevel(o, w, seg=2):
    b = o.modifiers.new("Bevel", 'BEVEL'); b.width = w * MM; b.segments = seg
    b.limit_method = 'ANGLE'; b.angle_limit = math.radians(35); b.harden_normals = True
def box(name, x0, x1, y0, y1, z0, z1, material="body", bevel=0.0, parent=root):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=((x1 - x0) * MM, (y1 - y0) * MM, (z1 - z0) * MM), verts=bm.verts)
    bmesh.ops.translate(bm, vec=((x0 + x1) / 2 * MM, (y0 + y1) / 2 * MM, (z0 + z1) / 2 * MM), verts=bm.verts)
    o = new_obj(name, bm, parent, material)
    if bevel: add_bevel(o, bevel)
    return o
def cyl(name, axis, c, r, length, material, seg=24, parent=root):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r * MM, radius2=r * MM, depth=length * MM)
    if axis == 'X': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'))
    if axis == 'Y': bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'X'))
    bmesh.ops.translate(bm, vec=Vector(c) * MM, verts=bm.verts)
    return new_obj(name, bm, parent, material)
def dome(name, c, r, material, parent=root):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=r * MM)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -1e-6], context='VERTS')   # 只留朝 +Y 的半球
    bmesh.ops.scale(bm, vec=(1, 0.55, 1), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(c) * MM, verts=bm.verts)
    return new_obj(name, bm, parent, material)
def wire(name, pts, r, material, parent=root):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = Vector(p) * MM; bp.handle_left_type = bp.handle_right_type = 'AUTO'
    cu.bevel_depth = r * MM; cu.bevel_resolution = 1; cu.resolution_u = 6
    return link(bpy.data.objects.new(name, cu), parent, material)

X0, X1 = -W / 2, W / 2
# ---------------------------------------------------------------- 柜体（前面开口的钢板箱）
body = bpy.data.objects.new("Body", None); link(body)
box("Plinth", X0 + 20, X1 - 20, 20, D - 20, 0, BASE, "base", 4, body)
box("Body_Back", X0, X1, 0, T, BASE, H, "body", 3, body)
box("Body_SideW", X0, X0 + T, 0, D - DOOR_T, BASE, H, "body", 3, body)
box("Body_SideE", X1 - T, X1, 0, D - DOOR_T, BASE, H, "body", 3, body)
box("Body_Top", X0, X1, 0, D - DOOR_T, H - T, H, "body", 3, body)
box("Body_TopCap", X0 - 10, X1 + 10, -10, D - DOOR_T + 10, H, H + 15, "body", 5, body)
box("Body_Bottom", X0, X1, 0, D - DOOR_T, BASE, BASE + T, "body", 3, body)
box("Body_FrameMullion", -15, 15, D - DOOR_T - 20, D - DOOR_T, BASE + T, H - T, "body", 2, body)
box("MountPlate", X0 + 40, X1 - 40, T, T + 4, BASE + 50, H - 50, "inner", 1, body)

# ---------------------------------------------------------------- 继电器 4 排 × 8 只（护罩联锁 = 第 2 排左数第 3 个）
PY = T + 4                         # 安装板正面
ROWS = [1750, 1500, 1250, 1000]   # 每排导轨中心高（mm）
relays = bpy.data.objects.new("Relays", None); link(relays)
for r, zr in enumerate(ROWS):
    box(f"DinRail_{r+1}", X0 + 60, X1 - 60, PY, PY + 8, zr + 15, zr + 50, "rail", 0.5, relays)
    for c in range(8):
        x = X1 - 90 - c * 105 - 70                                  # 从人的左手（+X）数起
        name = f"Relay_R{r+1}C{c+1}"
        grp = bpy.data.objects.new(name, None); link(grp, relays)
        grp.location = Vector((x + 35, PY + 8, zr)) * MM          # 原点 = 继电器底座底边中点（贴导轨）
        box(name + "_Base", -35, 35, 0, 30, -20, 110, "relayb", 2, grp)
        box(name + "_Cover", -32, 32, 30, 95, -10, 130, "relay", 4, grp)
        box(name + "_Label", -20, 20, 95, 96, 70, 100, "white", 0, grp)
        for k, xx in enumerate((-22, 0, 22)):                     # 底座端子螺钉
            cyl(f"{name}_Screw{k+1}", 'Y', (xx, 31, -12), 5, 3, "rail", 10, grp)
        if (r, c) == (1, 2):
            # 1987 红胶布“护罩 87”缠一圈，2008 白胶布“保留 08”贴在下方；字由贴花（人工/Claude 手写稿）上
            box(name + "_RedTape_1987", -34, 34, 28, 98, 55, 85, "red", 1, grp)
            box(name + "_WhiteTape_2008", -33.5, 33.5, 29, 97, 10, 32, "white", 1, grp)

# ---------------------------------------------------------------- 端子排、1998 灰线槽、2025 新线槽、线束
box("TerminalStrip_Rail", X0 + 60, X1 - 60, PY, PY + 8, 755, 790, "rail", 0.5, body)
for i in range(40):
    x = X0 + 70 + i * 21.5
    box(f"Terminal_{i+1:02d}", x, x + 18, PY + 8, PY + 55, 730, 815, "term", 1, body)
def duct(name, z0, z1, depth, mat_name, slots=True):
    g = bpy.data.objects.new(name, None); link(g, body)
    box(name + "_Back", X0 + 60, X1 - 60, PY, PY + 3, z0, z1, mat_name, 0, g)
    box(name + "_WallTop", X0 + 60, X1 - 60, PY, PY + depth, z1 - 3, z1, mat_name, 0.5, g)
    box(name + "_WallBot", X0 + 60, X1 - 60, PY, PY + depth, z0, z0 + 3, mat_name, 0.5, g)
    box(name + "_Lid", X0 + 58, X1 - 58, PY + depth, PY + depth + 3, z0 - 2, z1 + 2, mat_name, 1, g)
    if slots:
        for i in range(30):                                          # 线槽齿缝（盖板上刻线）
            x = X0 + 75 + i * 29
            box(f"{name}_Slot{i+1:02d}", x, x + 6, PY + depth + 2.5, PY + depth + 3.2, z1 - 22, z1 + 2, "base", 0, g)
    return g
duct("Duct_1998", 560, 640, 60, "duct98")
duct("Duct_2025", 410, 470, 50, "duct25")
for i in range(5):                                                   # 2025 扎带 + 二维码标签
    x = X0 + 140 + i * 180
    box(f"Duct_2025_ZipTie{i+1}", x, x + 5, PY + 50, PY + 58, 395, 485, "tie", 0, body)
    box(f"Duct_2025_QRLabel{i+1}", x + 20, x + 50, PY + 53.5, PY + 54.5, 425, 455, "qr", 0, body)
box("Duct_2025_StatusLED_Housing", X0 + 80, X0 + 120, PY + 50, PY + 60, 420, 460, "duct25", 1, body)
cyl("Duct_2025_StatusLED", 'Y', (X0 + 100, PY + 61, 440), 4, 2, "led", 12, body)
# 每只继电器底端一根旧线下到 1998 线槽；第 2 排第 3 只多一根 2025 蓝线绕到新线槽（衡接管的那根）
wires = bpy.data.objects.new("Wiring", None); link(wires, body)
for r, zr in enumerate(ROWS):
    for c in range(8):
        x = X1 - 125 - c * 105 + (r - 1.5) * 4
        m = "wire" if (r + c) % 3 else "wireb"
        wire(f"Wire_R{r+1}C{c+1}", [(x, PY + 40, zr - 20), (x + 8, PY + 62, zr - 60), (x + (c - 3.5) * 2, PY + 66, 640)], 2.2, m, wires)
wire("Wire_2025_GuardInterlock", [(X1 - 125 - 2 * 105 - 12, PY + 40, 1480), (X1 - 125 - 2 * 105 - 60, PY + 75, 1300), (X1 - 380, PY + 80, 900), (X1 - 400, PY + 60, 470)], 2.8, "wire25", wires)
for i, z in enumerate((1380, 1100, 860)):
    cyl(f"Wire_2025_Tie{i+1}", 'Z', (X1 - 125 - 2 * 105 - 50 + i * 18, PY + 75, z), 6, 4, "tie", 10, wires)

# ---------------------------------------------------------------- 双开门（原点在铰链轴）
DY = D - DOOR_T
def door(side):
    hx = X1 if side == "L" else X0                     # 左门铰链在人的左手边（+X）
    g = bpy.data.objects.new(f"Door_{side}", None); link(g)
    g.location = Vector((hx, DY, 0)) * MM
    sgn = -1 if side == "L" else 1                     # 门板从铰链向中缝延伸的方向
    xa, xb = sorted((0, sgn * 498))
    box(f"Door_{side}_Panel", xa, xb, 0, DOOR_T, BASE + 2, H - 2, "body", 4, g)
    box(f"Door_{side}_Gasket", xa + 15, xb - 15, -4, 0, BASE + 20, H - 20, "base", 0, g)
    for k, z in enumerate((BASE + 180, H - 180)):      # 铰链
        cyl(f"Door_{side}_Hinge{k+1}", 'Z', (0, DOOR_T / 2, z), 9, 90, "lock", 12, g)
    return g, sgn
dl, sl = door("L")
dr, sr = door("R")
# 左门：交接状态板（2025 加装，中心高 1600）+ 4 灯
bx0, bx1 = 50, 470                                      # 相对柜左边
box("HandoverBoard", -bx1, -bx0, DOOR_T, DOOR_T + 6, 1500, 1700, "board", 2, dl)
for l in range(4):
    cx = 110 + l * 100
    cyl(f"HandoverLamp_{l+1}_Bezel", 'Y', (-cx, DOOR_T + 6 + 4, 1600), 36, 8, "bezel", 32, dl)
    lamp = dome(f"HandoverLamp_{l+1}", (-cx, DOOR_T + 6 + 8, 1600), 30, "lampon", dl)
    box(f"HandoverBoard_Num{l+1}", -cx - 8, -cx + 8, DOOR_T + 6, DOOR_T + 6.6, 1525, 1545, "base", 0, dl)
for i, (x, z) in enumerate(((60, 1510), (460, 1510), (60, 1690), (460, 1690))):
    cyl(f"HandoverBoard_Rivet{i+1}", 'Y', (-x, DOOR_T + 7, z), 4, 3, "lock", 10, dl)
# 左门门锁（一字锁芯，螺丝刀开）
cyl("Door_L_Lock", 'Y', (-470, DOOR_T + 4, 1070), 14, 8, "lock", 24, dl)
box("Door_L_Lock_Slot", -470 - 9, -470 + 9, DOOR_T + 8, DOOR_T + 9, 1068, 1072, "base", 0, dl)
# 右门：通风百叶 + 铭牌框
for i in range(6):
    z = 500 + i * 40
    box(f"Door_R_Louver{i+1}", 100, 400, DOOR_T, DOOR_T + 8, z, z + 14, "body", 2, dr)
box("Door_R_NameplateFrame", 140, 440, DOOR_T, DOOR_T + 3, 1760, 1850, "plate", 1, dr)
cyl("Door_R_Handle", 'Z', (470, DOOR_T + 30, 1070), 10, 160, "lock", 16, dr)
for k, z in enumerate((1000, 1140)):
    cyl(f"Door_R_HandlePost{k+1}", 'Y', (470, DOOR_T + 15, z), 7, 30, "lock", 12, dr)

# ---------------------------------------------------------------- 落地修改器、平滑、UV、统计、导出
def all_mesh(): return [o for o in scn.objects if o.type in ('MESH', 'CURVE')]
for o in all_mesh():
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    if o.type == 'CURVE': bpy.ops.object.convert(target='MESH')
    else:
        for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    o.select_set(False)
for o in all_mesh():
    o.data.shade_smooth(); o.data.set_sharp_from_angle(angle=math.radians(40))
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT'); o.select_set(False)
# 年代叠层可平铺贴图（Textures/gen_textures.py 生成）
TEX = {"body": "CAB_Body_1956GreyGreen", "base": "LT01_CastIron", "rail": "LT01_MachinedSteel", "lock": "LT01_WornBright"}
for k, t in TEX.items(): lsb_textures.hook(M[k], t)
lsb_textures.world_uv(all_mesh(), {M[k] for k in TEX})

tris = 0
for o in all_mesh(): o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
print(f"[CAB] objects={len(all_mesh())} tris={tris}")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f"LSB_ENV_CTL_RelayCabinet_Old_{VER}.blend"))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"LSB_ENV_CTL_RelayCabinet_Old_{VER}.fbx"),
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE', bake_space_transform=True, add_leaf_bones=False)
open(os.path.join(OUT, "build_stats.txt"), "w").write(f"objects={len(all_mesh())}\ntris={tris}\n")

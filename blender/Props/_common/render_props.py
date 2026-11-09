# 道具组预览渲染（Cycles CPU，960×540，32 samples）。由 Claude 编写（AI 编写脚本程序化生成）。
# 运行：python3 render_props.py <build 输出根目录（各资产子目录：dsk ldg tpr tbx rod lmp lkr）> <预览输出目录> [视图,…]
# 视图：B（关键机位 B：N1 (27.6, 1.6, 13.0) Yaw 270° 俯 15° 32mm）、desk、ledger、tape、toolbox、rod、lamp、lockers
# 资产按 unity_manifest 的实例位置摆进世界坐标（Blender X = Unity X，Blender Y = Unity Z，Blender Z = Unity Y）。
# 灰盒里不归道具组的东西（椅子、点检单、对讲机、柜内物件）用灰色方块按灰盒尺寸占位，图里标“灰盒占位”。
import bpy, sys, os, math
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import props_lib as L

args = sys.argv[1:]
SRC, OUT = os.path.abspath(args[0]), os.path.abspath(args[1])
VIEWS = args[2].split(",") if len(args) > 2 else ["B", "desk", "ledger", "tape", "toolbox", "rod", "lamp", "lockers"]
os.makedirs(OUT, exist_ok=True)
U = lambda x, y, z: Vector((x, z, y))          # Unity (x, y, z) → Blender

def load(sub, name, pos):
    path = os.path.join(SRC, sub, f"{name}.blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst): dst.objects = list(src.objects)
    root = None
    for o in dst.objects:
        if o is None: continue
        bpy.context.scene.collection.objects.link(o)
        if o.parent is None and o.name.startswith(name.rsplit("_v", 1)[0]): root = o
    root.location = pos
    bpy.context.view_layer.update()
    return root
def part(root, name):
    for o in root.children_recursive:
        if o.name.split(".")[0] == name: return o
def box(name, c, s, hx="#808080", emit=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=c); o = bpy.context.active_object; o.name = name; o.scale = s
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*[c_ ** 2.2 for c_ in L.hexc(hx)], 1); b.inputs["Roughness"].default_value = 0.7
    o.data.materials.append(m); return o

def glass_fix():
    """预览里玻璃板用 Cycles 透明（Unity 端也要透明，见说明.md）。"""
    m = bpy.data.materials.get("M_DSK_Glass")
    if m:
        # 和 Unity URP Lit 透明（Alpha 混合）一样的效果：低不透明度 + 光滑反射
        b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.03; b.inputs["Alpha"].default_value = 0.12

def world_scene(lamps_on=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn, cam, bg = L.render_setup(samples=32, world=(0.30, 0.36, 0.44), strength=0.35)
    L.floor(0, 60, (18, 9), "#8C887E")
    desk = load("dsk", "LSB_ENV_PRP_ShiftDesk_v01", U(26.5, 0, 13.0))
    ledger = load("ldg", "LSB_ENV_PRP_ShiftLedger_v01", U(26.5, 0.772, 13.0))
    tape = load("tpr", "LSB_ENV_PRP_TapeRecorder_v01", U(26.55, 0.62, 13.55))
    tbx = load("tbx", "LSB_ENV_PRP_Toolbox_v01", U(27.3, 0, 12.3))
    rod = load("rod", "LSB_ENV_PRP_ListeningRod_v01", U(27.17, 0.2635, 12.24))
    lk = load("lkr", "LSB_ENV_PRP_Lockers_v01", U(32.9, 0, 17.6))
    lamps = []
    for x in (2, 6, 10, 14, 18, 22):
        for z in (4.5, 9.0, 13.5):
            lamps.append(load("lmp", "LSB_ENV_LGT_PendantLamp_v01", U(x, 5.5, z)))
            if lamps_on:
                L.light(f"Pt{x}_{z}", 'POINT', U(x, 5.35, z), U(x, 0, z), 160, color=(1.0, 0.78, 0.52))
    # 灰盒占位（不归道具组）
    box("灰盒占位_椅子", U(27.2, 0.45, 13.0), (0.45, 0.45, 0.06), "#6A5544")
    box("灰盒占位_点检单", U(26.3, 0.765, 12.75), (0.3, 0.21, 0.004), "#EBEBE0")
    box("灰盒占位_对讲机", U(26.0, 0.86, 12.7), (0.06, 0.04, 0.18), "#262628")
    box("灰盒占位_西山墙", U(0, 3, 9), (0.3, 18, 6), "#C7C2B3"); box("灰盒占位_墙裙", U(0.2, 0.6, 9), (0.1, 18, 1.2), "#5E7D6A")
    box("灰盒占位_北墙", U(15, 3, 18.1), (30, 0.2, 6), "#C7C2B3")
    for x in (3.4, 9.4, 15.4, 21.4): box(f"灰盒占位_车床{x}", U(x, 0.6, 11.0), (2.8, 0.7, 1.2), "#4F6B5C")
    # 16:00 太阳：方位 235°（西南），高度 18°，光从西南射向东北
    az, el = math.radians(235), math.radians(18)
    to_sun = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    s = bpy.data.lights.new("Sun", 'SUN'); s.energy = 3.0; s.color = (1.0, 0.86, 0.66); s.angle = math.radians(1.5)
    so = bpy.data.objects.new("Sun", s); scn.collection.objects.link(so); so.rotation_euler = (-to_sun).to_track_quat('-Z', 'Y').to_euler()
    glass_fix()
    return scn, cam, dict(desk=desk, ledger=ledger, tape=tape, tbx=tbx, rod=rod, lk=lk, lamps=lamps)

def node_cam(cam, pos_u, yaw, pitch, mm):
    cam.location = U(*pos_u)
    cam.rotation_euler = (math.radians(90 - pitch), 0, math.radians(-yaw))   # Unity yaw：0 = 北，顺时针为正；Blender 绕 Z 逆时针
    cam.data.type = 'PERSP'; cam.data.lens = mm; cam.data.sensor_width = 36; cam.data.sensor_fit = 'HORIZONTAL'

def closeup_cam(cam, pivot_u, dist, yaw, pitch, mm):
    d = Vector((math.sin(math.radians(yaw)), math.cos(math.radians(yaw)), 0))   # 视线水平方向（Blender XY）
    piv = U(*pivot_u)
    cam.location = piv - d * dist * math.cos(math.radians(pitch)) + Vector((0, 0, dist * math.sin(math.radians(pitch))))
    cam.rotation_euler = (piv - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'PERSP'; cam.data.lens = mm; cam.data.sensor_width = 36

def render(path):
    bpy.context.scene.render.filepath = path; bpy.ops.render.render(write_still=True); print("rendered", path, flush=True)

if "B" in VIEWS or "tape" in VIEWS or "lockers" in VIEWS or "lamp" in VIEWS:
    scn, cam, A = world_scene()
    if "B" in VIEWS:
        node_cam(cam, (27.6, 1.6, 13.0), 270, 15, 32)
        render(os.path.join(OUT, "机位B_N1_交班桌_16点.png"))
        # 补充：同一位置俯 35°（规格的俯 15° 下交班本只露在画面最下沿，见说明.md）
        node_cam(cam, (27.6, 1.6, 13.0), 270, 35, 32)
        render(os.path.join(OUT, "机位B_补充_同位置俯35度.png"))
    if "tape" in VIEWS:
        # CU5：抽屉拉开 0.3 m（向东），磁带 / 录音机 / 杂物跟着抽屉走；特写机位 pivot (26.55, 0.62, 13.55) 距 0.9，Yaw 270，俯 40，50mm
        for p in ("Drawer",):
            part(A["desk"], p).location.x += 0.3
        A["tape"].location.x += 0.3
        L.light("CU_fill", 'AREA', U(27.4, 1.5, 13.6), U(26.85, 0.6, 13.55), 25, size=0.6, color=(1, 0.9, 0.75))
        closeup_cam(cam, (26.55, 0.62, 13.55), 0.9, 270, 40, 50)
        render(os.path.join(OUT, "磁带录音机_CU5_抽屉拉开.png"))
        # 剖视：藏掉台面 / 封边 / 玻璃板，看抽屉里的磁带标签和录音机（CU5 原机位被台面挡住，见说明.md）
        for o in A["desk"].children_recursive:
            if o.name.split(".")[0] in ("TopPanel", "Lip_W", "Lip_E", "Lip_S", "Lip_N", "Glass", "Burn_1", "Burn_2", "Rail_E_Drawer"): o.hide_render = True
        for o in bpy.data.objects:
            if o.name.startswith("灰盒占位_对讲机") or o.name.startswith("灰盒占位_点检单"): o.hide_render = True
        closeup_cam(cam, (26.85, 0.60, 13.52), 0.75, 270, 55, 50)
        render(os.path.join(OUT, "磁带录音机_剖视_去掉台面.png"))
        for o in A["desk"].children_recursive: o.hide_render = False
        for o in bpy.data.objects:
            if o.name.startswith("灰盒占位_"): o.hide_render = False
        part(A["desk"], "Drawer").location.x -= 0.3; A["tape"].location.x -= 0.3
    if "lockers" in VIEWS:
        dz = part(A["lk"], "LockerDoor_Zhou"); dz.rotation_euler.z = math.radians(-105)   # Unity 铰链 Y +105° = 俯视顺时针
        L.light("Room", 'AREA', U(33, 3.3, 15.5), U(33, 0.9, 17.6), 120, size=2.0, color=(1, 0.9, 0.75))
        box("灰盒占位_手套", U(34.1, 1.25, 17.45), (0.3, 0.18, 0.06), "#D9D1B8"); box("灰盒占位_饭盒", U(34.25, 1.62, 17.5), (0.18, 0.12, 0.08), "#DCDCCB")
        box("灰盒占位_工牌套", U(33.86, 1.45, 17.2), (0.09, 0.005, 0.12), "#BF332E"); box("灰盒占位_合影", U(33.82, 1.55, 17.3), (0.005, 0.25, 0.18), "#99948A")
        node_cam(cam, (34.1, 1.6, 16.2), 0, 0, 30)
        render(os.path.join(OUT, "更衣柜_N2_老周柜门开.png"))
        closeup_cam(cam, (32.9, 0.95, 17.6), 4.2, 10, 8, 32)
        render(os.path.join(OUT, "更衣柜_六只全景.png"))
        dz.rotation_euler.z = 0
    if "lamp" in VIEWS:
        closeup_cam(cam, (10, 5.4, 9.0), 1.4, 300, -35, 40)
        render(os.path.join(OUT, "搪瓷吊灯_仰视_亮.png"))
        node_cam(cam, (14.5, 1.6, 15.5), 220, -12, 28)
        render(os.path.join(OUT, "搪瓷吊灯_SH-A视角.png"))

def studio(sub, name, pos=(0, 0, 0)):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn, cam, bg = L.render_setup(samples=32, world=(0.42, 0.47, 0.52), strength=0.5)
    r = load(sub, name, Vector(pos)); glass_fix()
    return scn, cam, r
if "desk" in VIEWS:
    scn, cam, r = studio("dsk", "LSB_ENV_PRP_ShiftDesk_v01"); L.floor(0, 8)
    L.light("Key", 'AREA', (2.0, -1.6, 2.4), (0, 0, 0.6), 150, size=2)
    L.shoot(cam, os.path.join(OUT, "交班桌_台灯_东南透视.png"), (1.9, -1.9, 1.5), (0, 0.05, 0.6), 35)
if "ledger" in VIEWS:
    scn, cam, r = studio("ldg", "LSB_ENV_PRP_ShiftLedger_v01"); L.floor(0, 4, hx="#5A3A24")
    L.light("Key", 'AREA', (0.5, -0.5, 0.8), (0, 0, 0), 10, size=0.6)
    L.shoot(cam, os.path.join(OUT, "交班本_合上_俯视透视.png"), (0.42, -0.28, 0.40), (-0.01, 0.0, 0.02), 50)
if "toolbox" in VIEWS or "rod" in VIEWS:
    scn, cam, r = studio("tbx", "LSB_ENV_PRP_Toolbox_v01"); L.floor(0, 4)
    rod = load("rod", "LSB_ENV_PRP_ListeningRod_v01", Vector((-0.13, -0.06, 0.2635)))
    L.light("Key", 'AREA', (0.8, 1.0, 1.2), (0, 0, 0.1), 25, size=1)
    if "toolbox" in VIEWS: L.shoot(cam, os.path.join(OUT, "工具箱_听针放在盖上.png"), (0.75, 0.85, 0.65), (0, 0, 0.14), 40)
    if "rod" in VIEWS:
        rod.location = (0, 0, 0.6); rod.rotation_euler = (0, 0, math.radians(20))
        L.shoot(cam, os.path.join(OUT, "听针_特写.png"), (0.12, -0.42, 0.75), (0.08, 0, 0.6), 50)

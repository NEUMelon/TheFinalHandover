# 天车组 CR01 预览渲染（由 Claude 编写）：python3 render_preview.py <blend> <输出目录> [视角,逗号分隔] [百分比] [samples]
# 视角：D = 关键机位 D（N8：Unity (7.5, 0.9, 6.5)，Yaw 225°，仰 20°，28mm，竖向 24 mm 底片，22:00 夜）
#       D_close = N8 附近、往吊钩 / 缸收紧一点的视角；overall = 整体（东北上方俯看）；hook = 吊钩 + 缸特写
# 资产原点 = Unity 世界 (6.0, 0, 0)，所以 Blender 坐标 = (Ux − 6, Uz, Uy)。
import bpy, sys, math, os
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, out = os.path.abspath(args[0]), os.path.abspath(args[1])
views = args[2].split(",") if len(args) > 2 else ["D", "overall"]
pct = int(args[3]) if len(args) > 3 else 100
spp = int(args[4]) if len(args) > 4 else 32
os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)
scn = bpy.context.scene
scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = spp; scn.cycles.use_denoising = True
scn.render.threads_mode = 'FIXED'; scn.render.threads = 2
scn.render.resolution_x, scn.render.resolution_y = 960, 540; scn.render.resolution_percentage = pct
scn.view_settings.view_transform = 'AgX'
def U(x, y, z): return Vector((x - 6.0, z, y))
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]
# 简易厂房环境：地面（水磨石灰）+ 吊车梁两条（只为看清端梁压轨，不导出）
def flat(name, col, rough=0.7):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*col, 1); b.inputs["Roughness"].default_value = rough
    return m
bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 9, 0)); g = bpy.context.active_object; g.data.materials.append(flat("Floor", (0.32, 0.30, 0.27)))
for yy in (0.75, 17.25):
    bpy.ops.mesh.primitive_cube_add(location=(0, yy, 6.8)); b = bpy.context.active_object; b.scale = (15, 0.2, 0.2)
    b.data.materials.append(flat("Runway", (0.35, 0.35, 0.33)))
cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
cam_d.sensor_fit = 'VERTICAL'; cam_d.sensor_height = 24.0
def light(name, kind, loc, energy, col=(1, 1, 1), size=0.2, target=None, spot=None):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = col
    if kind in ('AREA',): l.size = size
    else: l.shadow_soft_size = size
    if spot: l.spot_size = math.radians(spot); l.spot_blend = 0.5
    o = bpy.data.objects.new(name, l); scn.collection.objects.link(o); o.location = loc
    if target is not None: o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o
lights = []
def clear_lights():
    for o in lights: bpy.data.objects.remove(o, do_unlink=True)
    lights.clear()
def night():
    # 22:00：钨丝灯（白盒规格 §4：X 2,6,10,… / Z 4.5, 9, 13.5，吊高 Y 5.5，2700K）+ 很暗的晒图蓝环境
    clear_lights()
    bg.inputs[0].default_value = (0.012, 0.02, 0.035, 1); bg.inputs[1].default_value = 1.0
    for x in (2, 6, 10):
        for z in (4.5, 9.0, 13.5):
            lights.append(light(f"Lamp{x}_{z}", 'SPOT', U(x, 5.5, z), 260, (1.0, 0.72, 0.42), 0.15, U(x, 0, z), spot=110))
            lights.append(light(f"LampUp{x}_{z}", 'POINT', U(x, 5.6, z), 18, (1.0, 0.72, 0.42), 0.2))
def day():
    clear_lights()
    bg.inputs[0].default_value = (0.42, 0.47, 0.52, 1); bg.inputs[1].default_value = 0.8
    lights.append(light("Sun", 'SUN', (0, 0, 20), 3.0, (1.0, 0.93, 0.85), 0.05, target=(-3, 6, 0)))
def shoot(name, loc, target=None, lens=28, yaw=None, pitch=None):
    cam.location = loc; cam_d.lens = lens
    if yaw is not None:
        d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    else:
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True)
    print("[render]", name, flush=True)
if "D" in views:
    night(); shoot("CR01_机位D_N8_夜", U(7.5, 0.9, 6.5), lens=28, yaw=225, pitch=20)
if "D_close" in views:
    night(); shoot("CR01_机位D附近_吊钩搪瓷缸", U(7.1, 1.3, 5.6), target=U(6.0, 3.25, 4.0), lens=35)
if "overall" in views:
    day(); shoot("CR01_整体", U(14.5, 5.5, 14.0), target=U(6.0, 6.6, 7.0), lens=24)
if "hook" in views:
    day(); shoot("CR01_吊钩缸_特写", U(6.9, 3.4, 5.1), target=U(6.0, 3.3, 4.0), lens=50)
if "cab" in views:
    night(); shoot("CR01_司机室仰视", U(6.9, 3.2, 4.2), target=U(6.0, 5.25, 2.0), lens=35)
# ---- 机位 D 替代候选（协调者 10-15 要求：人站地上，眼高 0.9–1.6，离吊钩 2–5 m；夜景，同 D 光照）----
# 每个候选给 Unity 位置 + 瞄准点；渲染后打印 Yaw（0°=北，顺时针）/ 仰角，方便抄进看板
CANDS = {
    "cand1": ("CR01_机位D候选1_东北4m_眼高1.6", (8.8, 1.6, 7.2), (6.0, 4.9, 3.4), 28),
    "cand2": ("CR01_机位D候选2_北偏东3.6m_眼高1.6", (7.4, 1.6, 7.4), (6.0, 4.8, 3.2), 28),
    "cand3": ("CR01_机位D候选3_东北5m_眼高0.9", (9.0, 0.9, 8.0), (6.0, 4.7, 3.4), 28),
    "cand4": ("CR01_机位D候选4_东北5m_眼高1.6_40mm", (9.2, 1.6, 7.8), (6.0, 4.15, 3.6), 40),
    "cand5": ("CR01_机位D候选5_东北4.4m_眼高1.6_35mm", (8.6, 1.6, 7.6), (6.0, 4.3, 3.5), 35),
    "cand6": ("CR01_机位D候选6_东北3m_眼高1.6_28mm", (8.0, 1.6, 6.2), (6.0, 4.15, 3.4), 28),
}
for key, (name, p, t, lens) in CANDS.items():
    if key not in views: continue
    d = Vector(t) - Vector(p)
    yaw = math.degrees(math.atan2(d.x, d.z)) % 360; pitch = math.degrees(math.atan2(d.y, math.hypot(d.x, d.z)))
    night(); shoot(name, U(*p), lens=lens, yaw=yaw, pitch=pitch)
    hd = math.hypot(6.0 - p[0], 4.0 - p[2])
    print(f"[cand] {key} pos={p} yaw={yaw:.1f} pitch_up={pitch:.1f} lens={lens} 离吊钩水平距离={hd:.2f}m 看缸仰角={math.degrees(math.atan2(3.02 - p[1], hd)):.1f}", flush=True)

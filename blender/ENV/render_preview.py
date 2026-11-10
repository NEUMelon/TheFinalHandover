# ENV 预览渲染（由 AI 辅助工具编写）：python3 render_preview.py <Workshop.blend> <LatheMid.fbx> <LT01.fbx> <输出目录> [views=A,E,mid] [samples=32]
# 场景 = ENV 建筑壳 + LT01（Hero，摆 (8.0, 0, 11.0)）+ LT02–04（Mid，摆 x0 = 2 / 14 / 20）。其余道具、天车、灯罩不在本资产里，不渲。
# 光：16:00 太阳方位 235°、高度 18°（白盒规格 §1），钨丝灯 6 排 × 3 盏（只放点光源，灯罩不是本资产），机加工区一个低密度体积雾看光柱。
import bpy, sys, os, math
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, midfbx, lt01fbx, out = [os.path.abspath(a) for a in args[:4]]
views = args[4].split(",") if len(args) > 4 else ["A", "E", "mid"]
SAMPLES = int(args[5]) if len(args) > 5 else 32
HERE = os.path.dirname(os.path.abspath(__file__))
TEXDIRS = [os.path.join(HERE, "Textures"), os.path.join(HERE, "..", "Textures", "Atlas")]
os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)
scn = bpy.context.scene
def imp(path, loc):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    e = bpy.data.objects.new("Place_" + os.path.basename(path), None); scn.collection.objects.link(e); e.location = loc
    for o in new:
        if o.parent is None: o.parent = e
    return new
imp(lt01fbx, (8.0, 11.0, 0))
for x0 in (2.0, 14.0, 20.0): imp(midfbx, (x0, 11.0, 0))
def hook(m):
    """M_<X>_Atlas → <X>_Atlas_BaseColor / Normal / MS（与 Unity Stage3ArtMaterials 同规则）。"""
    if not (m.name.startswith("M_") and "_Atlas" in m.name): return
    asset = m.name[2:m.name.index("_Atlas")]
    d = next((d for d in TEXDIRS if os.path.exists(os.path.join(d, f"{asset}_Atlas_BaseColor.png"))), None)
    if d is None: print("no textures for", m.name); return
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out_ = nt.nodes.new("ShaderNodeOutputMaterial"); b = nt.nodes.new("ShaderNodeBsdfPrincipled"); nt.links.new(b.outputs[0], out_.inputs[0])
    def ld(suf, cs):
        im = bpy.data.images.load(os.path.join(d, f"{asset}_Atlas_{suf}.png"), check_existing=True); im.colorspace_settings.name = cs
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
    t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
    t = ld("MS", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"]); nt.links.new(sp.outputs[0], b.inputs["Metallic"])
    t = ld("Normal", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
for m in bpy.data.materials: hook(m)
for im in bpy.data.images:          # LT01 FBX 里贴花图路径拼错（LT01/ + 绝对路径），按文件名到 stage3/Decals 找
    if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
        alt = os.path.join(HERE, "..", "Decals", os.path.basename(im.filepath))
        if os.path.exists(alt): im.filepath = alt
scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = SAMPLES; scn.cycles.use_denoising = True
scn.cycles.max_bounces = 4; scn.cycles.volume_bounces = 0; scn.cycles.volume_step_rate = 4.0
scn.render.resolution_x, scn.render.resolution_y = 960, 540
scn.view_settings.view_transform = 'AgX'; scn.view_settings.exposure = -0.4
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.36, 0.42, 0.50, 1); bg.inputs[1].default_value = 0.6   # 降饱和晒图蓝的天
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 6.0; sun.angle = math.radians(1.0); sun.color = (1.0, 0.86, 0.68)
so = bpy.data.objects.new("Sun", sun); scn.collection.objects.link(so)
az, el = math.radians(55), math.radians(18)          # 光从 235° 射向 55°
d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), -math.sin(el)))
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
for x in (2, 6, 10, 14, 18, 22):
    for y in (4.5, 9.0, 13.5):
        l = bpy.data.lights.new("Lamp", 'POINT'); l.energy = 120; l.color = (1.0, 0.72, 0.42); l.shadow_soft_size = 0.15
        lo = bpy.data.objects.new("Lamp", l); scn.collection.objects.link(lo); lo.location = (x, y, 5.4)
if "vol" not in views:
    bpy.ops.mesh.primitive_cube_add(location=(12, 9, 5.2)); v = bpy.context.active_object; v.scale = (12, 9, 5.2); v.name = "DustVolume"
    vm = bpy.data.materials.new("Dust"); vm.use_nodes = True; nt = vm.node_tree; nt.nodes.remove(nt.nodes["Principled BSDF"])
    pv = nt.nodes.new("ShaderNodeVolumePrincipled"); pv.inputs["Density"].default_value = 0.015; pv.inputs["Anisotropy"].default_value = 0.5
    nt.links.new(pv.outputs[0], nt.nodes["Material Output"].inputs["Volume"]); v.data.materials.append(vm)
    v.visible_shadow = False
cam_d = bpy.data.cameras.new("Cam"); cam_d.sensor_fit = 'VERTICAL'; cam_d.sensor_height = 24.0; cam_d.clip_start = 0.1; cam_d.clip_end = 200
cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
def shoot(name, loc, yaw, pitch, lens):
    cam.location = loc; cam_d.lens = lens
    dd = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
    cam.rotation_euler = dd.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True); print("rendered", name, flush=True)
if "A" in views: shoot("ENV_v01_机位A_SH-A_1600", (14.5, 15.5, 1.6), 220, -3, 32)          # SH-A：Yaw 220°，俯 3°，32 mm
if "E" in views: shoot("ENV_v01_机位E_N9_1600", (27.0, 8.6, 1.6), 270, 0, 28)              # N9 / SH-C：Yaw 270°，28 mm（日景，仅看结构）
if "F" in views: shoot("ENV_v01_机位F_N7_线槽", (28.5, 5.2, 1.6), 180, 5, 28)              # N7 朝南（控制间线槽；规格 35 mm，这里放宽到 28 mm 看全）
if "mid" in views: shoot("ENV_v01_LT03_Mid_近看", (16.6, 8.2, 1.7), 320, -18, 35)

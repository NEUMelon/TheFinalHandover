# 杂项道具预览（由 Claude 编写）：python3 render_preview.py <ENV Workshop.blend> <Misc.fbx> <输出目录> [samples=32]
# 场景 = ENV 建筑壳 + 本批道具。衡 MES 屏面是灰盒（不在本 FBX 里），预览里补两块冷白自发光片示意灰盒屏面位置。
import bpy, sys, os, math
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
blend, fbx, out = [os.path.abspath(a) for a in args[:3]]; SAMPLES = int(args[3]) if len(args) > 3 else 32
ONLY = args[4].split(",") if len(args) > 4 else None
HERE = os.path.dirname(os.path.abspath(__file__)); TEXDIRS = [os.path.join(HERE, "Textures"), os.path.join(HERE, "..", "ENV", "Textures")]
os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend); scn = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=fbx)
h = bpy.data.objects.get("MainSwitch_Handle"); print("MainSwitch_Handle location", tuple(round(v, 3) for v in h.location) if h else None)
def hook(m):
    if not (m.name.startswith("M_") and "_Atlas" in m.name): return
    asset = m.name[2:m.name.index("_Atlas")]
    d = next((d for d in TEXDIRS if os.path.exists(os.path.join(d, f"{asset}_Atlas_BaseColor.png"))), None)
    if d is None: print("no textures", m.name); return
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); b = nt.nodes.new("ShaderNodeBsdfPrincipled"); nt.links.new(b.outputs[0], o.inputs[0])
    def ld(suf, cs):
        im = bpy.data.images.load(os.path.join(d, f"{asset}_Atlas_{suf}.png"), check_existing=True); im.colorspace_settings.name = cs
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
    t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
    t = ld("MS", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"]); nt.links.new(sp.outputs[0], b.inputs["Metallic"])
    t = ld("Normal", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
for m in bpy.data.materials: hook(m)
sm = bpy.data.materials.new("GrayScreen"); sm.use_nodes = True; e = sm.node_tree.nodes["Principled BSDF"]
e.inputs["Emission Color"].default_value = (0.79, 0.87, 0.91, 1); e.inputs["Emission Strength"].default_value = 2.0; e.inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)
for cx in (28.1, 28.9):
    bpy.ops.mesh.primitive_cube_add(location=(cx, 4.0, 1.10)); s = bpy.context.active_object; s.scale = (0.3, 0.015, 0.19); s.data.materials.append(sm)
scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = SAMPLES; scn.cycles.use_denoising = True; scn.cycles.max_bounces = 4
scn.render.resolution_x, scn.render.resolution_y = 960, 540
scn.view_settings.view_transform = 'AgX'; scn.view_settings.exposure = -0.2
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.42, 0.50, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.6
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 6.0; sun.angle = math.radians(1.0); sun.color = (1.0, 0.86, 0.68)
so = bpy.data.objects.new("Sun", sun); scn.collection.objects.link(so)
d = Vector((math.sin(math.radians(55)) * math.cos(math.radians(18)), math.cos(math.radians(55)) * math.cos(math.radians(18)), -math.sin(math.radians(18))))
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
for p, en in ([((x, y, 5.4), 120) for x in (2, 6, 10, 14, 18, 22) for y in (4.5, 9.0, 13.5)] + [((27, 4, 2.9), 80), ((27, 13, 4.5), 80), ((33, 14, 3.4), 80), ((33, 5, 3.4), 80)]):
    l = bpy.data.lights.new("Lamp", 'POINT'); l.energy = en; l.color = (1.0, 0.72, 0.42); l.shadow_soft_size = 0.15
    lo = bpy.data.objects.new("Lamp", l); scn.collection.objects.link(lo); lo.location = p
cd = bpy.data.cameras.new("Cam"); cd.sensor_fit = 'VERTICAL'; cd.sensor_height = 24.0; cd.clip_start = 0.05
cam = bpy.data.objects.new("Cam", cd); scn.collection.objects.link(cam); scn.camera = cam
def shoot(name, loc, yaw, pitch, lens):
    if ONLY and not any(k in name for k in ONLY): return
    cam.location = loc; cd.lens = lens
    dd = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
    cam.rotation_euler = dd.to_track_quat('-Z', 'Y').to_euler()
    scn.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True); print("rendered", name, flush=True)
shoot("Misc_v01_标语牌与工件架", (5.5, 10.0, 1.6), 300, 2, 28)
shoot("Misc_v01_立钻与砂轮机", (17.6, 6.2, 1.7), 190, -12, 28)
shoot("Misc_v01_广播喇叭", (13.2, 3.2, 3.6), 210, 22, 45)
shoot("Misc_v01_控制间_MES桌与总闸", (27.0, 6.6, 1.65), 140, -8, 28)
shoot("Misc_v01_休息区", (35.6, 7.2, 1.6), 222, -18, 28)
shoot("Misc_v01_交班区椅子", (29.2, 14.6, 1.5), 250, -22, 30)
shoot("Misc_v01_更衣凳", (31.2, 12.0, 1.6), 40, -20, 28)

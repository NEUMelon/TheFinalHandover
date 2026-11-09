# 继电器柜预览：python3 render_cab.py <blend> <outdir> [res%]
import bpy, sys, math, os
from mathutils import Vector
blend, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
pct = int(sys.argv[3]) if len(sys.argv) > 3 else 100
bpy.ops.wm.open_mainfile(filepath=blend)
scn = bpy.context.scene
scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = 48; scn.cycles.use_denoising = True
scn.render.resolution_x, scn.render.resolution_y = 1920, 1080; scn.render.resolution_percentage = pct
scn.view_settings.view_transform = 'AgX'
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.47, 0.52, 1); bg.inputs[1].default_value = 0.5
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 1.5, 0)); g = bpy.context.active_object
gm = bpy.data.materials.new("Floor"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.29, 0.27, 1); g.data.materials.append(gm)
key = bpy.data.lights.new("Key", 'AREA'); key.energy = 400; key.size = 2
ko = bpy.data.objects.new("Key", key); scn.collection.objects.link(ko); ko.location = (-1.5, 3.0, 2.8)
ko.rotation_euler = (Vector((0, 0.3, 1.1)) - ko.location).to_track_quat('-Z', 'Y').to_euler()
cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
def shoot(name, loc, target, lens=35, ortho=None):
    cam.location = loc; cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if ortho: cam_d.type = 'ORTHO'; cam_d.ortho_scale = ortho
    else: cam_d.type = 'PERSP'; cam_d.lens = lens
    scn.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True)
dl, dr = bpy.data.objects["Door_L"], bpy.data.objects["Door_R"]
views = sys.argv[4].split(",") if len(sys.argv) > 4 else ["closed", "open", "front_open", "interlock"]
if "closed" in views: shoot("CAB_v02_门关_透视", (1.6, 3.2, 1.5), (0, 0.3, 1.05), 35)
dl.rotation_euler.z = math.radians(-105); dr.rotation_euler.z = math.radians(105)
if "open" in views: shoot("CAB_v02_门开_透视", (1.2, 3.0, 1.45), (0, 0.2, 1.05), 32)
if "front_open" in views: shoot("CAB_v02_门开_正视", (0, 6, 1.05), (0, 0, 1.05), ortho=3.9)
if "interlock" in views:
    # 20:00 手电：关环境光，只留一束聚光（ENV-02 机位意图）
    bg.inputs[1].default_value = 0.03; key.energy = 0
    sp = bpy.data.lights.new("Torch", 'SPOT'); sp.energy = 25; sp.spot_size = math.radians(28); sp.spot_blend = 0.6; sp.color = (1.0, 0.9, 0.75)
    so = bpy.data.objects.new("Torch", sp); scn.collection.objects.link(so); so.location = (0.35, 0.75, 1.45)
    t = bpy.data.objects["Relay_R2C3"].matrix_world.translation + Vector((0, 0.06, 0.06))
    so.rotation_euler = (t - so.location).to_track_quat('-Z', 'Y').to_euler()
    shoot("CAB_v02_特写_护罩联锁继电器_手电", (0.28, 0.62, 1.62), t, 45)

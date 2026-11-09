# 预览渲染：python3 render_lt01.py <blend> <outdir> [res%]
import bpy, sys, math, os
from mathutils import Vector
blend, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
pct = int(sys.argv[3]) if len(sys.argv) > 3 else 100
bpy.ops.wm.open_mainfile(filepath=blend)
scn = bpy.context.scene
scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = 48
scn.cycles.use_denoising = True
scn.render.resolution_x, scn.render.resolution_y = 1920, 1080
scn.render.resolution_percentage = pct
scn.view_settings.view_transform = 'AgX'
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.47, 0.52, 1); bg.inputs[1].default_value = 0.6
# 地面
bpy.ops.mesh.primitive_plane_add(size=12, location=(1.4, 0, 0)); g = bpy.context.active_object
gm = bpy.data.materials.new("Floor"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.31, 0.28, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
g.data.materials.append(gm)
# 16:00 太阳：从西南射向东北，高度 18°（Blender：光沿 -Z 局部方向）
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 4.0; sun.angle = math.radians(1.5); sun.color = (1.0, 0.93, 0.82)
so = bpy.data.objects.new("Sun", sun); scn.collection.objects.link(so)
d = Vector((math.cos(math.radians(18)) * math.sin(math.radians(45)), math.cos(math.radians(18)) * math.sin(math.radians(45)), -math.sin(math.radians(18))))
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
fill = bpy.data.lights.new("Fill", 'AREA'); fill.energy = 300; fill.size = 4
fo = bpy.data.objects.new("Fill", fill); scn.collection.objects.link(fo); fo.location = (1.4, -4, 3)
fo.rotation_euler = (Vector((1.4, 0, 0.9)) - fo.location).to_track_quat('-Z', 'Y').to_euler()
cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
def shoot(name, loc, target, ortho=None, lens=35):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if ortho: cam_d.type = 'ORTHO'; cam_d.ortho_scale = ortho
    else: cam_d.type = 'PERSP'; cam_d.lens = lens
    scn.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True)
views = sys.argv[4].split(",") if len(sys.argv) > 4 else ["persp", "front", "top", "side", "dent"]
if "persp" in views: shoot("LT01_v02_透视_操作侧", (3.6, -3.3, 1.9), (1.3, 0, 0.95), lens=35)
if "front" in views: shoot("LT01_v02_正视", (1.4, -8, 0.7), (1.4, 0, 0.7), ortho=3.3)
if "top" in views: shoot("LT01_v02_俯视", (1.4, 0, 8), (1.4, 0.0001, 0), ortho=3.3)
if "side" in views: shoot("LT01_v02_左视", (-8, 0, 0.8), (0, 0, 0.8), ortho=3.3)
if "dent" in views: shoot("LT01_v02_特写_护罩凹痕", (1.25, -0.75, 1.55), (1.04, -0.18, 1.27), lens=50)

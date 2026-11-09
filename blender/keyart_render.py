# Key art renders for the GDD v2. Usage: python3 keyart.py <out_dir> <shots,comma> <pct> <spp>
import bpy, sys, os, math, glob
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
OUT = os.path.abspath(args[0]); SHOTS = args[1].split(","); PCT = int(args[2]); SPP = int(args[3])
S = "/mnt/project-files/最后一次交班/20_场景地编/stage3"
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=f"{S}/ENV/Workshop/LSB_ENV_BLD_Workshop_v01.blend")
scn = bpy.context.scene
TEXDIRS = [d for d in glob.glob(f"{S}/**/", recursive=True) if glob.glob(d + "*_Atlas_BaseColor.png")]

def B(ux, uy, uz):  # unity -> blender
    return Vector((ux, uz, uy))

def imp(path, loc, yaw=0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    e = bpy.data.objects.new("Place_" + os.path.basename(path), None); scn.collection.objects.link(e)
    e.location = loc; e.rotation_euler = (0, 0, math.radians(yaw))
    for o in new:
        if o.parent is None: o.parent = e
    return new

def append_blend(path, loc):
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = list(src.objects)
    e = bpy.data.objects.new("Place_" + os.path.basename(path), None); scn.collection.objects.link(e); e.location = loc
    out = []
    for o in dst.objects:
        if o is None or o.type in ('CAMERA', 'LIGHT'): continue
        scn.collection.objects.link(o); out.append(o)
        if o.parent is None: o.parent = e
    return out

imp(f"{S}/LT01/LSB_ENV_MCH_Lathe_LT01_v04.fbx", B(8.0, 0, 11.0))
for x0 in (2.0, 14.0, 20.0): imp(f"{S}/ENV/LatheMid/LSB_ENV_MCH_Lathe_Mid_v01.fbx", B(x0, 0, 11.0))
crane = append_blend(f"{S}/Crane/LSB_ENV_CRN_Crane_CR01_v01.blend", Vector((6.0, 0, 0)))
LAMPS = {}
for x in (2, 6, 10, 14, 18, 22):
    for z in (4.5, 9.0, 13.5):
        LAMPS[(x, z)] = imp(f"{S}/Props/PendantLamp/LSB_ENV_LGT_PendantLamp_v01.fbx", B(x, 5.5, z))
imp(f"{S}/Props/ShiftDesk/LSB_ENV_PRP_ShiftDesk_v01.fbx", B(26.5, 0, 13.0))
imp(f"{S}/Props/ShiftLedger/LSB_ENV_PRP_ShiftLedger_v01.fbx", B(26.5, 0.772, 13.0))
imp(f"{S}/Props/ListeningRod/LSB_ENV_PRP_ListeningRod_v01.fbx", B(27.17, 0.2635, 12.24))
imp(f"{S}/Props/Toolbox/LSB_ENV_PRP_Toolbox_v01.fbx", B(27.3, 0, 12.3))
imp(f"{S}/Props_Misc/LSB_ENV_PRP_Misc_v01.fbx", B(0, 0, 0))
CAB = imp(f"{S}/RelayCabinet/LSB_ENV_CTL_RelayCabinet_Old_v04.fbx", B(26.1, 0, 0.3), yaw=float(os.environ.get("CABYAW", "0")))

def hook(m):
    if not (m.name.startswith("M_") and "_Atlas" in m.name): return
    asset = m.name[2:m.name.index("_Atlas")]
    d = next((d for d in TEXDIRS if os.path.exists(os.path.join(d, f"{asset}_Atlas_BaseColor.png"))), None)
    if d is None: print("no textures for", m.name); return
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial"); b = nt.nodes.new("ShaderNodeBsdfPrincipled"); nt.links.new(b.outputs[0], o_.inputs[0])
    def ld(suf, cs):
        im = bpy.data.images.load(os.path.join(d, f"{asset}_Atlas_{suf}.png"), check_existing=True); im.colorspace_settings.name = cs
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
    t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
    t = ld("MS", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(t.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"]); nt.links.new(sp.outputs[0], b.inputs["Metallic"])
    t = ld("Normal", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
for m in list(bpy.data.materials): hook(m)
for im in bpy.data.images:
    if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
        alt = os.path.join(S, "Decals", os.path.basename(im.filepath))
        if os.path.exists(alt): im.filepath = alt

def emis(name, col, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial"); e = nt.nodes.new("ShaderNodeEmission")
    e.inputs[0].default_value = (*col, 1); e.inputs[1].default_value = strength; nt.links.new(e.outputs[0], o_.inputs[0]); return m
GLOW_ON = emis("Glow_On", (1.0, 0.80, 0.55), 7.0)
GLOW_OFF = bpy.data.materials.new("Glow_Off"); GLOW_OFF.use_nodes = True
GLOW_OFF.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.78, 0.76, 0.70, 1)

scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = SPP; scn.cycles.use_denoising = True
scn.cycles.max_bounces = 5; scn.cycles.volume_bounces = 0; scn.cycles.volume_step_rate = 3.0
scn.render.resolution_x, scn.render.resolution_y = 2400, 1350; scn.render.resolution_percentage = PCT
scn.view_settings.view_transform = 'AgX'; scn.view_settings.look = 'AgX - Medium High Contrast'
scn.render.image_settings.file_format = 'PNG'
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True; BG = w.node_tree.nodes["Background"]

LIGHTS = []
def light(kind, loc, energy, col, size=0.15, target=None, spot=None):
    l = bpy.data.lights.new("L", kind); l.energy = energy; l.color = col
    if kind == 'AREA': l.size = size
    else: l.shadow_soft_size = size
    if spot: l.spot_size = math.radians(spot); l.spot_blend = 0.6
    o = bpy.data.objects.new("L", l); scn.collection.objects.link(o); o.location = loc
    if target is not None: o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    LIGHTS.append(o); return o
def clear():
    for o in LIGHTS: bpy.data.objects.remove(o, do_unlink=True)
    LIGHTS.clear()

def set_lamp(key, on):
    for o in LAMPS[key]:
        if o.type == 'MESH' and "Glow" in o.name:
            o.data.materials.clear(); o.data.materials.append(GLOW_ON if on else GLOW_OFF)
    if on:
        x, z = key
        light('SPOT', B(x, 5.25, z), 650, (1.0, 0.68, 0.38), 0.10, target=B(x, 0, z), spot=80)
        light('POINT', B(x, 5.45, z), 6, (1.0, 0.72, 0.45), 0.2)

vol = None
def dust(density):
    global vol
    if vol is None:
        bpy.ops.mesh.primitive_cube_add(location=(12, 9, 5.2)); vol = bpy.context.active_object; vol.scale = (12, 9, 5.2)
        vm = bpy.data.materials.new("Dust"); vm.use_nodes = True; nt = vm.node_tree; nt.nodes.remove(nt.nodes["Principled BSDF"])
        pv = nt.nodes.new("ShaderNodeVolumePrincipled"); pv.name = "PV"; pv.inputs["Anisotropy"].default_value = 0.55
        nt.links.new(pv.outputs[0], nt.nodes["Material Output"].inputs["Volume"]); vol.data.materials.append(vm); vol.visible_shadow = False
    vol.data.materials[0].node_tree.nodes["PV"].inputs["Density"].default_value = density

def sun(energy=7.0):
    s = bpy.data.lights.new("Sun", 'SUN'); s.energy = energy; s.angle = math.radians(0.8); s.color = (1.0, 0.80, 0.58)
    o = bpy.data.objects.new("Sun", s); scn.collection.objects.link(o)
    az, el = math.radians(55), math.radians(16)
    d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), -math.sin(el)))
    o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); LIGHTS.append(o)

def setup(mode, lit=None):
    clear()
    if mode == "day":
        BG.inputs[0].default_value = (0.42, 0.46, 0.52, 1); BG.inputs[1].default_value = 0.5; sun(); dust(0.018)
        for k in LAMPS: set_lamp(k, False)
        scn.view_settings.exposure = -0.55
    else:
        BG.inputs[0].default_value = (0.010, 0.018, 0.035, 1); BG.inputs[1].default_value = 1.0; dust(0.022)
        for k in LAMPS: set_lamp(k, lit is None or k in lit)
        scn.view_settings.exposure = -0.2

cam_d = bpy.data.cameras.new("Cam"); cam_d.sensor_fit = 'VERTICAL'; cam_d.sensor_height = 24.0; cam_d.clip_start = 0.05; cam_d.clip_end = 200
cam = bpy.data.objects.new("Cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
def yaw_t(loc, yaw, pitch):
    d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
    return Vector(loc) + d * 10
def shoot(name, loc, target, lens, dof=None, ev=0.0):
    base = scn.view_settings.exposure; scn.view_settings.exposure = base + ev
    cam.location = loc; cam_d.lens = lens
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    cam_d.dof.use_dof = dof is not None
    if dof: cam_d.dof.focus_distance = (Vector(target) - Vector(loc)).length; cam_d.dof.aperture_fstop = dof
    scn.render.filepath = os.path.join(OUT, name); bpy.ops.render.render(write_still=True); print("[render]", name, flush=True); scn.view_settings.exposure = base

for sh in SHOTS:
    if sh == "key":
        setup("night"); shoot("KA_key_night", B(3.6, 1.35, 8.0), B(11.0, 1.5, 11.8), 26)
    elif sh == "day":
        setup("day"); L = B(14.5, 1.6, 15.5); shoot("KA_1600_day", L, yaw_t(L, 220, -3), 32)
    elif sh == "out":
        setup("night", lit={(2, 4.5), (2, 9.0), (2, 13.5)})
        light('AREA', B(28.4, 1.3, 6.0), 60, (0.85, 0.93, 1.0), 0.8, target=B(20, 1.0, 9))
        shoot("KA_lightsout", B(23.2, 1.6, 8.0), B(2.0, 1.5, 9.5), 28, ev=1.0)
    elif sh == "hook":
        setup("night"); shoot("KA_hook", B(7.2, 1.0, 5.9), B(6.0, 3.9, 4.0), 30, ev=0.3)
    elif sh == "lathe":
        setup("night"); shoot("KA_lathe", B(5.2, 1.55, 8.4), B(8.2, 0.95, 11.2), 30)
    elif sh == "desk":
        setup("night")
        light('POINT', B(26.3, 1.25, 13.15), 35, (1.0, 0.75, 0.45), 0.08)
        light('AREA', B(28.0, 1.2, 11.0), 25, (0.85, 0.93, 1.0), 0.6, target=B(26.5, 0.8, 13.0))
        shoot("KA_desk", B(25.6, 1.5, 11.7), B(26.6, 0.78, 12.9), 35, dof=2.8)
    elif sh == "cab":
        setup("night")
        light('AREA', B(28.0, 1.5, 3.0), 50, (0.85, 0.93, 1.0), 0.8, target=B(26.1, 1.2, 0.6))
        shoot("KA_cab", B(25.2, 1.55, 2.9), B(26.1, 1.35, 0.4), 30, ev=0.4)
    elif sh == "ledger":
        setup("night", lit=set())
        light('POINT', B(26.15, 1.30, 13.25), 28, (1.0, 0.72, 0.42), 0.06)
        light('AREA', B(28.2, 1.4, 11.5), 10, (0.80, 0.90, 1.0), 0.5, target=B(26.5, 0.8, 13.0))
        shoot("KA_ledger", B(26.05, 1.32, 12.45), B(26.55, 0.78, 13.0), 50, dof=2.0, ev=0.3)
    elif sh == "rod":
        setup("night", lit={(22, 13.5)})
        light('POINT', B(26.6, 0.9, 12.0), 14, (1.0, 0.75, 0.45), 0.05)
        light('AREA', B(28.0, 0.8, 10.8), 12, (0.80, 0.90, 1.0), 0.4, target=B(27.17, 0.4, 12.24))
        shoot("KA_rod", B(26.55, 0.62, 11.55), B(27.15, 0.45, 12.24), 50, dof=2.0, ev=0.3)
    elif sh == "low":
        setup("night"); shoot("KA_lowlathe", B(6.2, 0.30, 8.6), B(9.0, 1.6, 11.6), 20, ev=0.2)
    elif sh == "aisle":
        setup("night"); shoot("KA_aisle", B(0.8, 3.2, 0.8), B(14.0, 0.8, 12.0), 20, ev=0.2)
    elif sh == "lamp":
        setup("night", lit={(6, 9.0), (10, 9.0), (14, 9.0), (18, 9.0)})
        shoot("KA_lamp", B(4.6, 3.4, 10.4), B(6.0, 5.35, 9.0), 40, dof=2.8, ev=-0.3)
    elif sh == "ledger2":
        setup("night", lit=set())
        light('POINT', B(26.25, 1.30, 13.35), 28, (1.0, 0.72, 0.42), 0.06)
        light('AREA', B(28.4, 1.4, 12.0), 10, (0.80, 0.90, 1.0), 0.5, target=B(26.5, 0.8, 13.0))
        shoot("KA_ledger2", B(27.05, 1.30, 13.50), B(26.48, 0.78, 12.98), 50, dof=2.0, ev=0.3)
    elif sh == "rod2":
        setup("night", lit={(22, 13.5)})
        light('POINT', B(26.75, 0.95, 12.0), 12, (1.0, 0.75, 0.45), 0.05)
        light('AREA', B(28.2, 0.9, 11.2), 14, (0.80, 0.90, 1.0), 0.4, target=B(27.2, 0.3, 12.3))
        shoot("KA_rod2", B(26.65, 0.62, 11.70), B(27.22, 0.27, 12.27), 50, dof=2.2, ev=0.3)

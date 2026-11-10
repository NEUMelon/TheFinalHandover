# 网页展厅用 GLB 导出。
#   python export_web.py asset <key> <out_dir>
#   python export_web.py scene <out_dir>
import bpy, sys, os, math, glob
from mathutils import Vector

# Stage 3 美术源目录（各资产的 FBX、贴图和厂房 .blend），用环境变量 LSB_STAGE3 指定
S = os.environ.get("LSB_STAGE3", "stage3")
TEX = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tex")
args = sys.argv[1:]
MODE = args[0]

ASSETS = {
    "lt01": f"{S}/LT01/LSB_ENV_MCH_Lathe_LT01_v04.fbx",
    "lathemid": f"{S}/ENV/LatheMid/LSB_ENV_MCH_Lathe_Mid_v01.fbx",
    "cabinet": f"{S}/RelayCabinet/LSB_ENV_CTL_RelayCabinet_Old_v04.fbx",
    "crane": f"{S}/Crane/LSB_ENV_CRN_Crane_CR01_v01.fbx",
    "lockers": f"{S}/Props/Lockers/LSB_ENV_PRP_Lockers_v01.fbx",
    "desk": f"{S}/Props/ShiftDesk/LSB_ENV_PRP_ShiftDesk_v01.fbx",
    "ledger": f"{S}/Props/ShiftLedger/LSB_ENV_PRP_ShiftLedger_v01.fbx",
    "tape": f"{S}/Props/TapeRecorder/LSB_ENV_PRP_TapeRecorder_v01.fbx",
    "toolbox": f"{S}/Props/Toolbox/LSB_ENV_PRP_Toolbox_v01.fbx",
    "rod": f"{S}/Props/ListeningRod/LSB_ENV_PRP_ListeningRod_v01.fbx",
    "lamp": f"{S}/Props/PendantLamp/LSB_ENV_LGT_PendantLamp_v01.fbx",
}


def B(ux, uy, uz):  # unity -> blender
    return Vector((ux, uz, uy))


def imp(path, loc=Vector((0, 0, 0)), yaw=0):
    scn = bpy.context.scene
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    e = bpy.data.objects.new("Place_" + os.path.basename(path).split("_v0")[0], None)
    scn.collection.objects.link(e)
    e.location = loc; e.rotation_euler = (0, 0, math.radians(yaw))
    for o in new:
        if o.parent is None: o.parent = e
    for o in new:
        if o.type in ('CAMERA', 'LIGHT'): bpy.data.objects.remove(o, do_unlink=True)
    return new


def hook(m):
    if not (m.name.startswith("M_") and "_Atlas" in m.name): return
    asset = m.name[2:m.name.index("_Atlas")]
    bc = os.path.join(TEX, f"{asset}_Atlas_BaseColor.png")
    if not os.path.exists(bc): print("no textures for", m.name); return
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial"); b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], o_.inputs[0])
    def ld(suf, cs):
        p = os.path.join(TEX, f"{asset}_Atlas_{suf}.png")
        if not os.path.exists(p): return None
        im = bpy.data.images.load(p, check_existing=True); im.colorspace_settings.name = cs
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
    t = ld("BaseColor", "sRGB"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
    t = ld("ORM", "Non-Color")
    if t:
        sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
        nt.links.new(sp.outputs[1], b.inputs["Roughness"]); nt.links.new(sp.outputs[2], b.inputs["Metallic"])
    else:
        b.inputs["Roughness"].default_value = 0.7; b.inputs["Metallic"].default_value = 0.0
    t = ld("Normal", "Non-Color")
    if t:
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])


def finish():
    for m in list(bpy.data.materials): hook(m)
    for im in list(bpy.data.images):
        if im.filepath and not os.path.exists(bpy.path.abspath(im.filepath)):
            for alt in glob.glob(f"{S}/**/" + os.path.basename(im.filepath), recursive=True):
                im.filepath = alt; break
        try:
            w, h = im.size
            if max(w, h) > 1024 and not os.path.dirname(bpy.path.abspath(im.filepath)) == TEX:
                r = 1024 / max(w, h); im.scale(max(1, int(w * r)), max(1, int(h * r)))
        except Exception as ex:
            print("img", im.name, ex)


def export(path):
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', export_image_format='JPEG',
                              export_jpeg_quality=82, export_apply=True, export_yup=True,
                              export_cameras=False, export_lights=False, export_extras=False)
    print("[glb]", path, os.path.getsize(path))


if MODE == "asset":
    key, out = args[1], args[2]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    imp(ASSETS[key])
    finish()
    export(os.path.join(out, key + ".glb"))
else:
    out = args[1]
    bpy.ops.wm.open_mainfile(filepath=f"{S}/ENV/Workshop/LSB_ENV_BLD_Workshop_v01.blend")
    scn = bpy.context.scene
    for o in list(bpy.data.objects):
        if o.type in ('CAMERA', 'LIGHT'): bpy.data.objects.remove(o, do_unlink=True)
    imp(ASSETS["lt01"], B(8.0, 0, 11.0))
    for x0 in (2.0, 14.0, 20.0): imp(ASSETS["lathemid"], B(x0, 0, 11.0))
    imp(ASSETS["crane"], Vector((6.0, 0, 0)))
    for i, x in enumerate((2, 6, 10, 14, 18, 22)):
        for j, z in enumerate((4.5, 9.0, 13.5)):
            new = imp(ASSETS["lamp"], B(x, 5.5, z))
            for o in new:
                if o.type == 'MESH': o.name = f"Lamp_r{i}_c{j}_" + ("Glow" if "Glow" in o.name else "Body")
    imp(f"{S}/Props/ShiftDesk/LSB_ENV_PRP_ShiftDesk_v01.fbx", B(26.5, 0, 13.0))
    imp(f"{S}/Props/ShiftLedger/LSB_ENV_PRP_ShiftLedger_v01.fbx", B(26.5, 0.772, 13.0))
    imp(f"{S}/Props/ListeningRod/LSB_ENV_PRP_ListeningRod_v01.fbx", B(27.17, 0.2635, 12.24))
    imp(f"{S}/Props/Toolbox/LSB_ENV_PRP_Toolbox_v01.fbx", B(27.3, 0, 12.3))
    imp(f"{S}/Props_Misc/LSB_ENV_PRP_Misc_v01.fbx", B(0, 0, 0))
    imp(ASSETS["cabinet"], B(26.1, 0, 0.3))
    imp(ASSETS["lockers"], B(32.9, 0.0, 17.6))
    imp(ASSETS["tape"], B(26.55, 0.62, 13.55))
    finish()
    export(os.path.join(out, "scene.glb"))

# 把 Textures/ 里的可平铺贴图接到 Blender 材质上（LT01 / 继电器柜构建脚本共用）。由 Claude 编写。
# 贴图 1 张 = 1 m，模型 UV 用 1 m 立方体投影（world_uv），所以各部件纹理密度一致（1024 px/m）。
import bpy, os, math

TEXDIR = os.path.dirname(os.path.abspath(__file__))

def hook(material, tex_name):
    """material 用 T_<tex_name>_BaseColor / _ORM / _Normal 三张图；ORM 的 G=粗糙度，B=金属度。"""
    nt = material.node_tree; n = nt.nodes; l = nt.links
    bsdf = n["Principled BSDF"]
    def img(suffix, colorspace):
        p = os.path.join(TEXDIR, f"T_{tex_name}_{suffix}.png")
        im = bpy.data.images.load(p, check_existing=True); im.colorspace_settings.name = colorspace
        t = n.new("ShaderNodeTexImage"); t.image = im; return t
    bc = img("BaseColor", "sRGB"); l.new(bc.outputs["Color"], bsdf.inputs["Base Color"])
    orm = img("ORM", "Non-Color"); sep = n.new("ShaderNodeSeparateColor"); l.new(orm.outputs["Color"], sep.inputs["Color"])
    l.new(sep.outputs["Green"], bsdf.inputs["Roughness"]); l.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
    nm = img("Normal", "Non-Color"); nmap = n.new("ShaderNodeNormalMap"); l.new(nm.outputs["Color"], nmap.inputs["Color"])
    l.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    material["lsb_texture_set"] = tex_name      # Unity 端按这个名字建材质

def world_uv(objs, textured_materials):
    """用到平铺贴图的物体改成 1 m 立方体投影 UV；其余保留 Smart UV。"""
    for o in objs:
        if o.type != 'MESH' or not any(m in textured_materials for m in o.data.materials): continue
        bpy.context.view_layer.objects.active = o; o.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.cube_project(cube_size=1.0, scale_to_bounds=False, correct_aspect=True)
        bpy.ops.object.mode_set(mode='OBJECT'); o.select_set(False)

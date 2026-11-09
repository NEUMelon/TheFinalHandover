# 《最后一次交班》Stage 3 · 按磨损动线合成年代叠层贴图集（由 Claude 编写，已登记 AI 使用日志）
# 场景主产规格 §2.4：“禁止均匀做旧和无动线依据的划痕”。所以漆面剥落量由每个点的磨损度 W 决定：
#   W = 基础磨损 + 棱边（网格曲率）+ 手常碰的位置（按距离衰减）+ 离地 15 cm 内的踢碰区
#   剥落面积：外层 = a1·W，露铁 = a2·W（遮罩 R 通道是剥落序位，值 > 1−面积 即剥落）
#   手常碰处另做“包浆”：粗糙度降低，露铁处变成磨亮金属。
# 做法（CPU 上快）：Blender 只用“自发光”烘 3 张数据图（世界位置、世界法线、材质 ID + 棱边度），
# 其余全部在 numpy 里按每个像素算：三向投影采样平铺贴图 → 叠层 → 粗糙度 / 金属度 → 剥落台阶法线。
# 输出：<资产>_Atlas_BaseColor / _Normal（OpenGL）/ _ORM（R=AO, G=粗糙度, B=金属度）/ _MS（URP：R=金属度, A=光滑度）。
import bpy, bmesh, os, math
import numpy as np
from PIL import Image

TEXDIR = os.path.dirname(os.path.abspath(__file__))
def _lin(hx): return np.array([(int(hx[i:i+2], 16) / 255) ** 2.2 for i in (1, 3, 5)], np.float32)
def _load(name):
    return np.asarray(Image.open(os.path.join(TEXDIR, name)).convert("RGB"), np.float32)[::-1] / 255.0   # 行 0 = V 0

class Painted:
    def __init__(s, mask, top, mid, touch, a1=0.3, a2=0.1, base_w=0.08, edge_k=0.6, floor_k=0.6):
        s.mask, s.top, s.mid, s.touch, s.a1, s.a2, s.base_w, s.edge_k, s.floor_k = mask, top, mid, touch, a1, a2, base_w, edge_k, floor_k
class Tiled:
    def __init__(s, tex): s.tex = tex

def _edge_attribute(objs):
    """每个顶点的棱边度：与相邻面法线的最大夹角（倒角处高，平面上 0），存成颜色属性 LSB_Edge。"""
    for o in objs:
        bm = bmesh.new(); bm.from_mesh(o.data)
        vals = []
        for v in bm.verts:
            vals.append(max((1.0 - f.normal.dot(v.normal) for f in v.link_faces), default=0.0))
        bm.free()
        attr = o.data.color_attributes.get("LSB_Edge") or o.data.color_attributes.new("LSB_Edge", 'FLOAT_COLOR', 'POINT')
        for i, e in enumerate(vals): attr.data[i].color = (min(e * 6.0, 1.0), 0, 0, 1)

def _emit_setup(mat, mode, mid):
    """把材质临时换成自发光数据：pos / nrm / id（R = 材质序号/16，G = 棱边度）。返回恢复用的原 Surface 输入。"""
    nt = mat.node_tree; out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    prev = out.inputs[0].links[0].from_socket if out.inputs[0].is_linked else None
    em = nt.nodes.get("LSB_Emit") or nt.nodes.new("ShaderNodeEmission"); em.name = "LSB_Emit"
    for l in list(em.inputs[0].links): nt.links.remove(l)
    geo = nt.nodes.get("LSB_Geo") or nt.nodes.new("ShaderNodeNewGeometry"); geo.name = "LSB_Geo"
    if mode in ("pos", "nrm"):                     # 自发光不保证保留负值：位置 +10 m，法线 ×0.5+0.5
        vm = nt.nodes.get("LSB_Off") or nt.nodes.new("ShaderNodeVectorMath"); vm.name = "LSB_Off"; vm.operation = 'MULTIPLY_ADD'
        nt.links.new(geo.outputs["Position" if mode == "pos" else "Normal"], vm.inputs[0])
        vm.inputs[1].default_value = (1, 1, 1) if mode == "pos" else (0.5, 0.5, 0.5)
        vm.inputs[2].default_value = (10, 10, 10) if mode == "pos" else (0.5, 0.5, 0.5)
        nt.links.new(vm.outputs[0], em.inputs[0])
    else:
        at = nt.nodes.get("LSB_EdgeAttr") or nt.nodes.new("ShaderNodeAttribute"); at.name = "LSB_EdgeAttr"; at.attribute_name = "LSB_Edge"
        sp = nt.nodes.get("LSB_Sep") or nt.nodes.new("ShaderNodeSeparateColor"); sp.name = "LSB_Sep"
        cc = nt.nodes.get("LSB_Comb") or nt.nodes.new("ShaderNodeCombineColor"); cc.name = "LSB_Comb"
        nt.links.new(at.outputs["Color"], sp.inputs[0]); nt.links.new(sp.outputs[0], cc.inputs[1])
        cc.inputs[0].default_value = (mid + 0.5) / 16.0; cc.inputs[2].default_value = 1.0     # B=1：有效像素
        nt.links.new(cc.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    return prev

def bake_atlas(objs, asset, out_dir, recipes, res=4096, margin=16):
    """recipes: {材质名: Painted(...) 或 Tiled(贴图名)}。用到这些材质的物体合成一张贴图集，换成 M_<资产>_Atlas。"""
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'; scn.cycles.device = 'CPU'; scn.cycles.samples = 1; scn.cycles.use_denoising = False
    mats = [bpy.data.materials[n] for n in recipes]
    targets = [o for o in objs if o.type == 'MESH' and any(m in mats for m in o.data.materials)]
    # 1) BakeAtlas UV：多物体 Smart UV，同一尺度排布
    bpy.ops.object.select_all(action='DESELECT')
    for o in targets:
        uvl = o.data.uv_layers.get("BakeAtlas") or o.data.uv_layers.new(name="BakeAtlas")
        o.data.uv_layers.active = uvl; o.select_set(True)
    bpy.context.view_layer.objects.active = targets[0]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.003, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.003, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    a3 = a2 = 0.0
    for o in targets:
        uvd = o.data.uv_layers["BakeAtlas"].data
        for p in o.data.polygons:
            a3 += p.area
            pts = [uvd[i].uv for i in p.loop_indices]
            a2 += abs(sum(pts[i].x * pts[i - 1].y - pts[i - 1].x * pts[i].y for i in range(len(pts)))) / 2
    density = res * math.sqrt(a2 / a3)
    print(f"[bake] {asset}: {len(targets)} objects, {a3:.2f} m², UV fill {a2:.1%}, ~{density:.0f} px/m", flush=True)
    # 2) 三张数据图
    data = {}
    for mode in ("pos", "nrm", "id"):
        im = bpy.data.images.new(f"LSB_{asset}_{mode}", res, res, alpha=True, float_buffer=True); im.colorspace_settings.name = "Non-Color"
        prevs = {}
        for k, m in enumerate(mats):
            prevs[m] = _emit_setup(m, mode, k)
            nt = m.node_tree
            n = nt.nodes.get("LSB_Target") or nt.nodes.new("ShaderNodeTexImage"); n.name = "LSB_Target"; n.image = im
            nt.nodes.active = n
        for o in targets: o.select_set(True)
        bpy.context.view_layer.objects.active = targets[0]
        bpy.ops.object.bake(type='EMIT', margin=margin, use_clear=True, uv_layer="BakeAtlas")
        buf = np.empty(res * res * 4, np.float32); im.pixels.foreach_get(buf)    # 别用 pixels[:]：4K 时会生成上亿个 Python 浮点
        data[mode] = buf.reshape(res, res, 4); bpy.data.images.remove(im)
        for m, p in prevs.items():
            nt = m.node_tree; out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
            if p: nt.links.new(p, out.inputs[0])
        print(f"[bake] {asset} {mode} done {__import__('time').strftime('%H:%M:%S')}", flush=True)
    for m in mats:
        nt = m.node_tree
        for nm in ("LSB_Emit", "LSB_Geo", "LSB_Off", "LSB_EdgeAttr", "LSB_Sep", "LSB_Comb", "LSB_Target"):
            if nt.nodes.get(nm): nt.nodes.remove(nt.nodes[nm])
    # 3) numpy 合成
    P, Nw, ID = data["pos"][..., :3] - 10.0, data["nrm"][..., :3] * 2 - 1, data["id"]
    valid = ID[..., 2] > 0.5
    mid = np.clip(np.round(ID[..., 0] * 16 - 0.5), 0, len(mats) - 1).astype(int)
    # 棱边度：在贴图集里按像素算世界法线的变化率（1/m）；岛边界（相邻像素位置不连续）不算。倒角半径约 2 cm → 曲率 ≈ 50/m
    px = 1.0 / density
    curv = np.zeros((res, res), np.float32)
    for ax_, sh in ((1, 1), (1, -1), (0, 1), (0, -1)):
        Pn = np.roll(P, sh, ax_); Nn = np.roll(Nw, sh, ax_)
        dpos = np.linalg.norm(Pn - P, axis=-1)
        ok = (dpos < px * 2.5) & np.roll(valid, sh, ax_)
        c = np.linalg.norm(Nn - Nw, axis=-1) / np.maximum(dpos, px * 0.5)
        curv = np.maximum(curv, np.where(ok, c, 0))
    edge = np.clip((curv - 8.0) / 40.0, 0, 1)
    k = max(1, int(round(0.012 * density)))                 # 磨损从棱边向外扩约 1.2 cm
    e2 = edge.copy()
    for _ in range(k):
        e2 = np.maximum(e2, np.maximum.reduce([np.roll(e2, 1, 0), np.roll(e2, -1, 0), np.roll(e2, 1, 1), np.roll(e2, -1, 1)]) * 0.85)
    edge = e2 * valid
    ax = np.argmax(np.abs(Nw), -1)                                   # 三向投影：1 m = 1 张平铺图
    u = np.where(ax == 0, P[..., 1], P[..., 0]); v = np.where(ax == 2, P[..., 1], P[..., 2])
    def tile(img):
        n = img.shape[0]
        return img[(np.floor(v * n).astype(int)) % n, (np.floor(u * n).astype(int)) % n]
    base = np.zeros((res, res, 3), np.float32); rough = np.full((res, res), 0.5, np.float32)
    metal = np.zeros((res, res), np.float32); height = np.zeros((res, res), np.float32)
    iron = tile(_load("T_LT01_CastIron_BaseColor.png")) ** 2.2
    for k, m in enumerate(mats):
        r = recipes[m.name]; sel = valid & (mid == k)
        if isinstance(r, Tiled):
            bc = tile(_load(f"T_{r.tex}_BaseColor.png")) ** 2.2; orm = tile(_load(f"T_{r.tex}_ORM.png"))
            base[sel] = bc[sel]; rough[sel] = orm[..., 1][sel]; metal[sel] = orm[..., 2][sel]
            continue
        mk = tile(_load(r.mask)); R, G, B = mk[..., 0], mk[..., 1], mk[..., 2]
        t = np.zeros((res, res), np.float32); tp = np.zeros((res, res), np.float32)   # tp：会被手磨亮的部分（撞击类不磨亮）
        for tch in r.touch:
            x, y, z, rad, kk = tch[:5]; polish = tch[5] if len(tch) > 5 else 1.0
            d = np.sqrt((P[..., 0] - x) ** 2 + (P[..., 1] - y) ** 2 + (P[..., 2] - z) ** 2)
            q = np.clip(1 - d / rad, 0, 1); w = kk * q * q * (3 - 2 * q)
            t += w; tp += w * polish
        t = np.clip(t, 0, 1); tp = np.clip(tp, 0, 1)
        fl = np.clip(1 - P[..., 2] / 0.15, 0, 1)
        W = np.clip(r.base_w + r.edge_k * edge + t + r.floor_k * fl, 0, 1)
        c1 = (R > 1 - r.a1 * W).astype(np.float32); c2 = (R > 1 - r.a2 * W).astype(np.float32)
        col = _lin(r.top) * (0.88 + 0.22 * G)[..., None]
        col = col * (1 - c1[..., None]) + _lin(r.mid) * c1[..., None]
        col = col * (1 - c2[..., None]) + iron * c2[..., None]
        pol = np.clip(c2 * tp * 1.2, 0, 1)[..., None]
        col = col * (1 - pol) + _lin("#B9B6AC") * pol                               # 手磨出的亮铁
        dirt = np.clip(B * 0.3 + (1 - t) * 0.0, 0, 1)[..., None]
        col = col * (1 - 0.35 * dirt)
        rg = 0.55 + 0.15 * c2 - 0.35 * tp
        mt = np.clip(0.6 * c2 + 0.4 * c2 * tp, 0, 1)
        base[sel] = col[sel]; rough[sel] = np.clip(rg, 0.08, 1)[sel]; metal[sel] = mt[sel]
        height[sel] = (1 - 0.35 * c1 - 0.5 * c2)[sel]
    # 剥落台阶 → 切线空间法线（贴图集 U/V 就是切线 / 副切线方向；1 px ≈ 1/density m，台阶 0.15 mm）
    s = 0.00015 * density * 6.0
    hx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * s
    hy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5 * s
    same = (np.roll(mid, -1, 1) == mid) & (np.roll(mid, 1, 1) == mid) & (np.roll(mid, -1, 0) == mid) & (np.roll(mid, 1, 0) == mid) & valid
    hx *= same; hy *= same
    nrm = np.stack([-hx, -hy, np.ones_like(hx)], -1); nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    os.makedirs(out_dir, exist_ok=True)
    def save(a, name, mode="RGB"):
        Image.fromarray((np.clip(a, 0, 1)[::-1] * 255 + 0.5).astype(np.uint8), mode).save(os.path.join(out_dir, name))
    save(base ** (1 / 2.2), f"{asset}_Atlas_BaseColor.png")
    save(nrm * 0.5 + 0.5, f"{asset}_Atlas_Normal.png")
    save(np.stack([np.ones_like(rough), rough, metal], -1), f"{asset}_Atlas_ORM.png")
    save(np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), 1 - rough], -1), f"{asset}_Atlas_MS.png", "RGBA")
    print(f"[bake] {asset} atlas written", flush=True)
    # 4) 换成单一贴图集材质；BakeAtlas 成为第一套 UV（Unity UV0）
    fm = bpy.data.materials.new(f"M_{asset}_Atlas"); fm.use_nodes = True
    nt = fm.node_tree; bsdf = nt.nodes["Principled BSDF"]
    def ld(name, cs):
        im = bpy.data.images.load(os.path.join(out_dir, name)); im.colorspace_settings.name = cs
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = im; return t
    t = ld(f"{asset}_Atlas_BaseColor.png", "sRGB"); nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    t = ld(f"{asset}_Atlas_ORM.png", "Non-Color"); sp = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(t.outputs[0], sp.inputs[0])
    nt.links.new(sp.outputs[1], bsdf.inputs["Roughness"]); nt.links.new(sp.outputs[2], bsdf.inputs["Metallic"])
    t = ld(f"{asset}_Atlas_Normal.png", "Non-Color"); nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs[0], nm.inputs[1])
    nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    fm["lsb_atlas"] = asset
    for o in targets:
        for i, m in enumerate(o.data.materials):
            if m in mats: o.data.materials[i] = fm
        old = o.data.uv_layers.get("UVMap")
        if old: o.data.uv_layers.remove(old)
        o.data.uv_layers["BakeAtlas"].name = "UVMap"
        ea = o.data.color_attributes.get("LSB_Edge")
        if ea: o.data.color_attributes.remove(ea)
    return density

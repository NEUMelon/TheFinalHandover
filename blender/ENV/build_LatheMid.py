# 《最后一次交班》LT02–LT04 背景车床（Mid）· 由 LT01 Hero 建模脚本减配。由 Claude 编写（AI 辅助，已登记 AI 使用日志）。
# 运行：python3 build_LatheMid.py <输出目录> [贴图集边长=2048]
# 做法：读 ../LT01/build_LT01.py 的源码（只读，不改文件），在内存里做下面的替换后执行，得到同一台 HC-620 的几何：
#   1. 去掉剧情细节：R1 传感器 + 2025 新线 + 扎带、L1 漆圈 / 样冲点、护罩凹痕（护罩改成无开孔的整片粗网格）
#   2. 减面：圆柱段数 × 0.55（最少 8）、倒角段数 ≤ 2、手轮轮缘 48×12 → 28×8
#   3. 不执行原脚本的保存 / 导出段（不会写 LT01/ 目录）
# 然后按 lsb_bake 的同一套“磨损动线”配方烘 2048² 贴图集 LSB_ENV_MCH_Lathe_Mid_Atlas（手碰点去掉 L1 与凹痕）。
# 局部原点 = 床身西端底面中心（同 LT01）；三台实例放在 (2.0 / 14.0 / 20.0, 0, 11.0)，见 unity_manifest.json。
import sys, os, re
HERE = os.path.dirname(os.path.abspath(__file__))
STAGE3 = os.path.dirname(HERE)
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(HERE, "LatheMid")
RES = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
TEXOUT = os.path.join(HERE, "Textures")
os.makedirs(OUT, exist_ok=True)
ASSET = "LSB_ENV_MCH_Lathe_Mid"

src_path = os.path.join(STAGE3, "LT01", "build_LT01.py")
src = open(src_path, encoding="utf-8").read()

def must_replace(s, old, new, count=1):
    assert s.count(old) >= 1, f"源码里找不到：{old[:60]}"
    return s.replace(old, new) if count is None else s.replace(old, new, count)

# 根物体名
src = must_replace(src, 'root = bpy.data.objects.new(f"LSB_ENV_MCH_Lathe_LT01_{VER}", None)', f'root = bpy.data.objects.new("{ASSET}_v01", None)')
# 减面：包一层 cyl / add_bevel
src = must_replace(src, "def tube_along(", '''_cyl0 = cyl
def cyl(name, axis, c, r, length, material="steel", seg=48, bevel=0.0, parent=root, start=None, cap=True):
    return _cyl0(name, axis, c, r, length, material, max(8, int(seg * 0.55)), bevel, parent, start, cap)
_ab0 = add_bevel
def add_bevel(o, w_mm, seg=3):
    return _ab0(o, w_mm, min(seg, 2))

def tube_along(''')
src = must_replace(src, "major_segments=48, minor_segments=12", "major_segments=28, minor_segments=8")
# 护罩：去掉凹痕开孔和凹痕网格，换成 50 mm × 10° 的整片粗网格
a = src.index('guard_grid("Guard_Shell", xs, phs, skip=')
b = src.index("\n", src.index('guard_grid("Guard_Dent"')) + 1
src = src[:a] + 'guard_grid("Guard_Shell", [GX0 + 50 * i for i in range((GX1 - GX0) // 50 + 1)], [PH0 + 10 * i for i in range((PH1 - PH0) // 10 + 1)])\n' + src[b:]
# 端板仍用 phs（只决定端板轮廓），保留
# 去掉 R1 传感器、2025 新线、扎带、L1 测点
a = src.index("# Sensor_R1 + Cable_2025"); b = src.index("# Nameplate 铭牌")
src = src[:a] + src[b:]
# 不执行保存 / 导出
src = src[:src.rindex("tris = 0")]

g = {"__name__": "lt01_src", "__file__": src_path}
saved_argv = sys.argv; sys.argv = [src_path, OUT]      # 原脚本按 argv[-1] 定 OUT；这里指向我们的目录（其实保存段已删）
exec(compile(src, src_path + " (Mid 减配)", "exec"), g)
sys.argv = saved_argv

import bpy
sys.path.insert(0, os.path.join(STAGE3, "Textures"))
import lsb_bake
scn = bpy.context.scene
objs = [o for o in scn.objects if o.type == 'MESH']
tris = 0
for o in objs: o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
print(f"[LatheMid] objects={len(objs)} tris={tris}", flush=True)

# 磨损动线（与 bake_LT01.py 同一套，去掉 L1 测点与 1987 凹痕两项；背景车床同样是天天在用的）
TOUCH = [
    (1.56, -0.40, 0.78, 0.30, 1.0), (1.65, -0.43, 1.105, 0.18, 0.8), (1.65, -0.45, 1.06, 0.30, 0.5),
    (0.65, -0.49, 1.20, 0.18, 0.9), (0.75, -0.47, 1.30, 0.30, 0.7), (0.33, -0.41, 0.83, 0.25, 0.6),
    (2.40, -0.06, 1.25, 0.15, 0.4), (1.05, -0.19, 1.05, 0.15, 0.6),
]
RECIPES = {
    "M_LT01_Paint_1983Green": lsb_bake.Painted("T_LT01_Paint_1983Green_Mask.png", "#5F7F72", "#8A968C", TOUCH, a1=0.30, a2=0.10, base_w=0.06, edge_k=0.7),
    "M_LT01_Guard_1956Grey": lsb_bake.Painted("T_LT01_Guard_1956Grey_Mask.png", "#6E7A72", "#4A564F", TOUCH, a1=0.40, a2=0.14, base_w=0.12, edge_k=0.7),
    "M_LT01_CastIron": lsb_bake.Tiled("LT01_CastIron"),
    "M_LT01_ChipTray": lsb_bake.Tiled("LT01_CastIron"),
    "M_LT01_MachinedSteel": lsb_bake.Tiled("LT01_MachinedSteel"),
    "M_LT01_WornBright": lsb_bake.Tiled("LT01_WornBright"),
}
d = lsb_bake.bake_atlas(objs, ASSET, TEXOUT, RECIPES, res=RES)
# 剩下的纯色小件材质改成本资产前缀（Unity 端按 FBX 目录建材质，不会和 LT01 混）
for m in bpy.data.materials:
    if m.name.startswith("M_LT01_") and m.users:
        m.name = m.name.replace("M_LT01_", f"M_{ASSET}_")
for m in [m for m in bpy.data.materials if m.users == 0]: bpy.data.materials.remove(m)
if bpy.data.materials.get(f"M_{ASSET}_Sensor2025"): bpy.data.materials[f"M_{ASSET}_Sensor2025"].name = f"M_{ASSET}_OilSightGlass"   # 背景车床上这个材质只剩油窗玻璃
tris = 0
for o in objs: o.data.calc_loop_triangles(); tris += len(o.data.loop_triangles)
open(os.path.join(OUT, "build_stats.txt"), "w").write(f"objects={len(objs)}\ntris={tris}\natlas={RES}\ndensity_px_per_m={d:.0f}\n")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f"{ASSET}_v01.blend"))
print(f"[LatheMid] saved, tris={tris}, density={d:.0f} px/m", flush=True)

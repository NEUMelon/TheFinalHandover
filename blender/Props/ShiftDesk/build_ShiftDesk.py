# 交班桌 + 台灯（Mid，8–12k）· 由 Claude 编写（AI 编写脚本程序化生成）
# 运行：python3 build_ShiftDesk.py <输出目录>
# 白盒：交班桌_桌面 (26.5, 0.74, 13.0) 0.8 × 0.04 × 1.6（台面上沿 0.76），桌腿 4 根 (±0.35, ±0.72)，抽屉_盒 (26.55, 0.62, 13.55) 0.45 × 0.14 × 0.35，
#       抽屉开 = 向东滑 0.3（WorldPresenter：DrawerBox.localPosition.x 26.55 → 26.85）；台灯_座 / 台灯_杆 在 (25.95, 13.45)。
# 局部原点 = 桌子底面中心 → 世界 (26.5, 0, 13.0)；+X = 东（坐人那一侧，N1 机位看到的一面），+Y = 北。
# 部件：
#   Desk        桌体（清漆木，东沿手肘处包浆磨亮，东南角两处烫痕）
#   GlassPlate  玻璃板（压着点检单；0.7605–0.772，白盒点检单 0.763–0.767 夹在里面）——Unity 里必须用透明材质，否则会挡住点检单
#   Drawer      抽屉（枢轴 = 白盒 DrawerBox 位置 (26.55, 0.62, 13.55)，跟灰盒抽屉 rigid 平移）
#   DeskLamp    台灯（1980 年代铸铁座 + 弯杆搪瓷罩台灯，罩口朝东下照桌面）
# 年代：桌子 1970 年代办公桌式样（清漆实木、四腿带牙板），台灯 1980 年代；玻璃板 + 压在下面的点检单是 2026 年仍在用的老习惯。
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
from mathutils import Matrix, Vector
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_ShiftDesk_v01")
A.mat("wood", "M_DSK_Wood", "#7A4E30", 0.45)
A.mat("worn", "M_DSK_WoodWorn", "#9A6A44", 0.25)
A.mat("metal", "M_DSK_Metal", "#B8B4A8", 0.3, 1.0)
A.mat("glass", "M_DSK_Glass", "#9FB8B0", 0.05, 0.0, alpha=0.15)
A.mat("lamp", "M_DSK_LampEnamel", "#4F6B5C", 0.35)
A.mat("lampin", "M_DSK_LampInner", "#E8E4D8", 0.3)
A.mat("chrome", "M_DSK_LampStem", "#C9C6BC", 0.2, 1.0)
A.mat("bulb", "M_DSK_LampBulb", "#EDE6D6", 0.2)
A.mat("cable", "M_DSK_LampCable", "#1E1E1E", 0.5)
A.mat("burn", "M_DSK_Burn", "#7A4E30", 0.45)
A.mat("burn2", "M_DSK_BurnWorn", "#9A6A44", 0.25)
D = A.part("Desk", (0, 0, 0))
# ---- 台面：中板 + 四边实木封边（东边封边 = 手肘、交班本常压的地方，磨亮）
A.box("TopPanel", (0, 0, 0.745), (0.72, 1.52, 0.03), "wood", D)
A.box("Lip_W", (-0.38, 0, 0.745), (0.04, 1.52, 0.03), "wood", D, bevel=0.004, seg=3)
A.box("Lip_E", (0.38, 0, 0.745), (0.04, 1.52, 0.03), "worn", D, bevel=0.004, seg=3)
A.box("Lip_S", (0, -0.78, 0.745), (0.80, 0.04, 0.03), "wood", D, bevel=0.004, seg=3)
A.box("Lip_N", (0, 0.78, 0.745), (0.80, 0.04, 0.03), "wood", D, bevel=0.004, seg=3)
# ---- 四条腿（略收分）
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = 0.35 * sx, 0.72 * sy
        o = A.poly(f"Leg_{sx}{sy}", [(x - .025, y - .025, 0.73), (x + .025, y - .025, 0.73), (x + .025, y + .025, 0.73), (x - .025, y + .025, 0.73),
                                    (x - .019, y - .019, 0.0), (x + .019, y - .019, 0.0), (x + .019, y + .019, 0.0), (x - .019, y + .019, 0.0)],
                   [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], "wood", D)
        A.bevel(o, 0.004, 2)
        A.box(f"Foot_{sx}{sy}", (x, y, 0.006), (0.04, 0.04, 0.012), "metal", D)
# ---- 牙板 / 横档
A.box("Apron_W", (-0.35, 0, 0.685), (0.02, 1.39, 0.09), "wood", D, bevel=0.002)
A.box("Apron_S", (0, -0.72, 0.685), (0.65, 0.02, 0.09), "wood", D, bevel=0.002)
A.box("Apron_N", (0, 0.72, 0.63), (0.65, 0.02, 0.20), "wood", D, bevel=0.002)
A.box("Rail_E", (0.35, -0.17, 0.715), (0.02, 1.05, 0.03), "wood", D, bevel=0.002)       # 坐人那一侧：膝盖上方只留一根横档
A.box("Rail_E_Drawer", (0.35, 0.53, 0.718), (0.02, 0.33, 0.024), "wood", D, bevel=0.002)
A.box("Rail_E_Under", (0.35, 0.53, 0.537), (0.02, 0.33, 0.014), "wood", D, bevel=0.002)
A.box("Stretcher_S", (0, -0.72, 0.12), (0.65, 0.025, 0.035), "wood", D, bevel=0.002)   # 脚踏横枨（坐的人脚蹬了几十年，磨亮）
A.box("Stretcher_N", (0, 0.72, 0.12), (0.65, 0.025, 0.035), "wood", D, bevel=0.002)
A.box("Housing_S", (0, 0.363, 0.63), (0.68, 0.014, 0.20), "wood", D)
A.box("Housing_Bottom", (0, 0.53, 0.535), (0.68, 0.33, 0.01), "wood", D)
# ---- 桌角烫痕（东南角封边，美术圣经 §6 “桌角烫痕（不画香烟）”）：两处贴花
import numpy as np
from PIL import Image
def scorch(seed, w=256, h=128):
    yy, xx = np.mgrid[0:h, 0:w]; rng = np.random.default_rng(seed)
    d = np.sqrt(((xx - w / 2) / (w * 0.42)) ** 2 + ((yy - h / 2) / (h * 0.36)) ** 2)
    n = rng.normal(0, 0.06, (h, w)); a = np.clip((1.0 - d + n) * 2.2, 0, 1) ** 1.5
    core = np.clip((0.55 - d) * 4, 0, 1)
    rgb = np.stack([60 - 35 * core, 38 - 24 * core, 24 - 16 * core], -1)
    return Image.fromarray(np.dstack([rgb, a * 235]).astype(np.uint8), "RGBA")
z = 0.7605
A.label("Burn_1", [(0.30, -0.797, z), (0.345, -0.797, z), (0.345, -0.779, z), (0.30, -0.779, z)], scorch(1), "burn", D)
A.label("Burn_2", [(0.37, -0.70, z), (0.392, -0.70, z), (0.392, -0.66, z), (0.37, -0.66, z)], scorch(2, 128, 256), "burn2", D)
# ---- 玻璃板
G = A.part("GlassPlate", (0, 0, 0.766))
A.box("Glass", (0, 0, 0.76625), (0.75, 1.55, 0.0115), "glass", G, bevel=0.0015)
# ---- 抽屉（枢轴 = 白盒抽屉中心）
DR = A.part("Drawer", (0.05, 0.55, 0.62))
A.box("DrawerFront", (0.371, 0.53, 0.625), (0.022, 0.31, 0.15), "wood", DR, bevel=0.003, seg=2)
A.box("DrawerSide_S", (0.095, 0.391, 0.62), (0.53, 0.012, 0.13), "wood", DR)
A.box("DrawerSide_N", (0.095, 0.669, 0.62), (0.53, 0.012, 0.13), "wood", DR)
A.box("DrawerBack", (-0.176, 0.53, 0.615), (0.012, 0.266, 0.12), "wood", DR)
A.box("DrawerBottom", (0.095, 0.53, 0.556), (0.53, 0.266, 0.008), "wood", DR)
# 铝拉手 + 锁眼
A.tube("DrawerPull", [(0.382, 0.48, 0.63), (0.398, 0.485, 0.63), (0.398, 0.575, 0.63), (0.382, 0.58, 0.63)], 0.005, "metal", DR, seg=8)
A.cyl("Keyhole", (0.383, 0.53, 0.68), 0.009, 0.004, 'x', "metal", DR, seg=12)
# ---- 台灯
T = A.part("DeskLamp", (-0.30, 0.50, 0.772))
A.lathe("LampBase", [(0.068, 0.0), (0.068, 0.010), (0.060, 0.022), (0.030, 0.034), (0.013, 0.038)], (-0.30, 0.50, 0.772), "lamp", T, seg=24, close_top=True, normals="out")
A.box("LampSwitch", (-0.255, 0.50, 0.790), (0.022, 0.016, 0.012), "chrome", T, bevel=0.002)
stem = [(-0.30, 0.50, 0.805), (-0.30, 0.50, 1.06), (-0.31, 0.50, 1.12), (-0.345, 0.50, 1.155), (-0.40, 0.50, 1.165), (-0.44, 0.50, 1.15)]
A.tube("LampStem", stem, 0.006, "chrome", T, seg=10)
A.tube("LampCable", [(-0.33, 0.52, 0.776), (-0.36, 0.56, 0.774), (-0.37, 0.65, 0.774), (-0.385, 0.72, 0.774), (-0.40, 0.76, 0.70)], 0.0025, "cable", T, seg=6)
# 罩：沿 (0.55, 0, −0.83) 朝东下照桌面
ax = Vector((0.55, 0, -0.83)).normalized()
rot = Vector((0, 0, -1)).rotation_difference(ax).to_matrix().to_4x4()
cen = Vector((-0.465, 0.50, 1.115))
def place(o): o.matrix_world = Matrix.Translation(cen) @ rot; return o
place(A.lathe("LampShade", [(0.072, -0.040), (0.074, -0.036), (0.060, 0.000), (0.034, 0.030), (0.016, 0.040), (0.010, 0.046)], (0, 0, 0), "lamp", T, seg=20, normals="out"))
place(A.lathe("LampShadeIn", [(0.012, 0.042), (0.032, 0.028), (0.058, -0.002), (0.071, -0.038)], (0, 0, 0), "lampin", T, seg=20, normals="in"))
place(A.lathe("LampBulb", [(0.010, 0.040), (0.012, 0.025), (0.024, 0.008), (0.020, -0.010), (0.0, -0.016)], (0, 0, 0), "bulb", T, seg=10, normals="out"))
TOUCH_LAMP = [(-0.255, 0.50, 0.79, 0.05, 1.0), (-0.44, 0.50, 1.15, 0.08, 0.6)]
TOUCH_DESK = []
R = {"M_DSK_Wood": L.lsb_bake.Tiled("PRP_WoodVarnish"), "M_DSK_WoodWorn": L.lsb_bake.Tiled("PRP_WoodPolished"), "M_DSK_Burn": L.lsb_bake.Tiled("PRP_WoodVarnish"), "M_DSK_BurnWorn": L.lsb_bake.Tiled("PRP_WoodPolished"),
     "M_DSK_Metal": L.lsb_bake.Tiled("LT01_WornBright"), "M_DSK_LampStem": L.lsb_bake.Tiled("LT01_WornBright"),
     "M_DSK_LampEnamel": L.lsb_bake.Painted(os.path.join(L.PTEX, "T_PRP_Paint_A_Mask.png"), "#4F6B5C", "#2A2C2B", TOUCH_LAMP, a1=0.25, a2=0.1, base_w=0.05, edge_k=0.8, floor_k=0.0),
     "M_DSK_LampCable": L.lsb_bake.Tiled("PRP_PlasticDark")}
L.pipeline(A, "PRP_ShiftDesk", R, 2048, OUT, budget="8–12k（桌 + 台灯）")

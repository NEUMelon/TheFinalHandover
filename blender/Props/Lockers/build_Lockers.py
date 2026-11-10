# 更衣柜 ×6（Mid，6k；老周那格加细节，柜门会开）· 由 AI 辅助工具编写（AI 编写脚本程序化生成）
# 运行：python3 build_Lockers.py <输出目录>
# 白盒：更衣柜 X = 30.9 / 31.7 / 32.5 / 33.3 / 34.1（老周）/ 34.9，Z 17.6，每个 0.6 × 1.8 × 0.5，柜门朝南（门面 Z 17.345）；
#       老周柜门_铰链 = (33.81, 0.9, 17.34)，WorldPresenter 开门 = 铰链 localRotation Y 105°（门往南外开）。
# 局部原点 = 六只柜一排的底面中心 → 世界 (32.9, 0, 17.6)。+X 东，+Y 北（柜背），柜门朝 −Y。
# 部件：Lockers（五只关着的柜）；Locker_Zhou（老周那只的柜体 / 隔板 / 挂钩 / 工作帽，枢轴 = 该柜底面中心）；LockerDoor_Zhou（老周柜门，枢轴 = 灰盒铰链，跟灰盒 rot）。
# 年代：1983 技改时添置的钢板更衣柜，灰蓝漆（白盒同色系），下层灰底漆；门把手、门下沿踢碰处磨到露铁，老周那只最重（用了 15 年 + 退休后 28 年没清）。
# 柜内白盒物件（手套、工牌套、合影、饭盒）是交互物，不在本模型里；隔板高度按它们的白盒高度放：下隔板上沿 1.215（手套底），上隔板上沿 1.575（饭盒底）。
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_Lockers_v01")
A.mat("paint", "M_LKR_Paint", "#6E7F86", 0.5, 0.2)
A.mat("base", "M_LKR_Plinth", "#2E302E", 0.7, 0.3)
A.mat("vent", "M_LKR_VentShadow", "#151617", 0.9)
A.mat("chrome", "M_LKR_Handle", "#B8B4A8", 0.3, 1.0)
A.mat("zcard", "M_LKZ_Card", "#D9CFB4", 0.85)
A.mat("slot", "M_LKR_CardSlot", "#3A3D3B", 0.6)
# 老周那只单独一张贴图集（N2 机位正对它，近看）：同色材质另起名字
A.mat("zpaint", "M_LKZ_Paint", "#6E7F86", 0.5, 0.2)
A.mat("zinner", "M_LKZ_Inner", "#8A979B", 0.55, 0.2)
A.mat("zbase", "M_LKZ_Plinth", "#2E302E", 0.7, 0.3)
A.mat("zchrome", "M_LKZ_Handle", "#B8B4A8", 0.3, 1.0)
A.mat("cap", "M_LKR_Cap", "#3C4A57", 0.85)
XS = [-2.0, -1.2, -0.4, 0.4, 1.2, 2.0]
ZHOU = 4
W, D, H = 0.6, 0.5, 1.8
FY = -D / 2                       # 柜体前沿（世界 Z 17.35）
P = A.part("Lockers", (0, 0, 0))
DOOR_W, DOOR_H, DOOR_T = 0.56, 1.74, 0.015
DOOR_Y = -0.26                    # 门中面（世界 Z 17.34）
def door_details(x0, parent, tag, zhou=False):
    pt, ch = ("zpaint", "zchrome") if zhou else ("paint", "chrome")
    """x0 = 门板西沿（铰链侧）。百叶、名牌框、把手、挂锁扣。"""
    xc = x0 + DOOR_W / 2; yf = DOOR_Y - DOOR_T / 2
    for g, zc in enumerate((1.58, 0.30)):
        for i in range(4):
            z = zc + (i - 1.5) * 0.028
            A.box(f"Vent{tag}_{g}{i}", (xc, yf - 0.0005, z), (0.16, 0.002, 0.007), "vent", parent)
            A.box(f"VentLip{tag}_{g}{i}", (xc, yf - 0.004, z + 0.005), (0.165, 0.008, 0.002), pt, parent)
    # 名牌框（插卡）
    A.box(f"CardFrame{tag}", (xc, yf - 0.0015, 1.40), (0.10, 0.003, 0.045), ch, parent, bevel=0.001)
    A.box(f"CardSlot{tag}", (xc, yf - 0.0031, 1.40), (0.088, 0.0004, 0.034), "slot", parent)
    # 竖把手 + 挂锁扣（东侧 = 开门侧）
    xh = x0 + DOOR_W - 0.055
    A.box(f"HandleBase{tag}", (xh, yf - 0.002, 1.00), (0.03, 0.004, 0.16), ch, parent, bevel=0.0015)
    A.tube(f"Handle{tag}", [(xh, yf - 0.003, 0.94), (xh, yf - 0.025, 0.95), (xh, yf - 0.025, 1.05), (xh, yf - 0.003, 1.06)], 0.006, ch, parent, seg=8)
    A.box(f"Hasp{tag}", (xh, yf - 0.006, 0.88), (0.02, 0.012, 0.03), ch, parent, bevel=0.002)
    # 门板压筋（上下两道横筋）
    for z in (0.75, 1.20):
        A.box(f"Rib{tag}_{z}", (xc, yf - 0.002, z), (DOOR_W - 0.08, 0.004, 0.012), pt, parent, bevel=0.0015)
for i, x in enumerate(XS):
    t = f"_{i + 1}"
    if i != ZHOU:
        A.box(f"Body{t}", (x, 0.005, H / 2 + 0.03), (W, D - 0.01, H - 0.06), "paint", P, bevel=0.006, seg=2)
        A.box(f"Plinth{t}", (x, 0.01, 0.025), (W - 0.03, D - 0.04, 0.05), "base", P)
        A.box(f"Door{t}", (x, DOOR_Y, 0.9), (DOOR_W, DOOR_T, DOOR_H), "paint", P, bevel=0.004, seg=2)
        door_details(x - DOOR_W / 2, P, t)
        for z in (0.35, 1.45):   # 西侧铰链
            A.cyl(f"Hinge{t}_{z}", (x - W / 2 + 0.004, DOOR_Y - 0.004, z), 0.006, 0.07, 'z', "chrome", P, seg=8)
# ---- 老周那只：空心柜体 + 两层隔板 + 挂钩 + 工作帽（单独部件 Locker_Zhou，交互高亮只亮这一只）
x = XS[ZHOU]; t = "_Zhou"; th = 0.008
P = A.part("Locker_Zhou", (x, 0, 0))
A.box(f"Back{t}", (x, D / 2 - th / 2, H / 2 + 0.03), (W, th, H - 0.06), "zpaint", P)
A.box(f"SideW{t}", (x - W / 2 + th / 2, 0.0, H / 2 + 0.03), (th, D, H - 0.06), "zpaint", P, bevel=0.003)
A.box(f"SideE{t}", (x + W / 2 - th / 2, 0.0, H / 2 + 0.03), (th, D, H - 0.06), "zpaint", P, bevel=0.003)
A.box(f"Top{t}", (x, 0.0, H - th / 2), (W, D, th), "zpaint", P, bevel=0.003)
A.box(f"Bottom{t}", (x, 0.0, 0.06 + th / 2), (W - 2 * th, D - th, th), "zinner", P)
A.box(f"Plinth{t}", (x, 0.01, 0.03), (W - 0.03, D - 0.04, 0.06), "zbase", P)
A.box(f"FrameTop{t}", (x, FY + 0.01, H - 0.04), (W - 2 * th, 0.02, 0.06), "zpaint", P)       # 门框上横
A.box(f"FrameBottom{t}", (x, FY + 0.01, 0.09), (W - 2 * th, 0.02, 0.06), "zpaint", P)
for zt in (1.215, 1.575):
    A.box(f"Shelf{t}_{zt}", (x, 0.02, zt - 0.006), (W - 2 * th, D - 0.05, 0.012), "zinner", P, bevel=0.002)
    A.box(f"ShelfLip{t}_{zt}", (x, FY + 0.03, zt - 0.02), (W - 2 * th, 0.008, 0.03), "zinner", P)
A.box(f"InnerBack{t}", (x, D / 2 - th - 0.001, 0.9), (W - 2 * th - 0.002, 0.002, 1.66), "zinner", P)
for dx in (-0.12, 0.12):   # 挂衣钩（上隔板下面）
    A.tube(f"Hook{t}{dx}", [(x + dx, D / 2 - th - 0.002, 1.50), (x + dx, D / 2 - 0.05, 1.50), (x + dx, D / 2 - 0.065, 1.47), (x + dx, D / 2 - 0.06, 1.45)], 0.003, "zchrome", P, seg=6)
for z in (0.35, 1.45):
    A.cyl(f"Hinge{t}_{z}", (x - W / 2 + 0.004, DOOR_Y - 0.004, z), 0.006, 0.07, 'z', "zchrome", P, seg=8)
# 工作帽（美术圣经 §9 老周 1983 蓝灰工作帽；事实链 E1-7），放在上隔板西半边，避开白盒饭盒 (34.16–34.34)
cx, cy, cz = x - 0.13, 0.02, 1.575
A.lathe(f"CapCrown{t}", [(0.085, 0.0), (0.088, 0.03), (0.075, 0.07), (0.045, 0.092), (0.0, 0.098)], (cx, cy, cz), "cap", P, seg=20, normals="out")
A.poly(f"CapBrim{t}", [(cx - 0.06, cy - 0.07, cz + 0.006), (cx + 0.06, cy - 0.07, cz + 0.006), (cx + 0.05, cy - 0.13, cz + 0.002), (cx - 0.05, cy - 0.13, cz + 0.002)], [(3, 2, 1, 0)], "cap", P)
A.solidify(A.scn.objects[f"CapBrim{t}"], 0.003)
# ---- 老周柜门（枢轴 = 灰盒铰链）
HX = x - 0.29
Dz = A.part("LockerDoor_Zhou", (HX, -0.26, 0.9))
A.box(f"Door{t}", (HX + 0.28, DOOR_Y, 0.9), (DOOR_W, DOOR_T, DOOR_H), "zpaint", Dz, bevel=0.004, seg=2)
door_details(HX, Dz, t, zhou=True)
# 老周的名牌：发黄纸卡，手写“周铁生”，压在名牌框里
yc = DOOR_Y - DOOR_T / 2 - 0.0034
img = L.text_img([("周铁生", 1.0, 0.55)], size=(400, 150), font_px=96, rot=-2, jitter=3, ink=(45, 45, 60, 255))
A.label(f"NameCard{t}", [(HX + 0.28 - 0.043, yc, 1.40 - 0.016), (HX + 0.28 + 0.043, yc, 1.40 - 0.016), (HX + 0.28 + 0.043, yc, 1.40 + 0.016), (HX + 0.28 - 0.043, yc, 1.40 + 0.016)], img, "zcard", Dz)
# 磨损动线：每只柜的把手、门下沿踢门；老周那只把手更重
TOUCH = []
for i, xx in enumerate(XS):
    k = 1.0 if i == ZHOU else 0.55
    TOUCH += [(xx + 0.225, -0.28, 1.0, 0.13, k), (xx + 0.1, -0.27, 0.1, 0.25, 0.35 * k), (xx + 0.225, -0.28, 0.88, 0.06, 0.8 * k, 0.0)]
PM = lambda m, top, mid, t, **k: L.lsb_bake.Painted(os.path.join(L.PTEX, m), top, mid, t, **k)
R1 = {"M_LKR_Paint": PM("T_PRP_Paint_B_Mask.png", "#6E7F86", "#4F5A5E", TOUCH, a1=0.3, a2=0.1, base_w=0.0, edge_k=0.5, floor_k=0.5),
      "M_LKR_Plinth": L.lsb_bake.Tiled("LT01_CastIron"), "M_LKR_Handle": L.lsb_bake.Tiled("LT01_WornBright")}
R2 = {"M_LKZ_Paint": PM("T_PRP_Paint_B_Mask.png", "#6E7F86", "#4F5A5E", TOUCH, a1=0.3, a2=0.1, base_w=0.01, edge_k=0.55, floor_k=0.5),
      "M_LKZ_Inner": PM("T_PRP_Paint_A_Mask.png", "#8A979B", "#4F5A5E", [], a1=0.15, a2=0.04, base_w=0.05, edge_k=0.5, floor_k=0.0),
      "M_LKZ_Plinth": L.lsb_bake.Tiled("LT01_CastIron"), "M_LKZ_Handle": L.lsb_bake.Tiled("LT01_WornBright"),
      "M_LKZ_Card": L.lsb_bake.Tiled("PRP_PaperAged")}   # 工作帽保留纯色 M_LKR_Cap（蓝灰布）
L.pipeline(A, [("PRP_Lockers", R1, 2048), ("PRP_LockerZhou", R2, 2048)], None, None, OUT, budget="6k")

# 交班桌旁工具箱（BG，2k）· 由 Claude 编写（AI 编写脚本程序化生成）
# 运行：python3 build_Toolbox.py <输出目录>
# 白盒：工具箱 (27.3, 0.13, 12.3)，0.5 × 0.25 × 0.25，放地上。局部原点 = 底面中心 → 世界 (27.3, 0, 12.3)，长边沿 X（东西）。
# 造型：1980 年代手提钢板工具箱，盖子带折叠提手，正面（+Y = 北，朝交班桌椅子 / N1）两只搭扣，背面通长铰链。
# 漆：灰蓝（蓝绿灰色系内，和交班桌木色、墙裙绿拉开），下层灰底漆，提手、搭扣、底角磨到露铁。
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_Toolbox_v01")
A.mat("paint", "M_TBX_Paint", "#3C4C58", 0.5, 0.2)
A.mat("steel", "M_TBX_Steel", "#9EA3A3", 0.35, 1.0)
A.mat("grip", "M_TBX_Grip", "#26262A", 0.6)
P = A.part("Toolbox", (0, 0, 0))
W, D = 0.48, 0.23
A.box("Shell", (0, 0, 0.098), (W, D, 0.176), "paint", P, bevel=0.004, seg=2)
A.box("Lid", (0, 0, 0.215), (W + 0.008, D + 0.008, 0.062), "paint", P, bevel=0.016, seg=2)
A.box("LidRim", (0, 0, 0.187), (W + 0.012, D + 0.012, 0.008), "paint", P, bevel=0.002)
for x in (-0.2, 0.2): A.box(f"Skid{x}", (x, 0, 0.005), (0.03, D - 0.01, 0.01), "steel", P, bevel=0.002)
# 背面通长铰链
A.cyl("Hinge", (0, -D / 2 - 0.006, 0.182), 0.005, W - 0.04, 'x', "steel", P, seg=8)
# 正面两只搭扣：底座 + 扣舌
for x in (-0.15, 0.15):
    A.box(f"ClaspBase{x}", (x, D / 2 + 0.003, 0.165), (0.04, 0.006, 0.03), "steel", P, bevel=0.0015)
    A.box(f"ClaspLever{x}", (x, D / 2 + 0.008, 0.19), (0.026, 0.006, 0.05), "steel", P, bevel=0.0015)
# 提手：两只铰耳 + 平放的 U 形提梁 + 胶套
for x in (-0.11, 0.11):
    A.box(f"HandleEar{x}", (x, 0, 0.251), (0.02, 0.03, 0.012), "steel", P, bevel=0.002)
A.tube("Handle", [(-0.11, 0.0, 0.254), (-0.1, 0.06, 0.256), (-0.05, 0.075, 0.256), (0.05, 0.075, 0.256), (0.1, 0.06, 0.256), (0.11, 0.0, 0.254)], 0.0045, "steel", P, seg=8)
A.cyl("HandleGrip", (0, 0.075, 0.256), 0.0085, 0.09, 'x', "grip", P, seg=10)
TOUCH = [(0, 0.07, 0.26, 0.12, 1.0), (-0.15, 0.12, 0.18, 0.06, 0.9), (0.15, 0.12, 0.18, 0.06, 0.9), (0, 0.12, 0.2, 0.25, 0.3)]
R = {"M_TBX_Paint": L.lsb_bake.Painted(os.path.join(L.PTEX, "T_PRP_Paint_B_Mask.png"), "#3C4C58", "#6B6F68", TOUCH, a1=0.2, a2=0.06, base_w=0.04, edge_k=0.7, floor_k=0.4),
     "M_TBX_Steel": L.lsb_bake.Tiled("LT01_WornBright"), "M_TBX_Grip": L.lsb_bake.Tiled("PRP_PlasticDark")}
L.pipeline(A, "PRP_Toolbox", R, 1024, OUT, budget="2k")

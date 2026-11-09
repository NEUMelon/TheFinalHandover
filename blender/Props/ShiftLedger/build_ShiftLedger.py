# 交班本 3D 本体（Hero 道具，3–5k；封面 UI 另做）· 由 Claude 编写（AI 编写脚本程序化生成）
# 运行：python3 build_ShiftLedger.py <输出目录>
# 白盒：交班本 (26.5, 0.785, 13.0)，0.3（X）× 0.05 × 0.22（Z），只有合上的状态（打开、翻页全在 UI 里做，白盒没有 3D 打开态）。
# 局部原点 = 本子底面中心 → 世界 (26.5, 0.772, 13.0)（交班桌玻璃板上沿 0.772）。
# 朝向：坐在桌东侧、面朝西的人看——书顶朝西（−X），书脊在左手（南，−Y），切口朝北（+Y）。
# 造型（美术圣经 §6）：硬壳墨绿布面、贴满剪页（1950s 发黄薄纸、1980s 横格稿纸、2000s 打印单从切口和书顶支出来）、
#   书角包布磨白、一根褪色晒图蓝书签带垂到桌面；封面一张旧纸签“交班记录 二〇二六”（封面正式 UI 由 2D 另做，此签可在 UI 定稿后换）。
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_ShiftLedger_v01")
A.mat("cloth", "M_LDG_Cloth", "#2F4A3C", 0.75)
A.mat("corner", "M_LDG_Corner", "#3A3833", 0.6)
A.mat("paper", "M_LDG_Paper", "#D9CFB4", 0.85)
A.mat("white", "M_LDG_PaperPrint", "#E9E6DC", 0.8)
A.mat("ribbon", "M_LDG_Ribbon", "#3C5068", 0.6)
A.mat("label", "M_LDG_Label", "#D9CFB4", 0.85)
B = A.part("Ledger", (0, 0, 0))
LX, LY, T = 0.300, 0.220, 0.0032          # 封面板长（沿 X）、宽（沿 Y）、厚
H = 0.050
# 封面 / 封底
A.box("CoverBack", (0.0, 0.0015, T / 2), (LX, LY - 0.003, T), "cloth", B, bevel=0.0013, seg=3)
A.box("CoverFront", (0.0, 0.0015, H - T / 2), (LX, LY - 0.003, T), "cloth", B, bevel=0.0013, seg=3)
# 圆书脊（南侧）
A.lathe("Spine", [(H / 2, -LX / 2), (H / 2 + 0.001, -LX / 2 + 0.004), (H / 2 + 0.001, LX / 2 - 0.004), (H / 2, LX / 2)], (0, -LY / 2 + 0.006, H / 2), "cloth", B, seg=28, axis='x', normals="out")
# 页块：分几叠（贴了剪页的地方鼓起来），切口不齐
import random
rnd = random.Random(7)
# 页块芯 + 切口 / 书顶两圈薄片（只做露在外面的边，免得藏在里面的大面积浪费贴图）
A.box("PagesCore", (-0.0015, -0.004, H / 2), (LX - 0.013, LY - 0.022, H - 2 * T - 0.0004), "paper", B)
z = T; stacks = 10
for i in range(stacks):
    th = (H - 2 * T) / stacks
    dx = rnd.uniform(-0.002, 0.0015); dy = rnd.uniform(-0.0025, 0.0)
    # 切口（北）一条
    A.box(f"PagesFore_{i}", (-0.001, LY / 2 - 0.016 + dy / 2, z + th / 2), (LX - 0.012, 0.014 + dy, th * 0.97), "paper", B, bevel=0.0009, seg=2)
    # 书顶（西）一条
    A.box(f"PagesHead_{i}", (-LX / 2 + 0.012 + dx / 2, -0.004, z + th / 2), (0.016 - dx, LY - 0.024, th * 0.97), "paper", B, bevel=0.0009, seg=2)
    z += th
# 剪页：从切口（北）和书顶（西）支出来；三种年代的纸
slips = [  # (中心 x, y, z, 尺寸 x, y, 材质)
    (-0.06, 0.112, 0.012, 0.07, 0.018, "paper"), (0.04, 0.115, 0.020, 0.05, 0.024, "white"), (0.10, 0.109, 0.031, 0.06, 0.012, "paper"),
    (-0.10, 0.113, 0.038, 0.045, 0.020, "white"), (0.0, 0.110, 0.027, 0.09, 0.014, "paper"),
    (-0.156, 0.03, 0.016, 0.022, 0.08, "paper"), (-0.158, -0.05, 0.034, 0.028, 0.05, "white"), (-0.153, 0.07, 0.042, 0.018, 0.04, "paper"),
]
for k in range(8):   # 再补几张：切口、书顶各几张短的
    slips.append((rnd.uniform(-0.12, 0.12), 0.108 + rnd.uniform(0, 0.004), rnd.uniform(0.008, 0.044), rnd.uniform(0.02, 0.05), rnd.uniform(0.006, 0.014), rnd.choice(["paper", "white"])))
for k, (x, y, zz, sx, sy, m) in enumerate(slips):
    o = A.box(f"Slip_{k}", (x, y, zz), (sx, sy, 0.0006), m, B)
    # 支出来的纸头略微下垂：沿伸出方向切 3 段，外端往下压
    import bmesh as _bm
    bm = _bm.new(); bm.from_mesh(o.data)
    _bm.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs((e.verts[0].co - e.verts[1].co).normalized()[1 if y > 0.1 else 0]) > 0.9], cuts=3)
    for v in bm.verts:
        out = (v.co.y - 0.106) if y > 0.1 else (-0.146 - v.co.x)
        if out > 0: v.co.z -= (out ** 2) * 6.0
    bm.to_mesh(o.data); bm.free()
# 书角包布（切口两角），磨白
for sx in (-1, 1):
    x = sx * (LX / 2 - 0.012); y = LY / 2 - 0.012
    for zc in (T / 2, H - T / 2):
        A.poly(f"Corner_{sx}_{zc:.3f}", [(sx * LX / 2 + sx * 0.0004, LY / 2 - 0.028, zc + 0.0019 * (1 if zc > 0.01 else -1)), (sx * LX / 2 + sx * 0.0004, LY / 2 + 0.0019, zc + 0.0019 * (1 if zc > 0.01 else -1)),
                                          (sx * (LX / 2 - 0.028), LY / 2 + 0.0019, zc + 0.0019 * (1 if zc > 0.01 else -1))], [(0, 1, 2) if (sx > 0) == (zc > 0.01) else (2, 1, 0)], "corner", B)
# 书签带：从书顶中间伸出，垂到桌面
A.poly("Ribbon", [(-0.146, -0.012, 0.030), (-0.146, -0.004, 0.030), (-0.158, -0.004, 0.022), (-0.165, -0.002, 0.004), (-0.195, 0.004, 0.0004),
                  (-0.195, 0.012, 0.0004), (-0.165, 0.006, 0.004), (-0.158, -0.012, 0.022)],
       [(0, 1, 2, 7), (7, 2, 3, 6), (6, 3, 4, 5)], "ribbon", B)
A.solidify(A.scn.objects["Ribbon"], 0.0006)
# 封面纸签：字头朝西（坐在东侧的人看是正的）
img = L.text_img([("交班记录", 1.0, 0.40), ("二〇二六", 0.55, 0.78)], size=(512, 300), font_px=110, rule=False, rot=-1.5, jitter=2)
zt = H + 0.0003
A.label("CoverLabel", [(0.045, -0.035, zt), (0.045, 0.055, zt), (-0.025, 0.055, zt), (-0.025, -0.035, zt)], img, "label", B)
R = {"M_LDG_Cloth": L.lsb_bake.Tiled("PRP_BookCloth_Green"), "M_LDG_Paper": L.lsb_bake.Tiled("PRP_PaperAged"), "M_LDG_Label": L.lsb_bake.Tiled("PRP_PaperAged"),
     "M_LDG_Corner": L.lsb_bake.Tiled("PRP_PlasticDark")}
L.pipeline(A, "PRP_ShiftLedger", R, 2048, OUT, budget="3–5k")

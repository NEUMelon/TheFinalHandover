# 机加工区吊杆搪瓷灯（Mid，0.8k/盏，实例化 18 盏）· 由 AI 辅助工具编写（AI 编写脚本程序化生成）
# 运行：python3 build_PendantLamp.py <输出目录>
# 白盒：灯罩_X{x}_Z{z}（圆柱 Ø0.45 × 0.18，中心 Y 5.5）+ 吊杆（0.02 × 3.5，Y 5.5–9.0）。
# 局部原点 = 灯罩中心 → 世界 (x, 5.5, z)；吊杆向上 3.5 m 到屋架下弦（Y 9.0），顶端一块抱箍。
# 造型（美术圣经 §5：吊杆搪瓷灯罩白炽灯，待核 K-11）：外墙裙绿（降饱和）搪瓷、内白，口沿卷边，罩口沿掉瓷露黑底。
# 部件：Lamp（外罩 + 灯头 + 抱箍 = 贴图集；吊杆 = 纯色 M_LMP_Rod）；LampGlow（罩内白搪瓷面 + 灯泡，材质 M_LMP_Glow_On，亮灭跟灰盒灯罩）。
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_LGT_PendantLamp_v01")
A.mat("enamel", "M_LMP_Enamel", "#4F6B5C", 0.35, 0.0)
A.mat("iron", "M_LMP_Iron", "#3A3D3B", 0.6, 0.6)
A.mat("rod", "M_LMP_Rod", "#2E302E", 0.55, 0.5)   # 吊杆：纯色（3.4 m 长条放进贴图集会把其它部件的像素挤没）
A.mat("glow", "M_LMP_Glow_On", "#F2E6CC", 0.4, 0.0, emit="#FFD9A0", emit_str=4.0)
P = A.part("Lamp", (0, 0, 0))
G = A.part("LampGlow", (0, 0, 0))
SEG = 18
# 外罩：口沿卷边 → 碗身 → 颈
A.lathe("Shade", [(0.219, -0.090), (0.226, -0.092), (0.229, -0.086), (0.208, -0.050), (0.172, -0.010), (0.122, 0.030), (0.078, 0.060), (0.048, 0.076), (0.044, 0.082)], (0, 0, 0), "enamel", P, seg=SEG, normals="out")
# 灯头（铸铁壳）+ 吊杆 + 屋架抱箍
A.lathe("Socket", [(0.044, 0.082), (0.044, 0.125), (0.030, 0.140), (0.011, 0.146)], (0, 0, 0), "iron", P, seg=12, normals="out")
A.cyl("Rod", (0, 0, 0.146 + (3.50 - 0.146) / 2), 0.010, 3.50 - 0.146, 'z', "rod", P, seg=8, cap=False)
A.box("Clamp", (0, 0, 3.49), (0.08, 0.05, 0.03), "iron", P, bevel=0.004)
# 罩内白搪瓷（法线朝下 / 朝内）+ 灯泡
inner = A.lathe("ShadeInner", [(0.040, 0.078), (0.074, 0.056), (0.118, 0.026), (0.168, -0.013), (0.204, -0.052), (0.219, -0.090)], (0, 0, 0), "glow", G, seg=SEG, normals="in")
A.lathe("Bulb", [(0.012, 0.080), (0.014, 0.060), (0.030, 0.035), (0.034, 0.012), (0.026, -0.008), (0.0, -0.016)], (0, 0, 0), "glow", G, seg=10, normals="out")
R = {"M_LMP_Enamel": L.lsb_bake.Painted(os.path.join(L.PTEX, "T_PRP_Paint_A_Mask.png"), "#4F6B5C", "#2A2C2B", [], a1=0.25, a2=0.12, base_w=0.04, edge_k=0.9, floor_k=0.0),
     "M_LMP_Iron": L.lsb_bake.Tiled("LT01_CastIron")}
L.pipeline(A, "LGT_PendantLamp", R, 512, OUT, budget="0.8k/盏")

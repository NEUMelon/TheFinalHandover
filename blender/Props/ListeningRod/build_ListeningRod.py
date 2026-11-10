# 听针（金属长柄听棒 + 木柄，美术圣经 §6；形制待核 K-12）· 由 AI 辅助工具编写（AI 编写脚本程序化生成）
# 运行：python3 build_ListeningRod.py <输出目录>
# 局部原点 = 木柄握持段中心（轴线上）；针沿 +X 伸出，耳贴在 −X 端。平放在工具箱盖上时原点离盖面 17.5 mm（耳贴半径）。
# 部件：ListeningRod（整件，一个部件）。木柄中段用“磨亮木”（老周左手几十年握出来的包浆）。
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_ListeningRod_v01")
A.mat("wood", "M_PRB_Wood", "#7A4E30", 0.45)
A.mat("worn", "M_PRB_WoodWorn", "#9A6A44", 0.25)
A.mat("steel", "M_PRB_Steel", "#9EA3A3", 0.3, 1.0)
A.mat("brass", "M_PRB_Ferrule", "#B8A57A", 0.35, 0.9)
A.mat("ear", "M_PRB_Earpiece", "#2B2826", 0.45)
P = A.part("ListeningRod", (0, 0, 0))
# 耳贴：胶木圆片（贴耳朵那一面略凹）
A.lathe("Earpiece", [(0, -0.002), (0.012, -0.0015), (0.0175, 0.0), (0.0175, 0.006), (0.010, 0.010), (0.0095, 0.014)], (-0.074, 0, 0), "ear", P, seg=20, axis='x')
# 木柄：鼓形，中段握持处（磨亮）
A.lathe("Handle_End", [(0.0095, 0.0), (0.0115, 0.012), (0.0122, 0.030)], (-0.06, 0, 0), "wood", P, seg=16, axis='x')
A.lathe("Handle_Grip", [(0.0122, 0.0), (0.0126, 0.03), (0.0122, 0.06)], (-0.03, 0, 0), "worn", P, seg=16, axis='x')
A.lathe("Handle_Front", [(0.0122, 0.0), (0.0112, 0.02), (0.0100, 0.030)], (0.03, 0, 0), "wood", P, seg=16, axis='x')
A.lathe("Ferrule", [(0.0102, 0.0), (0.0102, 0.016), (0.0060, 0.020), (0.0035, 0.024)], (0.06, 0, 0), "brass", P, seg=16, axis='x')
# 钢针 Ø6，末端收尖（圆头，不扎设备）
A.lathe("Rod", [(0.0030, 0.0), (0.0030, 0.27), (0.0022, 0.295), (0.0012, 0.300), (0, 0.3015)], (0.084, 0, 0), "steel", P, seg=10, axis='x')
R = {"M_PRB_Wood": L.lsb_bake.Tiled("PRP_WoodVarnish"), "M_PRB_WoodWorn": L.lsb_bake.Tiled("PRP_WoodPolished"),
     "M_PRB_Steel": L.lsb_bake.Tiled("LT01_WornBright"), "M_PRB_Ferrule": L.lsb_bake.Tiled("LT01_MachinedSteel"),
     "M_PRB_Earpiece": L.lsb_bake.Tiled("PRP_PlasticDark")}
L.pipeline(A, "PRP_ListeningRod", R, 512, OUT, budget="1k")

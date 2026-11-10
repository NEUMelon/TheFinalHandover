# 抽屉里的盒式磁带 + 单卡录音机（Mid，4k）· 由 AI 辅助工具编写（AI 编写脚本程序化生成）
# 运行：python3 build_TapeRecorder.py <输出目录>
# 白盒：抽屉 DrawerBox (26.55, 0.62, 13.55)（开 = 向东滑 0.3），盒式磁带 局部 (−0.05, 0.04, 0.05) 0.1 × 0.015 × 0.065，单卡录音机 局部 (0.05, 0.06, −0.1) 0.28 × 0.08 × 0.12。
# 局部原点 = 白盒抽屉枢轴 → 世界 (26.55, 0.62, 13.55)；三个部件都跟 DrawerBox 平移（rigid）。交班桌模型的抽屉底板上沿在局部 z −0.06。
# 录音机：1980 年代单卡收录机（待核 K-13），仰面躺在抽屉里——喇叭网、卡带仓朝上，琴键和提手在北侧。无品牌字样。
# 磁带：躺在一沓旧单据上，长边沿南北（CU5 从东往西看时标签是正的），手写标签“广播站　孙桂兰”（ContentDB.Puzzles.cs:79 的原文）。
# 位置微调（交班桌抽屉比白盒抽屉窄 4 cm）：录音机中心北移 1.5 cm；磁带转 90°。
import sys, os, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "_common"))
import props_lib as L
OUT = os.path.abspath(sys.argv[-1])
A = L.Asset("LSB_ENV_PRP_TapeRecorder_v01")
A.mat("plastic", "M_TPR_Plastic", "#26262A", 0.5)
A.mat("alu", "M_TPR_Alu", "#A7A9A6", 0.3, 1.0)
A.mat("grille", "M_TPR_Grille", "#A7A9A6", 0.4, 1.0)
A.mat("chrome", "M_TPR_Chrome", "#C9C6BC", 0.2, 1.0)
A.mat("window", "M_TPR_Window", "#1A1E22", 0.08)
A.mat("dial", "M_TPR_Dial", "#D8D2BE", 0.4)
A.mat("needle", "M_TPR_Needle", "#E8B45A", 0.4)
A.mat("shell", "M_TPR_TapeShell", "#3A3A3C", 0.35)
A.mat("tapewin", "M_TPR_TapeWindow", "#5A5E60", 0.1)
A.mat("reel", "M_TPR_TapeReel", "#3B2E25", 0.5)
A.mat("label", "M_TPR_Label", "#D9CFB4", 0.85)
A.mat("paper", "M_TPR_Paper", "#D9CFB4", 0.85)
Z0 = -0.06                          # 抽屉底板上沿（局部）
# ================= 录音机
RX, RY = 0.05, -0.085
R_ = A.part("Recorder", (RX, RY, Z0 + 0.04))
W, Dp, Hh = 0.28, 0.12, 0.08         # 宽（X）、高（立起来时，这里沿 Y）、厚（躺着时的高度，沿 Z）
A.box("RecBody", (RX, RY, Z0 + Hh / 2), (W, Dp, Hh - 0.004), "plastic", R_, bevel=0.008, seg=3)
A.box("RecFace", (RX, RY - 0.004, Z0 + Hh - 0.0015), (W - 0.02, Dp - 0.028, 0.003), "alu", R_, bevel=0.001)
A.lathe("RecSpeaker", [(0, 0.0), (0.044, 0.0), (0.047, -0.002)], (RX - 0.075, RY - 0.004, Z0 + Hh + 0.0002), "grille", R_, seg=32, close_top=False, normals="out")
A.lathe("RecSpeakerRim", [(0.042, -0.0005), (0.045, 0.0018), (0.052, 0.0018), (0.054, -0.002)], (RX - 0.075, RY - 0.004, Z0 + Hh), "chrome", R_, seg=32, normals="out")
# 卡带仓门：框 + 茶色窗
A.box("RecDoor", (RX + 0.06, RY - 0.004, Z0 + Hh + 0.0005), (0.115, 0.075, 0.003), "plastic", R_, bevel=0.0015)
A.box("RecDoorWin", (RX + 0.06, RY - 0.004, Z0 + Hh + 0.0021), (0.085, 0.034, 0.0006), "window", R_)
# 调谐刻度窗 + 指针、两个旋钮（音量 / 调谐）
A.box("RecDial", (RX + 0.0, RY - 0.04, Z0 + Hh + 0.0005), (0.24, 0.012, 0.002), "dial", R_)
A.box("RecNeedle", (RX + 0.035, RY - 0.04, Z0 + Hh + 0.0017), (0.0015, 0.013, 0.0006), "needle", R_)
for i, x in enumerate((RX + 0.125, RX - 0.125)):
    A.cyl(f"RecKnob{i}", (x, RY + 0.005, Z0 + Hh - 0.03), 0.011, 0.012, 'z', "plastic", R_, seg=16).location.z += 0  # 侧面拨轮露半个
# 北侧：6 个琴键（录、放、倒、快进、停、暂停）+ 折叠提手
for i in range(6):
    A.box(f"RecKey{i}", (RX + 0.03 + i * 0.019, RY + Dp / 2 + 0.004, Z0 + Hh * 0.62), (0.016, 0.012, 0.022), "chrome" if i == 1 else "plastic", R_, bevel=0.0015)
A.tube("RecHandle", [(RX - 0.12, RY + Dp / 2 - 0.002, Z0 + 0.05), (RX - 0.115, RY + Dp / 2 + 0.012, Z0 + 0.05), (RX - 0.02, RY + Dp / 2 + 0.014, Z0 + 0.05), (RX + 0.0, RY + Dp / 2 + 0.002, Z0 + 0.05)], 0.004, "chrome", R_, seg=8)
for x in (RX - 0.11, RX + 0.11):
    A.box(f"RecFoot{x:.2f}", (x, RY, Z0 + 0.001), (0.02, 0.06, 0.002), "plastic", R_)
# ================= 旧单据一沓（磁带底下；“抽屉里有些旧东西”）
C = A.part("DrawerClutter", (-0.05, 0.05, Z0))
for i in range(5):
    A.box(f"Paper{i}", (-0.05 + 0.004 * math.sin(i * 2.1), 0.05 + 0.003 * math.cos(i * 1.7), Z0 + 0.002 + i * 0.0042), (0.13 - i * 0.004, 0.095 + (i % 2) * 0.004, 0.0038), "paper", C, bevel=0.0012)
# ================= 磁带（长边沿 Y）
TZ = Z0 + 0.021 + 0.006
T = A.part("Tape", (-0.05, 0.05, TZ))
A.box("TapeShell", (-0.05, 0.05, TZ), (0.0635, 0.1005, 0.012), "shell", T, bevel=0.0015, seg=2)
A.box("TapeWindow", (-0.040, 0.05, TZ + 0.0061), (0.013, 0.040, 0.0004), "tapewin", T)
for dy in (-0.021, 0.021):
    A.cyl(f"TapeReel{dy}", (-0.040, 0.05 + dy, TZ + 0.0063), 0.0055, 0.0004, 'z', "reel", T, seg=12)
# 手写标签（字头朝西：坐在东侧 / CU5 机位看是正的）
img = L.text_img([("广播站　孙桂兰", 1.0, 0.55)], size=(800, 230), font_px=96, rule=True, rot=-2.0, jitter=3, ink=(30, 40, 90, 255))
zt = TZ + 0.0062
# 标签：卡带上半（西侧）一条，窗口和两个卷盘孔在它下面（东侧）
A.label("TapeLabel", [(-0.0545, 0.010, zt), (-0.0545, 0.090, zt), (-0.0775, 0.090, zt), (-0.0775, 0.010, zt)], img, "label", T)
R = {"M_TPR_Plastic": L.lsb_bake.Tiled("PRP_PlasticDark"), "M_TPR_Alu": L.lsb_bake.Tiled("PRP_BrushedAlu"), "M_TPR_Grille": L.lsb_bake.Tiled("PRP_SpeakerGrille"),
     "M_TPR_Chrome": L.lsb_bake.Tiled("LT01_WornBright"), "M_TPR_TapeShell": L.lsb_bake.Tiled("PRP_PlasticDark"),
     "M_TPR_Label": L.lsb_bake.Tiled("PRP_PaperAged"), "M_TPR_Paper": L.lsb_bake.Tiled("PRP_PaperAged")}
L.pipeline(A, "PRP_TapeRecorder", R, 2048, OUT, budget="4k")

# 《最后一次交班》Stage 3 · ENV 共用数据（贴图生成与建模脚本共用）。由 Claude 编写（AI 辅助，已登记 AI 使用日志）。
# 坐标：Blender X = 世界东（Unity X），Blender Y = 世界北（Unity Z），Blender Z = 上（Unity Y）；原点 = 车间西南角室内地面。

# 水磨石：一张平铺贴图 = 2 m × 2 m（2048 px，1024 px/m），四个 1 m 象限各是一块“分格”。
# 地面每个 1 m 分格随机取一个象限 + 8 种旋转 / 翻转之一（分格条把分格隔开，所以内容不连续也自然），避免 2 m 周期重复。
TERRAZZO_TILE_M = 2.0

def cell_variant(i, j):
    """分格 (i, j)（i = floor(X), j = floor(Y)）→ (象限 qx, qy, 方向 o)。整数哈希，贴图脚本和建模脚本结果一致。"""
    h = (i * 73856093) ^ (j * 19349663) ^ 0x5bd1e995
    h = (h * 2654435761) & 0xFFFFFFFF
    h ^= h >> 15
    q = h % 4; o = (h >> 4) % 8
    return q % 2, q // 2, o

def orient(s, t, o):
    """分格内局部坐标 (s, t) ∈ [0,1]² 按方向 o 变换（bit0 交换，bit1 翻 s，bit2 翻 t）。"""
    if o & 1: s, t = t, s
    if o & 2: s = 1 - s
    if o & 4: t = 1 - t
    return s, t

def cell_tile_uv(i, j, s, t):
    """分格 (i, j) 内 (s, t) → 平铺贴图 UV（0..1 = 2 m 贴图）。"""
    qx, qy, o = cell_variant(i, j)
    s2, t2 = orient(s, t, o)
    return (qx + s2) / 2.0, (qy + t2) / 2.0

# 机床脚下的油污分格：用单独的 2048² 烘焙贴图集（地面这些分格换成 M_LSB_ENV_BLD_FloorStain_Atlas）。
# (名字, 分格 X0, Y0, 宽 W, 高 H（米，整数）, 贴图集区域 px0, py0, 区域宽, 区域高)；py 从贴图底边（V=0）算起。
STAIN_ATLAS = 2048
STAIN_GUTTER = 4          # 区域四周各留 4 px，防 mip 串色
STAIN_PADS = [
    ("LT01",    7, 10, 4, 2,    0, 1024, 2048, 1024),   # Hero：512 px/m
    ("LT02",    1, 10, 4, 2,    0,    0, 1024,  512),   # 256 px/m
    ("LT03",   13, 10, 4, 2, 1024,    0, 1024,  512),
    ("LT04",   19, 10, 4, 2,    0,  512, 1024,  512),
    ("Drill",  15,  2, 2, 2, 1024,  512,  512,  512),
    ("Grinder",18,  2, 2, 1, 1536,  512,  512,  256),
]

def pad_uv(pad, x, y):
    """世界 (x, y)（在该分格区内）→ 贴图集 UV。"""
    _, X0, Y0, W, H, px, py, rw, rh = pad
    g = STAIN_GUTTER
    u = (px + g + (x - X0) / W * (rw - 2 * g)) / STAIN_ATLAS
    v = (py + g + (y - Y0) / H * (rh - 2 * g)) / STAIN_ATLAS
    return u, v

def pad_of_cell(i, j):
    for p in STAIN_PADS:
        if p[1] <= i < p[1] + p[3] and p[2] <= j < p[2] + p[4]: return p
    return None

# 车床：西端 x0（= cx − 1.4），轴线 Y 11.0（白盒规格 §4；LT01 x0 = 8.0）
LATHES = {"LT01": 8.0, "LT02": 2.0, "LT03": 14.0, "LT04": 20.0}
LATHE_Y = 11.0

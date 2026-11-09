// 《最后一次交班》Demo α —— 时频图贴图（谜题二）
// 用 SpectroModel.Energy 逐像素画 760×340 的示意时频图，颜色只用衡的冷白单色（C-Sys-BG → C-Sys），不用彩虹色谱（UI-DS §5.11）。
// ⚠ 模拟 / 占位数据：程序合成，不是 FFT，也不是任何设备的实测。
using System.Collections.Generic;
using LSB.Core;
using UnityEngine;

namespace LSB.Unity
{
    public static class SpectroTexture
    {
        public const int W = 760, H = 340;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string point)
        {
            if (cache.TryGetValue(point, out var tex) && tex != null) return tex;
            tex = new Texture2D(W, H, TextureFormat.RGB24, false) { name = "LSB_占位_时频图_" + point, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            var rnd = SpectroModel.Seeded(point);
            var bg = new Color(14 / 255f, 22 / 255f, 32 / 255f);
            var fg = new Color(230 / 255f, 240 / 255f, 245 / 255f);
            // 原型按列（时间）外循环、行（频率）内循环取随机数；这里保持同样顺序，画出来和原型一致
            for (int x = 0; x < W; x++)
            {
                float t = x / (float)W * SpectroModel.Duration;
                for (int y = 0; y < H; y++)
                {
                    float f = (1 - y / (float)H) * SpectroModel.FMax;
                    float k = SpectroModel.Energy(point, t, f, rnd);
                    if (y % 2 == 0) k *= 0.84f;                       // 扫描线（2px 间隔）
                    var c = Color.Lerp(bg, fg, k);
                    px[(H - 1 - y) * W + x] = c;                      // Texture2D 原点在左下
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            cache[point] = tex;
            return tex;
        }
    }
}

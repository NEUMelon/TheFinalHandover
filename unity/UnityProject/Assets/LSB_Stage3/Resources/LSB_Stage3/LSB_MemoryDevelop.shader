// 《最后一次交班》Stage 3 · 记忆显影（URP 14 / Unity 2022.3，Unlit 透明，预乘混合）  v02
// 与 40_特效/stage3_记忆显影/LSB_VFX_MemoryDevelop_LookDev_v01.html 的片元着色器是同一套算法：
//   45° AM 网点格；每格一个聚合门限、一个溶解门限；点面积 ∝ 遮罩浓度（半径 ∝ √浓度）；
//   刀刻线按像素门限显出；光锥（圆形，边缘柔化）外不出现；溶解时网点缩小、上浮、散去。
// 遮罩贴图 _Mask：R = 剪影浓度，G = 刀刻线。正式遮罩由团队手绘（§8.4），仓库里的是占位。
// 用法：画在相机前一块铺满视野的四边形上（MemoryDevelopFx.cs），不是 UI 材质（UI Toolkit 不能挂自定义 Shader）。
//   _ConeRect = (光锥中心 u, 光锥中心 v, 光锥直径占视野高度的比例, 视野宽高比)，由 C# 按 UI 里 .mem-cone 的位置每帧给。
//   _Dim = 光锥外压暗（原来由 UI 的 .memory 半透明黑承担；UI 盖在 3D 之上会把网点也压暗 42%，所以挪到这里，画在网点下面）。
// v02 改动（2026-10-15，审查记录见 40_特效/stage3_记忆显影/接入说明.md §3）：
//   ① 辅助函数加 LSB_ 前缀：URP Core.hlsl → core Common.hlsl → Random.hlsl 已有 float Hash(uint)，同名重载能编但容易误调；
//   ② 整数字面量改浮点；mask 采样坐标 saturate（PNG 默认 Repeat 寻址，光锥边缘不卷到对边）；
//   ③ 四边形从“光锥大小、居中”改为“铺满视野 + _ConeRect 定位”，与 UI 光锥（中心偏左）对齐；加 _Dim。
// 由 AI 辅助工具编写，记入 AI 使用日志；v02 已用 DXC 1.8 按 D3D11 / Vulkan / Metal 宏对 URP 14.0.12 头文件编过，
// 但没有在 Unity 里编译过（Unity 在 Windows D3D11 上用 FXC），以本机 Unity 2022.3 Console 为准。
Shader "LSB/MemoryDevelop"
{
    Properties
    {
        _Mask ("遮罩 (R 剪影 / G 刀刻线)", 2D) = "black" {}
        _Ink ("网点颜色", Color) = (0.9098, 0.7059, 0.3529, 1)
        _Agg ("聚合 0-1", Range(0, 1)) = 0
        _Dis ("溶解 0-1", Range(0, 1)) = 0
        _Cells ("光锥直径内格数", Range(16, 80)) = 40
        _ConeSoft ("锥边柔化", Range(0.01, 0.5)) = 0.15
        _GrainPx ("颗粒密度（光锥每边像素）", Float) = 640
        _GrainAmt ("颗粒强度", Range(0, 0.3)) = 0.10
        _ConeRect ("光锥 (中心u, 中心v, 直径/视野高, 宽高比)", Vector) = (0.5, 0.5, 0.6667, 1.7778)
        _Dim ("光锥外压暗", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "MemoryDevelop"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            // SRP Batcher：材质的全部属性都放在 UnityPerMaterial 里，不得遗漏
            CBUFFER_START(UnityPerMaterial)
                float4 _Mask_ST;
                half4 _Ink;
                float _Agg;
                float _Dis;
                float _Cells;
                float _ConeSoft;
                float _GrainPx;
                float _GrainAmt;
                float4 _ConeRect;
                float _Dim;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            static const float LSB_S45 = 0.70710678;
            float  LSB_Hash (float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float2 LSB_Rot  (float2 p) { return float2(LSB_S45 * p.x - LSB_S45 * p.y, LSB_S45 * p.x + LSB_S45 * p.y); }
            float2 LSB_IRot (float2 p) { return float2(LSB_S45 * p.x + LSB_S45 * p.y, -LSB_S45 * p.x + LSB_S45 * p.y); }
            // c：光锥坐标（0–1 正方形，光锥 = 内切圆）
            float  LSB_Cone (float2 c) { return 1.0 - smoothstep(0.5 * (1.0 - _ConeSoft), 0.5, length(c - 0.5)); }
            float  LSB_Env  (float agg, float dis)
            {
                return smoothstep(0.0, 0.3, agg) * (1.0 - smoothstep(0.0, 1.0, agg)) + smoothstep(0.0, 0.4, dis) * (1.0 - smoothstep(0.4, 1.0, dis));
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 视野 uv → 光锥坐标：以光锥中心为原点，横向乘宽高比（按视野高度归一），再除以直径
                float diam = max(_ConeRect.z, 1e-3);
                float2 c = (i.uv - _ConeRect.xy) * float2(_ConeRect.w, 1.0) / diam + 0.5;

                float cellSize = 1.0 / _Cells;
                float2 q = LSB_Rot(c / cellSize);
                float2 id = floor(q);
                float2 f = frac(q) - 0.5;
                float h1 = LSB_Hash(id), h2 = LSB_Hash(id + 17.31), h3 = LSB_Hash(id + 41.7);

                float2 cUv = LSB_IRot(id + 0.5) * cellSize;
                float dens = SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, saturate(cUv), 0.0).r * LSB_Cone(cUv);

                float a = smoothstep(h1 * 0.75, h1 * 0.75 + 0.25, _Agg);
                float k = smoothstep(h2 * 0.65, h2 * 0.65 + 0.35, _Dis);
                float r = 0.5 * sqrt(dens) * 1.08 * a * (1.0 - k);
                float2 drift = float2(h3 - 0.5, 0.6 + 0.6 * h2) * k * 0.9;
                float d = length(f - LSB_IRot(drift));
                float dotA = (1.0 - smoothstep(r - 0.07, r + 0.07, d)) * step(0.02, r);

                float cone = LSB_Cone(c);
                float g = SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, saturate(c), 0.0).g * cone;
                float2 pix = floor(c * _GrainPx);
                float n1 = LSB_Hash(pix), n2 = LSB_Hash(pix + 7.7);
                float lineA = g * smoothstep(n1 * 0.8, n1 * 0.8 + 0.2, _Agg) * (1.0 - smoothstep(n2 * 0.7, n2 * 0.7 + 0.3, _Dis));

                float gr = LSB_Hash(floor(c * _GrainPx * 1.3) + floor(_Time.y * 24.0));
                float grainAmt = cone * (0.03 + _GrainAmt * LSB_Env(_Agg, _Dis));
                float grain = step(1.0 - grainAmt, gr) * 0.55;

                float alpha = saturate(max(max(dotA, lineA), grain));
                // 网点（预乘暖黄）盖在压暗层上：压暗只在光锥外，锥内保持 2026 夜景原亮度（美术圣经关键帧 B）
                float dim = saturate(_Dim) * (1.0 - cone);
                return half4(_Ink.rgb * alpha, alpha + dim * (1.0 - alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}

// 《最后一次交班》Stage 3 · 余辉外壳（URP 14，Unlit 加色混合）
// 熄灭的灯罩 / 交接灯外面套一层略放大的同形外壳，用加色把“灯丝余辉”叠在 Demo α 换上的灭灯材质之上：
// 不改原渲染体的材质，所以 WorldPresenter.SetRow / SetLamp 换 sharedMaterial、Stage3ArtBinder 切模型亮 / 灭材质都不受影响。
// 颜色由 LightsOutAfterglowFx 每帧通过 MaterialPropertyBlock 写 _Color（HDR，已乘亮度），黑色 = 不可见。
// 与 40_特效/stage3_交接灯熄灭/LSB_VFX_LightsOut_LookDev_v01.html 的余辉曲线是同一套。
// 由 AI 辅助工具编写，记入 AI 使用日志；未在 Unity 里编译过，以本机 Unity 2022.3 为准。
Shader "LSB/AfterglowShell"
{
    Properties
    {
        [HDR] _Color ("余辉颜色（已乘亮度）", Color) = (0, 0, 0, 1)
        _Rim ("掠射角减弱", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "AfterglowShell"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Rim;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 正对相机的面最亮，侧面略暗（灯丝的光透过搪瓷 / 灯玻璃，边缘薄）
                half ndv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half k = lerp(1.0h, ndv, _Rim);
                return half4(_Color.rgb * k, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

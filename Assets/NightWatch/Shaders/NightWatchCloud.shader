// Low-poly clouds: flat faces, lit by the sun or the moon colour on one side and the sky colour
// on the other, a little hazed toward the horizon colour. No fog (they are far beyond it) and
// no shadows. They draw after the opaque pass, so they need no depth pass of their own.
// DayNightCycle sets the colours and the light direction every frame.
Shader "NightWatch/Cloud"
{
    Properties
    {
        _LitColor ("Lit side", Color) = (0.22, 0.25, 0.33, 1)
        _ShadeColor ("Shaded side", Color) = (0.05, 0.06, 0.10, 1)
        _HazeColor ("Haze (horizon colour)", Color) = (0.05, 0.07, 0.13, 1)
        _Haze ("Haze amount", Range(0, 1)) = 0.25
        _LightDir ("Direction to the light", Vector) = (0.3, 0.7, -0.6, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-50" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LitColor, _ShadeColor, _HazeColor;
                float4 _LightDir;
                half _Haze;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float k = saturate(dot(n, normalize(_LightDir.xyz)) * 0.5 + 0.5);
                float3 col = lerp(_ShadeColor.rgb, _LitColor.rgb, k * k);
                // Flat bottoms are a little darker, as real cumulus are.
                col *= lerp(0.8, 1.0, saturate(n.y * 0.5 + 0.5));
                col = lerp(col, _HazeColor.rgb, _Haze);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

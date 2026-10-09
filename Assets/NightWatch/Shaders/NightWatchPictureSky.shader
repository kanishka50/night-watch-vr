// The Night Watch sky from pictures: two painted panoramas (Screaming Brain Studios, CC0), faded
// from one to the next as the time of day moves, each turned so the sun or moon in the picture
// sits where our sun or moon light comes from. Our twinkling stars are drawn on top at night.
// DayNightCycle sets the pictures, the fade, the turns and the brightness every frame.
Shader "NightWatch/PictureSky"
{
    Properties
    {
        _SkyA ("Picture A (panorama)", 2D) = "black" {}
        _SkyB ("Picture B (panorama)", 2D) = "black" {}
        _Blend ("A to B (0 to 1)", Range(0, 1)) = 0
        _TurnA ("Turn of A (degrees)", Float) = 0
        _TurnB ("Turn of B (degrees)", Float) = 0
        _Exposure ("Brightness", Range(0, 2)) = 1
        _GroundColor ("Below the horizon", Color) = (0.03, 0.04, 0.08, 1)
        _Stars ("Stars (0 to 1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyA);
            SAMPLER(sampler_SkyA);
            TEXTURE2D(_SkyB);
            SAMPLER(sampler_SkyB);

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyA_ST, _SkyB_ST;
                half4 _GroundColor;
                float _Blend, _TurnA, _TurnB;
                half _Exposure, _Stars;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // A panorama read in a direction. Bearing 0 = north (+z), 90 = east (+x); a turn moves
            // the picture round the horizon. Read without mipmaps, so the wrap seam stays clean.
            half3 Panorama(TEXTURE2D_PARAM(tex, samp), float3 d, float turn)
            {
                float bearing = degrees(atan2(d.x, d.z));
                float u = frac((bearing + turn) / 360.0);
                float v = 0.5 + asin(clamp(d.y, -1.0, 1.0)) / PI;
                return SAMPLE_TEXTURE2D_LOD(tex, samp, float2(u, v), 0).rgb;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float y = d.y;

                half3 a = Panorama(TEXTURE2D_ARGS(_SkyA, sampler_SkyA), d, _TurnA);
                half3 b = Panorama(TEXTURE2D_ARGS(_SkyB, sampler_SkyB), d, _TurnB);
                half3 sky = lerp(a, b, _Blend) * _Exposure;

                // Stars, as in NightWatch/Sky: small, twinkling, fading near the horizon, and only
                // where the picture is dark (not over its moon or bright clouds).
                float3 p = d * 220.0;
                float3 cell = floor(p);
                float h = Hash13(cell);
                float3 spot = Hash33(cell) * 0.6 + 0.2;
                float dist = length(frac(p) - spot);
                float w = clamp(fwidth(dist), 0.03, 0.08);
                float star = 1.0 - smoothstep(0.12 - w, 0.12 + w, dist);
                star *= step(0.975, h) * (0.35 + 0.65 * (h - 0.975) / 0.025);
                star *= 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 4.0) + h * 60.0);
                float3 starColor = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.92, 0.8), frac(h * 37.0));
                float dark = 1.0 - saturate(dot(sky, float3(0.3, 0.5, 0.2)) * 3.0);
                sky += starColor * star * _Stars * saturate(y * 5.0) * dark;

                // Below the horizon: the fogged land's colour (the pictures mirror the sky there).
                sky = lerp(sky, _GroundColor.rgb, saturate(-y * 8.0));
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

// The Night Watch sky: a zenith-to-horizon gradient, a warm glow on the sun's side of the
// horizon, a sun disc, a moon disc with a halo and twinkling stars. DayNightCycle sets the
// colours and directions every frame; the defaults below are the night look (for editing).
Shader "NightWatch/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith (top of the sky)", Color) = (0.015, 0.025, 0.07, 1)
        _HorizonColor ("Horizon", Color) = (0.05, 0.07, 0.13, 1)
        _GroundColor ("Below the horizon", Color) = (0.03, 0.04, 0.08, 1)
        _SunDir ("Direction to the sun", Vector) = (0.97, -0.03, -0.14, 0)
        _SunColor ("Sun colour", Color) = (1, 0.55, 0.3, 1)
        _SunGlow ("Sun glow (0 to 1.5)", Range(0, 1.5)) = 0
        _MoonDir ("Direction to the moon", Vector) = (0.3, 0.7, -0.6, 0)
        _MoonColor ("Moon colour", Color) = (0.85, 0.9, 1, 1)
        _MoonShow ("Moon (0 to 1)", Range(0, 1)) = 1
        _Stars ("Stars (0 to 1)", Range(0, 1)) = 1
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

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _MoonColor;
                float4 _SunDir, _MoonDir;
                half _SunGlow, _MoonShow, _Stars;
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
                // The skybox mesh is centred on the camera, so its vertex is the view direction.
                o.dir = v.positionOS.xyz;
                return o;
            }

            // Hashes without sine (Dave Hoskins), steady on every GPU.
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

            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash13(float3(i, 1.0));
                float b = Hash13(float3(i + float2(1, 0), 1.0));
                float c = Hash13(float3(i + float2(0, 1), 1.0));
                float d = Hash13(float3(i + float2(1, 1), 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float y = d.y;

                // The gradient: horizon colour low down, zenith colour overhead.
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(y), 0.45));

                // The sun: a warm band along the horizon on its side, a soft glow and a disc.
                float3 sunDir = normalize(_SunDir.xyz);
                float sd = dot(d, sunDir);
                float2 flatD = normalize(d.xz + 1e-5);
                float2 flatSun = normalize(sunDir.xz + 1e-5);
                float side = saturate(dot(flatD, flatSun) * 0.5 + 0.5);
                float band = exp(-max(y, 0.0) * 5.0);
                float glow = pow(side, 3.0) * band * 0.6 + pow(saturate(sd), 12.0) * 0.4 + pow(saturate(sd), 300.0) * 1.2;
                sky += _SunColor.rgb * _SunGlow * glow;
                float sunDisc = smoothstep(0.99955, 0.99972, sd) * saturate(_SunGlow * 4.0) * step(-0.02, y);
                sky = lerp(sky, _SunColor.rgb * 2.5 + 0.5, sunDisc);

                // Stars: one in about 40 cells of a fine grid on the sky, each twinkling at its own speed.
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

                // The moon: a pale disc with soft darker patches (its seas), and a halo.
                float3 moonDir = normalize(_MoonDir.xyz);
                float md = dot(d, moonDir);
                float3 right = normalize(cross(moonDir, float3(0, 1, 0)) + float3(1e-5, 0, 0));
                float3 upv = cross(right, moonDir);
                float2 uv = float2(dot(d, right), dot(d, upv)) / 0.0349;   // 2 degrees = 1
                float seas = Noise2(uv * 2.2 + 3.0) * 0.6 + Noise2(uv * 5.0 + 7.0) * 0.4;
                float moonDisc = smoothstep(0.99925, 0.99942, md) * step(-0.02, y);
                float3 moonFace = _MoonColor.rgb * (1.15 - 0.35 * smoothstep(0.45, 0.75, seas));
                float halo = pow(saturate(md), 400.0) * 0.25 + pow(saturate(md), 30.0) * 0.06;
                sky += _MoonColor.rgb * halo * _MoonShow;

                // Stars fade near the horizon (haze) and beside the moon.
                float starMask = saturate(y * 5.0) * (1.0 - saturate(pow(saturate(md), 60.0) * 2.0)) * (1.0 - moonDisc);
                sky += starColor * star * _Stars * starMask;
                sky = lerp(sky, moonFace, moonDisc * _MoonShow);

                // Below the horizon: blend into the ground colour (the fogged land meets it there).
                sky = lerp(sky, _GroundColor.rgb, saturate(-y * 8.0));
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

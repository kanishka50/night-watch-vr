// Night Watch water: the tank, the flooded paddy plots and the canal.
//  - two ripple normal maps (WaterSetup makes the picture in code) drift in different directions,
//    or both run along the canal, so the surface moves;
//  - looking down you see the water's own deep colour, looking across you see the sky mirrored
//    (fresnel: water reflects more at a low angle);
//  - the sky comes from the sky reflection probe (DayNightCycle shares it as _NightWatchSkyCube);
//  - a bright glint of the sun by day and a silver moon path at night (the main light);
//  - Unity's fog, like everything else.
// The mesh UVs are in metres (WaterSetup / CanalBuilder): world x and z for still water, across
// and along the channel for the canal, so "flow" means along the mesh's v.
Shader "NightWatch/Water"
{
    Properties
    {
        _DeepColor ("Water colour (seen from above)", Color) = (0.05, 0.13, 0.14, 1)
        [NoScaleOffset][Normal] _RippleMap ("Ripple normals (tiling)", 2D) = "bump" {}
        _RippleSize ("Ripple size (metres per picture)", Float) = 5
        _RippleStrength ("Ripple strength", Range(0, 1.5)) = 0.5
        _FlowDir ("Drift direction (u, v)", Vector) = (1, 0.35, 0, 0)
        _FlowSpeed ("Drift speed (metres a second)", Float) = 0.08
        _CrossDrift ("Second ripples cross at an angle (0 = same way)", Range(0, 1)) = 1
        _Reflection ("Sky reflection", Range(0, 1)) = 0.9
        _Glint ("Sun and moon glint", Range(0, 4)) = 1.5
        _Murk ("Light scattered in the water (muddy = more)", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            float4 _FlowDir;
            float _RippleSize, _RippleStrength, _FlowSpeed, _CrossDrift;
            half _Reflection, _Glint, _Murk;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_RippleMap);
            SAMPLER(sampler_RippleMap);
            // The sky reflection probe's picture, shared by DayNightCycle (unset outside Play).
            TEXTURECUBE(_NightWatchSkyCube);
            SAMPLER(sampler_NightWatchSkyCube);
            float _NightWatchSkyCubeOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.uv = v.uv;
                o.normalWS = nrm.normalWS;
                o.tangentWS = half4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.fog = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half3 Sky(half3 dir)
            {
                if (_NightWatchSkyCubeOn > 0.5)
                    return SAMPLE_TEXTURECUBE_LOD(_NightWatchSkyCube, sampler_NightWatchSkyCube, dir, 0).rgb;
                return GlossyEnvironmentReflection(dir, 0.05h, 1.0h);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Two ripple layers: one drifting along _FlowDir, one smaller and faster, turned
                // across it (still water) or running the same way (the canal).
                float t = _Time.y * _FlowSpeed;
                float2 dir = normalize(_FlowDir.xy + 1e-5);
                float2 across = float2(-dir.y, dir.x);
                float2 dir2 = normalize(lerp(dir, dir * 0.6 + across * 0.8, _CrossDrift));
                float2 uv1 = i.uv / _RippleSize - dir * t / _RippleSize;
                float2 uv2 = i.uv / (_RippleSize * 0.57) - dir2 * t * 1.6 / (_RippleSize * 0.57) + 0.37;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uv1), _RippleStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uv2), _RippleStrength * 0.8);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));

                // Calmer far away (the ripples blur into one smooth sheet, as on a real lake).
                float dist = distance(i.positionWS, GetCameraPositionWS());
                nTS = normalize(lerp(nTS, half3(0, 0, 1), saturate(dist / 120.0)));

                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 n = normalize(nTS.x * i.tangentWS.xyz + nTS.y * bitangent + nTS.z * i.normalWS);

                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half nv = saturate(dot(n, view));
                half fresnel = 0.03 + 0.97 * pow(1.0 - nv, 5.0);
                fresnel = saturate(fresnel * _Reflection * 1.4);

                // The sky in the water; a flat lake never mirrors the ground under the horizon.
                half3 r = reflect(-view, n);
                r.y = abs(r.y);
                half3 sky = Sky(r);

                // The water's own colour: lit a little by the sky and by the sun or moon.
                Light light = GetMainLight();
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 body = _DeepColor.rgb * (ambient + light.color * saturate(light.direction.y) * (0.25 + 0.5 * _Murk));

                // The glint: a sharp highlight on each ripple facing the light (a path of sparkles).
                half3 h = normalize(light.direction + view);
                half nh = saturate(dot(n, h));
                half glint = pow(nh, 600.0) * 6.0 + pow(nh, 90.0) * 0.25;
                glint *= _Glint * saturate(light.direction.y * 8.0);

                half3 colour = lerp(body, sky, fresnel) + light.color * glint;
                colour = MixFog(colour, i.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        // Depth only (for the depth pre-pass, if a later setting turns one on).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // Depth and normals (for screen-space effects, if a later setting turns one on).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

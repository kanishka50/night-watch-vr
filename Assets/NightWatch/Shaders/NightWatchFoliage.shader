// Night Watch foliage: the village's plants (rice, grass, reeds, shrubs, bananas, palms, trees)
// moving in the wind. It draws like the glTF material it replaces (same property names, so the
// texture, colour, cut-out and two-sided setting carry over), lit by the sun or moon, the sky and
// the torches and fires (URP lights), with fog.
// The wind bends each plant from its foot: nothing at the ground, most at the top. A slow wave
// rolls across the land (whole fields of rice lean together), gusts come through now and then,
// and leaves flutter a little. WindDriver sets the wind for the whole scene (_NightWatchWind);
// each material says how much its plant bends (VillageBuilder sets it per model).
Shader "NightWatch/Foliage"
{
    Properties
    {
        baseColorTexture ("Base colour texture", 2D) = "white" {}
        baseColorFactor ("Base colour", Color) = (1, 1, 1, 1)
        alphaCutoff ("Cut-out threshold", Range(0, 1)) = 0.5
        roughnessFactor ("Roughness", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (0 = both sides)", Float) = 0
        [Toggle] _AlphaToMask ("Alpha to coverage", Float) = 0

        _WindBend ("Lean at the top (share of the plant's height)", Range(0, 0.3)) = 0.08
        _WindFlutter ("Leaf flutter", Range(0, 1)) = 0.3
        _WindRowY ("Part to model: y row (set by the builder)", Vector) = (0, 1, 0, 0)
        _ModelBase ("Model foot (y, model space)", Float) = 0
        _ModelHeight ("Model height (model space)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 baseColorTexture_ST;
            half4 baseColorFactor;
            half alphaCutoff, roughnessFactor;
            float _WindBend, _WindFlutter, _ModelBase, _ModelHeight;
            float4 _WindRowY;
        CBUFFER_END

        TEXTURE2D(baseColorTexture);
        SAMPLER(samplerbaseColorTexture);

        // The scene's wind (WindDriver): xy = direction it blows toward (world x, z),
        // z = strength (1 = a steady breeze), w = 1 when set (otherwise a default breeze).
        float4 _NightWatchWind;

        // Moves one vertex with the wind. positionOS is the part's own point; the part's matrix
        // (with the copy's place, turn and size) is UNITY_MATRIX_M.
        float3 Wind(float3 positionOS, float3 positionWS)
        {
            float2 dir = _NightWatchWind.w > 0.5 ? normalize(_NightWatchWind.xy + 1e-5) : normalize(float2(-1, -1));
            float strength = _NightWatchWind.w > 0.5 ? _NightWatchWind.z : 1.0;

            // How high this point is on its plant (0 at the foot, 1 at the top).
            float modelY = dot(_WindRowY, float4(positionOS, 1.0));
            float up = saturate((modelY - _ModelBase) / max(_ModelHeight, 1e-4));
            float bend = up * up;

            // The plant's height in the world (copies can be scaled).
            float scaleY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            float height = _ModelHeight * scaleY * length(_WindRowY.xyz);

            // A wave rolling across the land, a slower second one, and gusts now and then.
            float2 foot = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23);
            float t = _Time.y;
            float along = dot(foot, dir);
            float wave = 0.5 + 0.5 * sin(along * 0.35 - t * 1.6);
            float swell = 0.5 + 0.5 * sin(along * 0.11 + foot.x * 0.05 - t * 0.55);
            float gust = pow(saturate(sin(along * 0.045 - t * 0.33) * 0.5 + 0.5), 6.0);
            float lean = (0.25 + 0.45 * wave * swell + 0.9 * gust) * strength;

            float3 offset = float3(dir.x, 0, dir.y) * (lean * _WindBend * height * bend);
            // Leaves shiver: a small fast movement that differs from point to point.
            float shiver = sin(t * 7.0 + dot(positionWS, float3(3.1, 2.3, 2.7))) * 0.5
                         + sin(t * 11.0 + dot(positionWS, float3(1.7, 3.3, 1.1))) * 0.5;
            offset += float3(dir.y, 0.6, -dir.x) * (shiver * _WindFlutter * 0.04 * up * min(height, 3.0) * (0.4 + strength * 0.6));
            // Bending lowers the top a little (keeps the stems about the same length).
            offset.y -= dot(offset.xz, offset.xz) / max(height, 0.2) * 0.5 * bend;
            return positionWS + offset;
        }

        half4 BaseColour(float2 uv, half4 vertexColour)
        {
            half4 c = SAMPLE_TEXTURE2D(baseColorTexture, samplerbaseColorTexture, uv * baseColorTexture_ST.xy + baseColorTexture_ST.zw);
            return c * baseColorFactor * vertexColour;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
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
                float3 positionWS = Wind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz));
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 base = BaseColour(i.uv, i.color);
                #if defined(_ALPHATEST_ON)
                    clip(base.a - alphaCutoff);
                #endif

                // Thin leaves: the back side is lit as if it faced the viewer.
                half3 normal = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normal;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.bakedGI = SampleSH(normal);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = base.rgb;
                surface.alpha = 1.0;
                surface.metallic = 0.0;
                surface.smoothness = 1.0 - roughnessFactor;
                surface.occlusion = 1.0;
                surface.normalTS = half3(0, 0, 1);

                half4 colour = UniversalFragmentPBR(input, surface);
                colour.rgb = MixFog(colour.rgb, i.fog);
                return half4(colour.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 positionWS = Wind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                    clip(BaseColour(i.uv, i.color).a - alphaCutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(Wind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz)));
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(_ALPHATEST_ON)
                    clip(BaseColour(i.uv, i.color).a - alphaCutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

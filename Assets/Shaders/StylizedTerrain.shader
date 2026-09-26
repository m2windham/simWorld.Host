Shader "SimWorld/StylizedTerrain"
{
    Properties
    {
        [MainTexture] _MainTex("Cell Terrain Texture", 2D) = "white" {}
        [MainColor] _MeadowColor("Meadow Grass Color", Color) = (0.337, 0.490, 0.176, 1.0)
        _RidgeColor("Sunlit Grass Ridge Color", Color) = (0.478, 0.639, 0.212, 1.0)
        _CliffColor("Cliff Rock Color", Color) = (0.431, 0.408, 0.373, 1.0)
        _StepHeight("Terrace Step Height", Float) = 1.0
        _StepSharpness("Terrace Edge Sharpness", Range(0.0, 1.0)) = 0.8
        _CliffSlopeThreshold("Cliff Slope Threshold (N.y)", Range(0.0, 1.0)) = 0.70
        _CliffBlend("Cliff Blend Softness", Range(0.0, 0.5)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MeadowColor;
                float4 _RidgeColor;
                float4 _CliffColor;
                float _StepHeight;
                float _StepSharpness;
                float _CliffSlopeThreshold;
                float _CliffBlend;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float fogFactor     : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);

                // 1. Stepped terracing: elevation quantization & edge contour highlight
                float stepHeight = max(_StepHeight, 0.001);
                float stepVal = input.positionWS.y / stepHeight;
                float stepFrac = frac(stepVal);
                float lipWidth = max(0.01, 1.0 - _StepSharpness);
                float lipHighlight = smoothstep(1.0 - lipWidth, 1.0, stepFrac);

                float3 grassColor = lerp(_MeadowColor.rgb, _RidgeColor.rgb, lipHighlight);

                // 2. Slope angle blending (World Normal Y vs Cliff threshold)
                float slopeFlatness = saturate(normalWS.y);
                float cliffFactor = 1.0 - smoothstep(_CliffSlopeThreshold - _CliffBlend, _CliffSlopeThreshold + _CliffBlend, slopeFlatness);
                float3 surfaceAlbedo = lerp(grassColor, _CliffColor.rgb, cliffFactor);

                // 3. Modulate with cell terrain texture
                float4 cellSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 albedo = surfaceAlbedo * cellSample.rgb;

                // 4. Stylized Wrap Lighting & Ambient SH
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float wrapDiffuse = saturate((NdotL + 0.35) / 1.35);
                float3 lightTerm = mainLight.color * (wrapDiffuse * mainLight.shadowAttenuation);
                float3 ambientTerm = SampleSH(normalWS);

                float3 finalColor = albedo * (lightTerm + ambientTerm);
                finalColor = MixFog(finalColor, input.fogFactor);
                return float4(finalColor, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MeadowColor;
                float4 _RidgeColor;
                float4 _CliffColor;
                float _StepHeight;
                float _StepSharpness;
                float _CliffSlopeThreshold;
                float _CliffBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            float4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MeadowColor;
                float4 _RidgeColor;
                float4 _CliffColor;
                float _StepHeight;
                float _StepSharpness;
                float _CliffSlopeThreshold;
                float _CliffBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MeadowColor;
                float4 _RidgeColor;
                float4 _CliffColor;
                float _StepHeight;
                float _StepSharpness;
                float _CliffSlopeThreshold;
                float _CliffBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                return float4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}

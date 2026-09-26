Shader "SimWorld/StylizedWater"
{
    Properties
    {
        _ShallowColor("Shallow Turquoise", Color) = (0.282, 0.596, 0.643, 0.85)
        _DeepColor("Deep Channel Teal", Color) = (0.145, 0.345, 0.380, 0.95)
        _DepthDistance("Depth Max Distance", Range(0.1, 10.0)) = 3.0
        _FoamColor("Shoreline Foam Color", Color) = (0.918, 0.969, 0.973, 1.0)
        _FoamDistance("Foam Edge Band", Range(0.01, 1.0)) = 0.25
        _WaveSpeed("Wave Scroll Speed", Vector) = (0.05, 0.08, 0, 0)
        _WaveScale("Wave Frequency", Range(0.1, 20.0)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _WaveSpeed;
                float _DepthDistance;
                float _FoamDistance;
                float _WaveScale;
            CBUFFER_END

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
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. Scene Depth Sampling via DeclareDepthTexture.hlsl
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEyeDepth = -TransformWorldToView(input.positionWS).z;
                float deltaDepth = max(0.0, sceneEyeDepth - surfaceEyeDepth);

                // 2. Depth Tinting (Shallow Turquoise to Deep Teal)
                float depthFactor = saturate(deltaDepth / max(_DepthDistance, 0.001));
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // 3. Dual UV Wave Scroll Perturbation
                float2 uv1 = input.positionWS.xz * (_WaveScale * 0.1) + _WaveSpeed.xy * _Time.y;
                float2 uv2 = input.positionWS.xz * (_WaveScale * 0.15) - _WaveSpeed.yx * (_Time.y * 0.7);
                float waveNoise = sin(uv1.x * 6.28 + uv1.y * 3.14) * 0.5 + cos(uv2.x * 4.0 - uv2.y * 6.0) * 0.5;

                // 4. Soft Shoreline Edge Contact Foam Band
                float foamBand = 1.0 - saturate(deltaDepth / max(_FoamDistance, 0.001));
                float foamEdge = saturate(foamBand + waveNoise * 0.25 * foamBand);
                float foamFactor = smoothstep(0.2, 0.7, foamEdge);

                float3 finalRGB = lerp(waterColor.rgb, _FoamColor.rgb, foamFactor);
                float finalAlpha = lerp(waterColor.a, _FoamColor.a, foamFactor);

                // 5. Light interaction & Sun Specular Glint
                Light mainLight = GetMainLight();
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfVec = normalize(mainLight.direction + viewDir);
                float3 perturbedNormal = normalize(float3(-waveNoise * 0.08, 1.0, waveNoise * 0.08));
                float spec = pow(saturate(dot(perturbedNormal, halfVec)), 32.0);
                finalRGB += mainLight.color * (spec * 0.35);

                finalRGB = MixFog(finalRGB, input.fogFactor);
                return float4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

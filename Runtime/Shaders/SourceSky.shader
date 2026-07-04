Shader "Source2Unity/Source Sky"
{
    Properties
    {
        [NoScaleOffset] _EnvCubemap("Sky Cubemap", Cube) = "" {}
        [HDR] _EnvTint("Sky Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure", Range(0, 8)) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Skybox"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_EnvCubemap);
            SAMPLER(sampler_EnvCubemap);

            CBUFFER_START(UnityPerMaterial)
                half4 _EnvTint;
                half _Exposure;
                half _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.directionWS = positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.directionWS);

                float rad = _Rotation * 0.01745329251;
                float s = sin(rad);
                float c = cos(rad);
                float3 rotated = float3(dir.x * c - dir.z * s, dir.y, dir.x * s + dir.z * c);

                half3 sky = SAMPLE_TEXTURECUBE(_EnvCubemap, sampler_EnvCubemap, rotated).rgb;
                sky *= _EnvTint.rgb * _Exposure;
                return half4(sky, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

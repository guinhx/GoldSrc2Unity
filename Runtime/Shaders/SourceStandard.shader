Shader "Source2Unity/Source Standard"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [HDR] _EmissionColor("Self-Illum / Emission", Color) = (0, 0, 0, 0)
        _EmissionMap("Emission Mask", 2D) = "black" {}

        [NoScaleOffset] _EnvCubemap("Environment Cubemap", Cube) = "" {}
        [HDR] _EnvTint("Environment Tint", Color) = (1, 1, 1, 1)
        _EnvStrength("Environment Strength", Range(0, 4)) = 1.0
        _EnvFresnelPower("Environment Fresnel", Range(0.5, 8)) = 3.0

        _PhongBoost("Phong Boost", Range(0, 16)) = 1.0
        _PhongExponent("Phong Exponent", Range(1, 128)) = 16.0
        _PhongExponentMap("Phong Exponent Map", 2D) = "white" {}
        _PhongExponentScale("Phong Exponent Scale", Range(1, 256)) = 128.0
        _PhongFresnelRanges("Phong Fresnel Ranges", Vector) = (0.0, 0.5, 1.0, 0.0)

        [HDR] _AmbientCubePX("+X Ambient", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _AmbientCubeNX("-X Ambient", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _AmbientCubePY("+Y Ambient", Color) = (0.35, 0.38, 0.45, 1)
        [HDR] _AmbientCubeNY("-Y Ambient", Color) = (0.08, 0.07, 0.06, 1)
        [HDR] _AmbientCubePZ("+Z Ambient", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _AmbientCubeNZ("-Z Ambient", Color) = (0.2, 0.2, 0.2, 1)

        _RimBoost("Rim Boost", Range(0, 8)) = 1.0
        _RimExponent("Rim Exponent", Range(0.5, 16)) = 3.0

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [HideInInspector] _Surface("Surface Type", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _HALFLAMBERT
            #pragma shader_feature_local _PHONG
            #pragma shader_feature_local _PHONG_EXPONENTMAP
            #pragma shader_feature_local _AMBIENT_CUBE
            #pragma shader_feature_local _ENVMAP
            #pragma shader_feature_local _ENVMAPMASK_BASEALPHA
            #pragma shader_feature_local _ENVMAPMASK_BUMPALPHA
            #pragma shader_feature_local _SELFILLUM
            #pragma shader_feature_local _SELFILLUM_MASK_BASEALPHA
            #pragma shader_feature_local _RIMLIGHT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _SURFACE_TYPE_TRANSPARENT

            #include "Source2UnityCommon.hlsl"

            SourceVaryings vert(SourceAttributes input)
            {
                return SourceVertex(input);
            }

            half4 frag(SourceVaryings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = baseSample * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                half3 normalTS = SampleNormalTS(input.uv);
                half3 normalWS = TransformNormalToWorld(normalTS, input.normalWS, input.tangentWS, input.bitangentWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half bumpAlpha = SampleBumpAlpha(input.uv);

                half3 color = SourceComputeLighting(albedo.rgb, normalWS, viewDirWS, input.positionWS, baseSample.a, bumpAlpha, input.uv);
                color += SampleSelfIllum(input.uv, baseSample.a);
                color = MixFog(color, input.fogFactor);

                #ifdef _SURFACE_TYPE_TRANSPARENT
                    return half4(color, albedo.a);
                #else
                    return half4(color, 1.0h);
                #endif
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
            #pragma target 3.0
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

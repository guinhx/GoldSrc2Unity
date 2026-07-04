Shader "Source2Unity/Source GoldSrc"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
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
        }

        Pass
        {
            Name "GoldSrcLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #pragma shader_feature_local _HALFLAMBERT
            #pragma shader_feature_local _FLATSHADE
            #pragma shader_feature_local _CHROME
            #pragma shader_feature_local _FULLBRIGHT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _ADDITIVE_BLEND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewNormalWS : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
                half   fogFactor    : TEXCOORD4;
            };

            half GoldSrcDiffuse(half ndotl)
            {
            #ifdef _HALFLAMBERT
                half w = saturate(ndotl * 0.5h + 0.5h);
                return w * w;
            #else
                return saturate(ndotl);
            #endif
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewNormalWS = mul((float3x3)UNITY_MATRIX_V, output.normalWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
            #ifdef _CHROME
                // GoldSrc chrome = matcap: sample base texture by view-space normal XY
                float2 matcapUV = input.viewNormalWS.xy * 0.5h + 0.5h;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, matcapUV).rgb * _BaseColor.rgb;
            #else
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb;

                #ifdef _ALPHATEST_ON
                    clip(baseSample.a * _BaseColor.a - _Cutoff);
                #endif
            #endif

            #ifdef _FULLBRIGHT
                half3 color = albedo;
            #else
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);

                half ndotl;
            #ifdef _FLATSHADE
                // Flatshade: uniform brightness — average of main light only, no crisp terminators
                ndotl = 0.65h;
            #else
                ndotl = dot(normalWS, lightDir);
            #endif

                half diffuseTerm = GoldSrcDiffuse(ndotl);
                half3 ambient = SampleSH(normalWS);
                half3 color = albedo * (ambient + mainLight.color * diffuseTerm * mainLight.shadowAttenuation);
            #endif

                color = MixFog(color, input.fogFactor);

            #ifdef _ADDITIVE_BLEND
                return half4(color, 1.0h);
            #else
                return half4(color, 1.0h);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Source2Unity/Source Unlit"
}

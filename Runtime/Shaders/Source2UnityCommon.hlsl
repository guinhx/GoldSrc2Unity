#ifndef SOURCE2UNITY_COMMON_INCLUDED
#define SOURCE2UNITY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_PhongExponentMap);
SAMPLER(sampler_PhongExponentMap);
TEXTURECUBE(_EnvCubemap);
SAMPLER(sampler_EnvCubemap);

TEXTURE2D(_LightMap);
SAMPLER(sampler_LightMap);
TEXTURE2D(_LightMapBump0);
SAMPLER(sampler_LightMapBump0);
TEXTURE2D(_LightMapBump1);
SAMPLER(sampler_LightMapBump1);
TEXTURE2D(_LightMapBump2);
SAMPLER(sampler_LightMapBump2);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _LightMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _EnvTint;
    half4 _PhongFresnelRanges;
    half4 _AmbientCubePX;
    half4 _AmbientCubeNX;
    half4 _AmbientCubePY;
    half4 _AmbientCubeNY;
    half4 _AmbientCubePZ;
    half4 _AmbientCubeNZ;
    half _BumpScale;
    half _EnvStrength;
    half _EnvFresnelPower;
    half _PhongBoost;
    half _PhongExponent;
    half _PhongExponentScale;
    half _RimExponent;
    half _RimBoost;
    half _LightMapBoost;
    half _Cutoff;
CBUFFER_END

struct SourceAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
};

struct SourceLightmappedAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float2 uvLightmap : TEXCOORD1;
};

struct SourceVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 normalWS     : TEXCOORD1;
    float3 tangentWS    : TEXCOORD2;
    float3 bitangentWS  : TEXCOORD3;
    float3 viewDirWS    : TEXCOORD4;
    float3 positionWS   : TEXCOORD5;
    half   fogFactor    : TEXCOORD6;
};

struct SourceLightmappedVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float2 uvLightmap   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float3 tangentWS    : TEXCOORD3;
    float3 bitangentWS  : TEXCOORD4;
    float3 viewDirWS    : TEXCOORD5;
    float3 positionWS   : TEXCOORD6;
    half   fogFactor    : TEXCOORD7;
};

SourceVaryings SourceVertex(SourceAttributes input)
{
    SourceVaryings output;
    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = normInputs.normalWS;
    output.tangentWS = normInputs.tangentWS;
    output.bitangentWS = normInputs.bitangentWS;
    output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
    output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
    return output;
}

SourceLightmappedVaryings SourceLightmappedVertex(SourceLightmappedAttributes input)
{
    SourceLightmappedVaryings output;
    VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = posInputs.positionCS;
    output.positionWS = posInputs.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.uvLightmap = TRANSFORM_TEX(input.uvLightmap, _LightMap);
    output.normalWS = normInputs.normalWS;
    output.tangentWS = normInputs.tangentWS;
    output.bitangentWS = normInputs.bitangentWS;
    output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
    output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
    return output;
}

// Valve Half-Lambert: (N·L×0.5+0.5)² — SIGGRAPH 2006 / source-sdk DiffuseTerm()
half SourceDiffuseTerm(half ndotl)
{
#ifdef _HALFLAMBERT
    half wrapped = saturate(ndotl * 0.5h + 0.5h);
    return wrapped * wrapped;
#else
    return saturate(ndotl);
#endif
}

// SIGGRAPH 2006 Listing 1 — six-color ambient cube from world-space normal
half3 SampleAmbientCube(half3 worldNormal)
{
#ifdef _AMBIENT_CUBE
    half3 nSquared = worldNormal * worldNormal;
    bool3 isNegative = worldNormal < 0.0h;

    half3 px = isNegative.x ? _AmbientCubeNX.rgb : _AmbientCubePX.rgb;
    half3 py = isNegative.y ? _AmbientCubeNY.rgb : _AmbientCubePY.rgb;
    half3 pz = isNegative.z ? _AmbientCubeNZ.rgb : _AmbientCubePZ.rgb;

    return nSquared.x * px + nSquared.y * py + nSquared.z * pz;
#else
    return SampleSH(worldNormal);
#endif
}

half3 SampleNormalTS(float2 uv)
{
#ifdef _NORMALMAP
    half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
    return UnpackNormalScale(nMap, _BumpScale);
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

half SampleBumpAlpha(float2 uv)
{
#ifdef _NORMALMAP
    return SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv).a;
#else
    return 1.0h;
#endif
}

half3 TransformNormalToWorld(half3 normalTS, half3 normalWS, half3 tangentWS, half3 bitangentWS)
{
    half3x3 tbn = half3x3(normalize(tangentWS), normalize(bitangentWS), normalize(normalWS));
    return normalize(mul(normalTS, tbn));
}

half GetPhongExponent(float2 uv)
{
#ifdef _PHONG_EXPONENTMAP
    half sampled = SAMPLE_TEXTURE2D(_PhongExponentMap, sampler_PhongExponentMap, uv).r;
    return max(sampled * _PhongExponentScale, 1.0h);
#else
    return max(_PhongExponent, 1.0h);
#endif
}

// Valve Phong fresnel ranges: [min, mid, max] view-dependent spec scale
half SourcePhongFresnel(half3 normalWS, half3 viewDirWS)
{
    half vdn = saturate(dot(normalWS, viewDirWS));
    half rangeMin = _PhongFresnelRanges.x;
    half rangeMid = _PhongFresnelRanges.y;
    half rangeMax = _PhongFresnelRanges.z;
    half t = saturate((1.0h - vdn - rangeMin) / max(rangeMid - rangeMin, 1e-4h));
    return lerp(rangeMax, rangeMin, t);
}

half3 SourcePhongSpecular(half3 normalWS, half3 viewDirWS, half3 lightDirWS, half mask, half phongExponent)
{
#ifdef _PHONG
    half3 halfDir = normalize(lightDirWS + viewDirWS);
    half ndoth = saturate(dot(normalWS, halfDir));
    half fresnel = SourcePhongFresnel(normalWS, viewDirWS);
    half spec = pow(ndoth, phongExponent) * _PhongBoost * fresnel * mask;
    return spec.xxx;
#else
    return half3(0.0h, 0.0h, 0.0h);
#endif
}

half GetEnvMapMask(half baseAlpha, half bumpAlpha)
{
#if defined(_ENVMAPMASK_BUMPALPHA)
    return bumpAlpha;
#elif defined(_ENVMAPMASK_BASEALPHA)
    return baseAlpha;
#else
    return 1.0h;
#endif
}

half3 SampleEnvironmentReflection(half3 normalWS, half3 viewDirWS, half envMask)
{
#ifdef _ENVMAP
    half3 reflectDir = reflect(-viewDirWS, normalWS);
    half3 env = SAMPLE_TEXTURECUBE(_EnvCubemap, sampler_EnvCubemap, reflectDir).rgb;
    env *= _EnvTint.rgb;

    half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(_EnvFresnelPower, 0.5h));
    half strength = _EnvStrength * envMask * lerp(0.35h, 1.0h, fresnel);
    return env * strength;
#else
    return half3(0.0h, 0.0h, 0.0h);
#endif
}

half3 SourceRimLight(half3 normalWS, half3 viewDirWS, half3 lightColor)
{
#ifdef _RIMLIGHT
    half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), max(_RimExponent, 0.5h));
    return lightColor * rim * _RimBoost;
#else
    return half3(0.0h, 0.0h, 0.0h);
#endif
}

half3 SampleSelfIllum(float2 uv, half baseAlpha)
{
#ifdef _SELFILLUM
    #ifdef _SELFILLUM_MASK_BASEALPHA
        half mask = baseAlpha;
    #else
        half mask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).r;
    #endif
    return _EmissionColor.rgb * mask;
#else
    return half3(0.0h, 0.0h, 0.0h);
#endif
}

// Radiosity Normal Mapping basis (tangent space) — GDC 2004 / Source SDK
static const half3 kRadiosityBasis0 = half3(0.816496580927726h, 0.0h, 0.577350269189625h);
static const half3 kRadiosityBasis1 = half3(-0.816496580927726h, 0.0h, 0.577350269189625h);
static const half3 kRadiosityBasis2 = half3(0.0h, 0.866025403784438h, 0.5h);

half3 SampleRadiosityNormalMapping(float2 uvLightmap, half3 normalTS)
{
#ifdef _RNM
    half3 lm0 = SAMPLE_TEXTURE2D(_LightMapBump0, sampler_LightMapBump0, uvLightmap).rgb;
    half3 lm1 = SAMPLE_TEXTURE2D(_LightMapBump1, sampler_LightMapBump1, uvLightmap).rgb;
    half3 lm2 = SAMPLE_TEXTURE2D(_LightMapBump2, sampler_LightMapBump2, uvLightmap).rgb;

    half w0 = saturate(dot(normalTS, kRadiosityBasis0));
    half w1 = saturate(dot(normalTS, kRadiosityBasis1));
    half w2 = saturate(dot(normalTS, kRadiosityBasis2));

    return lm0 * w0 + lm1 * w1 + lm2 * w2;
#else
    return SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, uvLightmap).rgb * _LightMapBoost;
#endif
}

half3 SourceComputeLightmapped(
    half3 albedo,
    half3 normalWS,
    half3 normalTS,
    half3 viewDirWS,
    float2 uvLightmap,
    half baseAlpha,
    half bumpAlpha)
{
    half3 lightmap = SampleRadiosityNormalMapping(uvLightmap, normalTS);
    half3 lit = albedo * lightmap;

    half envMask = GetEnvMapMask(baseAlpha, bumpAlpha);
    half3 env = SampleEnvironmentReflection(normalWS, viewDirWS, envMask);

    return lit + env;
}

half3 SourceComputeLighting(
    half3 albedo,
    half3 normalWS,
    half3 viewDirWS,
    float3 positionWS,
    half baseAlpha,
    half bumpAlpha,
    float2 uv)
{
#ifdef _FULLBRIGHT
    return albedo;
#endif

    half envMask = GetEnvMapMask(baseAlpha, bumpAlpha);
    half phongExponent = GetPhongExponent(uv);

    half3 ambient = SampleAmbientCube(normalWS) * albedo;

    half3 diffuse = half3(0.0h, 0.0h, 0.0h);
    half3 specular = half3(0.0h, 0.0h, 0.0h);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half3 lightDir = normalize(mainLight.direction);
    half ndotl = dot(normalWS, lightDir);
    half diffuseTerm = SourceDiffuseTerm(ndotl);

    diffuse += albedo * mainLight.color * diffuseTerm * mainLight.shadowAttenuation;
    specular += SourcePhongSpecular(normalWS, viewDirWS, lightDir, 1.0h, phongExponent) * mainLight.color * mainLight.shadowAttenuation;

    #ifdef _ADDITIONAL_LIGHTS
    uint lightCount = GetAdditionalLightsCount();
    for (uint li = 0u; li < lightCount; li++)
    {
        Light addLight = GetAdditionalLight(li, positionWS);
        half3 addDir = normalize(addLight.direction);
        half addNdotL = dot(normalWS, addDir);
        half addDiffuse = SourceDiffuseTerm(addNdotL);
        diffuse += albedo * addLight.color * addDiffuse * addLight.distanceAttenuation * addLight.shadowAttenuation;
        specular += SourcePhongSpecular(normalWS, viewDirWS, addDir, 1.0h, phongExponent) * addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation;
    }
    #endif

    half3 env = SampleEnvironmentReflection(normalWS, viewDirWS, envMask);
    half3 rim = SourceRimLight(normalWS, viewDirWS, mainLight.color);

    return ambient + diffuse + specular + env + rim;
}

#endif

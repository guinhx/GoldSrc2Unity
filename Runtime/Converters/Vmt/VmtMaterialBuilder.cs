using System;
using System.Globalization;
using Source2Unity.Converters.Pipeline;
using Source2Unity.Formats.KeyValues;
using Source2Unity.Formats.Vmt;
using Source2Unity.Formats.Vmt.Parsers;
using UnityEngine;

namespace Source2Unity.Converters.Vmt
{
    public sealed class VmtBuildResult
    {
        public Material Material { get; init; }
        public Texture2D BaseTexture { get; init; }
        public Texture2D BumpTexture { get; init; }
        public Texture2D SelfIllumTexture { get; init; }
        public Texture2D PhongExponentTexture { get; init; }
        public Texture2D LightMapTexture { get; init; }
        public Cubemap EnvCubemap { get; init; }
        public Texture3D VolumeTexture { get; init; }
    }

    public static class VmtMaterialBuilder
    {
        public static VmtBuildResult Build(VmtParseResult result, AssetLoadContext context)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            KvObject root = result.Root;
            bool isLightmapped = IsLightmappedShader(result.ShaderName);
            var shader = ResolveShader(result.ShaderName, root);
            var material = new Material(shader) { name = result.ShaderName ?? "VMT Material" };

            Texture2D baseTex = TryLoadTexture(root, SourceShaderMapping.BaseTextureKeys, context, linear: false);
            Texture2D bumpTex = TryLoadTexture(root, SourceShaderMapping.BumpMapKeys, context, linear: true);
            Texture2D selfIllumTex = TryLoadTexture(root, SourceShaderMapping.SelfIllumKeys, context, linear: false);
            Texture2D phongExpTex = TryLoadTexture(root, SourceShaderMapping.PhongExponentKeys, context, linear: true);
            Texture2D lightMapTex = TryLoadLightMap(root, SourceShaderMapping.LightMapKeys, context);
            Texture2D rnm0 = TryLoadLightMap(root, new[] { SourceShaderMapping.RadiosityLightMapKeys[0] }, context);
            Texture2D rnm1 = TryLoadLightMap(root, new[] { SourceShaderMapping.RadiosityLightMapKeys[1] }, context);
            Texture2D rnm2 = TryLoadLightMap(root, new[] { SourceShaderMapping.RadiosityLightMapKeys[2] }, context);
            Cubemap envCubemap = TryLoadEnvCubemap(root, context);
            Texture3D volumeTex = TryLoadVolumeTexture(root, context);

            if (baseTex != null)
                material.SetTexture("_BaseMap", baseTex);

            if (bumpTex != null)
            {
                material.SetTexture("_BumpMap", bumpTex);
                material.EnableKeyword("_NORMALMAP");
            }

            if (selfIllumTex != null)
                material.SetTexture("_EmissionMap", selfIllumTex);

            if (phongExpTex != null)
                Source2UnityShaders.ApplyPhongExponentTexture(material, phongExpTex, root);

            if (isLightmapped)
                Source2UnityShaders.ApplyLightmap(material, lightMapTex, rnm0, rnm1, rnm2);

            if (envCubemap != null)
                ApplyEnvCubemap(material, envCubemap, root);

            if (volumeTex != null)
            {
                material.SetTexture("_VolumeMap", volumeTex);
                material.SetFloat("_VolumeDepth", volumeTex.depth);
            }

            ApplyColorTint(material, root);
            Source2UnityShaders.ApplySourceLightingKeywords(material, root);
            Source2UnityShaders.ApplySourceScalarProperties(material, root);
            Source2UnityShaders.ApplySurfaceState(material, root);

            if (!isLightmapped)
                Source2UnityShaders.ApplyAmbientCube(material);

            return new VmtBuildResult
            {
                Material = material,
                BaseTexture = baseTex,
                BumpTexture = bumpTex,
                SelfIllumTexture = selfIllumTex,
                PhongExponentTexture = phongExpTex,
                LightMapTexture = lightMapTex,
                EnvCubemap = envCubemap,
                VolumeTexture = volumeTex
            };
        }

        private static Shader ResolveShader(string shaderName, KvObject root)
        {
            if (!string.IsNullOrEmpty(shaderName))
            {
                if (IsSkyShader(shaderName))
                    return Source2UnityShaders.FindSky();

                foreach (string unlit in SourceShaderMapping.UnlitShaderNames)
                {
                    if (shaderName.IndexOf(unlit, StringComparison.OrdinalIgnoreCase) >= 0)
                        return Source2UnityShaders.FindUnlit();
                }

                if (IsLightmappedShader(shaderName))
                    return Source2UnityShaders.FindLightmapped();
            }

            if (IsTruthy(root.GetString("$envmap")) && string.IsNullOrEmpty(root.GetString("$basetexture")))
                return Source2UnityShaders.FindSky();

            return Source2UnityShaders.FindStandard();
        }

        private static bool IsLightmappedShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;

            return shaderName.IndexOf("LightmappedGeneric", StringComparison.OrdinalIgnoreCase) >= 0
                || shaderName.IndexOf("Lightmapped_4WayBlend", StringComparison.OrdinalIgnoreCase) >= 0
                || shaderName.IndexOf("WorldVertexTransition", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSkyShader(string shaderName)
        {
            return shaderName.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D TryLoadTexture(
            KvObject root,
            System.Collections.Generic.IReadOnlyDictionary<string, string> keys,
            AssetLoadContext context,
            bool linear)
        {
            foreach (var pair in keys)
            {
                string value = root.GetString(pair.Key);
                if (string.IsNullOrEmpty(value))
                    continue;

                string vtfPath = VmtExternalPaths.GetTexturePath(value);
                if (context.TryLoadTexture(vtfPath, out var texture, linear))
                    return texture;
            }

            return null;
        }

        private static Texture2D TryLoadLightMap(KvObject root, string[] keys, AssetLoadContext context)
        {
            foreach (string key in keys)
            {
                string value = root.GetString(key);
                if (string.IsNullOrEmpty(value))
                    continue;

                string vtfPath = VmtExternalPaths.GetTexturePath(value);
                if (context.TryLoadTexture(vtfPath, out var texture, linear: false))
                    return texture;
            }

            return null;
        }

        private static Cubemap TryLoadEnvCubemap(KvObject root, AssetLoadContext context)
        {
            string value = root.GetString("$envmap");
            if (string.IsNullOrEmpty(value))
                return null;

            if (IsBuiltInEnvmap(value))
                return ResolveBuiltInEnvCubemap(context);

            string vtfPath = VmtExternalPaths.GetTexturePath(value);
            return context.TryLoadCubemap(vtfPath, out var cubemap) ? cubemap : null;
        }

        private static Cubemap ResolveBuiltInEnvCubemap(AssetLoadContext context)
        {
            if (context.FallbackEnvCubemap != null)
                return context.FallbackEnvCubemap;

            if (RenderSettings.customReflectionTexture is Cubemap cubemap)
                return cubemap;

            return null;
        }

        private static Texture3D TryLoadVolumeTexture(KvObject root, AssetLoadContext context)
        {
            foreach (string key in SourceShaderMapping.VolumeTextureKeys)
            {
                string value = root.GetString(key);
                if (string.IsNullOrEmpty(value))
                    continue;

                string vtfPath = VmtExternalPaths.GetTexturePath(value);
                if (context.TryLoadVtf(vtfPath, out var build) && build.VolumeTexture != null)
                    return build.VolumeTexture;
            }

            return null;
        }

        private static bool IsBuiltInEnvmap(string value)
        {
            return value.Equals("env_cubemap", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyEnvCubemap(Material material, Cubemap cubemap, KvObject root)
        {
            Source2UnityShaders.EnableEnvmap(material, cubemap);

            if (TryParseColor(root.GetString("$envmaptint"), out Color tint))
                material.SetColor("_EnvTint", tint);

            if (float.TryParse(root.GetString("$envmapcontrast"), NumberStyles.Float, CultureInfo.InvariantCulture, out float contrast))
                material.SetFloat("_EnvStrength", Mathf.Clamp(contrast, 0f, 4f));
            else
                material.SetFloat("_EnvStrength", 1f);
        }

        private static void ApplyColorTint(Material material, KvObject root)
        {
            if (!TryParseColor(root.GetString("$color") ?? root.GetString("$color2"), out Color tint))
                return;

            material.SetColor("_BaseColor", tint);

            if (root.TryGetString("$selfillumtint", out string emissiveTint)
                && TryParseColor(emissiveTint, out Color emission))
            {
                material.SetColor("_EmissionColor", emission);
            }
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim().Trim('[', ']', '{', '}');
            string[] parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return false;

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r))
                return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
                return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                return false;

            float a = 1f;
            if (parts.Length >= 4)
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a);

            if (r > 1f || g > 1f || b > 1f)
            {
                r /= 255f;
                g /= 255f;
                b /= 255f;
                if (a > 1f) a /= 255f;
            }

            color = new Color(r, g, b, a);
            return true;
        }
    }
}

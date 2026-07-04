using System;
using System.Collections.Generic;
using Source2Unity.Converters.Mdl;
using Source2Unity.Converters.Vmt;
using Source2Unity.Converters.Vtf;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl;
using Source2Unity.Formats.Vmt;
using Source2Unity.Formats.Vmt.Parsers;
using Source2Unity.Formats.Vtf;
using UnityEngine;

namespace Source2Unity.Converters.Pipeline
{
    public sealed class AssetLoadContext
    {
        public AssetLoadContext(IContentResolver resolver)
        {
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public IContentResolver Resolver { get; }

        /// <summary>
        /// Cubemap used when VMT specifies <c>$envmap env_cubemap</c> (map entity cubemap).
        /// Falls back to <see cref="RenderSettings.customReflectionTexture"/> when null.
        /// </summary>
        public Cubemap FallbackEnvCubemap { get; set; }

        private readonly Dictionary<string, VtfBuildResult> _vtfCache = new Dictionary<string, VtfBuildResult>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        public bool TryLoadVtf(string logicalPath, out VtfBuildResult buildResult, VtfTextureBuildOptions options = null)
        {
            logicalPath = logicalPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(logicalPath))
            {
                buildResult = null;
                return false;
            }

            options ??= VtfTextureBuildOptions.Default;
            string cacheKey = BuildVtfCacheKey(logicalPath, options);

            if (_vtfCache.TryGetValue(cacheKey, out buildResult))
                return buildResult != null;

            if (!Resolver.TryOpenRead(logicalPath, out var stream))
            {
                _vtfCache[cacheKey] = null;
                buildResult = null;
                return false;
            }

            using (stream)
            {
                var parseResult = new VtfFile().Read(stream);
                buildResult = VtfTextureBuilder.Build(parseResult, options);
                _vtfCache[cacheKey] = buildResult;
                return true;
            }
        }

        public bool TryLoadTexture(string logicalPath, out Texture2D texture, bool linear = false)
        {
            if (TryLoadVtf(logicalPath, out var build, new VtfTextureBuildOptions { Linear = linear }))
            {
                texture = build.Texture;
                return texture != null;
            }

            texture = null;
            return false;
        }

        public bool TryLoadCubemap(string logicalPath, out Cubemap cubemap)
        {
            if (TryLoadVtf(logicalPath, out var build) && build.Cubemap != null)
            {
                cubemap = build.Cubemap;
                return true;
            }

            cubemap = null;
            return false;
        }

        public bool TryLoadMaterial(string logicalPath, out Material material)
        {
            logicalPath = logicalPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(logicalPath))
            {
                material = null;
                return false;
            }

            if (_materialCache.TryGetValue(logicalPath, out material))
                return material != null;

            if (!Resolver.TryOpenRead(logicalPath, out var stream))
            {
                _materialCache[logicalPath] = null;
                material = null;
                return false;
            }

            using (stream)
            {
                var parseResult = new VmtFile().Read(stream, logicalPath, Resolver);
                material = VmtMaterialBuilder.Build(parseResult, this).Material;
                _materialCache[logicalPath] = material;
                return true;
            }
        }

        public MdlBuildResult LoadModel(string logicalPath)
        {
            var mdlFile = new MdlFile();
            var parseResult = mdlFile.Read(logicalPath, Resolver);
            return MdlModelBuilder.Build(parseResult);
        }

        private static string BuildVtfCacheKey(string logicalPath, VtfTextureBuildOptions options)
        {
            return options.Linear ? logicalPath + "|linear" : logicalPath;
        }
    }

    public static class AssetConverterRegistry
    {
        public static VtfBuildResult LoadVtf(string logicalPath, AssetLoadContext context, VtfTextureBuildOptions options = null)
        {
            if (!context.TryLoadVtf(logicalPath, out var build))
                throw new InvalidOperationException($"VTF not found or unsupported: {logicalPath}");
            return build;
        }

        public static Texture2D LoadTexture(string logicalPath, AssetLoadContext context, bool linear = false)
        {
            if (!context.TryLoadTexture(logicalPath, out var texture, linear))
                throw new InvalidOperationException($"Texture not found or unsupported: {logicalPath}");
            return texture;
        }

        public static Cubemap LoadCubemap(string logicalPath, AssetLoadContext context)
        {
            if (!context.TryLoadCubemap(logicalPath, out var cubemap))
                throw new InvalidOperationException($"Cubemap not found or unsupported: {logicalPath}");
            return cubemap;
        }

        public static Material LoadMaterial(string logicalPath, AssetLoadContext context)
        {
            if (!context.TryLoadMaterial(logicalPath, out var material))
                throw new InvalidOperationException($"Material not found or unsupported: {logicalPath}");
            return material;
        }

        public static MdlBuildResult LoadModel(string logicalPath, AssetLoadContext context)
        {
            return context.LoadModel(logicalPath);
        }
    }
}

using System;
using System.IO;
using Source2Unity.Converters.Pipeline;
using Source2Unity.Converters.Vmt;
using Source2Unity.Formats.Vmt;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(1, "vmt")]
    public class VmtAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                var resolver = new EditorImportContentResolver(ctx.assetPath);
                var context = new AssetLoadContext(resolver);
                string logicalPath = EditorImportContentResolver.ToLogicalPath(ctx.assetPath);

                using var stream = File.OpenRead(ctx.assetPath);
                var parseResult = new VmtFile().Read(stream, logicalPath, resolver);
                var buildResult = VmtMaterialBuilder.Build(parseResult, context);

                buildResult.Material.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
                ctx.AddObjectToAsset("mat", buildResult.Material);
                ctx.SetMainObject(buildResult.Material);

                if (buildResult.BaseTexture != null)
                {
                    buildResult.BaseTexture.name = buildResult.BaseTexture.name ?? "BaseTexture";
                    ctx.AddObjectToAsset("basetex", buildResult.BaseTexture);
                }

                if (buildResult.BumpTexture != null)
                {
                    buildResult.BumpTexture.name = buildResult.BumpTexture.name ?? "BumpTexture";
                    ctx.AddObjectToAsset("bumptex", buildResult.BumpTexture);
                }

                if (buildResult.SelfIllumTexture != null
                    && buildResult.SelfIllumTexture != buildResult.BaseTexture)
                {
                    buildResult.SelfIllumTexture.name = buildResult.SelfIllumTexture.name ?? "SelfIllumTexture";
                    ctx.AddObjectToAsset("selfillumtex", buildResult.SelfIllumTexture);
                }

                if (buildResult.EnvCubemap != null)
                {
                    buildResult.EnvCubemap.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + "_EnvCubemap";
                    ctx.AddObjectToAsset("envcubemap", buildResult.EnvCubemap);
                }

                if (buildResult.VolumeTexture != null)
                {
                    buildResult.VolumeTexture.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + "_Volume";
                    ctx.AddObjectToAsset("volume", buildResult.VolumeTexture);
                }
            }
            catch (NotSupportedException ex)
            {
                ctx.LogImportWarning($"VMT format not supported: {ex.Message}");
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"Failed to import VMT: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

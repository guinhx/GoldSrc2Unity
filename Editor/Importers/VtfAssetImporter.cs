using System;
using System.IO;
using Source2Unity.Converters.Vtf;
using Source2Unity.Formats.Vtf;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(3, "vtf")]
    public class VtfAssetImporter : ScriptedImporter
    {
        [Tooltip("Use linear color space for data textures (normals, roughness).")]
        public bool linear;

        [Tooltip("Build a Unity Cubemap when the VTF contains cubemap faces.")]
        public bool importCubemap = true;

        [Tooltip("Build a Texture3D when the VTF has depth > 1.")]
        public bool importVolume = true;

        [Tooltip("Import all animation frames when the VTF has multiple frames.")]
        public bool importAnimationFrames = true;

        [Tooltip("Pack animation frames into a horizontal sprite sheet sub-asset.")]
        public bool packSpriteSheet;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                using var stream = File.OpenRead(ctx.assetPath);
                var parseResult = new VtfFile().Read(stream);
                var buildResult = VtfTextureBuilder.Build(parseResult, new VtfTextureBuildOptions
                {
                    Linear = linear,
                    BuildCubemap = importCubemap,
                    BuildVolume = importVolume,
                    BuildAnimationFrames = importAnimationFrames,
                    PackAnimationIntoSpriteSheet = packSpriteSheet,
                    OnWarning = message => ctx.LogImportWarning(message)
                });

                string baseName = Path.GetFileNameWithoutExtension(ctx.assetPath);
                UnityEngine.Object mainObject = null;

                if (buildResult.Texture != null)
                {
                    buildResult.Texture.name = baseName;
                    ctx.AddObjectToAsset("tex", buildResult.Texture);
                    mainObject = buildResult.Texture;
                }

                if (buildResult.Cubemap != null)
                {
                    buildResult.Cubemap.name = baseName + "_Cubemap";
                    ctx.AddObjectToAsset("cubemap", buildResult.Cubemap);
                    mainObject ??= buildResult.Cubemap;
                }

                if (buildResult.VolumeTexture != null)
                {
                    buildResult.VolumeTexture.name = baseName + "_Volume";
                    ctx.AddObjectToAsset("volume", buildResult.VolumeTexture);
                    mainObject ??= buildResult.VolumeTexture;
                }

                if (buildResult.AnimationFrames != null && buildResult.AnimationFrames.Length > 1)
                {
                    for (int i = 0; i < buildResult.AnimationFrames.Length; i++)
                    {
                        buildResult.AnimationFrames[i].name = $"{baseName}_frame{i:D2}";
                        ctx.AddObjectToAsset($"frame_{i:D2}", buildResult.AnimationFrames[i]);
                    }
                }

                if (buildResult.SpriteSheet != null)
                {
                    buildResult.SpriteSheet.name = baseName + "_SpriteSheet";
                    ctx.AddObjectToAsset("spritesheet", buildResult.SpriteSheet);
                }

                if (buildResult.IsCubemap || buildResult.IsAnimated || buildResult.IsVolume)
                {
                    var importData = ScriptableObject.CreateInstance<VtfImportData>();
                    importData.name = baseName + "_ImportData";
                    importData.IsCubemap = buildResult.IsCubemap;
                    importData.IsAnimated = buildResult.IsAnimated;
                    importData.IsVolume = buildResult.IsVolume;
                    importData.FrameRate = buildResult.SuggestedFrameRate;
                    importData.AnimationFrames = buildResult.AnimationFrames;
                    importData.Cubemap = buildResult.Cubemap;
                    importData.VolumeTexture = buildResult.VolumeTexture;
                    importData.SpriteSheet = buildResult.SpriteSheet;
                    ctx.AddObjectToAsset("importdata", importData);
                }

                if (mainObject != null)
                    ctx.SetMainObject(mainObject);
            }
            catch (NotSupportedException ex)
            {
                ctx.LogImportWarning($"VTF format not supported: {ex.Message}");
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"Failed to import VTF: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

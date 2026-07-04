using System;
using Source2Unity.Converters.Mdl;
using Source2Unity.Formats.Mdl;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(14, "mdl")]
    public class MdlAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                var mdlFile = new MdlFile();
                var parseResult = mdlFile.Read(ctx.assetPath);
                var buildResult = MdlModelBuilder.Build(parseResult);

                ctx.AddObjectToAsset("root", buildResult.Root);
                ctx.SetMainObject(buildResult.Root);

                for (int i = 0; i < buildResult.Textures.Count; i++)
                    ctx.AddObjectToAsset($"tex_{i}", buildResult.Textures[i]);

                for (int i = 0; i < buildResult.Materials.Count; i++)
                    ctx.AddObjectToAsset($"mat_{i}", buildResult.Materials[i]);

                for (int i = 0; i < buildResult.Meshes.Count; i++)
                    ctx.AddObjectToAsset($"mesh_{i}", buildResult.Meshes[i]);

                for (int i = 0; i < buildResult.Clips.Count; i++)
                    ctx.AddObjectToAsset($"clip_{i}", buildResult.Clips[i]);
            }
            catch (NotSupportedException ex)
            {
                ctx.LogImportWarning($"MDL format not supported: {ex.Message}");
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"Failed to import MDL: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

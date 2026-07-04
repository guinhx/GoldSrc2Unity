using System;
using Source2Unity.Formats.Vpk;
using Source2Unity.Formats.Vpk.Parsers;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Source2Unity.Editor.Importers
{
    [ScriptedImporter(1, "vpk")]
    public class VpkAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            if (!fileName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
            {
                var placeholder = ScriptableObject.CreateInstance<VpkArchiveAsset>();
                placeholder.name = fileName;
                placeholder.Version = "Chunk file (not a directory)";
                ctx.AddObjectToAsset("vpk", placeholder);
                ctx.SetMainObject(placeholder);
                return;
            }

            try
            {
                using var archive = new VpkArchive();
                var result = archive.Read(ctx.assetPath);

                var asset = ScriptableObject.CreateInstance<VpkArchiveAsset>();
                asset.name = fileName;
                asset.Version = result.Version.ToString();
                asset.TreeSize = result.TreeSize;

                int totalFiles = 0;
                foreach (var group in result.Entries)
                    totalFiles += group.Value.Count;
                asset.TotalFileCount = totalFiles;
                asset.ExtensionCount = result.Entries.Count;

                ctx.AddObjectToAsset("vpk", asset);
                ctx.SetMainObject(asset);
            }
            catch (Exception ex)
            {
                ctx.LogImportWarning($"Failed to import VPK: {ex.Message}");
            }
        }
    }

    public class VpkArchiveAsset : ScriptableObject
    {
        public string Version;
        public uint TreeSize;
        public int TotalFileCount;
        public int ExtensionCount;
    }
}

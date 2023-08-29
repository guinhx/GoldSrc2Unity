using GoldSrc2Unity.Source.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GoldSrc2Unity.Editor.Importer
{
    [ScriptedImporter(1, "mdl")]
    public class MDLImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var mdlFile = new MdlFile(ctx.assetPath);
            if (mdlFile.Open() && mdlFile.Read())
            {
               // TODO: After finish reading the MDL file, we need to import meshes and animations from them to Unity.
            }
            else
            {
                Debug.Log($"Failed to import MDL file: {ctx.assetPath}");
            }
            mdlFile.Dispose();
        }
    }
}
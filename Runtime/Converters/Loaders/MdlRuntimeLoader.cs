using Source2Unity.Converters.Mdl;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl;

namespace Source2Unity.Converters.Loaders
{
    /// <summary>
    /// Loads GoldSrc MDL models at runtime from any file path (StreamingAssets, mod folders, etc.).
    /// Uses the same conversion pipeline as the Editor ScriptedImporter.
    /// </summary>
    public sealed class MdlRuntimeLoader
    {
        /// <summary>
        /// Loads an MDL file and returns a fully-assembled GameObject with skinned mesh,
        /// bone hierarchy, textures, materials, and legacy animation clips.
        /// The caller is responsible for the lifecycle of the returned GameObject.
        /// </summary>
        public MdlBuildResult Load(string filePath, IContentResolver resolver = null)
        {
            var mdlFile = new MdlFile();
            var result = resolver != null
                ? mdlFile.Read(filePath, resolver)
                : mdlFile.Read(filePath);
            return MdlModelBuilder.Build(result);
        }
    }
}

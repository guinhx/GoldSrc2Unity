using Source2Unity.Formats.Vpk;

namespace Source2Unity.Formats.Vmt
{
    /// <summary>
    /// Resolves VMT texture key values to VTF logical paths inside VPK or on disk.
    /// </summary>
    public static class VmtExternalPaths
    {
        public static string GetTexturePath(string textureKeyValue)
        {
            if (string.IsNullOrWhiteSpace(textureKeyValue))
                return null;

            string path = textureKeyValue.Replace('\\', '/').Trim();
            if (path.EndsWith(".vtf"))
                return VpkPath.Normalize(path);

            if (!path.StartsWith("materials/"))
                path = "materials/" + path;

            return VpkPath.Normalize(path + ".vtf");
        }

        public static string GetMaterialPath(string materialKeyValue)
        {
            if (string.IsNullOrWhiteSpace(materialKeyValue))
                return null;

            string path = materialKeyValue.Replace('\\', '/').Trim();
            if (path.EndsWith(".vmt"))
                return VpkPath.Normalize(path);

            if (!path.StartsWith("materials/"))
                path = "materials/" + path;

            return VpkPath.Normalize(path + ".vmt");
        }
    }
}

using System.IO;
using Source2Unity.Formats.Vpk;

namespace Source2Unity.Formats.Mdl
{
    /// <summary>
    /// Resolves GoldSrc MDL companion file paths (external textures, sequence groups).
    /// Works with both filesystem paths and VPK logical paths.
    /// </summary>
    public static class MdlExternalPaths
    {
        public static string GetExternalTexturePath(string mainLogicalPath)
        {
            string dir = GetDirectory(mainLogicalPath);
            string baseName = GetFileNameWithoutExtension(mainLogicalPath);
            string ext = GetExtension(mainLogicalPath);
            return VpkPath.Combine(dir, baseName + "T" + ext);
        }

        public static string GetExternalSequencePath(string mainLogicalPath, int groupIndex)
        {
            string dir = GetDirectory(mainLogicalPath);
            string baseName = GetFileNameWithoutExtension(mainLogicalPath);
            string ext = GetExtension(mainLogicalPath);
            return VpkPath.Combine(dir, baseName + groupIndex.ToString("D2") + ext);
        }

        private static string GetDirectory(string path)
        {
            path = VpkPath.Normalize(path);
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash < 0)
                return string.Empty;

            return path.Substring(0, lastSlash);
        }

        private static string GetFileNameWithoutExtension(string path)
        {
            path = VpkPath.Normalize(path);
            int lastSlash = path.LastIndexOf('/');
            string filePart = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
            int dot = filePart.LastIndexOf('.');
            return dot < 0 ? filePart : filePart.Substring(0, dot);
        }

        private static string GetExtension(string path)
        {
            path = VpkPath.Normalize(path);
            int dot = path.LastIndexOf('.');
            if (dot < 0)
                return string.Empty;

            return path.Substring(dot);
        }
    }
}

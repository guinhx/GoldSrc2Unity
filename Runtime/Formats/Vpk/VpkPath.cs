using System;
using System.IO;

namespace Source2Unity.Formats.Vpk
{
    /// <summary>
    /// Normalizes logical paths for VPK entry lookup.
    /// VPK uses forward slashes, case-insensitive paths, and a single space for empty extension/directory.
    /// </summary>
    public static class VpkPath
    {
        public const string EmptyComponent = " ";

        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.Replace('\\', '/').TrimStart('/');
        }

        public static string ToLookupKey(string path)
        {
            return Normalize(path).ToLowerInvariant();
        }

        /// <summary>
        /// Splits a logical path into VPK tree components (extension, directory, fileName).
        /// </summary>
        public static void ParseComponents(string path, out string extension, out string directory, out string fileName)
        {
            path = Normalize(path);

            int lastSlash = path.LastIndexOf('/');
            string filePart = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
            directory = lastSlash >= 0 ? path.Substring(0, lastSlash) : string.Empty;

            int dot = filePart.LastIndexOf('.');
            if (dot < 0)
            {
                extension = EmptyComponent;
                fileName = filePart;
            }
            else
            {
                extension = filePart.Substring(dot + 1);
                fileName = filePart.Substring(0, dot);
            }

            if (string.IsNullOrEmpty(extension))
                extension = EmptyComponent;
            if (string.IsNullOrEmpty(directory))
                directory = EmptyComponent;
        }

        /// <summary>
        /// Combines directory and file name using forward slashes (logical path).
        /// </summary>
        public static string Combine(string directory, string fileName)
        {
            if (string.IsNullOrEmpty(directory) || directory == EmptyComponent)
                return fileName;

            return directory.Replace('\\', '/') + "/" + fileName;
        }

        /// <summary>
        /// Converts a logical path to a filesystem path when a root directory is provided.
        /// </summary>
        public static string ToFileSystemPath(string logicalPath, string rootDirectory = null)
        {
            if (string.IsNullOrEmpty(rootDirectory) || Path.IsPathRooted(logicalPath))
                return logicalPath.Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(rootDirectory, logicalPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

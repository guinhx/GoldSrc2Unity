using System;
using System.Collections.Generic;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk;

namespace Source2Unity.Editor.Importers
{
    /// <summary>
    /// Resolves Source-style logical paths (materials/foo/bar.vtf) relative to an importing Unity asset.
    /// </summary>
    public sealed class EditorImportContentResolver : IContentResolver
    {
        private readonly string _assetDirectory;

        public EditorImportContentResolver(string assetPath)
        {
            _assetDirectory = Path.GetDirectoryName(assetPath);
        }

        /// <summary>
        /// Converts a Unity asset path to a Source-style logical path (materials/...).
        /// </summary>
        public static string ToLogicalPath(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            const string marker = "/materials/";
            int idx = assetPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return assetPath.Substring(idx + 1);

            return Path.GetFileName(assetPath);
        }

        public bool Exists(string logicalPath) => TryOpenRead(logicalPath, out _);

        public bool TryOpenRead(string logicalPath, out Stream stream)
        {
            stream = null;
            if (string.IsNullOrEmpty(logicalPath))
                return false;

            logicalPath = VpkPath.Normalize(logicalPath);

            foreach (string candidate in EnumerateCandidates(logicalPath))
            {
                if (!File.Exists(candidate))
                    continue;

                stream = File.OpenRead(candidate);
                return true;
            }

            return false;
        }

        private IEnumerable<string> EnumerateCandidates(string logicalPath)
        {
            string fileName = Path.GetFileName(logicalPath.Replace('/', Path.DirectorySeparatorChar));

            yield return Path.Combine(_assetDirectory, fileName);

            if (logicalPath.StartsWith("materials/"))
            {
                string relative = logicalPath.Substring("materials/".Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                yield return Path.Combine(_assetDirectory, relative);
            }

            string rooted = logicalPath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(rooted))
                yield return rooted;
        }
    }
}

using System.IO;

namespace Source2Unity.Formats.Common
{
    /// <summary>
    /// Resolves logical paths to files on disk.
    /// When <see cref="RootDirectory"/> is set, relative paths use forward-slash notation.
    /// </summary>
    public sealed class DiskContentResolver : IContentResolver
    {
        public string RootDirectory { get; }

        public DiskContentResolver(string rootDirectory = null)
        {
            RootDirectory = rootDirectory;
        }

        public bool Exists(string logicalPath)
        {
            return File.Exists(ResolvePath(logicalPath));
        }

        public bool TryOpenRead(string logicalPath, out Stream stream)
        {
            string path = ResolvePath(logicalPath);
            if (!File.Exists(path))
            {
                stream = null;
                return false;
            }

            stream = File.OpenRead(path);
            return true;
        }

        private string ResolvePath(string logicalPath)
        {
            if (string.IsNullOrEmpty(RootDirectory) || Path.IsPathRooted(logicalPath))
                return logicalPath.Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(RootDirectory, logicalPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

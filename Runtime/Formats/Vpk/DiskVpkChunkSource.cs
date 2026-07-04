using System.IO;

namespace Source2Unity.Formats.Vpk
{
    /// <summary>
    /// Default chunk source using the local filesystem.
    /// </summary>
    public sealed class DiskVpkChunkSource : IVpkChunkSource
    {
        public Stream OpenDirectoryFile(string dirFilePath)
        {
            return File.OpenRead(dirFilePath);
        }

        public Stream OpenChunk(string dirFilePath, int archiveIndex)
        {
            string path = VpkArchive.GetChunkPath(dirFilePath, archiveIndex);
            return File.OpenRead(path);
        }
    }
}

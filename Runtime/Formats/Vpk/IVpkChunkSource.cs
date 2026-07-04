using System.IO;

namespace Source2Unity.Formats.Vpk
{
    /// <summary>
    /// Provides physical access to VPK chunk files (_dir.vpk and _NNN.vpk).
    /// Implement on platforms where <see cref="File.OpenRead"/> is unavailable (e.g. Android StreamingAssets).
    /// </summary>
    public interface IVpkChunkSource
    {
        Stream OpenDirectoryFile(string dirFilePath);
        Stream OpenChunk(string dirFilePath, int archiveIndex);
    }
}

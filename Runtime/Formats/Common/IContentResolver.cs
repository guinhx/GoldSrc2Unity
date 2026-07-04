using System.IO;

namespace Source2Unity.Formats.Common
{
    /// <summary>
    /// Resolves logical asset paths to readable streams.
    /// Implementations may search disk folders, mounted VPK archives, or other sources.
    /// </summary>
    public interface IContentResolver
    {
        bool TryOpenRead(string logicalPath, out Stream stream);
        bool Exists(string logicalPath);
    }
}

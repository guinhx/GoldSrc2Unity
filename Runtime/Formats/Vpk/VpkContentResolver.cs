using System.IO;
using Source2Unity.Formats.Common;

namespace Source2Unity.Formats.Vpk
{
    /// <summary>
    /// Resolves logical paths against a mounted <see cref="VpkArchive"/>.
    /// Does not own the archive — the caller must keep it alive and dispose it.
    /// </summary>
    public sealed class VpkContentResolver : IContentResolver
    {
        private readonly VpkArchive _archive;

        public VpkContentResolver(VpkArchive archive)
        {
            _archive = archive;
        }

        public bool Exists(string logicalPath)
        {
            return _archive.Contains(logicalPath);
        }

        public bool TryOpenRead(string logicalPath, out Stream stream)
        {
            if (!_archive.TryFindEntry(logicalPath, out var entry))
            {
                stream = null;
                return false;
            }

            stream = _archive.ReadEntryStream(entry);
            return true;
        }
    }
}

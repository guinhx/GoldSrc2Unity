using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Vpk.Structures
{
    /// <summary>
    /// Directory entry for a single file within the VPK tree.
    /// Size: 18 bytes (fixed portion, preload data follows).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VpkDirectoryEntry
    {
        public uint Crc32;
        public ushort PreloadSize;
        public ushort ArchiveIndex;
        public uint EntryOffset;
        public uint EntryLength;
        public ushort Terminator;

        public const ushort DirectoryArchiveIndex = 0x7FFF;
        public const ushort ExpectedTerminator = 0xFFFF;
    }
}

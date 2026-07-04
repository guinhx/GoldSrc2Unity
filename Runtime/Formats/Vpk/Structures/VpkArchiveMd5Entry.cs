using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Vpk.Structures
{
    /// <summary>
    /// MD5 checksum entry for archive validation (VPK v2).
    /// Size: 28 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct VpkArchiveMd5Entry
    {
        public uint ArchiveIndex;
        public uint StartingOffset;
        public uint Count;
        public fixed byte Md5Sum[16];
    }

    /// <summary>
    /// Other MD5 checksums block (VPK v2).
    /// Contains hashes of tree, archive md5 section, and full file.
    /// Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct VpkOtherMd5Entry
    {
        public fixed byte TreeSum[16];
        public fixed byte ArchiveMd5Sum[16];
        public fixed byte WholeFileSum[16];
    }
}

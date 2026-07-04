using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Vpk.Structures
{
    /// <summary>
    /// VPK version 1 header. Size: 12 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VpkHeaderV1
    {
        public uint Signature;
        public uint Version;
        public uint TreeSize;
    }

    /// <summary>
    /// VPK version 2 header. Size: 28 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VpkHeaderV2
    {
        public uint Signature;
        public uint Version;
        public uint TreeSize;
        public uint FileDataSize;
        public uint ArchiveMd5Size;
        public uint OtherMd5Size;
        public uint SignatureSize;
    }
}

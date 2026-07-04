using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Sequence group descriptor (mstudioseqgroup_t).
    /// Size: 104 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlSequenceGroup
    {
        public fixed byte Label[32];
        public fixed byte Name[64];
        public int Unused1;
        public int Unused2;
    }
}

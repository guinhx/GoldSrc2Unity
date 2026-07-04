using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Body part (mstudiobodyparts_t).
    /// Size: 76 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlBodyPart
    {
        public fixed byte Name[64];
        public int NumModels;
        public int Base;
        public int ModelIndex;
    }
}

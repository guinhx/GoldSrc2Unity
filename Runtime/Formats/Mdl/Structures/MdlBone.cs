using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Bone definition (mstudiobone_t).
    /// Size: 112 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlBone
    {
        public fixed byte Name[32];
        public int Parent;
        public int Flags;
        public fixed int BoneController[6];
        public fixed float Value[6];
        public fixed float Scale[6];
    }
}

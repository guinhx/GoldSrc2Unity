using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Animation data offsets per bone (mstudioanim_t).
    /// Contains 6 unsigned short offsets, one per DoF channel (X, Y, Z, XR, YR, ZR).
    /// Each offset is relative to the start of this struct.
    /// Size: 12 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlAnim
    {
        public fixed ushort Offset[6];
    }

    /// <summary>
    /// Compressed animation value (mstudioanimvalue_t).
    /// Union: either a control byte-pair (Valid, Total) or a short value.
    /// Size: 2 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct MdlAnimValue
    {
        [FieldOffset(0)] public byte Valid;
        [FieldOffset(1)] public byte Total;
        [FieldOffset(0)] public short Value;
    }
}

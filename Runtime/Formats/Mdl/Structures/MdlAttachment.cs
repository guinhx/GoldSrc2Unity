using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Attachment point (mstudioattachment_t).
    /// Size: 88 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlAttachment
    {
        public fixed byte Name[32];
        public int Type;
        public int Bone;
        public Vector3F Org;
        public Vector3F Vector0;
        public Vector3F Vector1;
        public Vector3F Vector2;
    }
}

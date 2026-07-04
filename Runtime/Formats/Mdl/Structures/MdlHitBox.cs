using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Hit box / bounding box (mstudiobbox_t).
    /// Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MdlHitBox
    {
        public int Bone;
        public int Group;
        public Vector3F BbMin;
        public Vector3F BbMax;
    }
}

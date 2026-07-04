using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Bone controller (mstudiobonecontroller_t).
    /// Size: 24 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MdlBoneController
    {
        public int Bone;
        public int Type;
        public float Start;
        public float End;
        public int Rest;
        public int Index;
    }
}

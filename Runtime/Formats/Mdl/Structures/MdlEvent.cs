using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Animation event (mstudioevent_t).
    /// Size: 76 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlEvent
    {
        public int Frame;
        public int Event;
        public int Type;
        public fixed byte Options[64];
    }
}

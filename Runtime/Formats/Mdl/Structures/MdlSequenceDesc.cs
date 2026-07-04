using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Sequence description (mstudioseqdesc_t).
    /// Size: 176 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlSequenceDesc
    {
        public fixed byte Label[32];

        public float Fps;
        public int Flags;

        public int Activity;
        public int ActWeight;

        public int NumEvents;
        public int EventIndex;

        public int NumFrames;

        public int NumPivots;
        public int PivotIndex;

        public int MotionType;
        public int MotionBone;
        public Vector3F LinearMovement;
        public int AutoMovePosIndex;
        public int AutoMoveAngleIndex;

        public Vector3F BbMin;
        public Vector3F BbMax;

        public int NumBlends;
        public int AnimIndex;

        public fixed int BlendType[2];
        public fixed float BlendStart[2];
        public fixed float BlendEnd[2];
        public int BlendParent;

        public int SeqGroup;

        public int EntryNode;
        public int ExitNode;
        public int NodeFlags;

        public int NextSeq;
    }
}

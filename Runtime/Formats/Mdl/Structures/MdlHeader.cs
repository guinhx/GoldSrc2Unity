using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Main MDL file header (studiohdr_t). 
    /// Binary layout matches Valve's engine/studio.h exactly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlHeader
    {
        public uint Id;
        public int Version;

        public fixed byte Name[64];
        public int Length;

        public Vector3F EyePosition;
        public Vector3F Min;
        public Vector3F Max;
        public Vector3F BbMin;
        public Vector3F BbMax;

        public int Flags;

        public int NumBones;
        public int BoneIndex;

        public int NumBoneControllers;
        public int BoneControllerIndex;

        public int NumHitBoxes;
        public int HitBoxIndex;

        public int NumSeq;
        public int SeqIndex;

        public int NumSeqGroups;
        public int SeqGroupIndex;

        public int NumTextures;
        public int TextureIndex;
        public int TextureDataIndex;

        public int NumSkinRef;
        public int NumSkinFamilies;
        public int SkinIndex;

        public int NumBodyParts;
        public int BodyPartIndex;

        public int NumAttachments;
        public int AttachmentIndex;

        public int SoundTable;
        public int SoundIndex;
        public int SoundGroups;
        public int SoundGroupIndex;

        public int NumTransitions;
        public int TransitionIndex;
    }

    /// <summary>
    /// Sequence group file header (studioseqhdr_t).
    /// Used for external sequence files (IDSQ magic).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlSequenceHeader
    {
        public uint Id;
        public int Version;
        public fixed byte Name[64];
        public int Length;
    }
}

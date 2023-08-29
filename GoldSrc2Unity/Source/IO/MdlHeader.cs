using System.Runtime.InteropServices;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlHeader
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
    public string Magic;

    public int Version;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    public int Size;
    public Vector3 EyePosition;
    public Vector3 Min;
    public Vector3 Max;
    public Vector3 BoundingBoxMin;
    public Vector3 BoundingBoxMax;
    public int Flags;
    public int NumBones;
    public int BoneIndex;
    public int NumBoneControllers;
    public int BoneControllerIndex;
    public int NumHitBoxes;
    public int HitBoxIndex;
    public int NumSequences;
    public int SequenceIndex;
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
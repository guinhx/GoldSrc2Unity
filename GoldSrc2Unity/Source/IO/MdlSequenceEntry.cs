using System.Runtime.InteropServices;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlSequenceEntry
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Name;

    public float FPS;
    public int Flags;
    public int Activity;
    public int ActivityWeight;
    public int NumEvents;
    public int EventIndex;
    public int NumFrames;
    public int NumPivots;
    public int PivotIndex;
    public int MotionType;
    public int MotionBone;
    public Vector3 LinearMovement;
    public int AutoMovePosIndex;
    public int AutoMoveAngleIndex;
    public Vector3 BoundingBoxMin;
    public Vector3 BoundingBoxMax;
    public int NumBlends;
    public int AnimIndex;
    public int BlendType0;
    public int BlendType1;
    public float BlendStart0;
    public float BlendStart1;
    public float BlendEnd0;
    public float BlendEnd1;
    public float BlendParent;
    public int SeqGroup;
    public int EntryNode;
    public int ExitNode;
    public int NodeFlags;
    public int NextSeq;
}
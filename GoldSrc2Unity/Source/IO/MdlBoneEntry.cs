using System.Runtime.InteropServices;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlBoneEntry
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Name;

    public int Parent;
    public int Flags;

    public int X;
    public int Y;
    public int Z;

    public int RotX;
    public int RotY;
    public int RotZ;

    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 PositionScale;
    public Vector3 RotationScale;
}
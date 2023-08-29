using System.Runtime.InteropServices;
using UnityEngine;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlSequenceFrameEntry
{
    public int Index;
    public Vector3 Position;
    public Vector3 Rotation;
}
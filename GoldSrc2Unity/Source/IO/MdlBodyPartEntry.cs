using System.Runtime.InteropServices;

namespace GoldSrc2Unity.Source.IO;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MdlBodyPartEntry
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;
    public int NumModels;
    public int Base;
    public int ModelIndex;
}
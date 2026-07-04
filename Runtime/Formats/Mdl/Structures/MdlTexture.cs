using System.Runtime.InteropServices;

namespace Source2Unity.Formats.Mdl.Structures
{
    /// <summary>
    /// Texture info (mstudiotexture_t).
    /// Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MdlTexture
    {
        public fixed byte Name[64];
        public int Flags;
        public int Width;
        public int Height;
        public int Index;
    }

    public static class MdlTextureFlags
    {
        public const int FlatShade = 0x0001;
        public const int Chrome = 0x0002;
        public const int FullBright = 0x0004;
        public const int NoMips = 0x0008;
        public const int Alpha = 0x0010;
        public const int Additive = 0x0020;
        public const int Masked = 0x0040;
    }
}

using System;
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

    [Flags]
    public enum MdlTextureFlags
    {
        None = 0x0000,
        FlatShade = 0x0001,
        Chrome = 0x0002,
        FullBright = 0x0004,
        NoMips = 0x0008,
        Alpha = 0x0010,
        Additive = 0x0020,
        Masked = 0x0040
    }
}

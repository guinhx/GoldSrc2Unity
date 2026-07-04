using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl.Structures;
using Source2Unity.Formats.Vtf;

namespace Source2Unity.Formats.Vtf.Parsers
{
    public sealed class VtfParseResult
    {
        public int MajorVersion { get; init; }
        public int MinorVersion { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public uint Flags { get; init; }
        public VtfImageFormat Format { get; init; }
        public int MipCount { get; init; }
        public int FrameCount { get; init; } = 1;
        public int FaceCount { get; init; } = 1;
        public int Depth { get; init; } = 1;
        public Vector3F Reflectivity { get; init; }
        public byte[] ImageChain { get; init; }
        public byte[] BaseMipData { get; init; }

        public bool IsCubemap => FaceCount > 1 || (Flags & VtfConstants.FlagEnvmap) != 0;
        public bool IsAnimated => FrameCount > 1;
        public bool IsVolume => Depth > 1;
    }

    public interface IVtfParser
    {
        VtfParseResult Parse(BinaryStreamReader reader);
    }
}

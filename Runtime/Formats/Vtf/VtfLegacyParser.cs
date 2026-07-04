using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl.Structures;
using Source2Unity.Formats.Vtf.Parsers;

namespace Source2Unity.Formats.Vtf
{
    /// <summary>
    /// Parses VTF 7.0–7.2 (legacy layout: header + thumbnail + mip chain).
    /// Supports flat 2D textures (single frame/face/depth).
    /// </summary>
    internal static class VtfLegacyParser
    {
        public static VtfParseResult Parse(BinaryStreamReader reader)
        {
            reader.Seek(0);

            uint signature = reader.ReadUInt32();
            if (signature != VtfConstants.Signature)
                throw new InvalidDataException($"Invalid VTF signature: 0x{signature:X8}");

            int major = (int)reader.ReadUInt32();
            int minor = (int)reader.ReadUInt32();
            if (major != VtfConstants.MajorVersion || minor > 2)
                throw new NotSupportedException($"VTF legacy parser supports 7.0–7.2, got {major}.{minor}.");

            uint headerSize = reader.ReadUInt32();
            int width = reader.ReadUInt16();
            int height = reader.ReadUInt16();
            uint flags = reader.ReadUInt32();
            int frameCount = reader.ReadUInt16();
            reader.ReadUInt16(); // startFrame

            reader.ReadUInt32(); // padding

            var reflectivity = new Vector3F
            {
                X = reader.ReadFloat(),
                Y = reader.ReadFloat(),
                Z = reader.ReadFloat()
            };

            reader.ReadUInt32(); // padding
            reader.ReadFloat(); // bumpMapScale

            var format = (VtfImageFormat)reader.ReadUInt32();
            int mipCount = reader.ReadByte();
            reader.ReadUInt32(); // thumbnailFormat
            int thumbWidth = reader.ReadByte();
            int thumbHeight = reader.ReadByte();

            int depth = 1;
            if (minor >= 2)
                depth = reader.ReadUInt16();

            if (headerSize > 0 && headerSize > reader.Position)
                reader.Seek(headerSize);

            int thumbSize = VtfMipLayout.ComputeThumbnailSize(thumbWidth, thumbHeight);
            if (thumbSize > 0)
                reader.ReadBytes(thumbSize);

            int faces = (flags & (1 << 14)) != 0 ? (minor == 0 ? 6 : 7) : 1;
            int mipLevels = Math.Max(1, mipCount);

            int chainSize = VtfMipLayout.ComputeTotalImageChainSize(format, width, height, mipLevels, frameCount, faces, depth);
            byte[] chain = reader.ReadBytes(chainSize);

            byte[] baseMip = VtfMipLayout.ExtractBaseMip(chain, format, width, height, mipLevels, frameCount, faces, depth);

            return new VtfParseResult
            {
                MajorVersion = major,
                MinorVersion = minor,
                Width = width,
                Height = height,
                Flags = flags,
                Format = format,
                MipCount = mipLevels,
                FrameCount = frameCount,
                FaceCount = faces,
                Depth = depth,
                Reflectivity = reflectivity,
                ImageChain = chain,
                BaseMipData = baseMip
            };
        }
    }
}

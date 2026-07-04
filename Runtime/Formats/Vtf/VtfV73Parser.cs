using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl.Structures;
using Source2Unity.Formats.Vtf.Parsers;

namespace Source2Unity.Formats.Vtf
{
    /// <summary>
    /// Parses VTF 7.3+ using the resource dictionary to locate image data.
    /// Supports flat 2D textures (single frame/face/depth).
    /// </summary>
    internal static class VtfV73Parser
    {
        private const int ResourceEntrySize = 8;

        public static VtfParseResult Parse(BinaryStreamReader reader)
        {
            reader.Seek(0);

            uint signature = reader.ReadUInt32();
            if (signature != VtfConstants.Signature)
                throw new InvalidDataException($"Invalid VTF signature: 0x{signature:X8}");

            int major = (int)reader.ReadUInt32();
            int minor = (int)reader.ReadUInt32();
            if (major != VtfConstants.MajorVersion || minor < 3)
                throw new NotSupportedException($"VTF 7.3+ parser expected minor >= 3, got {major}.{minor}.");

            uint headerSize = reader.ReadUInt32();
            int width = reader.ReadUInt16();
            int height = reader.ReadUInt16();
            uint flags = reader.ReadUInt32();
            int frameCount = reader.ReadUInt16();
            reader.ReadUInt16();

            reader.ReadUInt32();

            var reflectivity = new Vector3F
            {
                X = reader.ReadFloat(),
                Y = reader.ReadFloat(),
                Z = reader.ReadFloat()
            };

            reader.ReadUInt32();
            reader.ReadFloat();

            var format = (VtfImageFormat)reader.ReadUInt32();
            int mipCount = reader.ReadByte();
            reader.ReadUInt32();
            reader.ReadByte();
            reader.ReadByte();

            int depth = reader.ReadUInt16();
            reader.ReadBytes(3);
            int resourceCount = (int)reader.ReadUInt32();
            reader.ReadBytes(8);

            long resourceTableStart = reader.Position;
            uint imageOffset = 0;

            for (int i = 0; i < resourceCount; i++)
            {
                reader.Seek(resourceTableStart + i * ResourceEntrySize);
                int type = reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
                byte resourceFlags = reader.ReadByte();
                uint data = reader.ReadUInt32();

                if (type == VtfConstants.ResourceTypeImage)
                {
                    if ((resourceFlags & VtfConstants.ResourceFlagInline) != 0)
                        throw new NotSupportedException("Inline VTF IMAGE resources are not supported.");

                    imageOffset = data;
                    break;
                }
            }

            if (imageOffset == 0)
                throw new InvalidDataException("VTF 7.3+ file has no IMAGE resource.");

            reader.Seek(imageOffset);

            int faces = (flags & (1 << 14)) != 0 ? 6 : 1;
            int mipLevels = Math.Max(1, mipCount);
            int chainSize = VtfMipLayout.ComputeTotalImageChainSize(format, width, height, mipLevels, frameCount, faces, Math.Max(1, depth));
            byte[] chain = reader.ReadBytes(chainSize);

            byte[] baseMip = VtfMipLayout.ExtractBaseMip(chain, format, width, height, mipLevels, frameCount, faces, Math.Max(1, depth));

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
                Depth = Math.Max(1, depth),
                Reflectivity = reflectivity,
                ImageChain = chain,
                BaseMipData = baseMip
            };
        }

    }
}

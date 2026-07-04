using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Mdl.Parsers;

namespace Source2Unity.Formats.Mdl
{
    public sealed class MdlFile : IFormatReader<MdlParseResult>
    {
        public MdlParseResult Read(Stream stream)
        {
            using var reader = new BinaryStreamReader(stream, leaveOpen: true);
            var version = MdlVersionDetector.Detect(reader);
            reader.Position = 0;
            return ParseForVersion(reader, version, null);
        }

        public MdlParseResult Read(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("MDL file not found.", filePath);

            using var reader = new BinaryStreamReader(filePath);
            var version = MdlVersionDetector.Detect(reader);
            reader.Position = 0;
            return ParseForVersion(reader, version, filePath);
        }

        private static MdlParseResult ParseForVersion(BinaryStreamReader reader, MdlVersion version, string filePath)
        {
            switch (version)
            {
                case MdlVersion.GoldSrc:
                    var parser = new MdlV10Parser();
                    return parser.Parse(reader, filePath);

                case MdlVersion.Quake1:
                    throw new NotSupportedException("Quake 1 MDL (v6) parsing is not yet implemented.");

                case MdlVersion.Source:
                    throw new NotSupportedException("Source Engine MDL (v44-49) parsing is not yet implemented.");

                case MdlVersion.GoldSrcSequence:
                    throw new InvalidDataException("Cannot open a sequence group file directly. Open the main MDL file instead.");

                default:
                    throw new InvalidDataException("Unknown or unsupported MDL format.");
            }
        }
    }
}

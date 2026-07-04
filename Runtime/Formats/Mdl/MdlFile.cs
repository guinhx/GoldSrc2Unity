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
            return Read(stream, logicalPath: null, resolver: null);
        }

        public MdlParseResult Read(Stream stream, string logicalPath, IContentResolver resolver = null)
        {
            using var reader = new BinaryStreamReader(stream, leaveOpen: true);
            var version = MdlVersionDetector.Detect(reader);
            reader.Position = 0;
            return ParseForVersion(reader, version, logicalPath, resolver);
        }

        public MdlParseResult Read(string filePath)
        {
            return Read(filePath, new DiskContentResolver());
        }

        public MdlParseResult Read(string logicalPath, IContentResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            if (!resolver.TryOpenRead(logicalPath, out var stream))
                throw new FileNotFoundException("MDL file not found.", logicalPath);

            using (stream)
            {
                using var reader = new BinaryStreamReader(stream, leaveOpen: false);
                var version = MdlVersionDetector.Detect(reader);
                reader.Position = 0;
                return ParseForVersion(reader, version, logicalPath, resolver);
            }
        }

        private static MdlParseResult ParseForVersion(
            BinaryStreamReader reader,
            MdlVersion version,
            string logicalPath,
            IContentResolver resolver)
        {
            switch (version)
            {
                case MdlVersion.GoldSrc:
                    var parser = new MdlV10Parser();
                    return parser.Parse(reader, logicalPath, resolver);

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

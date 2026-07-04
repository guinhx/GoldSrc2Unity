using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vtf.Parsers;

namespace Source2Unity.Formats.Vtf
{
    public sealed class VtfFile : IFormatReader<VtfParseResult>
    {
        public VtfParseResult Read(Stream stream)
        {
            using var reader = new BinaryStreamReader(stream, leaveOpen: true);
            return Parse(reader);
        }

        public VtfParseResult Read(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryStreamReader(stream, leaveOpen: false);
            return Parse(reader);
        }

        private static VtfParseResult Parse(BinaryStreamReader reader)
        {
            var version = VtfVersionDetector.Detect(reader);
            reader.Seek(0);

            return version switch
            {
                VtfVersion.Legacy => VtfLegacyParser.Parse(reader),
                VtfVersion.V73 => VtfV73Parser.Parse(reader),
                _ => throw new InvalidDataException("Unknown or unsupported VTF format.")
            };
        }
    }
}

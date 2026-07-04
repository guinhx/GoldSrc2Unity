using System;
using System.Collections.Generic;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk.Structures;

namespace Source2Unity.Formats.Vpk.Parsers
{
    public sealed class VpkV1Parser : IVpkParser
    {
        public VpkParseResult Parse(string filePath)
        {
            using var reader = new BinaryStreamReader(filePath);
            var header = reader.ReadStruct<VpkHeaderV1>();

            if (header.Signature != VpkConstants.Signature)
                throw new InvalidDataException($"Invalid VPK signature: 0x{header.Signature:X8}");
            if (header.Version != 1)
                throw new InvalidDataException($"Expected VPK v1, got version {header.Version}");

            var entries = VpkTreeParser.ParseTree(reader);

            return new VpkParseResult
            {
                Version = VpkVersion.V1,
                TreeSize = header.TreeSize,
                HeaderSize = VpkConstants.HeaderSizeV1,
                DirectoryFilePath = filePath,
                Entries = entries
            };
        }
    }
}

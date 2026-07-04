using System;
using System.Collections.Generic;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk.Structures;

namespace Source2Unity.Formats.Vpk.Parsers
{
    public sealed class VpkV2Parser : IVpkParser
    {
        public VpkParseResult Parse(string filePath)
        {
            using var reader = new BinaryStreamReader(filePath);
            var header = reader.ReadStruct<VpkHeaderV2>();

            if (header.Signature != VpkConstants.Signature)
                throw new InvalidDataException($"Invalid VPK signature: 0x{header.Signature:X8}");
            if (header.Version != 2)
                throw new InvalidDataException($"Expected VPK v2, got version {header.Version}");

            var entries = VpkTreeParser.ParseTree(reader);

            return new VpkParseResult
            {
                Version = VpkVersion.V2,
                TreeSize = header.TreeSize,
                HeaderSize = VpkConstants.HeaderSizeV2,
                DirectoryFilePath = filePath,
                Entries = entries
            };
        }
    }
}

using System.Collections.Generic;

namespace Source2Unity.Formats.Vpk.Parsers
{
    public interface IVpkParser
    {
        VpkParseResult Parse(string filePath);
    }

    public sealed class VpkParseResult
    {
        public VpkVersion Version { get; init; }
        public uint TreeSize { get; init; }
        public int HeaderSize { get; init; }
        public string DirectoryFilePath { get; init; }
        public IReadOnlyDictionary<string, List<VpkFileEntry>> Entries { get; init; }
    }

    public sealed class VpkFileEntry
    {
        public string Extension { get; init; }
        public string DirectoryPath { get; init; }
        public string FileName { get; init; }
        public uint Crc32 { get; init; }
        public ushort ArchiveIndex { get; init; }
        public uint EntryOffset { get; init; }
        public uint EntryLength { get; init; }
        public byte[] PreloadData { get; init; }

        public string GetFullPath()
        {
            string dir = string.IsNullOrEmpty(DirectoryPath) || DirectoryPath == " "
                ? ""
                : DirectoryPath + "/";
            return $"{dir}{FileName}.{Extension}";
        }

        public uint TotalLength => (uint)(PreloadData?.Length ?? 0) + EntryLength;
    }
}

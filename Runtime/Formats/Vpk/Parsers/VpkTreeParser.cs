using System;
using System.Collections.Generic;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk.Structures;

namespace Source2Unity.Formats.Vpk.Parsers
{
    internal static class VpkTreeParser
    {
        public static Dictionary<string, List<VpkFileEntry>> ParseTree(BinaryStreamReader reader)
        {
            var entries = new Dictionary<string, List<VpkFileEntry>>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                string extension = reader.ReadNullTerminatedString();
                if (string.IsNullOrEmpty(extension))
                    break;

                while (true)
                {
                    string directory = reader.ReadNullTerminatedString();
                    if (string.IsNullOrEmpty(directory))
                        break;

                    while (true)
                    {
                        string fileName = reader.ReadNullTerminatedString();
                        if (string.IsNullOrEmpty(fileName))
                            break;

                        var dirEntry = reader.ReadStruct<VpkDirectoryEntry>();

                        byte[] preloadData = Array.Empty<byte>();
                        if (dirEntry.PreloadSize > 0)
                            preloadData = reader.ReadBytes(dirEntry.PreloadSize);

                        var fileEntry = new VpkFileEntry
                        {
                            Extension = extension,
                            DirectoryPath = directory,
                            FileName = fileName,
                            Crc32 = dirEntry.Crc32,
                            ArchiveIndex = dirEntry.ArchiveIndex,
                            EntryOffset = dirEntry.EntryOffset,
                            EntryLength = dirEntry.EntryLength,
                            PreloadData = preloadData
                        };

                        if (!entries.TryGetValue(extension, out var list))
                        {
                            list = new List<VpkFileEntry>();
                            entries[extension] = list;
                        }
                        list.Add(fileEntry);
                    }
                }
            }

            return entries;
        }
    }
}

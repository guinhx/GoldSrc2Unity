using System;
using System.Collections.Generic;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk.Parsers;
using Source2Unity.Formats.Vpk.Structures;

namespace Source2Unity.Formats.Vpk
{
    public sealed class VpkArchive : IFormatReader<VpkParseResult>, IDisposable
    {
        private VpkParseResult _result;
        private readonly Dictionary<int, FileStream> _archiveStreams = new();

        public VpkParseResult Read(Stream stream)
        {
            throw new NotSupportedException("VPK reading requires file paths for multi-archive access. Use Read(string) instead.");
        }

        public VpkParseResult Read(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("VPK file not found.", filePath);

            var version = DetectVersion(filePath);
            IVpkParser parser = version switch
            {
                VpkVersion.V1 => new VpkV1Parser(),
                VpkVersion.V2 => new VpkV2Parser(),
                _ => throw new InvalidDataException("Unknown or unsupported VPK format.")
            };

            _result = parser.Parse(filePath);
            return _result;
        }

        public byte[] ReadEntry(VpkFileEntry entry)
        {
            if (_result == null)
                throw new InvalidOperationException("No VPK has been read. Call Read() first.");

            byte[] preload = entry.PreloadData ?? Array.Empty<byte>();

            if (entry.EntryLength == 0)
                return preload;

            byte[] archiveData = new byte[entry.EntryLength];

            if (entry.ArchiveIndex == VpkConstants.DirectoryArchiveIndex)
            {
                long dataStart = _result.HeaderSize + _result.TreeSize + entry.EntryOffset;
                using var dirStream = File.OpenRead(_result.DirectoryFilePath);
                dirStream.Seek(dataStart, SeekOrigin.Begin);
                ReadFully(dirStream, archiveData);
            }
            else
            {
                var stream = GetArchiveStream(entry.ArchiveIndex);
                stream.Seek(entry.EntryOffset, SeekOrigin.Begin);
                ReadFully(stream, archiveData);
            }

            if (preload.Length == 0)
                return archiveData;

            var combined = new byte[preload.Length + archiveData.Length];
            Buffer.BlockCopy(preload, 0, combined, 0, preload.Length);
            Buffer.BlockCopy(archiveData, 0, combined, preload.Length, archiveData.Length);
            return combined;
        }

        private static void ReadFully(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected end of stream: read {offset} of {buffer.Length} bytes.");
                offset += read;
            }
        }

        public VpkFileEntry FindEntry(string path)
        {
            if (_result == null)
                throw new InvalidOperationException("No VPK has been read. Call Read() first.");

            string extension = Path.GetExtension(path).TrimStart('.');
            if (string.IsNullOrEmpty(extension)) extension = " ";
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? " ";
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (string.IsNullOrEmpty(directory)) directory = " ";

            if (!_result.Entries.TryGetValue(extension, out var list))
                return null;

            foreach (var entry in list)
            {
                if (string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.DirectoryPath, directory, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private FileStream GetArchiveStream(int archiveIndex)
        {
            if (_archiveStreams.TryGetValue(archiveIndex, out var existing))
                return existing;

            string archivePath = GetArchivePath(_result.DirectoryFilePath, archiveIndex);
            if (!File.Exists(archivePath))
                throw new FileNotFoundException($"VPK archive chunk not found: {archivePath}", archivePath);

            var stream = File.OpenRead(archivePath);
            _archiveStreams[archiveIndex] = stream;
            return stream;
        }

        private static string GetArchivePath(string dirFilePath, int archiveIndex)
        {
            string dir = Path.GetDirectoryName(dirFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(dirFilePath);

            if (baseName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(0, baseName.Length - 4);

            return Path.Combine(dir, $"{baseName}_{archiveIndex:D3}.vpk");
        }

        private static VpkVersion DetectVersion(string filePath)
        {
            using var reader = new BinaryStreamReader(filePath);
            if (reader.Length < 12)
                return VpkVersion.Unknown;

            uint signature = reader.ReadUInt32();
            if (signature != VpkConstants.Signature)
                return VpkVersion.Unknown;

            uint version = reader.ReadUInt32();
            return version switch
            {
                1 => VpkVersion.V1,
                2 => VpkVersion.V2,
                _ => VpkVersion.Unknown
            };
        }

        public void Dispose()
        {
            foreach (var stream in _archiveStreams.Values)
                stream.Dispose();
            _archiveStreams.Clear();
        }
    }
}

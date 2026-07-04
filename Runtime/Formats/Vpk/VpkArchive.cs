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
        private Dictionary<string, VpkFileEntry> _entryIndex;
        private readonly IVpkChunkSource _chunkSource;
        private readonly Dictionary<int, Stream> _archiveStreams = new();

        public VpkParseResult Result => _result;

        public VpkArchive() : this(new DiskVpkChunkSource())
        {
        }

        public VpkArchive(IVpkChunkSource chunkSource)
        {
            _chunkSource = chunkSource ?? throw new ArgumentNullException(nameof(chunkSource));
        }

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
            BuildEntryIndex();
            return _result;
        }

        private void BuildEntryIndex()
        {
            _entryIndex = new Dictionary<string, VpkFileEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in _result.Entries)
            {
                foreach (var entry in group.Value)
                {
                    string key = VpkPath.ToLookupKey(entry.GetFullPath());
                    _entryIndex[key] = entry;
                }
            }
        }

        public bool Contains(string path)
        {
            EnsureLoaded();
            return _entryIndex.ContainsKey(VpkPath.ToLookupKey(path));
        }

        public bool TryFindEntry(string path, out VpkFileEntry entry)
        {
            EnsureLoaded();
            return _entryIndex.TryGetValue(VpkPath.ToLookupKey(path), out entry);
        }

        public VpkFileEntry FindEntry(string path)
        {
            TryFindEntry(path, out var entry);
            return entry;
        }

        public byte[] ReadEntry(VpkFileEntry entry)
        {
            using var stream = ReadEntryStream(entry);
            if (stream is MemoryStream ms)
                return ms.ToArray();

            var buffer = new byte[entry.TotalLength];
            ReadFully(stream, buffer);
            return buffer;
        }

        public Stream ReadEntryStream(VpkFileEntry entry)
        {
            EnsureLoaded();
            ValidateEntryBounds(entry);

            byte[] preload = entry.PreloadData ?? Array.Empty<byte>();

            if (entry.EntryLength == 0)
                return new MemoryStream(preload, writable: false);

            Stream payloadStream = OpenEntryPayloadStream(entry);

            if (preload.Length == 0)
                return payloadStream;

            return new PreloadedEntryStream(preload, payloadStream);
        }

        private Stream OpenEntryPayloadStream(VpkFileEntry entry)
        {
            if (entry.ArchiveIndex == VpkConstants.DirectoryArchiveIndex)
            {
                long dataStart = _result.HeaderSize + _result.TreeSize + entry.EntryOffset;
                var stream = _chunkSource.OpenDirectoryFile(_result.DirectoryFilePath);
                stream.Seek(dataStart, SeekOrigin.Begin);
                return new BoundedStream(stream, entry.EntryLength, ownsBase: true);
            }

            var archiveStream = GetArchiveStream(entry.ArchiveIndex);
            archiveStream.Seek(entry.EntryOffset, SeekOrigin.Begin);
            return new BoundedStream(archiveStream, entry.EntryLength, ownsBase: false);
        }

        private void ValidateEntryBounds(VpkFileEntry entry)
        {
            if (entry.EntryLength == 0)
                return;

            if (entry.ArchiveIndex == VpkConstants.DirectoryArchiveIndex)
            {
                long dataStart = _result.HeaderSize + _result.TreeSize + entry.EntryOffset;
                long dataEnd = dataStart + entry.EntryLength;
                long fileLength;
                using (var dirProbe = _chunkSource.OpenDirectoryFile(_result.DirectoryFilePath))
                    fileLength = dirProbe.Length;
                if (dataEnd > fileLength)
                {
                    throw new InvalidDataException(
                        $"VPK entry '{entry.GetFullPath()}' exceeds directory file bounds: " +
                        $"offset {entry.EntryOffset}, length {entry.EntryLength}, file size {fileLength}.");
                }
            }
            else
            {
                var stream = GetArchiveStream(entry.ArchiveIndex);
                long dataEnd = entry.EntryOffset + entry.EntryLength;
                if (dataEnd > stream.Length)
                {
                    throw new InvalidDataException(
                        $"VPK entry '{entry.GetFullPath()}' exceeds chunk bounds: " +
                        $"archive {entry.ArchiveIndex}, offset {entry.EntryOffset}, length {entry.EntryLength}, chunk size {stream.Length}.");
                }
            }
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

        private void EnsureLoaded()
        {
            if (_result == null || _entryIndex == null)
                throw new InvalidOperationException("No VPK has been read. Call Read() first.");
        }

        private Stream GetArchiveStream(int archiveIndex)
        {
            if (_archiveStreams.TryGetValue(archiveIndex, out var existing))
                return existing;

            var stream = _chunkSource.OpenChunk(_result.DirectoryFilePath, archiveIndex);
            _archiveStreams[archiveIndex] = stream;
            return stream;
        }

        public static string GetChunkPath(string dirFilePath, int archiveIndex)
        {
            string dir = Path.GetDirectoryName(dirFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(dirFilePath);

            if (baseName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(0, baseName.Length - 4);

            return Path.Combine(dir, $"{baseName}_{archiveIndex:D3}.vpk");
        }

        private static string GetArchivePath(string dirFilePath, int archiveIndex)
            => GetChunkPath(dirFilePath, archiveIndex);

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
            _entryIndex = null;
            _result = null;
        }

        /// <summary>Reads from preload bytes then continues into a secondary stream.</summary>
        private sealed class PreloadedEntryStream : Stream
        {
            private readonly byte[] _preload;
            private readonly Stream _payload;
            private int _preloadPosition;
            private bool _disposed;

            public PreloadedEntryStream(byte[] preload, Stream payload)
            {
                _preload = preload;
                _payload = payload;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _preload.Length + _payload.Length;
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;

                if (_preloadPosition < _preload.Length)
                {
                    int preloadRead = Math.Min(count, _preload.Length - _preloadPosition);
                    Buffer.BlockCopy(_preload, _preloadPosition, buffer, offset, preloadRead);
                    _preloadPosition += preloadRead;
                    totalRead += preloadRead;
                    offset += preloadRead;
                    count -= preloadRead;
                }

                if (count > 0)
                    totalRead += _payload.Read(buffer, offset, count);

                return totalRead;
            }

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (_disposed) return;
                if (disposing)
                    _payload.Dispose();
                _disposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>Exposes a fixed-length region of an underlying stream.</summary>
        private sealed class BoundedStream : Stream
        {
            private readonly Stream _base;
            private readonly long _length;
            private readonly bool _ownsBase;
            private long _position;
            private bool _disposed;

            public BoundedStream(Stream baseStream, long length, bool ownsBase)
            {
                _base = baseStream;
                _length = length;
                _ownsBase = ownsBase;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = _length - _position;
                if (remaining <= 0)
                    return 0;

                int toRead = (int)Math.Min(count, remaining);
                int read = _base.Read(buffer, offset, toRead);
                _position += read;
                return read;
            }

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (_disposed) return;
                if (disposing && _ownsBase)
                    _base.Dispose();
                _disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}

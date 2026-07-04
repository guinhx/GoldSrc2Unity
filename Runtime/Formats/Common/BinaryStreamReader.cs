using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Source2Unity.Formats.Common
{
    public sealed class BinaryStreamReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private readonly bool _leaveOpen;

        public BinaryStreamReader(Stream stream, bool leaveOpen = false)
        {
            _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen);
            _leaveOpen = leaveOpen;
        }

        public BinaryStreamReader(string filePath)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }

        public long Position
        {
            get => _reader.BaseStream.Position;
            set => _reader.BaseStream.Position = value;
        }

        public long Length => _reader.BaseStream.Length;

        public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
        {
            _reader.BaseStream.Seek(offset, origin);
        }

        public unsafe T ReadStruct<T>() where T : unmanaged
        {
            int size = sizeof(T);
            Span<byte> buffer = stackalloc byte[size];
            int bytesRead = _reader.Read(buffer);
            if (bytesRead < size)
                throw new EndOfStreamException($"Expected {size} bytes for {typeof(T).Name}, got {bytesRead}.");
            return MemoryMarshal.Read<T>(buffer);
        }

        public unsafe T[] ReadStructArray<T>(int count) where T : unmanaged
        {
            if (count <= 0) return Array.Empty<T>();
            int size = sizeof(T);
            int totalBytes = size * count;
            var bytes = _reader.ReadBytes(totalBytes);
            if (bytes.Length < totalBytes)
                throw new EndOfStreamException($"Expected {totalBytes} bytes for {typeof(T).Name}[{count}], got {bytes.Length}.");
            return MemoryMarshal.Cast<byte, T>(bytes.AsSpan()).ToArray();
        }

        public byte[] ReadBytes(int count)
        {
            if (count <= 0) return Array.Empty<byte>();
            var bytes = _reader.ReadBytes(count);
            if (bytes.Length < count)
                throw new EndOfStreamException($"Expected {count} bytes, got {bytes.Length}.");
            return bytes;
        }

        public byte ReadByte() => _reader.ReadByte();
        public short ReadInt16() => _reader.ReadInt16();
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public int ReadInt32() => _reader.ReadInt32();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public float ReadFloat() => _reader.ReadSingle();

        public short[] ReadInt16Array(int count)
        {
            if (count <= 0) return Array.Empty<short>();
            var result = new short[count];
            for (int i = 0; i < count; i++)
                result[i] = _reader.ReadInt16();
            return result;
        }

        public ushort[] ReadUInt16Array(int count)
        {
            if (count <= 0) return Array.Empty<ushort>();
            var result = new ushort[count];
            for (int i = 0; i < count; i++)
                result[i] = _reader.ReadUInt16();
            return result;
        }

        public string ReadFixedString(int length)
        {
            var bytes = _reader.ReadBytes(length);
            int nullIndex = Array.IndexOf(bytes, (byte)0);
            int effectiveLength = nullIndex >= 0 ? nullIndex : length;
            return Encoding.ASCII.GetString(bytes, 0, effectiveLength);
        }

        public string ReadNullTerminatedString()
        {
            var sb = new StringBuilder();
            byte b;
            while ((b = _reader.ReadByte()) != 0)
                sb.Append((char)b);
            return sb.ToString();
        }

        public void Dispose()
        {
            if (!_leaveOpen)
                _reader.Dispose();
        }
    }
}

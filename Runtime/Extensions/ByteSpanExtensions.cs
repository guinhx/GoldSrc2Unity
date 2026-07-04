using System;
using System.Runtime.InteropServices;

namespace Source2Unity.Extensions
{
    public static class ByteSpanExtensions
    {
        public static T ToStruct<T>(this ReadOnlySpan<byte> span) where T : unmanaged
        {
            return MemoryMarshal.Read<T>(span);
        }

        public static unsafe string ToFixedString(this ReadOnlySpan<byte> span, int maxLength)
        {
            int length = span.Slice(0, Math.Min(span.Length, maxLength)).IndexOf((byte)0);
            if (length < 0) length = Math.Min(span.Length, maxLength);
            return System.Text.Encoding.ASCII.GetString(span.Slice(0, length));
        }
    }
}

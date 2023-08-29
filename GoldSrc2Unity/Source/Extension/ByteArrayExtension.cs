using System;
using System.Runtime.InteropServices;

namespace GoldSrc2Unity.Source.Extension;

public static class ByteArrayExtension
{
    public static T ToStruct<T>(this byte[] bytes) where T : struct
    {
        var structSize = Marshal.SizeOf(typeof(T));

        if (bytes.Length < structSize)
            throw new ArgumentException("Byte array is too small to convert to the struct.");

        var ptr = Marshal.AllocHGlobal(structSize);

        try
        {
            Marshal.Copy(bytes, 0, ptr, structSize);
            return (T)Marshal.PtrToStructure(ptr, typeof(T))!;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
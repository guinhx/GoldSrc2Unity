using System.IO;
using System.Runtime.InteropServices;

namespace GoldSrc2Unity.Source.Extension;

public static class BinaryReaderExtension
{
    public static short[] ReadShortArray(this BinaryReader br, int num)
    {
        var arr = new short[num];
        for (var i = 0; i < num; i++) arr[i] = br.ReadInt16();
        return arr;
    }
    public static string ReadString(this BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
    public static T ReadStruct<T>(this BinaryReader reader) where T : struct
    {
        return reader.ReadBytes(Marshal.SizeOf<T>()).ToStruct<T>();
    }
}
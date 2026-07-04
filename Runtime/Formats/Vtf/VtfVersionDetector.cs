using System.IO;
using Source2Unity.Formats.Common;

namespace Source2Unity.Formats.Vtf
{
    internal enum VtfVersion
    {
        Unknown,
        Legacy, // 7.0–7.2
        V73     // 7.3+
    }

    internal static class VtfVersionDetector
    {
        public static VtfVersion Detect(BinaryStreamReader reader)
        {
            long saved = reader.Position;
            reader.Seek(0);

            if (reader.Length - reader.Position < 12)
            {
                reader.Seek(saved);
                return VtfVersion.Unknown;
            }

            uint signature = reader.ReadUInt32();
            if (signature != VtfConstants.Signature)
            {
                reader.Seek(saved);
                return VtfVersion.Unknown;
            }

            reader.ReadUInt32(); // major
            int minor = (int)reader.ReadUInt32();

            reader.Seek(saved);
            return minor >= 3 ? VtfVersion.V73 : VtfVersion.Legacy;
        }
    }
}

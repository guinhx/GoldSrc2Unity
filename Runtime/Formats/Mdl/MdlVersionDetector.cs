using Source2Unity.Formats.Common;

namespace Source2Unity.Formats.Mdl
{
    public static class MdlVersionDetector
    {
        public static MdlVersion Detect(BinaryStreamReader reader)
        {
            long originalPosition = reader.Position;
            try
            {
                if (reader.Length < 8)
                    return MdlVersion.Unknown;

                reader.Position = 0;
                uint magic = reader.ReadUInt32();
                int version = reader.ReadInt32();

                return Classify(magic, version);
            }
            finally
            {
                reader.Position = originalPosition;
            }
        }

        public static MdlVersion Classify(uint magic, int version)
        {
            switch (magic)
            {
                case MdlConstants.MagicIdpo:
                    if (version == MdlConstants.VersionQuake1)
                        return MdlVersion.Quake1;
                    break;

                case MdlConstants.MagicIdst:
                    if (version == MdlConstants.VersionGoldSrc)
                        return MdlVersion.GoldSrc;
                    if (version >= MdlConstants.VersionSourceMin && version <= MdlConstants.VersionSourceMax)
                        return MdlVersion.Source;
                    break;

                case MdlConstants.MagicIdsq:
                    if (version == MdlConstants.VersionGoldSrc)
                        return MdlVersion.GoldSrcSequence;
                    break;
            }

            return MdlVersion.Unknown;
        }
    }
}

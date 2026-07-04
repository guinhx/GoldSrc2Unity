namespace Source2Unity.Formats.Mdl
{
    public static class MdlConstants
    {
        public const uint MagicIdst = 0x54534449; // "IDST" little-endian
        public const uint MagicIdsq = 0x51534449; // "IDSQ" little-endian
        public const uint MagicIdpo = 0x4F504449; // "IDPO" little-endian

        public const int VersionGoldSrc = 10;
        public const int VersionQuake1 = 6;
        public const int VersionSourceMin = 44;
        public const int VersionSourceMax = 49;

        public const int MaxStudioBones = 128;
        public const int MaxStudioBodyParts = 32;
        public const int MaxStudioModels = 32;
        public const int MaxStudioMeshes = 256;
        public const int MaxStudioSequences = 2048;
        public const int MaxStudioSkins = 100;
        public const int MaxStudioEvents = 1024;
    }
}

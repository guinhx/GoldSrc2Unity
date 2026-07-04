namespace Source2Unity.Formats.Vtf
{
    public static class VtfConstants
    {
        public const uint Signature = 0x00565446; // "VTF\0" little-endian
        public const int MajorVersion = 7;
        public const int ResourceFlagInline = 0x02;
        public const int ResourceTypeImage = 0x000030;
        public const int ResourceTypeThumbnail = 0x000001;

        public const uint FlagEnvmap = 0x00008000;
        public const uint FlagCubemap = 0x00004000;
    }
}

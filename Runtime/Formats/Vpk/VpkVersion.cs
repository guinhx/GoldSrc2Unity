namespace Source2Unity.Formats.Vpk
{
    public enum VpkVersion
    {
        Unknown,
        V1,
        V2
    }

    public static class VpkConstants
    {
        public const uint Signature = 0x55AA1234;
        public const int HeaderSizeV1 = 12;
        public const int HeaderSizeV2 = 28;
        public const ushort DirectoryArchiveIndex = 0x7FFF;
        public const ushort EntryTerminator = 0xFFFF;
    }
}

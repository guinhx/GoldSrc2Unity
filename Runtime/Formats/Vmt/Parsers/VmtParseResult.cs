using Source2Unity.Formats.KeyValues;

namespace Source2Unity.Formats.Vmt.Parsers
{
    public sealed class VmtParseResult
    {
        public string LogicalPath { get; init; }
        public string ShaderName { get; init; }
        public KvObject Root { get; init; }
    }
}

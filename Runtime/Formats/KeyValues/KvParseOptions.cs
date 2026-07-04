using System.Collections.Generic;
using Source2Unity.Formats.Common;

namespace Source2Unity.Formats.KeyValues
{
    public sealed class KvParseOptions
    {
        public static KvParseOptions Default { get; } = new KvParseOptions();

        /// <summary>Optional resolver for #include directives.</summary>
        public IContentResolver IncludeResolver { get; init; }

        /// <summary>Logical path of the file being parsed (for relative #include).</summary>
        public string SourcePath { get; init; }

        /// <summary>Tracks include paths to prevent circular #include loops.</summary>
        public HashSet<string> IncludeStack { get; init; }
    }
}

using System;
using System.IO;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.KeyValues;
using Source2Unity.Formats.Vmt.Parsers;

namespace Source2Unity.Formats.Vmt
{
    public sealed class VmtFile : IFormatReader<VmtParseResult>
    {
        public VmtParseResult Read(Stream stream)
        {
            return Read(stream, logicalPath: null, resolver: null);
        }

        public VmtParseResult Read(string filePath)
        {
            return Read(filePath, new DiskContentResolver());
        }

        public VmtParseResult Read(Stream stream, string logicalPath, IContentResolver resolver = null)
        {
            var options = new KvParseOptions
            {
                IncludeResolver = resolver,
                SourcePath = logicalPath
            };

            var root = KvParser.Parse(stream, options);
            return ToResult(root, logicalPath);
        }

        public VmtParseResult Read(string logicalPath, IContentResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            if (!resolver.TryOpenRead(logicalPath, out var stream))
                throw new FileNotFoundException("VMT file not found.", logicalPath);

            using (stream)
                return Read(stream, logicalPath, resolver);
        }

        private static VmtParseResult ToResult(KvObject root, string logicalPath)
        {
            if (root == null)
                throw new InvalidDataException("VMT file is empty.");

            return new VmtParseResult
            {
                LogicalPath = logicalPath,
                ShaderName = root.Name,
                Root = root
            };
        }
    }
}

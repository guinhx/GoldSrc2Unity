using System.Collections.Generic;
using System.IO;

namespace Source2Unity.Formats.Common
{
    /// <summary>
    /// Search-path stack: the first resolver that contains the path wins.
    /// Later resolvers can override earlier ones when added last (Source-style search order).
    /// </summary>
    public sealed class CompositeContentResolver : IContentResolver
    {
        private readonly List<IContentResolver> _resolvers = new();

        public void Add(IContentResolver resolver)
        {
            if (resolver != null)
                _resolvers.Add(resolver);
        }

        public bool Exists(string logicalPath)
        {
            for (int i = _resolvers.Count - 1; i >= 0; i--)
            {
                if (_resolvers[i].Exists(logicalPath))
                    return true;
            }

            return false;
        }

        public bool TryOpenRead(string logicalPath, out Stream stream)
        {
            for (int i = _resolvers.Count - 1; i >= 0; i--)
            {
                if (_resolvers[i].TryOpenRead(logicalPath, out stream))
                    return true;
            }

            stream = null;
            return false;
        }
    }
}

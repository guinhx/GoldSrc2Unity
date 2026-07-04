using System.Collections.Generic;

namespace Source2Unity.Formats.KeyValues
{
    internal static class KvMerge
    {
        /// <summary>
        /// Flattens an included KeyValues document into leaf/object entries suitable for merging
        /// into the current object body. Shader-root wrappers are unwrapped.
        /// </summary>
        public static IEnumerable<KvObject> FlattenInclude(KvObject included)
        {
            if (included == null)
                yield break;

            if (included.IsObject)
            {
                foreach (var child in included.Children)
                    yield return child;
                yield break;
            }

            yield return included;
        }
    }
}

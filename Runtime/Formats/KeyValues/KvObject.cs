using System.Collections.Generic;

namespace Source2Unity.Formats.KeyValues
{
    /// <summary>
    /// Immutable KeyValues node. Leaf nodes have a scalar <see cref="Value"/>;
    /// object nodes have <see cref="Children"/>.
    /// </summary>
    public sealed class KvObject
    {
        public string Name { get; init; }
        public string Value { get; init; }
        public IReadOnlyList<KvObject> Children { get; init; }

        public bool IsObject => Children != null && Children.Count > 0;
        public bool IsLeaf => !IsObject;

        public KvObject GetChild(string name)
        {
            if (Children == null) return null;
            foreach (var child in Children)
            {
                if (string.Equals(child.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        /// <summary>Returns the last matching scalar value (Source VMT later-keys-win semantics).</summary>
        public string GetString(string name, string defaultValue = null)
        {
            if (Children == null) return defaultValue;

            string result = defaultValue;
            foreach (var child in Children)
            {
                if (string.Equals(child.Name, name, System.StringComparison.OrdinalIgnoreCase) && child.IsLeaf)
                    result = child.Value;
            }

            return result;
        }

        public bool TryGetString(string name, out string value)
        {
            value = GetString(name);
            return value != null;
        }
    }
}

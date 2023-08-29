using System;
using System.Collections.Generic;

namespace GoldSrc2Unity.Source.Extension;

public static class CollectionExtension
{
    public static IList<T> Fill<T>(this IList<T> collection, T value, int count)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");

        collection.Clear();

        for (var i = 0; i < count; i++)
        {
            collection.Add(value);
        }
        return collection;
    }
}
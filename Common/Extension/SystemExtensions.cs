using System.Collections.Generic;

namespace aoyon.facetune;

internal static class SystemExtensions
{
    public static int GetSequenceHashCode<T>(this IEnumerable<T> sequence)
    {
        var hash = 0;
        foreach (var item in sequence)
        {
            if (item == null) continue;
            hash ^= item.GetHashCode();
        }
        return hash;
    }
}
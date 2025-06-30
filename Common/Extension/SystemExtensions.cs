using System.Collections.Generic;

namespace aoyon.facetune;

internal static class SystemExtensions
{
    public static int GetSequenceHashCode<T>(this IEnumerable<T> sequence) where T : notnull
    {
        var hash = 0;
        foreach (var item in sequence)
        {
            hash ^= item.GetHashCode();
        }
        return hash;
    }
}
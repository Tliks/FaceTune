using System.Collections.Generic;

namespace Aoyon.FaceTune;

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
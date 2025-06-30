namespace aoyon.facetune;

internal static class DestroyedUnityObjectHelpers
{
    public static T? DestroyedAsNull<T>(this T? obj) where T : notnull, UnityEngine.Object
    {
        return (obj == null) ? null : obj;
    }
    public static IEnumerable<T> SkipDestroyed<T>(this IEnumerable<T?> source)
        where T : notnull, UnityEngine.Object
    {
        return source.Where(item => item != null)!;
    }
    public static IEnumerable<TResult> OfType<TResult>(this IEnumerable<UnityEngine.Object?> source)
        where TResult : notnull, UnityEngine.Object
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (var item in source)
        {
            if (item is TResult result && item != null)
            {
                yield return result;
            }
        }
    }
}

namespace Aoyon.FaceTune;

internal static class UnityObjectExtensions
{
    public static T? DestroyedAsNull<T>(this T? obj)
        where T : notnull, UnityEngine.Object
    {
        return obj == null ? null : obj;
    }

    public static IEnumerable<T> SkipDestroyed<T>(this IEnumerable<T?> source)
        where T : notnull, UnityEngine.Object
    {
        foreach (var item in source)
        {
            if (item != null)
            {
                yield return item;
            }
        }
    }

    public static IEnumerable<TResult> OfType<TResult>(
        this IEnumerable<UnityEngine.Object?> source)
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

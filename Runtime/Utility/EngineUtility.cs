namespace com.aoyon.facetune;

internal static class EngineUtility
{
    public static T? NullCast<T>(this T? obj) where T : UnityEngine.Object
    {
        return (obj == null) ? null : obj;
    }

    public static IEnumerable<TResult> UnityOfType<TResult>(this IEnumerable source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (object? item in source)
        {
            if (item is TResult result)
            {
                if (result is UnityEngine.Object unityObject)
                {
                    if (unityObject != null)
                    {
                        yield return result;
                    }
                }
                else
                {
                    yield return result;
                }
            }
        }
    }
}

namespace com.aoyon.facetune;

internal static class EngineUtility
{
    public static T? NullCast<T>(this T obj) where T : UnityEngine.Object
    {
        return (obj == null) ? null : obj;
    }
}

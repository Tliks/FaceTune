using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune;

internal static class EngineUtility
{
    public static string? GetRelativePath(GameObject root, GameObject child)
    {
        return RuntimeUtil.RelativePath(root, child);
    }
}


using nadena.dev.ndmf.runtime;

namespace aoyon.facetune.platform;

internal static class PlatformSupport
{
    private static readonly List<IPlatformSupport> s_supports = new();
    private static readonly IPlatformSupport s_fallback = new FallbackSupport();
    
    public static void Register(IPlatformSupport support)
    {
        s_supports.Add(support);
    }

    public static Transform? FindAvatarInParents(Transform transform)
    {
        return RuntimeUtil.FindAvatarInParents(transform); // NDMFが対応する範囲が上限
    }

    public static IPlatformSupport GetSupportInParents(Transform transform)
    {
        var avatar = FindAvatarInParents(transform);
        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }
        return GetSupport(avatar);
    }

    public static IPlatformSupport GetSupport(Transform root)
    {
        return GetSupports(root).First();
    }

    public static IEnumerable<IPlatformSupport> GetSupports(Transform root)
    {
        foreach (var support in s_supports)
        {
            if (support.IsTarget(root))
            {
                support.Initialize(root);
                yield return support;
            }
        }
        s_fallback.Initialize(root);
        yield return s_fallback;
    }
}

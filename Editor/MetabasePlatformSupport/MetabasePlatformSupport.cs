using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Platforms;

internal static class MetabasePlatformSupport
{
    private static readonly List<IMetabasePlatformSupport> s_supports = new();
    private static readonly IMetabasePlatformSupport s_fallback = new FallbackSupport();
    
    public static void Register(IMetabasePlatformSupport support)
    {
        s_supports.Add(support);
    }

    public static Transform? FindAvatarInParents(Transform transform)
    {
        return RuntimeUtil.FindAvatarInParents(transform); // NDMFが対応する範囲が上限
    }

    public static IMetabasePlatformSupport GetSupportInParents(Transform transform)
    {
        var avatar = FindAvatarInParents(transform);
        if (avatar == null)
        {
            throw new Exception("Avatar not found");
        }
        return GetSupport(avatar);
    }

    public static IMetabasePlatformSupport GetSupport(Transform root)
    {
        return GetSupports(root).First();
    }

    public static IEnumerable<IMetabasePlatformSupport> GetSupports(Transform root)
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

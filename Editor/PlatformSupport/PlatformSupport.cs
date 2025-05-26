using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;

namespace com.aoyon.facetune.platform;

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

    private static IEnumerable<IPlatformSupport> GetSupports(Transform root)
    {
        foreach (var support in s_supports)
        {
            if (support.IsTarget(root))
            {
                yield return support;
            }
        }
        yield return s_fallback;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        return GetSupports(root).Select(s => s.GetFaceRenderer()).FirstOrNull(r => r != null);
    }

    public static void InstallPresets(BuildContext buildContext, SessionContext context, IEnumerable<Preset> presets)
    {
        GetSupports(context.Root.transform).First().InstallPresets(buildContext, context, presets);
    }

    public static IEnumerable<string> GetTrackedBlendShape(SessionContext context)
    {
        return GetSupports(context.Root.transform).First().GetTrackedBlendShape(context);
    }

    public static string AssignParameterName(Transform root, ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return GetSupports(root).First().AssignParameterName(menuItem, usedNames);
    }
}

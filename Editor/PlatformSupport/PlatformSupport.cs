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

    private static IPlatformSupport GetSupport(Transform root)
    {
        var support = s_supports.FirstOrNull(s => s.IsTarget(root));
        support ??= s_fallback;
        support.Initialize(root);
        return support;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        return GetSupport(root).GetFaceRenderer();
    }

    public static void InstallPresets(BuildContext buildContext, SessionContext context, IEnumerable<Preset> presets)
    {
        GetSupport(context.Root.transform).InstallPresets(buildContext, context, presets);
    }

    public static IEnumerable<string> GetTrackedBlendShape(SessionContext context)
    {
        return GetSupport(context.Root.transform).GetTrackedBlendShape(context);
    }

    public static string AssignParameterName(Transform root, ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        return GetSupport(root).AssignParameterName(menuItem, usedNames);
    }
}

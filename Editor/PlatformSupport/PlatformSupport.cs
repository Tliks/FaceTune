using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune.platform;

internal static class FTPlatformSupport
{
    private static readonly IPlatformSupport[] s_supports;
    private static readonly IPlatformSupport s_fallback = new FallbackSupport();
    
    static FTPlatformSupport()
    {
        s_supports = new IPlatformSupport[]
        {
#if FT_VRCSDK3_AVATARS
            new VRChatSuport(),
#endif
        };
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
}

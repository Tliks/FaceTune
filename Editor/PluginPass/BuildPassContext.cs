using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class FTPassContext : IExtensionContext
{
    public BuildContext BuildContext { get; private set; } = null!;
    public IPlatformSupport PlatformSupport { get; private set; } = null!;
    public SessionContext? SessionContext { get; private set; }
    public PatternData? PatternData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        BuildContext = context;
        var root = context.AvatarRootObject.transform;
        PlatformSupport = platform.PlatformSupport.GetSupport(root);
        PlatformSupport.Initialize(root);
        if (!SessionContextBuilder.TryBuild(context.AvatarRootObject, out var sessionContext)) return;
        SessionContext = sessionContext;
    }

    void IExtensionContext.OnDeactivate(BuildContext context)
    {
        SessionContext = null;
        PatternData = null;
    }

    public void SetPatternData(PatternData presetData)
    {
        PatternData = presetData;
    }
}

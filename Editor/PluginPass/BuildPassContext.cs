using nadena.dev.ndmf;
using com.aoyon.facetune.animator;

namespace com.aoyon.facetune.pass;

internal class FTPassContext : IExtensionContext
{
    public BuildContext BuildContext { get; private set; } = null!;
    public SessionContext? SessionContext { get; private set; }
    public PatternData? PatternData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        BuildContext = context;
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

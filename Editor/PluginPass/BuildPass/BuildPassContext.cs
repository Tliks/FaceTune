using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class BuildPassContext : IExtensionContext
{
    public SessionContext? SessionContext { get; private set; }
    public PatternData? PatternData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        if (!SessionContextBuilder.TryGet(context.AvatarRootObject, out var sessionContext)) return;
        
        Profiler.BeginSample("CollectPatternData");
        var patternData = PatternData.Collect(sessionContext);
        Profiler.EndSample();
        if (patternData == null) return;

        SessionContext = sessionContext;
        PatternData = patternData;
    }

    void IExtensionContext.OnDeactivate(BuildContext context)
    {
        SessionContext = null;
        PatternData = null;
    }
}
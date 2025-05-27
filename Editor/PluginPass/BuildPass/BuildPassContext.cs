using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class BuildPassContext : IExtensionContext
{
    public SessionContext? SessionContext { get; private set; }
    public PatternData? PatternData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        if (!SessionContextBuilder.TryGet(context.AvatarRootObject, out var sessionContext)) return;
        
        Profiler.BeginSample("CollectPresetData");
        var presetData = PatternData.CollectPresetData(sessionContext);
        Profiler.EndSample();
        if (presetData == null) return;

        SessionContext = sessionContext;
        PatternData = presetData;
    }

    void IExtensionContext.OnDeactivate(BuildContext context)
    {
        SessionContext = null;
        PatternData = null;
    }
}
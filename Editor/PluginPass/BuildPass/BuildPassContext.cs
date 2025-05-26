using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class BuildPassContext : IExtensionContext
{
    public SessionContext? SessionContext { get; private set; }
    public PresetData? PresetData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        if (!SessionContextBuilder.TryGet(context.AvatarRootObject, out var sessionContext)) return;
        
        Profiler.BeginSample("CollectPresetData");
        var presetData = CollectPresetData(sessionContext);
        Profiler.EndSample();
        if (presetData == null) return;

        SessionContext = sessionContext;
        PresetData = presetData;
    }

    private PresetData? CollectPresetData(SessionContext context)
    {
        var presetComponents = context.Root.GetComponentsInChildren<PresetComponent>(false);
        var presets = presetComponents
            .Select(c => c.GetPreset(context))
            .UnityOfType<Preset>()
            .ToList();
        if (presets.Count == 0) return null;
        return new PresetData(presets);
    }

    void IExtensionContext.OnDeactivate(BuildContext context)
    {
        SessionContext = null;
        PresetData = null;
    }
}


using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class BuildPassContext : IExtensionContext
{
    public SessionContext? SessionContext { get; private set; }
    public PresetData? PresetData { get; private set; }

    void IExtensionContext.OnActivate(BuildContext context)
    {
        var mainComponents = context.AvatarRootObject.GetComponentsInChildren<FaceTuneComponent>(false);
        if (mainComponents.Length == 0) return;
        if (mainComponents.Length > 1) throw new Exception("FaceTuneComponent is not unique");
        var mainComponent = mainComponents[0];

        if (!mainComponent.TryGetSessionContext(out var sessionContext)) return;
        
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


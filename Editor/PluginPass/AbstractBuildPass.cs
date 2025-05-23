using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal abstract class AbstractBuildPass<T> : Pass<T> where T : AbstractBuildPass<T>, new()
{
    protected sealed override void Execute(BuildContext context)
    {
        var buildPassContext = context.GetState(b =>
        {
            var mainComponents = context.AvatarRootObject.GetComponentsInChildren<FaceTuneComponent>(false);
            if (mainComponents.Length == 0) return null;
            if (mainComponents.Length > 1) throw new Exception("FaceTuneComponent is not unique");
            var mainComponent = mainComponents[0];

            if (!mainComponent.TryGetSessionContext(out var sessionContext)) return null;
            
            Profiler.BeginSample("CollectPresetData");
            var presetData = CollectPresetData(sessionContext);
            Profiler.EndSample();
            if (presetData == null) return null;
            
            return new BuildPassContext(context, sessionContext, presetData);
        });
        if (buildPassContext == null) return;
        ExecuteCore(buildPassContext);
    }

    protected abstract void ExecuteCore(BuildPassContext context);

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
}

internal class BuildPassContext
{
    public BuildContext BuildContext { get; }
    public SessionContext SessionContext { get; }
    public PresetData PresetData { get; }

    public BuildPassContext(BuildContext buildContext, SessionContext sessionContext, PresetData presetData)
    {
        BuildContext = buildContext;
        SessionContext = sessionContext;
        PresetData = presetData;
    }
}


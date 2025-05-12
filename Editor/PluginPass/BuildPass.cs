using nadena.dev.ndmf;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class BuildPass : Pass<BuildPass>
{
    public override string QualifiedName => "com.aoyon.facetune.build";

    public override string DisplayName => "Build FaceTune";

    protected override void Execute(BuildContext context)
    {
        var mainComponents = context.AvatarRootObject
            .GetComponentsInChildren<FaceTuneComponent>(false);

        if (mainComponents.Length == 0) return;
        if (mainComponents.Length > 1) throw new Exception("FaceTuneComponent is not unique");

        var mainComponent = mainComponents[0];
        if (!mainComponent.TryGetSessionContext(out var sessionContext)) return;

        var presetComponents = mainComponent.GetComponentsInChildren<PresetComponent>(false);
        if (presetComponents.Length == 0) return;
        
        var allPresets = presetComponents
            .Select(c => c.GetPreset(sessionContext))
            .ToList();
        
        // optionやmenuitemは一旦後回し

        FTPlatformSupport.InstallPresets(context, sessionContext, allPresets);

        foreach (var component in mainComponent.GetComponentsInChildren<FaceTuneTagComponent>(true))
        {
            Object.DestroyImmediate(component);
        }
    }
}
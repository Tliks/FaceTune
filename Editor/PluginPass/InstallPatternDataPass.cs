using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class DisableExistingControlAndInstallPatternDataPass : Pass<DisableExistingControlAndInstallPatternDataPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.disable-existing-control-and-install-pattern-data";
    public override string DisplayName => "Disable Existing Control and Install PatternData";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.GetState<BuildPassState>();
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;

        var patternData = context.GetState<PatternData>();

        var settings = sessionContext.Root.GetComponentsInChildren<DisableExistingControlComponent>(true);
        bool overrideShapes;
        bool overrideProperties;
        if (settings.Length == 0)
        {
            overrideShapes = false;
            overrideProperties = false;
        }
        else if (settings.Length == 1)
        {
            overrideShapes = settings[0].OverrideBlendShapes;
            overrideProperties = settings[0].OverrideProperties;
        }
        else
        {
            Debug.LogWarning("Multiple DisableExistingControlComponent is not supported");
            overrideShapes = settings[0].OverrideBlendShapes;
            overrideProperties = settings[0].OverrideProperties;
        }

        Profiler.BeginSample("InstallPatternData");
        passContext.PlatformSupport.DisableExistingControlAndInstallPatternData(passContext, overrideShapes, overrideProperties, patternData);
        Profiler.EndSample();
    }
}
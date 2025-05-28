using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class InstallPatternDataPass : Pass<InstallPatternDataPass>
{
    public override string QualifiedName => "com.aoyon.facetune.install-pattern-data";
    public override string DisplayName => "Install PatternData";

    protected override void Execute(BuildContext context)
    {
        var buildPassContext = context.Extension<BuildPassContext>();
        if (buildPassContext == null) return;

        var sessionContext = buildPassContext.SessionContext;
        if (sessionContext == null) return;
        var presetData = buildPassContext.PatternData;
        if (presetData == null) return;

        var disableExistingControl = sessionContext.Root.GetComponentsInChildren<DisableExistingControlComponent>(false).Any();

        Profiler.BeginSample("InstallPatternData");
        platform.PlatformSupport.InstallPatternData(context, sessionContext, presetData, disableExistingControl);
        Profiler.EndSample();
    }
}
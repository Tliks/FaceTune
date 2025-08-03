using nadena.dev.ndmf;

namespace aoyon.facetune.build;

internal class InstallPatternDataPass : Pass<InstallPatternDataPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.install-pattern-data";
    public override string DisplayName => "Install PatternData";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;

        var patternData = context.GetState<PatternData>();

        Profiler.BeginSample("InstallPatternData");
        buildPassContext.PlatformSupport.InstallPatternData(buildPassContext, context, new InstallerData(patternData));
        Profiler.EndSample();
    }
}

internal class InstallerData
{
    public PatternData PatternData;

    public InstallerData(PatternData patternData)
    {
        PatternData = patternData;
    }
}
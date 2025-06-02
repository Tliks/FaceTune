using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class InstallPatternDataPass : Pass<InstallPatternDataPass>
{
    public override string QualifiedName => "com.aoyon.facetune.install-pattern-data";
    public override string DisplayName => "Install PatternData";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;
        var presetData = passContext.PatternData;
        if (presetData == null) throw new InvalidOperationException("PatternData is null");
        if (presetData.IsEmpty) return;

        Profiler.BeginSample("InstallPatternData");
        platform.PlatformSupport.InstallPatternData(passContext, presetData);
        Profiler.EndSample();
    }
}
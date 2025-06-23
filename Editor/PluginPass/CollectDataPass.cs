using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class CollectDataPass : Pass<CollectDataPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.collect-data";
    public override string DisplayName => "Collect Data";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.Extension<FTPassContext>()!;
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;

        Profiler.BeginSample("CollectPatternData");
        var patternData = PatternData.Collect(sessionContext);
        Profiler.EndSample();

        passContext.SetPatternData(patternData);
    }
}

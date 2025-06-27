using nadena.dev.ndmf;

namespace com.aoyon.facetune.ndmf;

internal class CollectDataPass : Pass<CollectDataPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.collect-data";
    public override string DisplayName => "Collect Data";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;
        context.GetState(ctx => PatternData.Collect(buildPassContext.SessionContext));
    }
}
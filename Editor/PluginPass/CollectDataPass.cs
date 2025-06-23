using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class CollectDataPass : Pass<CollectDataPass>
{
    public override string QualifiedName => $"{FaceTuneConsts.QualifiedName}.collect-data";
    public override string DisplayName => "Collect Data";

    protected override void Execute(BuildContext context)
    {
        var passContext = context.GetState<BuildPassState>();
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null) return;
        context.GetState(ctx => PatternData.Collect(sessionContext));
    }
}
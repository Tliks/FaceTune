using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Build;

internal class CollectDataPass : Pass<CollectDataPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-data";
    public override string DisplayName => "Collect Data";

    protected override void Execute(BuildContext context)
    {
        if (context.GetState<BuildPassState>().TryGetBuildPassContext(out var buildPassContext) is false) return;
        context.GetState(ctx => PatternData.Collect(buildPassContext.AvatarContext));
    }
}
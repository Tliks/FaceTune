using nadena.dev.ndmf;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Preview;

[assembly: ExportsPlugin(typeof(Aoyon.FaceTune.PluginDefinition))]

namespace Aoyon.FaceTune;

[RunsOnAllPlatforms]
public sealed class PluginDefinition : Plugin<PluginDefinition>
{
    public override string QualifiedName => FaceTuneConstants.QualifiedName; // "aoyon.facetune"
    public override string DisplayName => FaceTuneConstants.Name;

    protected override void Configure()
    {
        var sequence = InPhase(BuildPhase.Resolving);
        sequence.Run("Get State", ctx => ctx.GetState(ctx => new BuildPassState(ctx)));
        sequence.Run(ResolveReferencesPass.Instance);

        sequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        sequence.Run(CollectBuildSettingsPass.Instance);
        sequence.Run(NormalizeAuthoringHierarchyPass.Instance);
        sequence.Run(CompileExpressionProgramPass.Instance);
        sequence.Run(ApplyDefaultShapesPass.Instance)
            .PreviewingWith(new RealTimeExpressionPreview());
        sequence.Run(InstallExpressionProgramPass.Instance);
        sequence.Run(RemoveFaceTuneComponentsPass.Instance);

        sequence = InPhase(BuildPhase.PlatformFinish);
        sequence.Run("Empty Pass", _ => { })
            .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
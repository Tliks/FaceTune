using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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
        sequence.Run(ResolveReferencesPass.Instance);
        sequence.Run("Get State", ctx => ctx.GetState(ctx => new BuildPassState(ctx)));

        sequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        sequence.Run(ModifyHierarchyPass.Instance);
        sequence.Run(CollectDataPass.Instance);
        sequence.Run(ProcessTrackedShapesPass.Instance);
        sequence.Run(ApplyDefaultShapesPass.Instance)
            .PreviewingWith(new RealTimeExpressionPreview());
        sequence.WithRequiredExtension(typeof(VirtualControllerContext), sq1 => 
        {
            sq1.Run(InstallPatternDataPass.Instance);
        });
        sequence.Run(RemoveFaceTuneComponentsPass.Instance);

        sequence = InPhase(BuildPhase.PlatformFinish);
        sequence.Run("Empty Pass", _ => { })
            .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
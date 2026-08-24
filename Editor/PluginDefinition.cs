using nadena.dev.ndmf;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Preview;

[assembly: ExportsPlugin(typeof(Aoyon.FaceTune.PluginDefinition))]

namespace Aoyon.FaceTune;

[RunsOnAllPlatforms]
internal sealed class PluginDefinition : Plugin<PluginDefinition>
{
    public override string QualifiedName => FaceTuneConstants.QualifiedName; // "aoyon.facetune"
    public override string DisplayName => FaceTuneConstants.Name;

    protected override void Configure()
    {
        var sequence = InPhase(BuildPhase.Resolving);
        sequence.Run(ResolveReferencesPass.Instance);

        sequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        sequence.Run(GetBuildSettingsPass.Instance);
        sequence.Run(CanonicalizeComponentsPass.Instance);
        sequence.Run(CreateAvatarControlSettingsPass.Instance);
        sequence.Run(CreateParameterPlanPass.Instance);
        sequence.Run(CreateExpressionPlanPass.Instance);
        sequence.Run(CreateMenuPlanPass.Instance);
        sequence.Run(ApplyDefaultShapesPass.Instance)
            .PreviewingWith(new RealTimeExpressionPreview());
        sequence.Run(BuildPlatformAssetsPass.Instance);
        sequence.Run(RemoveFaceTuneComponentsPass.Instance);

        sequence = InPhase(BuildPhase.Transforming)
            .AfterPlugin("nadena.dev.modular-avatar")
            .AfterPlugin("net.rs64.tex-trans-tool");
        sequence.Run(FinishPlatformBuildPass.Instance);

        sequence = InPhase(BuildPhase.PlatformFinish);
        sequence.Run("Empty Pass", _ => { })
            .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
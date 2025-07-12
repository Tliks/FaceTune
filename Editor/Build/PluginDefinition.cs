using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using aoyon.facetune.preview;

[assembly: ExportsPlugin(typeof(aoyon.facetune.build.PluginDefinition))]

namespace aoyon.facetune.build;

public sealed class PluginDefinition : Plugin<PluginDefinition>
{
    public override string QualifiedName => FaceTuneConsts.QualifiedName; // "aoyon.facetune"
    public override string DisplayName => FaceTuneConsts.Name;

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
        sequence.Run(ApplyDefaulShapesPass.Instance)
            .PreviewingWith(new FacialStylePreview());
        sequence.WithRequiredExtension(typeof(AnimatorServicesContext), sq1 => 
        {
            sq1.Run(InstallPatternDataPass.Instance);
        });
        sequence.Run(RemoveFTComponentsPass.Instance);

        sequence = InPhase(BuildPhase.Optimizing)
            .AfterPlugin("nadena.dev.modular-avatar")
            .AfterPlugin("net.rs64.tex-trans-tool")
            .AfterPlugin("com.anatawa12.avatar-optimizer");
        sequence.Run("Empty Pass", _ => { })
            .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
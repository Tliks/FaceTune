using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using com.aoyon.facetune.pass;
using com.aoyon.facetune.preview;

[assembly: ExportsPlugin(typeof(com.aoyon.facetune.PluginDefinition))]

namespace com.aoyon.facetune;

public sealed class PluginDefinition : Plugin<PluginDefinition>
{
    public override string QualifiedName => FaceTuneConsts.QualifiedName;
    public override string DisplayName => FaceTuneConsts.Name;

    protected override void Configure()
    {
        var sequence = InPhase(BuildPhase.Resolving);
        sequence.Run(ResolveReferencesPass.Instance);

        sequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        sequence.Run("Get State", ctx => ctx.GetState(ctx => BuildPassState.Get(ctx)));
        sequence.Run(ModifyHierarchyPass.Instance);
        sequence.Run(CollectDataPass.Instance);
        sequence.Run(ProcessTrackedShapesPass.Instance);
        sequence.Run(ApplyDefaulShapesPass.Instance).PreviewingWith(new DefaultShapesPreview());
        sequence.WithRequiredExtension(typeof(AnimatorServicesContext), sq1 => 
        {
            sq1
            .Run(DisableExistingControlAndInstallPatternDataPass.Instance);
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
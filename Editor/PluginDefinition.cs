using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using com.aoyon.facetune.pass;
using com.aoyon.facetune.preview;

[assembly: ExportsPlugin(typeof(com.aoyon.facetune.PluginDefinition))]

namespace com.aoyon.facetune;

public sealed class PluginDefinition : Plugin<PluginDefinition>
{
    public override string QualifiedName => "com.aoyon.facetune";
    public override string DisplayName => "FaceTune";

    protected override void Configure()
    {
        InPhase(BuildPhase.Resolving)
        .Run(ResolveReferencesPass.Instance);

        var mainSequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        
        mainSequence.WithRequiredExtension(typeof(FTPassContext), sq =>
        {
            sq
            .Run(ModifyHierarchyPass.Instance).Then
            .Run(CollectDataPass.Instance).Then
            .Run(ProcessPresetPass.Instance).Then
            .Run(ProcessTrackedShapesPass.Instance).Then
            .Run(ApplyDefaulShapesPass.Instance).PreviewingWith(new DefaultShapesPreview()).Then
            .WithRequiredExtension(typeof(AnimatorServicesContext), sq1 => 
            {
                sq1
                .Run(DisableExistingControlPass.Instance).Then
                .Run(InstallPatternDataPass.Instance);
            });
        });

        mainSequence
        .Run(RemoveFTComponentsPass.Instance);

        InPhase(BuildPhase.Optimizing)
        .AfterPlugin("nadena.dev.modular-avatar")
        .AfterPlugin("net.rs64.tex-trans-tool")
        .AfterPlugin("com.anatawa12.avatar-optimizer")
        .Run("Empty Pass", _ => { })
        .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
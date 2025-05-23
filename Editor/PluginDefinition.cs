using nadena.dev.ndmf;
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

        InPhase(BuildPhase.Transforming)
        .BeforePlugin("nadena.dev.modular-avatar")
        .Run(ApplyDefaulShapesPass.Instance).PreviewingWith(new DefaultShapesPreview()).Then
        .Run(ProcessTrackedShapesPass.Instance).Then
        .Run(InstallPresetsPass.Instance).Then
        .Run(RemoveFTComponentsPass.Instance);

        InPhase(BuildPhase.Optimizing)
        .AfterPlugin("nadena.dev.modular-avatar")
        .AfterPlugin("net.rs64.tex-trans-tool")
        .AfterPlugin("com.anatawa12.avatar-optimizer")
        .Run("Empty Pass", _ => { })
        .PreviewingWith(new EditingShapesPreview(), new SelectedShapesPreview());
    }
}
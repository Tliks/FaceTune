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

        var mainSequence = InPhase(BuildPhase.Transforming)
            .BeforePlugin("nadena.dev.modular-avatar");
        
        mainSequence
        // Add Condition Component Phase
        .Run(NegotiateMAMenuItemPass.Instance).Then // Todo: PresetがToggleかSubMenuか不明。一旦自動生成のToggleと仮定し、子のMenuを無視。
        // Edit Condition Phase
        .Run(ProcessPresetPass.Instance).Then /// Generate CommonCondition
        .Run(CommonConditionPass.Instance).Then
        // PostProcess Phase
        .Run(NormalizeDataPass.Instance).Then
        // Collect Data and Build
        .WithRequiredExtensions(new Type[] { typeof(BuildPassContext) }, buildSequence => 
        {
            buildSequence
            .Run(ApplyDefaulShapesPass.Instance).PreviewingWith(new DefaultShapesPreview()).Then
            .Run(ProcessTrackedShapesPass.Instance).Then
            .Run(InstallPatternDataPass.Instance);
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
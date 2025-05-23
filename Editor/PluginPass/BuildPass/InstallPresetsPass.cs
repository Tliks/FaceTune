namespace com.aoyon.facetune.pass;

internal class InstallPresetsPass : AbstractBuildPass<InstallPresetsPass>
{
    public override string QualifiedName => "com.aoyon.facetune.install-presets";
    public override string DisplayName => "Install Presets";

    protected override void Execute(BuildPassContext context)
    {
        // optionやmenuitemは一旦後回し

        Profiler.BeginSample("InstallPresetData");
        platform.PlatformSupport.InstallPresets(context.BuildContext, context.SessionContext, context.PresetData.Presets);
        Profiler.EndSample();
    }
}
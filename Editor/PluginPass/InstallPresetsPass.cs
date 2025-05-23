using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.pass;

internal class InstallPresetsPass : AbstractBuildPass<InstallPresetsPass>
{
    public override string QualifiedName => "com.aoyon.facetune.install-presets";
    public override string DisplayName => "Install Presets";

    protected override void ExecuteCore(BuildPassContext context)
    {
        // optionやmenuitemは一旦後回し

        Profiler.BeginSample("InstallPresetData");
        FTPlatformSupport.InstallPresets(context.BuildContext, context.SessionContext, context.PresetData.Presets);
        Profiler.EndSample();
    }
}
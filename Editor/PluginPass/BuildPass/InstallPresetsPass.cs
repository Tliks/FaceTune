using nadena.dev.ndmf;

namespace com.aoyon.facetune.pass;

internal class InstallPresetsPass : Pass<InstallPresetsPass>
{
    public override string QualifiedName => "com.aoyon.facetune.install-presets";
    public override string DisplayName => "Install Presets";

    protected override void Execute(BuildContext context)
    {
        var buildPassContext = context.Extension<BuildPassContext>();
        if (buildPassContext == null) return;

        var sessionContext = buildPassContext.SessionContext;
        if (sessionContext == null) return;
        var presetData = buildPassContext.PresetData;
        if (presetData == null) return;

        Profiler.BeginSample("InstallPresetData");
        platform.PlatformSupport.InstallPresets(context, sessionContext, presetData.Presets);
        Profiler.EndSample();
    }
}
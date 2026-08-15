namespace Aoyon.FaceTune.Build;

internal class CollectBuildSettingsPass : FaceTunePass<CollectBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-build-settings";
    public override string DisplayName => "Collect Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var components = root.GetComponentsInChildren<AvatarSettingsComponent>(true);
        if (components.Length > 1)
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, components);
        var settings = components.FirstOrDefault();
        var excluded = context.PlatformSupport.GetExternallyControlledBlendShapeNames().ToHashSet();
        if (settings != null) excluded.UnionWith(settings.ExcludedBlendShapeNames.Where(name => !string.IsNullOrWhiteSpace(name)));

        context.SetAuthoringSettings(new AuthoringBuildSettings(
            context.AvatarContext,
            excluded.ToImmutableHashSet(),
            settings?.AvoidEyeBlinkConflicts ?? true,
            settings?.AvoidLipSyncConflicts ?? true,
            context.PlatformSupport.CreateBuiltInParameterDomains()));
    }
}

namespace Aoyon.FaceTune.Build;

internal class CollectBuildSettingsPass : FaceTunePass<CollectBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-build-settings";
    public override string DisplayName => "Collect Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var settingsComponents = root.GetComponentsInChildren<SettingsComponent>(true);
        if (settingsComponents.Length > 1)
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        var avatarSettings = settingsComponents.FirstOrDefault()?.Settings ?? AvatarSettings.Default;

        var excludedBlendShapeNames = context.PlatformSupport.GetExternallyControlledBlendShapeNames().ToHashSet();
        excludedBlendShapeNames.UnionWith(avatarSettings.ExcludedBlendShapeNames.Where(x => !string.IsNullOrWhiteSpace(x)));

        context.SetAuthoringSettings(new AuthoringBuildSettings(
            context.AvatarContext,
            excludedBlendShapeNames.ToImmutableHashSet(),
            avatarSettings.AvoidEyeBlinkConflicts,
            avatarSettings.AvoidLipSyncConflicts,
            context.PlatformSupport.CreateBuiltInParameterDomains()));
    }
}

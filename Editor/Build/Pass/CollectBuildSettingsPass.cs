namespace Aoyon.FaceTune.Build;

internal class CollectBuildSettingsPass : FaceTunePass<CollectBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-build-settings";
    public override string DisplayName => "Collect Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetSettings(Collect(context));
    }

    private static BuildSettings Collect(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        
        var settingsComponents = root.GetComponentsInChildren<SettingsComponent>(true);
        if (settingsComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        }
        var avatarSettings = settingsComponents.FirstOrDefault()?.Settings ?? AvatarSettings.Default;

        var excludedBlendShapeNames = context.PlatformSupport.GetExternallyControlledBlendShapeNames().ToHashSet();
        excludedBlendShapeNames.UnionWith(avatarSettings.ExcludedBlendShapeNames.Where(x => !string.IsNullOrWhiteSpace(x)));

        var mmdPlayback = context.PlatformSupport.ResolveMmdPlaybackSettings();

        var disableEyeBlinkComponents = root.GetComponentsInChildren<DisableEyeBlinkComponent>(true);
        if (disableEyeBlinkComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleDisableEyeBlinkComponent", null, disableEyeBlinkComponents);
        }
        var disableEyeBlinkParameter = disableEyeBlinkComponents.FirstOrDefault()?.DisableParameterName ?? string.Empty;

        var disableLipSyncComponents = root.GetComponentsInChildren<DisableLipSyncComponent>(true);
        if (disableLipSyncComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleDisableLipSyncComponent", null, disableLipSyncComponents);
        }
        var disableLipSyncParameter = disableLipSyncComponents.FirstOrDefault()?.DisableParameterName ?? string.Empty;

        var lockFacialComponents = root.GetComponentsInChildren<LockFacialComponent>(true);
        if (lockFacialComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleLockFacialComponent", null, lockFacialComponents);
        }
        var lockFacialParameter = lockFacialComponents.FirstOrDefault()?.ConditionParameterName ?? string.Empty;

        return new BuildSettings(
            context.AvatarContext,
            excludedBlendShapeNames.ToImmutableHashSet(),
            avatarSettings.ParmaterCompression,
            avatarSettings.SupressTrackingControl,
            context.PlatformSupport.CreateBuiltInParameterDomains(),
            mmdPlayback,
            disableEyeBlinkParameter,
            disableLipSyncParameter,
            lockFacialParameter);
    }
}

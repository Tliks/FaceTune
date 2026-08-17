namespace Aoyon.FaceTune.Build;

internal sealed class GetBuildSettingsPass : FaceTunePass<GetBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.get-build-settings";
    public override string DisplayName => "Get Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var components = root.GetComponentsInChildren<AvatarSettingsComponent>(true);
        if (components.Length > 1)
        {
            LocalizedLog.Warning(
                "Log:warning:AvatarContext:MultipleSettingsComponent",
                null,
                components);
        }
        var settings = components.FirstOrDefault().DestroyedAsNull();
        var externallyControlled = context.PlatformSupport
            .GetExternallyControlledBlendShapeNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var explicitlyExcluded = settings?.ExcludedBlendShapeNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal)
            ?? ImmutableHashSet.Create<string>(StringComparer.Ordinal);

        context.SetSettings(new BuildSettings(
            context.AvatarContext,
            externallyControlled,
            explicitlyExcluded,
            settings?.AvoidEyeBlinkConflicts ?? true,
            settings?.AvoidLipSyncConflicts ?? true,
            context.PlatformSupport.CreateBuiltInParameterDomains()));
    }
}

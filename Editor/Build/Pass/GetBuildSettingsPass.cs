using Aoyon.FaceTune.Platforms;

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
        var platformSupports = MetabasePlatformSupport.GetForAvatar(root.transform)
            .Append(context.PlatformSupport)
            .ToArray();
        var externalEyeBlink = platformSupports
            .SelectMany(support => support.GetExternalEyeBlinkBlendShapeNames())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var externalLipSync = platformSupports
            .SelectMany(support => support.GetExternalLipSyncBlendShapeNames())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var explicitlyExcluded = AvatarContext.GetExplicitlyExcludedBlendShapeNames(root);

        context.SetSettings(new BuildSettings(
            context.AvatarContext,
            externalEyeBlink,
            externalLipSync,
            explicitlyExcluded,
            settings?.AvoidEyeBlinkConflicts ?? true,
            settings?.AvoidLipSyncConflicts ?? true,
            context.PlatformSupport.CreateBuiltInParameterDomains()));
    }
}

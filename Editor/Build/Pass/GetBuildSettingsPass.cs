using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal sealed class GetBuildSettingsPass : FaceTunePass<GetBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.get-build-settings";
    public override string DisplayName => "Get Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var settings = root.GetComponentsInChildren<AvatarSettingsComponent>(true)
            .FirstOrDefault().DestroyedAsNull();
        var platformSupports = MetabasePlatformSupport.GetForAvatar(root.transform)
            .Append(context.PlatformSupport)
            .ToArray();
        var explicitlyExcluded = AvatarContext.GetExplicitlyExcludedBlendShapeNames(root);

        context.SetSettings(new BuildSettings(
            context.AvatarContext,
            GetProhibited(platformSupports, FaceTuneWriteKind.FacialData),
            GetProhibited(platformSupports, FaceTuneWriteKind.EyeBlinkAnimation),
            GetProhibited(platformSupports, FaceTuneWriteKind.LipSyncAnimation),
            explicitlyExcluded,
            settings?.AvoidEyeBlinkConflicts ?? true,
            settings?.AvoidLipSyncConflicts ?? true,
            context.PlatformSupport.CreateBuiltInParameterDomains()));
    }

    private static ImmutableHashSet<string> GetProhibited(
        IEnumerable<IMetabasePlatformSupport> platformSupports,
        FaceTuneWriteKind writeKind)
    {
        return platformSupports
            .SelectMany(support => support.GetProhibitedBlendShapeNames(writeKind))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }
}

namespace Aoyon.FaceTune.Build;

internal record struct AuthoringBuildSettings(
    AvatarContext AvatarContext,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains);

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    bool AvoidEyeBlinkConflicts,
    bool AvoidLipSyncConflicts,
    ParameterDomainRegistry ParameterDomains,
    MmdPlaybackSettings MmdPlayback,
    DnfCondition? DisableEyeBlinkWhen,
    DnfCondition? DisableLipSyncWhen,
    DnfCondition? LockFacialWhen)
{
}

internal record struct MmdPlaybackSettings(
    bool Enabled,
    IReadOnlyCollection<string> BlendShapeNames,
    DnfCondition? DisableWhen,
    MmdDisableMode DisableMode)
{
    public static MmdPlaybackSettings Disabled { get; } = new(false, Array.Empty<string>(), null, MmdDisableMode.Auto);
}

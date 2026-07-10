namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    float DurationSeconds,
    bool ParmaterCompression,
    bool SupressTrackingControl,
    ParameterDomainRegistry ParameterDomains,
    MmdPlaybackSettings MmdPlayback,
    string DisableEyeBlinkParameterName,
    string DisableLipSyncParameterName,
    string LockFacialParameterName)
{
}

internal record struct MmdPlaybackSettings(
    bool Enabled,
    string DisableParameterName,
    MmdDisableMode DisableMode)
{
    public static MmdPlaybackSettings Disabled { get; } = new(false, string.Empty, MmdDisableMode.Auto);
}

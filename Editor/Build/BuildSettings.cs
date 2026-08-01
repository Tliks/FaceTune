namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
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
    IReadOnlyCollection<string> BlendShapeNames,
    string DisableParameterName,
    MmdDisableMode DisableMode)
{
    public static MmdPlaybackSettings Disabled { get; } = new(false, Array.Empty<string>(), string.Empty, MmdDisableMode.Auto);
}

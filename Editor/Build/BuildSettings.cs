using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal record struct BuildSettings(
    AvatarContext AvatarContext,
    IMetabasePlatformSupport PlatformSupport,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    float DurationSeconds,
    bool ParmaterCompression,
    bool SupressTrackingControl,
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

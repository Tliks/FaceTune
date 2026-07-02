namespace Aoyon.FaceTune.Build;

internal sealed record FaceTuneBuildSettings(
    AvatarSettings AvatarSettings,
    IReadOnlyCollection<string> ExcludedBlendShapeNames,
    MmdPlaybackSettings MmdPlayback,
    string DisableEyeBlinkParameterName,
    string DisableLipSyncParameterName,
    string LockFacialParameterName)
{
    public static FaceTuneBuildSettings Default { get; } = new(
        AvatarSettings.Default,
        ImmutableHashSet<string>.Empty,
        MmdPlaybackSettings.Disabled,
        string.Empty,
        string.Empty,
        string.Empty);
}

internal sealed record MmdPlaybackSettings(
    bool Enabled,
    string DisableParameterName,
    MmdDisableMode DisableMode)
{
    public static MmdPlaybackSettings Disabled { get; } = new(false, string.Empty, MmdDisableMode.Auto);
}

namespace Aoyon.FaceTune.Build;

internal record struct AvatarControlSettings(
    MmdPlaybackSettings MmdPlayback,
    DnfCondition DisableEyeBlinkWhen,
    DnfCondition DisableLipSyncWhen,
    DnfCondition LockFacialWhen,
    bool SupportAfk);

internal record struct MmdPlaybackSettings(
    DnfCondition PlaybackWhen,
    IReadOnlyCollection<string> ExplicitBlendShapeNames,
    MMDSupportSettings.Mode DisableMode)
{
    public bool Enabled => !PlaybackWhen.IsNever;

    public static MmdPlaybackSettings Disabled { get; } = new(
        DnfCondition.Never,
        Array.Empty<string>(),
        MMDSupportSettings.Mode.Auto);
}

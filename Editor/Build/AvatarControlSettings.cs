namespace Aoyon.FaceTune.Build;

internal record struct AvatarControlSettings(
    MmdPlaybackSettings MmdPlayback,
    DnfCondition? DisableEyeBlinkWhen,
    DnfCondition? DisableLipSyncWhen,
    DnfCondition? LockFacialWhen);

internal record struct MmdPlaybackSettings(
    bool Enabled,
    IReadOnlyCollection<string> ExplicitBlendShapeNames,
    ConditionSelection? Condition,
    MMDSupportSettings.Mode DisableMode)
{
    public static MmdPlaybackSettings Disabled { get; } = new(
        false,
        Array.Empty<string>(),
        null,
        MMDSupportSettings.Mode.Auto);
}

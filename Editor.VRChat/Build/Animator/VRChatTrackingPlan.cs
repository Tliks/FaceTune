using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>ExpressionとAvatar ControlをTracking Animator生成用に解釈した計画。</summary>
internal sealed class VRChatTrackingPlan
{
    public const int DisabledMode = 0;
    public const int BuiltInMode = 1;
    public const int FirstCustomMode = 2;

    public bool ShouldBuildEyeBlinkLayer { get; }
    public bool ShouldBuildLipSyncLayer { get; }
    public bool ShouldBuildAnyLayer => ShouldBuildEyeBlinkLayer || ShouldBuildLipSyncLayer;
    public DnfCondition? ForceDisableEyeBlinkWhen { get; }
    public DnfCondition? ForceDisableLipSyncWhen { get; }
    public ImmutableList<EyeBlinkSettings> EyeBlinkAnimations { get; }
    public ImmutableList<LipSyncSettings> LipSyncCancellers { get; }

    private readonly ImmutableDictionary<EyeBlinkSettings, int> _eyeBlinkModes;
    private readonly ImmutableDictionary<LipSyncSettings, int> _lipSyncModes;

    private VRChatTrackingPlan(
        ImmutableList<ExpressionItem> items,
        AvatarControlSettings avatarControlSettings)
    {
        ForceDisableEyeBlinkWhen = avatarControlSettings.DisableEyeBlinkWhen;
        ForceDisableLipSyncWhen = avatarControlSettings.DisableLipSyncWhen;
        EyeBlinkAnimations = CollectEyeBlinkAnimations(items);
        LipSyncCancellers = CollectLipSyncCancellers(items);
        _eyeBlinkModes = CreateModeMap(EyeBlinkAnimations);
        _lipSyncModes = CreateModeMap(LipSyncCancellers);
        ShouldBuildEyeBlinkLayer = ForceDisableEyeBlinkWhen != null
                                   || EyeBlinkAnimations.Count > 0
                                   || items.Any(item =>
                                       item.AllowEyeBlink == TrackingPermission.Disallow);
        ShouldBuildLipSyncLayer = ForceDisableLipSyncWhen != null
                                 || LipSyncCancellers.Count > 0
                                 || items.Any(item =>
                                     item.AllowLipSync == TrackingPermission.Disallow);
    }

    public static VRChatTrackingPlan Build(
        ImmutableList<ExpressionItem> items,
        AvatarControlSettings avatarControlSettings)
        => new(items, avatarControlSettings);

    public int? EyeBlinkModeFor(ExpressionItem expression)
    {
        if (expression.AllowEyeBlink == TrackingPermission.Keep) return null;
        if (expression.AllowEyeBlink == TrackingPermission.Disallow) return DisabledMode;
        if (expression.EyeBlink.EyeBlinkMode == EyeBlinkSettings.Kind.BuiltIn) return BuiltInMode;

        if (_eyeBlinkModes.TryGetValue(expression.EyeBlink, out var mode)) return mode;
        throw new InvalidOperationException("Eye blink animation mode was not registered.");
    }

    public int? LipSyncModeFor(ExpressionItem expression)
    {
        if (expression.AllowLipSync == TrackingPermission.Keep) return null;
        if (expression.AllowLipSync == TrackingPermission.Disallow) return DisabledMode;
        if (expression.LipSync.CancellerBlendShapes.Count == 0) return BuiltInMode;

        if (_lipSyncModes.TryGetValue(expression.LipSync, out var mode)) return mode;
        throw new InvalidOperationException("Lip sync canceller mode was not registered.");
    }

    private static ImmutableDictionary<T, int> CreateModeMap<T>(ImmutableList<T> settings)
        where T : notnull
    {
        var modes = ImmutableDictionary.CreateBuilder<T, int>();
        for (var index = 0; index < settings.Count; index++)
            modes.Add(settings[index], FirstCustomMode + index);
        return modes.ToImmutable();
    }

    private static ImmutableList<EyeBlinkSettings> CollectEyeBlinkAnimations(
        IEnumerable<ExpressionItem> items)
    {
        var animations = ImmutableList.CreateBuilder<EyeBlinkSettings>();
        var seen = new HashSet<EyeBlinkSettings>();
        foreach (var settings in items.Select(item => item.EyeBlink))
        {
            if (settings.EyeBlinkMode != EyeBlinkSettings.Kind.BuiltIn
                && seen.Add(settings))
            {
                animations.Add(settings);
            }
        }
        return animations.ToImmutable();
    }

    private static ImmutableList<LipSyncSettings> CollectLipSyncCancellers(
        IEnumerable<ExpressionItem> items)
    {
        var cancellers = ImmutableList.CreateBuilder<LipSyncSettings>();
        var seen = new HashSet<LipSyncSettings>();
        foreach (var settings in items.Select(item => item.LipSync))
        {
            if (settings.CancellerBlendShapes.Count > 0 && seen.Add(settings))
                cancellers.Add(settings);
        }
        return cancellers.ToImmutable();
    }
}

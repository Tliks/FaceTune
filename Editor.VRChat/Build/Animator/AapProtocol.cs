using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>Tracking計画をAnimator Parameter上のAAP表現へ変換する。</summary>
internal sealed class AapProtocol
{
    private const float ActiveThreshold = 0.999f;
    private const string AapParameterPrefix = FaceTuneConstants.GeneratedParameterPrefix + "/AAP/";
    private const string EyeBlinkModePrefix = AapParameterPrefix + "Blink/";
    private const string LipSyncModePrefix = AapParameterPrefix + "LipSync/";

    private readonly VRChatTrackingPlan _plan;
    private readonly ImmutableList<string> _eyeBlinkModeNames;
    private readonly ImmutableList<string> _lipSyncModeNames;

    public AapProtocol(VRChatTrackingPlan plan)
    {
        _plan = plan;
        _eyeBlinkModeNames = CreateModeNames(plan.EyeBlinkAnimations.Count, EyeBlinkModeName);
        _lipSyncModeNames = CreateModeNames(plan.LipSyncCancellers.Count, LipSyncModeName);
    }

    public ImmutableList<(string ParameterName, float Value)> BuildTrackingReplacementWrites(
        VRCAnimatorTrackingControl.TrackingType eyeTracking,
        VRCAnimatorTrackingControl.TrackingType mouthTracking)
    {
        var writes = ImmutableList.CreateBuilder<(string ParameterName, float Value)>();
        switch (eyeTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                AddModeWrites(writes, _eyeBlinkModeNames, VRChatTrackingPlan.BuiltInMode);
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                AddModeWrites(writes, _eyeBlinkModeNames, VRChatTrackingPlan.DisabledMode);
                break;
        }
        switch (mouthTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                AddModeWrites(writes, _lipSyncModeNames, VRChatTrackingPlan.BuiltInMode);
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                AddModeWrites(writes, _lipSyncModeNames, VRChatTrackingPlan.DisabledMode);
                break;
        }
        return writes.ToImmutable();
    }

    public ImmutableList<(string ParameterName, float Value)> BuildWrites(ExpressionItem expression)
    {
        var writes = ImmutableList.CreateBuilder<(string ParameterName, float Value)>();
        if (_plan.ShouldBuildEyeBlinkLayer
            && _plan.EyeBlinkModeFor(expression) is { } eyeBlinkMode)
        {
            AddModeWrites(writes, _eyeBlinkModeNames, eyeBlinkMode);
        }
        if (_plan.ShouldBuildLipSyncLayer
            && _plan.LipSyncModeFor(expression) is { } lipSyncMode)
        {
            AddModeWrites(writes, _lipSyncModeNames, lipSyncMode);
        }
        return writes.ToImmutable();
    }

    public void EnsureExpressionParameters(VirtualAnimatorController controller)
    {
        EnsureEyeBlinkParameters(controller);
        EnsureLipSyncParameters(controller);
    }

    public void EnsureEyeBlinkParameters(VirtualAnimatorController controller)
    {
        if (_plan.ShouldBuildEyeBlinkLayer)
            EnsureModeParameters(controller, _eyeBlinkModeNames);
    }

    public void EnsureLipSyncParameters(VirtualAnimatorController controller)
    {
        if (_plan.ShouldBuildLipSyncLayer)
            EnsureModeParameters(controller, _lipSyncModeNames);
    }

    public DnfCondition EyeBlinkModeIs(int mode)
        => ApplyForceDisable(
            ModeIs(EyeBlinkModeName(mode)),
            mode,
            _plan.ForceDisableEyeBlinkWhen);

    public DnfCondition LipSyncModeIs(int mode)
        => ApplyForceDisable(
            ModeIs(LipSyncModeName(mode)),
            mode,
            _plan.ForceDisableLipSyncWhen);

    private static ImmutableList<string> CreateModeNames(
        int customModeCount,
        Func<int, string> getName)
        => Enumerable.Range(
                VRChatTrackingPlan.DisabledMode,
                customModeCount + VRChatTrackingPlan.FirstCustomMode)
            .Select(getName)
            .ToImmutableList();

    private static string EyeBlinkModeName(int mode) => EyeBlinkModePrefix + mode;

    private static string LipSyncModeName(int mode) => LipSyncModePrefix + mode;

    private static DnfCondition ApplyForceDisable(
        DnfCondition modeWhen,
        int mode,
        DnfCondition? forceDisableWhen)
    {
        if (forceDisableWhen == null) return modeWhen;
        return mode == VRChatTrackingPlan.DisabledMode
            ? modeWhen.Or(forceDisableWhen)
            : modeWhen.And(forceDisableWhen.Complement());
    }

    private static void AddModeWrites(
        ICollection<(string ParameterName, float Value)> writes,
        ImmutableList<string> modeNames,
        int activeMode)
    {
        for (var mode = 0; mode < modeNames.Count; mode++)
            writes.Add((modeNames[mode], mode == activeMode ? 1f : 0f));
    }

    private static void EnsureModeParameters(
        VirtualAnimatorController controller,
        ImmutableList<string> modeNames)
    {
        for (var mode = 0; mode < modeNames.Count; mode++)
        {
            var isDefaultMode = mode == VRChatTrackingPlan.BuiltInMode;
            controller.EnsureFloatParameterExists(
                modeNames[mode],
                isDefaultMode ? 1f : 0f);
        }
    }

    private static DnfCondition ModeIs(string parameterName)
    {
        var condition = ParameterCondition.Float(
            parameterName,
            ComparisonType.GreaterThan,
            ActiveThreshold);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }
}

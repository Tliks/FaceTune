using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>表情のTrackingPermissionとBlink/LipSync設定をAAPパラメータへ変換する。</summary>
internal sealed class AapProtocol
{
    private const int BuiltInMode = 1;
    private const int FirstCustomMode = 2;
    private const string AapParameterPrefix = FaceTuneConstants.GeneratedParameterPrefix + "/AAP/";
    private const string EyeBlinkEnabledName = AapParameterPrefix + "Blink/Enabled";
    private const string EyeBlinkModeName = AapParameterPrefix + "Blink/Mode";
    private const string LipSyncEnabledName = AapParameterPrefix + "LipSync/Enabled";
    private const string LipSyncModeName = AapParameterPrefix + "LipSync/Mode";

    private readonly IReadOnlyList<EyeBlinkSettings> _eyeBlinkAnimations;
    private readonly IReadOnlyList<LipSyncSettings> _lipSyncCancellers;

    public bool ControlsEyeBlink { get; }
    public bool ControlsLipSync { get; }
    public DnfCondition EyeBlinkEnabledWhen => ParameterBool(EyeBlinkEnabledName, true);
    public DnfCondition BuiltInEyeBlinkModeWhen => EyeBlinkModeIs(BuiltInMode);
    public DnfCondition LipSyncEnabledWhen => ParameterBool(LipSyncEnabledName, true);
    public IReadOnlyList<(EyeBlinkSettings Settings, int Mode)> EyeBlinkAnimationModes
        => _eyeBlinkAnimations
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();
    public IReadOnlyList<(LipSyncSettings Settings, int Mode)> LipSyncCancellerModes
        => _lipSyncCancellers
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();

    private AapProtocol(IReadOnlyList<ExpressionItem> items)
    {
        var animations = new List<EyeBlinkSettings>();
        var animationKinds = new[]
        {
            EyeBlinkSettings.Kind.SimpleAnimation,
            EyeBlinkSettings.Kind.CustomAnimation
        };
        foreach (var kind in animationKinds)
        {
            var settingsForKind = items
                .Select(item => item.EyeBlink)
                .Where(settings => settings.EyeBlinkMode == kind);
            foreach (var settings in settingsForKind)
            {
                if (!animations.Contains(settings)) animations.Add(settings);
            }
        }

        _eyeBlinkAnimations = animations;
        ControlsEyeBlink = animations.Count > 0
            || items.Any(item => item.AllowEyeBlink == TrackingPermission.Disallow);

        var cancellers = new List<LipSyncSettings>();
        foreach (var settings in items
                     .Select(item => item.LipSync)
                     .Where(settings => settings.CancellerBlendShapes.Count > 0))
        {
            if (!cancellers.Contains(settings)) cancellers.Add(settings);
        }
        _lipSyncCancellers = cancellers;
        ControlsLipSync = cancellers.Count > 0
            || items.Any(item => item.AllowLipSync == TrackingPermission.Disallow);
    }

    public static AapProtocol From(IReadOnlyList<ExpressionItem> items) => new(items);

    /// <summary>
    /// 外部VRCAnimatorTrackingControlをAAP書込へ置き換えるための書き込み列を生成する。
    /// NoChangeは置換対象外なので何も書かない。
    /// </summary>
    public static IReadOnlyList<(string ParameterName, float Value)> BuildTrackingReplacementWrites(
        VRCAnimatorTrackingControl.TrackingType eyeTracking,
        VRCAnimatorTrackingControl.TrackingType mouthTracking)
    {
        var writes = new List<(string ParameterName, float Value)>();
        switch (eyeTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                writes.Add((EyeBlinkEnabledName, 1f));
                writes.Add((EyeBlinkModeName, BuiltInMode));
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                writes.Add((EyeBlinkEnabledName, 0f));
                break;
        }
        switch (mouthTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                writes.Add((LipSyncEnabledName, 1f));
                writes.Add((LipSyncModeName, BuiltInMode));
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                writes.Add((LipSyncEnabledName, 0f));
                break;
        }
        return writes;
    }

    public IReadOnlyList<(string ParameterName, float Value)> BuildWrites(ExpressionItem expression)
    {
        var writes = new List<(string ParameterName, float Value)>();
        if (ControlsEyeBlink && expression.AllowEyeBlink != TrackingPermission.Keep)
        {
            AddEnabledWrite(writes, EyeBlinkEnabledName, expression.AllowEyeBlink);
            writes.Add((EyeBlinkModeName, Value(EyeBlinkModeFor(expression.EyeBlink))));
        }
        if (ControlsLipSync && expression.AllowLipSync != TrackingPermission.Keep)
        {
            AddEnabledWrite(writes, LipSyncEnabledName, expression.AllowLipSync);
            writes.Add((LipSyncModeName, Value(LipSyncModeFor(expression.LipSync))));
        }
        return writes;
    }

    private static void AddEnabledWrite(
        ICollection<(string ParameterName, float Value)> writes,
        string parameterName,
        TrackingPermission permission)
    {
        switch (permission)
        {
            case TrackingPermission.Allow:
                writes.Add((parameterName, 1f));
                break;
            case TrackingPermission.Disallow:
                writes.Add((parameterName, 0f));
                break;
            case TrackingPermission.Keep:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permission));
        }
    }

    public void EnsureExpressionParameters(VirtualAnimatorController controller)
    {
        EnsureEyeBlinkParameters(controller);
        EnsureLipSyncParameters(controller);
    }

    public void EnsureEyeBlinkParameters(VirtualAnimatorController controller)
    {
        if (ControlsEyeBlink)
            EnsureParameters(controller, EyeBlinkEnabledName, EyeBlinkModeName);
    }

    public void EnsureLipSyncParameters(VirtualAnimatorController controller)
    {
        if (ControlsLipSync)
            EnsureParameters(controller, LipSyncEnabledName, LipSyncModeName);
    }

    private static void EnsureParameters(
        VirtualAnimatorController controller,
        string enabledName,
        string modeName)
    {
        controller.EnsureBoolParameterExists(enabledName, true);
        controller.EnsureFloatParameterExists(modeName, Value(BuiltInMode));
    }

    public DnfCondition EyeBlinkModeIs(int mode) => IndexIs(EyeBlinkModeName, mode);

    public DnfCondition LipSyncModeIs(int mode) => IndexIs(LipSyncModeName, mode);

    private int EyeBlinkModeFor(EyeBlinkSettings settings)
    {
        if (settings.EyeBlinkMode == EyeBlinkSettings.Kind.BuiltIn) return BuiltInMode;
        for (var index = 0; index < _eyeBlinkAnimations.Count; index++)
        {
            if (_eyeBlinkAnimations[index].Equals(settings)) return FirstCustomMode + index;
        }
        throw new InvalidOperationException("Eye blink animation mode was not registered.");
    }

    private int LipSyncModeFor(LipSyncSettings settings)
    {
        if (settings.CancellerBlendShapes.Count == 0) return BuiltInMode;
        for (var index = 0; index < _lipSyncCancellers.Count; index++)
        {
            if (_lipSyncCancellers[index].Equals(settings)) return FirstCustomMode + index;
        }
        throw new InvalidOperationException("Lip sync canceller mode was not registered.");
    }

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        var condition = ParameterCondition.Bool(parameterName, value);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }

    private static DnfCondition IndexIs(string parameterName, int index)
        => AnimatorHelper.DiscreteFloatIndexCondition(parameterName, index);

    private static float Value(int index) => AnimatorHelper.DiscreteFloatIndexToValue(index);
}

using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

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
    public DnfCondition BuiltInEyeBlinkModeWhen
        => EyeBlinkModeIs(BuiltInMode);
    public DnfCondition LipSyncEnabledWhen
        => ParameterBool(LipSyncEnabledName, true);
    public IReadOnlyList<(EyeBlinkSettings Settings, int Mode)> EyeBlinkAnimationModes
        => _eyeBlinkAnimations
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();
    public IReadOnlyList<(LipSyncSettings Settings, int Mode)> LipSyncCancellerModes
        => _lipSyncCancellers
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();

    private AapProtocol(
        IReadOnlyList<ExpressionItem> items,
        bool controlEyeBlinkTracking,
        bool controlLipSyncTracking)
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
                if (!animations.Contains(settings))
                {
                    animations.Add(settings);
                }
            }
        }

        _eyeBlinkAnimations = animations;
        ControlsEyeBlink = animations.Count > 0
            || (controlEyeBlinkTracking
                && items.Any(item => item.AllowEyeBlink == TrackingPermission.Disallow));

        var cancellers = new List<LipSyncSettings>();
        foreach (var settings in items
                     .Select(item => item.LipSync)
                     .Where(settings => settings.CancellerBlendShapes.Count > 0))
        {
            if (!cancellers.Contains(settings))
            {
                cancellers.Add(settings);
            }
        }
        _lipSyncCancellers = cancellers;
        ControlsLipSync = cancellers.Count > 0
            || (controlLipSyncTracking
                && items.Any(item => item.AllowLipSync == TrackingPermission.Disallow));
    }

    public static AapProtocol From(
        IReadOnlyList<ExpressionItem> items,
        bool controlEyeBlinkTracking,
        bool controlLipSyncTracking)
        => new(
            items,
            controlEyeBlinkTracking,
            controlLipSyncTracking);

    public IReadOnlyList<AapWrite> BuildWrites(ExpressionItem expression)
    {
        var writes = new List<AapWrite>();
        if (ControlsEyeBlink)
        {
            AddEnabledWrite(
                writes,
                EyeBlinkEnabledName,
                expression.AllowEyeBlink);
            writes.Add(new AapWrite(
                EyeBlinkModeName,
                Value(EyeBlinkModeFor(expression.EyeBlink))));
        }
        if (ControlsLipSync)
        {
            AddEnabledWrite(
                writes,
                LipSyncEnabledName,
                expression.AllowLipSync);
            writes.Add(new AapWrite(
                LipSyncModeName,
                Value(LipSyncModeFor(expression.LipSync))));
        }
        return writes;
    }

    private static void AddEnabledWrite(
        ICollection<AapWrite> writes,
        string parameterName,
        TrackingPermission permission)
    {
        switch (permission)
        {
            case TrackingPermission.Allow:
                writes.Add(new AapWrite(parameterName, 1f));
                break;
            case TrackingPermission.Disallow:
                writes.Add(new AapWrite(parameterName, 0f));
                break;
            case TrackingPermission.Keep:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permission));
        }
    }

    public void CollectExpressionParameters(
        Dictionary<string, PlanParameter> parameters)
    {
        CollectEyeBlinkParameters(parameters);
        CollectLipSyncParameters(parameters);
    }

    public void CollectEyeBlinkParameters(
        IDictionary<string, PlanParameter> parameters)
    {
        if (ControlsEyeBlink)
        {
            AddParameters(parameters, EyeBlinkEnabledName, EyeBlinkModeName);
        }
    }

    public void CollectLipSyncParameters(
        IDictionary<string, PlanParameter> parameters)
    {
        if (ControlsLipSync)
        {
            AddParameters(parameters, LipSyncEnabledName, LipSyncModeName);
        }
    }

    private static void AddParameters(
        IDictionary<string, PlanParameter> parameters,
        string enabledName,
        string modeName)
    {
        parameters.TryAdd(
            enabledName,
            new PlanParameter(
                enabledName,
                AnimatorControllerParameterType.Bool,
                1f));
        parameters.TryAdd(
            modeName,
            new PlanParameter(
                modeName,
                AnimatorControllerParameterType.Float,
                Value(BuiltInMode)));
    }

    public DnfCondition EyeBlinkModeIs(int mode)
        => IndexIs(EyeBlinkModeName, mode);

    public DnfCondition LipSyncModeIs(int mode)
        => IndexIs(LipSyncModeName, mode);

    private int EyeBlinkModeFor(EyeBlinkSettings settings)
    {
        if (settings.EyeBlinkMode == EyeBlinkSettings.Kind.BuiltIn)
        {
            return BuiltInMode;
        }
        for (var index = 0; index < _eyeBlinkAnimations.Count; index++)
        {
            if (_eyeBlinkAnimations[index].Equals(settings))
            {
                return FirstCustomMode + index;
            }
        }
        throw new InvalidOperationException(
            "Eye blink animation mode was not registered.");
    }

    private int LipSyncModeFor(LipSyncSettings settings)
    {
        if (settings.CancellerBlendShapes.Count == 0)
        {
            return BuiltInMode;
        }
        for (var index = 0; index < _lipSyncCancellers.Count; index++)
        {
            if (_lipSyncCancellers[index].Equals(settings))
            {
                return FirstCustomMode + index;
            }
        }
        throw new InvalidOperationException(
            "Lip sync canceller mode was not registered.");
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

    private static float Value(int index)
        => AnimatorHelper.DiscreteFloatIndexToValue(index);
}

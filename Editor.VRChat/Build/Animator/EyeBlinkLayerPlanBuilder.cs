using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class EyeBlinkLayerPlanBuilder
{
    private const string SpeedParameterPrefix =
        FaceTuneConstants.GeneratedParameterPrefix + "/Blink/Speed/";

    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;
    private readonly bool _useTrackingControl;

    public EyeBlinkLayerPlanBuilder(
        AapProtocol aap,
        DnfCondition? disableWhen,
        bool useTrackingControl)
    {
        _aap = aap;
        _disableWhen = useTrackingControl ? disableWhen : null;
        _useTrackingControl = useTrackingControl;
    }

    public EyeBlinkLayerPlan? Build(DnfCondition? mmdPlaybackWhen)
    {
        if (!_aap.ControlsEyeBlink && _disableWhen == null)
        {
            return null;
        }

        var enabled = _aap.ControlsEyeBlink
            ? _aap.EyeBlinkEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null)
        {
            enabled = enabled.And(_disableWhen.Complement());
        }

        var animations = BuildAnimations(enabled);
        var parameters = new Dictionary<string, PlanParameter>();
        _aap.CollectEyeBlinkParameters(parameters);
        CollectParameters(parameters, animations, mmdPlaybackWhen);

        var builtInWhen = _aap.ControlsEyeBlink
            ? enabled.And(_aap.BuiltInEyeBlinkModeWhen)
            : enabled;
        return new EyeBlinkLayerPlan(
            "Eye Blink",
            _useTrackingControl,
            DnfCondition.Always,
            enabled.Complement(),
            builtInWhen,
            DnfCondition.Any(animations.Select(animation => animation.When)),
            mmdPlaybackWhen,
            animations,
            parameters.Values.ToArray());
    }

    private IReadOnlyList<EyeBlinkAnimationPlan> BuildAnimations(DnfCondition enabled)
    {
        var simpleNumber = 0;
        var customNumber = 0;
        return _aap.EyeBlinkAnimationModes.Select(entry =>
        {
            var settings = entry.Settings;
            var closeShapes = new BlendShapeWeightSet(settings.SimpleBlinkBlendShapes);
            closeShapes.AddRange(settings.SimpleConflictPreventionBlendShapes);
            return new EyeBlinkAnimationPlan(
                settings.EyeBlinkMode == EyeBlinkSettings.Kind.SimpleAnimation
                    ? $"Simple {++simpleNumber}"
                    : $"Custom {++customNumber}",
                settings.EyeBlinkMode,
                enabled.And(_aap.EyeBlinkModeIs(entry.Mode)),
                settings.IntervalSeconds,
                settings.SimpleDurationsSeconds,
                closeShapes.ToArray(),
                settings.Animations.ToArray(),
                SpeedParameterPrefix + entry.Mode);
        }).ToArray();
    }

    private void CollectParameters(
        Dictionary<string, PlanParameter> parameters,
        IEnumerable<EyeBlinkAnimationPlan> animations,
        DnfCondition? mmdPlaybackWhen)
    {
        foreach (var animation in animations)
        {
            var maximumInterval = Math.Max(
                animation.IntervalSeconds.x,
                animation.IntervalSeconds.y);
            parameters.TryAdd(
                animation.SpeedParameterName,
                new PlanParameter(
                    animation.SpeedParameterName,
                    AnimatorControllerParameterType.Float,
                    1f / Math.Max(maximumInterval, 0.001f)));
        }

        if (_disableWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, _disableWhen);
        }
        if (mmdPlaybackWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, mmdPlaybackWhen);
        }
    }
}

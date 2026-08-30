using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTuneのtracking設定から単一のglobal EyeBlink layerを構築する。</summary>
internal sealed class EyeBlinkAnimatorBuilder
{
    private const string SpeedParameterPrefix =
        FaceTuneConstants.GeneratedParameterPrefix + "/Blink/Speed/";

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly MmdSupport _mmdSupport;
    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;

    public bool ShouldBuild => _aap.ControlsEyeBlink || _disableWhen != null;

    public EyeBlinkAnimatorBuilder(
        AvatarContext avatarContext,
        AnimatorGraph graph,
        MmdSupport mmdSupport,
        AapProtocol aap,
        DnfCondition? disableWhen)
    {
        _avatarContext = avatarContext;
        _graph = graph;
        _mmdSupport = mmdSupport;
        _aap = aap;
        _disableWhen = disableWhen;
    }

    public void Build(
        VirtualAnimatorController controller,
        int layerPriority)
    {
        if (!ShouldBuild) return;

        var enabled = _aap.ControlsEyeBlink
            ? _aap.EyeBlinkEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null) enabled = enabled.And(_disableWhen.Complement());

        var animationModes = _aap.EyeBlinkAnimationModes;
        var animationConditions = animationModes
            .Select(entry => enabled.And(_aap.EyeBlinkModeIs(entry.Mode)))
            .ToArray();

        _aap.EnsureEyeBlinkParameters(controller);
        foreach (var entry in animationModes)
        {
            var maximumInterval = Math.Max(
                entry.Settings.IntervalSeconds.x,
                entry.Settings.IntervalSeconds.y);
            controller.EnsureFloatParameterExists(
                SpeedParameterName(entry.Mode),
                1f / Math.Max(maximumInterval, 0.001f));
        }
        AnimatorGraph.EnsureConditionParameters(
            controller,
            _disableWhen,
            _mmdSupport.LayerPlaybackWhen);

        var origin = AnimatorGraph.DefaultStatePosition;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, "Eye Blink", layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        _graph.AddDefaultState(layer, origin);

        var mmdState = _mmdSupport.AddPassThroughState(
            layer,
            origin - new Vector3(0, yStep * 2, 0));
        if (mmdState != null) SetEyeBlinkTracking(mmdState, false);
        InstallModeState(
            layer,
            "Disabled",
            enabled.Complement(),
            false,
            origin + new Vector3(0, yStep * 2, 0));
        InstallModeState(
            layer,
            "Built-in",
            _aap.ControlsEyeBlink
                ? enabled.And(_aap.BuiltInEyeBlinkModeWhen)
                : enabled,
            true,
            origin + new Vector3(0, yStep * 3, 0));

        if (animationModes.Count > 0)
        {
            InstallAnimations(
                layer,
                animationModes,
                animationConditions,
                origin + new Vector3(0, yStep * 4, 0),
                xStep,
                yStep);
        }
    }

    private void InstallModeState(
        VirtualLayer layer,
        string name,
        DnfCondition when,
        bool tracking,
        Vector3 position)
    {
        if (when.IsNever) return;

        var state = _graph.AddState(layer, name, position);
        _graph.AsPassThrough(state);
        SetEyeBlinkTracking(state, tracking);
        _graph.AddEntryTransition(layer, state, when);
        _graph.SetExitTransitions(state, when.Complement(), 0f);
    }

    private void InstallAnimations(
        VirtualLayer layer,
        IReadOnlyList<(EyeBlinkSettings Settings, int Mode)> animationModes,
        IReadOnlyList<DnfCondition> animationConditions,
        Vector3 gatePosition,
        float xStep,
        float yStep)
    {
        var animationWhen = DnfCondition.Any(animationConditions);
        var gate = _graph.AddState(layer, "Animation", gatePosition);
        _graph.AsPassThrough(gate);
        SetEyeBlinkTracking(gate, false);
        _graph.AddEntryTransition(layer, gate, animationWhen);

        var simpleNumber = 0;
        var customNumber = 0;
        var position = gatePosition + new Vector3(xStep, 0, 0);
        for (var index = 0; index < animationModes.Count; index++)
        {
            var entry = animationModes[index];
            var name = entry.Settings.EyeBlinkMode == EyeBlinkSettings.Kind.SimpleAnimation
                ? $"Simple {++simpleNumber}"
                : $"Custom {++customNumber}";
            var stare = entry.Settings.EyeBlinkMode switch
            {
                EyeBlinkSettings.Kind.SimpleAnimation => InstallSimple(
                    layer,
                    name,
                    entry.Settings,
                    animationConditions[index],
                    SpeedParameterName(entry.Mode),
                    position,
                    xStep,
                    yStep),
                EyeBlinkSettings.Kind.CustomAnimation => InstallCustom(
                    layer,
                    name,
                    entry.Settings,
                    animationConditions[index],
                    SpeedParameterName(entry.Mode),
                    position,
                    xStep),
                _ => throw new InvalidOperationException(
                    $"Unsupported generated eye blink mode: {entry.Settings.EyeBlinkMode}")
            };
            _graph.AddStateTransition(gate, stare, animationConditions[index], 0f);
            position.y += entry.Settings.EyeBlinkMode == EyeBlinkSettings.Kind.SimpleAnimation
                ? yStep * 3
                : yStep * 2;
        }
        _graph.AddExitTransitions(gate, animationWhen.Complement(), 0f);
    }

    private VirtualState InstallSimple(
        VirtualLayer layer,
        string name,
        EyeBlinkSettings settings,
        DnfCondition when,
        string speedParameterName,
        Vector3 position,
        float xStep,
        float yStep)
    {
        var stare = AddWaitingState(
            layer,
            $"{name} Stare",
            position,
            settings.IntervalSeconds,
            speedParameterName);
        var entry = _graph.AddState(
            layer,
            $"{name} Entry",
            position + new Vector3(xStep, 0, 0));
        var exit = _graph.AddState(
            layer,
            $"{name} Exit",
            position + new Vector3(0, yStep, 0));
        var close = _graph.AddState(
            layer,
            $"{name} Close",
            position + new Vector3(xStep, yStep, 0));
        _graph.AsPassThrough(entry);
        _graph.AsPassThrough(exit);
        var holdDuration = Math.Max(0f, settings.SimpleDurationsSeconds.y);
        SetCloseClip(close, settings, holdDuration);

        var continueWhen = DnfCondition.Always;
        _graph.AddExitTimeTransition(stare, entry, continueWhen, 1f, 0f);
        _graph.AddStateTransition(
            entry,
            close,
            continueWhen,
            Math.Max(0f, settings.SimpleDurationsSeconds.x));
        var openingDuration = Math.Max(0f, settings.SimpleDurationsSeconds.z);
        if (holdDuration == 0f)
        {
            // Unity cannot satisfy an exit-time transition on a zero-length clip.
            _graph.AddStateTransition(close, exit, continueWhen, openingDuration);
        }
        else
        {
            _graph.AddExitTimeTransition(close, exit, continueWhen, 1f, openingDuration);
        }
        _graph.AddStateTransition(exit, stare, continueWhen, 0f);

        foreach (var state in new[] { stare, entry, close, exit })
            _graph.AddExitTransitions(state, when.Complement(), 0f);
        return stare;
    }

    private VirtualState InstallCustom(
        VirtualLayer layer,
        string name,
        EyeBlinkSettings settings,
        DnfCondition when,
        string speedParameterName,
        Vector3 position,
        float xStep)
    {
        var stare = AddWaitingState(
            layer,
            $"{name} Stare",
            position,
            settings.IntervalSeconds,
            speedParameterName);
        var blink = _graph.AddState(
            layer,
            $"{name} Blink",
            position + new Vector3(xStep, 0, 0));
        blink.SetNewClip(blink.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            settings.Animations);

        var continueWhen = DnfCondition.Always;
        _graph.AddExitTimeTransition(stare, blink, continueWhen, 1f, 0f);
        _graph.AddExitTimeTransition(blink, stare, continueWhen, 1f, 0f);
        _graph.AddExitTransitions(stare, when.Complement(), 0f);
        _graph.AddExitTransitions(blink, when.Complement(), 0f);
        return stare;
    }

    private VirtualState AddWaitingState(
        VirtualLayer layer,
        string name,
        Vector3 position,
        Vector2 intervalSeconds,
        string speedParameterName)
    {
        var state = _graph.AddState(layer, name, position);
        state.Motion = AnimatorHelper.CreateDelayClip(1f, name);
        state.SpeedParameter = speedParameterName;

        var minimumInterval = Math.Max(
            Math.Min(intervalSeconds.x, intervalSeconds.y),
            0.001f);
        var maximumInterval = Math.Max(
            Math.Max(intervalSeconds.x, intervalSeconds.y),
            minimumInterval);
        AddRandomDriver(state, speedParameterName, 1f / maximumInterval, 1f / minimumInterval);
        return state;
    }

    private void SetCloseClip(
        VirtualState state,
        EyeBlinkSettings settings,
        float holdDuration)
    {
        var closeShapes = new BlendShapeWeightSet(settings.SimpleBlinkBlendShapes);
        closeShapes.AddRange(settings.SimpleConflictPreventionBlendShapes);
        if (closeShapes.Count == 0)
        {
            state.Motion = AnimatorHelper.CreateDelayClip(holdDuration, state.Name);
            return;
        }

        var clip = state.SetNewClip(state.Name);
        foreach (var shape in closeShapes)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, shape.Weight),
                new Keyframe(holdDuration, shape.Weight));
            clip.AddBlendShapeAnimation(
                _avatarContext.BodyPath,
                new BlendShapeWeightAnimation(shape.Name, curve));
        }
    }

    private static string SpeedParameterName(int mode) => SpeedParameterPrefix + mode;

    private static void SetEyeBlinkTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingEyes = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }

    private static void AddRandomDriver(
        VirtualState state,
        string parameterName,
        float min,
        float max)
    {
        state.EnsureBehavior<VRCAvatarParameterDriver>().parameters.Add(
            new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Random,
                name = parameterName,
                valueMin = min,
                valueMax = max
            });
    }
}

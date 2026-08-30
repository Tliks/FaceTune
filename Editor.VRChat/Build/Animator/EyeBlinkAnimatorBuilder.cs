using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTuneのtracking設定から単一のglobal EyeBlink layerを構築する。</summary>
internal sealed class EyeBlinkAnimatorBuilder
{
    private static readonly Vector3 LayoutOrigin = new(300, 0, 0);
    private const string SpeedParameterPrefix =
        FaceTuneConstants.GeneratedParameterPrefix + "/Blink/Speed/";

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly MmdSupport _mmdSupport;
    private readonly VRChatTrackingPlan _plan;
    private readonly AapProtocol _aap;

    public EyeBlinkAnimatorBuilder(
        AvatarContext avatarContext,
        AnimatorGraph graph,
        MmdSupport mmdSupport,
        VRChatTrackingPlan plan,
        AapProtocol aap)
    {
        _avatarContext = avatarContext;
        _graph = graph;
        _mmdSupport = mmdSupport;
        _plan = plan;
        _aap = aap;
    }

    public void Build(
        VirtualAnimatorController controller,
        int layerPriority)
    {
        var animationModes = _plan.EyeBlinkAnimations
            .Select((settings, index) => (
                Settings: settings,
                Mode: VRChatTrackingPlan.FirstCustomMode + index))
            .ToImmutableList();
        var animationConditions = animationModes
            .Select(entry => _aap.EyeBlinkModeIs(entry.Mode))
            .ToImmutableList();
        var disabledWhen = _aap.EyeBlinkModeIs(VRChatTrackingPlan.DisabledMode);
        var builtInWhen = _aap.EyeBlinkModeIs(VRChatTrackingPlan.BuiltInMode);
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
            animationConditions
                .Append(disabledWhen)
                .Append(builtInWhen)
                .Append(_mmdSupport.LayerPlaybackWhen)
                .ToArray());

        var origin = LayoutOrigin;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, "Eye Blink", layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        var evaluationState = _graph.AddState(layer, "Mode Evaluation", origin);
        _graph.AsPassThrough(evaluationState);
        SetEyeBlinkTracking(evaluationState, false);

        var defaultState = _graph.AddInitialDelayState(
            layer,
            origin + new Vector3(0, yStep * 2, 0));
        SetEyeBlinkTracking(defaultState, false);
        _graph.AddExitTimeTransition(defaultState, evaluationState);

        var mmdState = _mmdSupport.AddPassThroughState(
            layer,
            origin - new Vector3(0, yStep * 2, 0),
            evaluationState);
        if (mmdState != null) SetEyeBlinkTracking(mmdState, false);
        InstallModeState(
            layer,
            "Disabled",
            disabledWhen,
            false,
            evaluationState,
            origin + new Vector3(xStep, 0, 0));
        InstallModeState(
            layer,
            "Built-in",
            builtInWhen,
            true,
            evaluationState,
            origin + new Vector3(xStep, yStep, 0));

        if (animationModes.Count > 0)
        {
            InstallAnimations(
                layer,
                animationModes,
                animationConditions,
                evaluationState,
                origin + new Vector3(xStep, yStep * 2, 0),
                yStep);
        }
    }

    private void InstallModeState(
        VirtualLayer layer,
        string name,
        DnfCondition when,
        bool tracking,
        VirtualState evaluationState,
        Vector3 position)
    {
        if (when.IsNever) return;

        var state = _graph.AddState(layer, name, position);
        _graph.AsPassThrough(state);
        SetEyeBlinkTracking(state, tracking);
        _graph.AddStateTransition(evaluationState, state, when, 0f);
        _graph.AddStateTransition(state, evaluationState, when.Complement(), 0f);
    }

    private void InstallAnimations(
        VirtualLayer layer,
        ImmutableList<(EyeBlinkSettings Settings, int Mode)> animationModes,
        ImmutableList<DnfCondition> animationConditions,
        VirtualState evaluationState,
        Vector3 position,
        float yStep)
    {
        var simpleNumber = 0;
        var customNumber = 0;
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
                    evaluationState,
                    SpeedParameterName(entry.Mode),
                    position,
                    yStep),
                EyeBlinkSettings.Kind.CustomAnimation => InstallCustom(
                    layer,
                    name,
                    entry.Settings,
                    animationConditions[index],
                    evaluationState,
                    SpeedParameterName(entry.Mode),
                    position,
                    yStep),
                _ => throw new InvalidOperationException(
                    $"Unsupported generated eye blink mode: {entry.Settings.EyeBlinkMode}")
            };
            SetEyeBlinkTracking(stare, false);
            _graph.AddStateTransition(
                evaluationState,
                stare,
                animationConditions[index],
                0f);
            position.y += entry.Settings.EyeBlinkMode == EyeBlinkSettings.Kind.SimpleAnimation
                ? yStep * 4
                : yStep * 2;
        }
    }

    private VirtualState InstallSimple(
        VirtualLayer layer,
        string name,
        EyeBlinkSettings settings,
        DnfCondition when,
        VirtualState evaluationState,
        string speedParameterName,
        Vector3 position,
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
            position + new Vector3(0, yStep, 0));
        var close = _graph.AddState(
            layer,
            $"{name} Close",
            position + new Vector3(0, yStep * 2, 0));
        var exit = _graph.AddState(
            layer,
            $"{name} Exit",
            position + new Vector3(0, yStep * 3, 0));
        _graph.AsPassThrough(entry);
        _graph.AsPassThrough(exit);
        var holdDuration = Math.Max(0f, settings.SimpleDurationsSeconds.y);
        SetCloseClip(close, settings, holdDuration);

        var continueWhen = DnfCondition.Always;
        foreach (var state in new[] { stare, entry, close, exit })
            _graph.AddStateTransition(state, evaluationState, when.Complement(), 0f);

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
        return stare;
    }

    private VirtualState InstallCustom(
        VirtualLayer layer,
        string name,
        EyeBlinkSettings settings,
        DnfCondition when,
        VirtualState evaluationState,
        string speedParameterName,
        Vector3 position,
        float yStep)
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
            position + new Vector3(0, yStep, 0));
        blink.SetNewClip(blink.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            settings.Animations);

        var continueWhen = DnfCondition.Always;
        _graph.AddStateTransition(stare, evaluationState, when.Complement(), 0f);
        _graph.AddStateTransition(blink, evaluationState, when.Complement(), 0f);
        _graph.AddExitTimeTransition(stare, blink, continueWhen, 1f, 0f);
        _graph.AddExitTimeTransition(blink, stare, continueWhen, 1f, 0f);
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

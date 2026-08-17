using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>単一のglobal EyeBlink layerを構築する。</summary>
internal sealed class EyeBlinkAnimatorInstaller
{
    private const float InitialRetryDurationSeconds = 0.1f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;

    public EyeBlinkAnimatorInstaller(AvatarContext avatarContext, AnimatorGraph graph)
    {
        _avatarContext = avatarContext;
        _graph = graph;
    }

    public void Install(
        VirtualAnimatorController controller,
        EyeBlinkLayerPlan plan,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, plan.Parameters);
        var origin = AnimatorGraph.DefaultStatePosition;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, plan.Name, layerPriority);
        layer.StateMachine!.ExitPosition += new Vector3(xStep, 0, 0);

        var initial = _graph.AddState(layer, "Initial", origin);
        _graph.AsPassThrough(initial);
        layer.StateMachine!.DefaultState = initial;
        _graph.SetExitTransitions(
            initial,
            plan.InitialExitWhen,
            InitialRetryDurationSeconds);

        InstallMmdState(layer, plan, origin, yStep);
        InstallModeState(
            layer,
            "Disabled",
            plan.DisabledWhen,
            false,
            plan.UseTrackingControl,
            origin + new Vector3(0, yStep * 2, 0));
        InstallModeState(
            layer,
            "Built-in",
            plan.BuiltInWhen,
            true,
            plan.UseTrackingControl,
            origin + new Vector3(0, yStep * 3, 0));
        if (plan.Animations.Count > 0)
        {
            InstallAnimations(
                layer,
                plan,
                origin + new Vector3(0, yStep * 4, 0),
                xStep,
                yStep);
        }
    }

    private void InstallMmdState(
        VirtualLayer layer,
        EyeBlinkLayerPlan plan,
        Vector3 origin,
        float yStep)
    {
        if (plan.MmdPlaybackWhen is not { } mmdWhen)
        {
            return;
        }

        var position = origin - new Vector3(0, yStep * 2, 0);
        var mmd = _graph.AddState(layer, "MMD Playback", position);
        _graph.AsPassThrough(mmd);
        _graph.SetAnyStateTransition(layer, mmd, mmdWhen, 0f);
        _graph.SetExitTransitions(mmd, mmdWhen.Complement(), 0f);
    }

    private void InstallModeState(
        VirtualLayer layer,
        string name,
        DnfCondition when,
        bool tracking,
        bool useTrackingControl,
        Vector3 position)
    {
        var state = _graph.AddState(layer, name, position);
        _graph.AsPassThrough(state);
        if (useTrackingControl)
        {
            SetEyeBlinkTracking(state, tracking);
        }
        _graph.AddEntryTransition(layer, state, when);
        _graph.SetExitTransitions(state, when.Complement(), 0f);
    }

    private void InstallAnimations(
        VirtualLayer layer,
        EyeBlinkLayerPlan plan,
        Vector3 gatePosition,
        float xStep,
        float yStep)
    {
        var gate = _graph.AddState(layer, "Animation", gatePosition);
        _graph.AsPassThrough(gate);
        if (plan.UseTrackingControl)
        {
            SetEyeBlinkTracking(gate, false);
        }
        _graph.AddEntryTransition(layer, gate, plan.AnimationWhen);

        var position = gatePosition + new Vector3(xStep, 0, 0);
        foreach (var animation in plan.Animations)
        {
            var stare = animation.Kind switch
            {
                EyeBlinkSettings.Kind.SimpleAnimation => InstallSimple(
                    layer,
                    animation,
                    plan.InitialExitWhen,
                    position,
                    xStep,
                    yStep),
                EyeBlinkSettings.Kind.CustomAnimation => InstallCustom(
                    layer,
                    animation,
                    plan.InitialExitWhen,
                    position,
                    xStep),
                _ => throw new InvalidOperationException(
                    $"Unsupported generated eye blink mode: {animation.Kind}")
            };
            _graph.AddStateTransition(gate, stare, animation.When, 0f);
            position.y += animation.Kind == EyeBlinkSettings.Kind.SimpleAnimation
                ? yStep * 3
                : yStep * 2;
        }
        _graph.AddExitTransitions(gate, plan.AnimationWhen.Complement(), 0f);
    }

    private VirtualState InstallSimple(
        VirtualLayer layer,
        EyeBlinkAnimationPlan plan,
        DnfCondition continueWhen,
        Vector3 position,
        float xStep,
        float yStep)
    {
        var stare = AddWaitingState(layer, $"{plan.Name} Stare", position, plan);
        var entry = _graph.AddState(
            layer,
            $"{plan.Name} Entry",
            position + new Vector3(xStep, 0, 0));
        var exit = _graph.AddState(
            layer,
            $"{plan.Name} Exit",
            position + new Vector3(0, yStep, 0));
        var close = _graph.AddState(
            layer,
            $"{plan.Name} Close",
            position + new Vector3(xStep, yStep, 0));
        _graph.AsPassThrough(entry);
        _graph.AsPassThrough(exit);
        SetCloseClip(close, plan);

        _graph.AddExitTimeTransition(stare, entry, continueWhen, 1f, 0f);
        _graph.AddStateTransition(
            entry,
            close,
            continueWhen,
            Math.Max(0f, plan.SimpleDurationsSeconds.x));
        _graph.AddExitTimeTransition(
            close,
            exit,
            continueWhen,
            1f,
            Math.Max(0f, plan.SimpleDurationsSeconds.z));
        _graph.AddStateTransition(exit, stare, continueWhen, 0f);

        foreach (var state in new[] { stare, entry, close, exit })
        {
            _graph.AddExitTransitions(state, plan.When.Complement(), 0f);
        }
        return stare;
    }

    private VirtualState InstallCustom(
        VirtualLayer layer,
        EyeBlinkAnimationPlan plan,
        DnfCondition continueWhen,
        Vector3 position,
        float xStep)
    {
        var stare = AddWaitingState(layer, $"{plan.Name} Stare", position, plan);
        var blink = _graph.AddState(
            layer,
            $"{plan.Name} Blink",
            position + new Vector3(xStep, 0, 0));
        blink.SetNewClip(blink.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            plan.CustomAnimations);

        _graph.AddExitTimeTransition(stare, blink, continueWhen, 1f, 0f);
        _graph.AddExitTimeTransition(blink, stare, continueWhen, 1f, 0f);
        _graph.AddExitTransitions(stare, plan.When.Complement(), 0f);
        _graph.AddExitTransitions(blink, plan.When.Complement(), 0f);
        return stare;
    }

    private VirtualState AddWaitingState(
        VirtualLayer layer,
        string name,
        Vector3 position,
        EyeBlinkAnimationPlan plan)
    {
        var state = _graph.AddState(layer, name, position);
        state.Motion = AnimatorHelper.CreateDelayClip(1f, name);
        state.SpeedParameter = plan.SpeedParameterName;

        var minimumInterval = Math.Max(Math.Min(plan.IntervalSeconds.x, plan.IntervalSeconds.y), 0.001f);
        var maximumInterval = Math.Max(Math.Max(plan.IntervalSeconds.x, plan.IntervalSeconds.y), minimumInterval);
        AddRandomDriver(state, plan.SpeedParameterName, 1f / maximumInterval, 1f / minimumInterval);
        return state;
    }

    private void SetCloseClip(VirtualState state, EyeBlinkAnimationPlan plan)
    {
        var holdDuration = Math.Max(0f, plan.SimpleDurationsSeconds.y);
        if (plan.SimpleCloseBlendShapes.Count == 0)
        {
            state.Motion = AnimatorHelper.CreateDelayClip(holdDuration, state.Name);
            return;
        }

        var clip = state.SetNewClip(state.Name);
        foreach (var shape in plan.SimpleCloseBlendShapes)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, shape.Weight),
                new Keyframe(holdDuration, shape.Weight));
            clip.AddBlendShapeAnimation(
                _avatarContext.BodyPath,
                new BlendShapeWeightAnimation(shape.Name, curve));
        }
    }

    private static void SetEyeBlinkTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingEyes = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }

    private static void AddRandomDriver(VirtualState state, string parameterName, float min, float max)
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

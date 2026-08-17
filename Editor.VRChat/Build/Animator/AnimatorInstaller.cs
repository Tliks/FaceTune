using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>controller単位のinstall順序を管理する。</summary>
internal sealed class AnimatorInstaller
{
    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly ExpressionAnimatorInstaller _expressions;
    private readonly EyeBlinkAnimatorInstaller _eyeBlink;
    private readonly LipSyncAnimatorInstaller _lipSync;

    public AnimatorInstaller(AvatarContext avatarContext, bool useWriteDefaults)
    {
        _avatarContext = avatarContext;
        _graph = new AnimatorGraph(useWriteDefaults);
        _expressions = new ExpressionAnimatorInstaller(avatarContext, _graph);
        _eyeBlink = new EyeBlinkAnimatorInstaller(avatarContext, _graph);
        _lipSync = new LipSyncAnimatorInstaller(avatarContext, _graph);
    }

    public void InstallInitial(
        VirtualAnimatorController controller,
        InitialLayerPlan plan,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, plan.Parameters);
        var origin = AnimatorGraph.DefaultStatePosition;
        var layer = _graph.AddLayer(controller, plan.Name, layerPriority);
        var defaultState = _graph.AddState(layer, plan.DefaultState.Name, origin);
        layer.StateMachine!.DefaultState = defaultState;
        SetInitialClip(defaultState, plan.DefaultState);
        _graph.SetExitTransitions(defaultState, plan.DefaultState.When, 0f);

        if (plan.MmdPlaybackState is not { } mmdPlan) return;

        var mmdState = _graph.AddState(
            layer,
            mmdPlan.Name,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0));
        SetInitialClip(mmdState, mmdPlan);
        _graph.AddEntryTransition(layer, mmdState, mmdPlan.When);
        _graph.SetExitTransitions(mmdState, mmdPlan.When.Complement(), 0f);

        if (plan.MmdDisableMode == MmdDisableMode.DisableFxLayer)
        {
            SetFxPlayableWeight(defaultState, 1f);
            SetFxPlayableWeight(mmdState, 0f);
        }
    }

    public void InstallUnit(
        VirtualAnimatorController controller,
        OutputUnitPlan unit,
        int layerPriority)
    {
        AnimatorGraph.EnsureParameters(controller, unit.Parameters);
        foreach (var layer in unit.ExpressionLayers)
            _expressions.Install(controller, layer, layerPriority);
    }

    public void InstallEyeBlink(
        VirtualAnimatorController controller,
        EyeBlinkLayerPlan plan,
        int layerPriority)
        => _eyeBlink.Install(controller, plan, layerPriority);

    public void InstallLipSync(
        VirtualAnimatorController controller,
        LipSyncLayerPlan plan,
        int layerPriority)
        => _lipSync.Install(controller, plan, layerPriority);

    private void SetInitialClip(VirtualState state, InitialStatePlan plan)
    {
        state.SetNewClip(plan.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            plan.BlendShapes.ToBlendShapeAnimations());
    }

    private static void SetFxPlayableWeight(VirtualState state, float weight)
    {
        var control = state.EnsureBehavior<VRCPlayableLayerControl>();
        control.layer = VRCPlayableLayerControl.BlendableLayer.FX;
        control.goalWeight = weight;
        control.blendDuration = 0f;
    }
}

/// <summary>Virtual Animatorの配置規則とtransition生成を一箇所にまとめる。</summary>
internal sealed class AnimatorGraph
{
    public static readonly Vector3 DefaultStatePosition = new(300, 0, 0);
    public const float PositionXStep = 250f;
    public const float PositionYStep = 50f;

    private readonly bool _useWriteDefaults;
    private readonly VirtualClip _emptyClip;

    public AnimatorGraph(bool useWriteDefaults)
    {
        _useWriteDefaults = useWriteDefaults;
        _emptyClip = AnimatorHelper.CreateCustomEmptyClip();
    }

    public VirtualLayer AddLayer(VirtualAnimatorController controller, string name, int priority)
        => controller.AddLayer(new LayerPriority(priority), $"{FaceTuneConstants.Name}: {name}");

    public VirtualState AddState(VirtualLayer layer, string name, Vector3 position)
    {
        var state = layer.StateMachine!.AddState(name, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    public void AsPassThrough(VirtualState state)
    {
        state.Motion = _useWriteDefaults ? null : _emptyClip;
    }

    public static void EnsureParameters(
        VirtualAnimatorController controller,
        IEnumerable<PlanParameter> parameters)
    {
        foreach (var parameter in parameters)
            controller.EnsureParameterExists(parameter.Type, parameter.Name, parameter.DefaultValue);
    }

    public void AddStateTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = when.Cases.Select(conditionCase =>
        {
            var transition = CreateStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void AddExitTimeTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float exitTime,
        float duration)
    {
        var transitions = when.Cases.Select(conditionCase =>
        {
            var transition = AnimatorHelper.CreateTransitionWithExitTime(exitTime, duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void SetExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        state.Transitions = ImmutableList<VirtualStateTransition>.Empty;
        AddExitTransitions(state, when, duration);
    }

    public void AddExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        var transitions = when.Cases.Select(conditionCase =>
        {
            var transition = CreateExitTransition(duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        // Mode/MMD解除は時間遷移より先に評価する。
        state.Transitions = transitions.Concat(state.Transitions).ToImmutableList();
    }

    public void SetAnyStateTransition(
        VirtualLayer layer,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = when.Cases.Select(conditionCase =>
        {
            var transition = CreateAnyStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        layer.StateMachine!.AnyStateTransitions = transitions.ToImmutableList();
    }

    public void AddEntryTransition(VirtualLayer layer, VirtualState destination, DnfCondition when)
    {
        var transitions = when.Cases.Select(conditionCase =>
        {
            var transition = CreateEntryTransition(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        layer.StateMachine!.EntryTransitions =
            layer.StateMachine.EntryTransitions.AddRange(transitions);
    }

    private static VirtualStateTransition CreateStateTransition(VirtualState destination, float duration)
    {
        var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
        transition.SetDestination(destination);
        return transition;
    }

    private static VirtualStateTransition CreateExitTransition(float duration)
    {
        var transition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
        transition.SetExitDestination();
        return transition;
    }

    private static VirtualStateTransition CreateAnyStateTransition(VirtualState destination, float duration)
    {
        var transition = CreateStateTransition(destination, duration);
        transition.CanTransitionToSelf = false;
        return transition;
    }

    private static VirtualTransition CreateEntryTransition(VirtualState destination)
    {
        var transition = VirtualTransition.Create();
        transition.SetDestination(destination);
        return transition;
    }

    private static IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        if (conditionCase.IsAlways)
            throw new InvalidOperationException(
                "Always conditions must be lowered before installing the animator build plan.");

        return conditionCase.Rules
            .Cast<AnimatorConditionRule>()
            .OrderBy(rule => rule.ParameterName, StringComparer.Ordinal)
            .ThenBy(rule => rule.ParameterType)
            .ThenBy(rule => rule.Condition.mode)
            .ThenBy(rule => rule.Condition.threshold)
            .Select(rule => rule.Condition);
    }
}

/// <summary>Expression layerと、そのmotionを構築する。</summary>
internal sealed class ExpressionAnimatorInstaller
{
    private const float InitialRetryDurationSeconds = 0.1f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly Dictionary<ExpressionClipKey, VirtualClip> _clips = new();

    public ExpressionAnimatorInstaller(AvatarContext avatarContext, AnimatorGraph graph)
    {
        _avatarContext = avatarContext;
        _graph = graph;
    }

    public void Install(
        VirtualAnimatorController controller,
        ExpressionLayerPlan plan,
        int layerPriority)
    {
        var origin = AnimatorGraph.DefaultStatePosition;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, plan.Name, layerPriority);

        var initial = _graph.AddState(layer, "Initial", origin);
        _graph.AsPassThrough(initial);
        layer.StateMachine!.DefaultState = initial;
        _graph.SetExitTransitions(
            initial,
            plan.InitialExitWhen,
            InitialRetryDurationSeconds);

        if (plan.MmdPlaybackWhen is { } mmdWhen)
        {
            var mmd = _graph.AddState(layer, "MMD Playback", origin - new Vector3(0, yStep * 2, 0));
            _graph.AsPassThrough(mmd);
            _graph.SetAnyStateTransition(layer, mmd, mmdWhen, 0f);
            _graph.SetExitTransitions(mmd, mmdWhen.Complement(), 0f);
        }

        if (plan.PassThroughWhen is { } passThroughWhen)
        {
            var passThrough = _graph.AddState(layer, "PassThrough", origin + new Vector3(0, yStep * 2, 0));
            _graph.AsPassThrough(passThrough);
            _graph.AddEntryTransition(layer, passThrough, passThroughWhen);
            _graph.SetExitTransitions(passThrough, passThroughWhen.Complement(), plan.TransitionDurationSeconds);
        }

        var position = origin + new Vector3(0, yStep * 4, 0);
        foreach (var statePlan in plan.States)
        {
            var state = _graph.AddState(layer, statePlan.Name, position);
            position.y += yStep;
            SetMotion(state, statePlan);
            _graph.AddEntryTransition(layer, state, statePlan.EnterWhen);
            _graph.SetExitTransitions(state, statePlan.ExitWhen, plan.TransitionDurationSeconds);
        }
    }

    private void SetMotion(VirtualState state, ExpressionStatePlan plan)
    {
        var key = new ExpressionClipKey(plan.Animations, plan.Settings, plan.AapWrites);
        state.Motion = _clips.GetOrAdd(key, _ => CreateClip(plan));
        if (plan.Settings.MultiFrameMode == MultiFrameSettings.Kind.Parameter
            && !string.IsNullOrEmpty(plan.Settings.ParameterName))
            state.TimeParameter = plan.Settings.ParameterName;
    }

    private VirtualClip CreateClip(ExpressionStatePlan plan)
    {
        var clip = VirtualClip.Create(plan.Name);
        clip.AddBlendShapeAnimations(_avatarContext.BodyPath, plan.Animations);
        foreach (var write in plan.AapWrites)
        {
            var curve = new AnimationCurve(new Keyframe(0f, write.Value));
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), write.ParameterName, curve);
        }
        if (plan.Settings.MultiFrameMode == MultiFrameSettings.Kind.Loop)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        return clip;
    }

    private sealed class ExpressionClipKey : IEquatable<ExpressionClipKey>
    {
        private readonly BlendShapeWeightAnimationSet _animations;
        private readonly MultiFrameSettings _settings;
        private readonly IReadOnlyList<AapWrite> _aapWrites;

        public ExpressionClipKey(
            BlendShapeWeightAnimationSet animations,
            MultiFrameSettings settings,
            IReadOnlyList<AapWrite> aapWrites)
        {
            _animations = animations;
            _settings = settings;
            _aapWrites = aapWrites;
        }

        public bool Equals(ExpressionClipKey? other)
            => other != null
               && _animations.Equals(other._animations)
               && _settings.Equals(other._settings)
               && _aapWrites.SequenceEqual(other._aapWrites);

        public override bool Equals(object? obj) => obj is ExpressionClipKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_animations);
            hash.Add(_settings);
            foreach (var write in _aapWrites) hash.Add(write);
            return hash.ToHashCode();
        }
    }
}

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
            origin + new Vector3(0, yStep * 2, 0));
        InstallModeState(
            layer,
            "Built-in",
            plan.BuiltInWhen,
            true,
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
        Vector3 position)
    {
        var state = _graph.AddState(layer, name, position);
        _graph.AsPassThrough(state);
        SetEyeBlinkTracking(state, tracking);
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
        SetEyeBlinkTracking(gate, false);
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

/// <summary>単一のglobal LipSync layerを構築する。</summary>
internal sealed class LipSyncAnimatorInstaller
{
    private const float InitialRetryDurationSeconds = 0.1f;
    private const float CancellerTransitionDurationSeconds = 0.05f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;

    public LipSyncAnimatorInstaller(AvatarContext avatarContext, AnimatorGraph graph)
    {
        _avatarContext = avatarContext;
        _graph = graph;
    }

    public void Install(
        VirtualAnimatorController controller,
        LipSyncLayerPlan plan,
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
        InstallDisabledState(layer, plan, origin + new Vector3(0, yStep * 2, 0));
        var builtIn = InstallBuiltInState(
            layer,
            plan,
            origin + new Vector3(0, yStep * 3, 0));
        InstallCancellers(layer, builtIn, plan, xStep, yStep);
    }

    private void InstallMmdState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 origin,
        float yStep)
    {
        if (plan.MmdPlaybackWhen is not { } mmdWhen)
        {
            return;
        }

        var mmd = _graph.AddState(
            layer,
            "MMD Playback",
            origin - new Vector3(0, yStep * 2, 0));
        _graph.AsPassThrough(mmd);
        _graph.SetAnyStateTransition(layer, mmd, mmdWhen, 0f);
        _graph.SetExitTransitions(mmd, mmdWhen.Complement(), 0f);
    }

    private void InstallDisabledState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 position)
    {
        var disabled = _graph.AddState(layer, "Disabled", position);
        _graph.AsPassThrough(disabled);
        SetLipSyncTracking(disabled, false);
        _graph.AddEntryTransition(layer, disabled, plan.DisabledWhen);
        _graph.SetExitTransitions(
            disabled,
            plan.DisabledWhen.Complement(),
            0f);
    }

    private VirtualState InstallBuiltInState(
        VirtualLayer layer,
        LipSyncLayerPlan plan,
        Vector3 position)
    {
        var builtIn = _graph.AddState(layer, "Built-in", position);
        _graph.AsPassThrough(builtIn);
        SetLipSyncTracking(builtIn, true);
        _graph.AddEntryTransition(layer, builtIn, plan.BuiltInWhen);
        _graph.SetExitTransitions(
            builtIn,
            plan.BuiltInWhen.Complement(),
            0f);
        return builtIn;
    }

    private void InstallCancellers(
        VirtualLayer layer,
        VirtualState builtIn,
        LipSyncLayerPlan plan,
        float xStep,
        float yStep)
    {
        var position = AnimatorGraph.DefaultStatePosition
            + new Vector3(xStep, yStep * 3, 0);
        foreach (var canceller in plan.Cancellers)
        {
            var idle = _graph.AddState(
                layer,
                $"{canceller.Name} Idle",
                position);
            var lipSyncing = _graph.AddState(
                layer,
                $"{canceller.Name} Lip Syncing",
                position + new Vector3(xStep, 0, 0));
            _graph.AsPassThrough(idle);
            SetCancellerClip(lipSyncing, canceller);

            _graph.AddStateTransition(builtIn, idle, canceller.When, 0f);
            _graph.AddStateTransition(
                idle,
                lipSyncing,
                plan.VoiceActiveWhen,
                CancellerTransitionDurationSeconds);
            _graph.AddStateTransition(
                lipSyncing,
                idle,
                plan.VoiceInactiveWhen,
                CancellerTransitionDurationSeconds);
            _graph.AddExitTransitions(
                idle,
                canceller.When.Complement(),
                0f);
            _graph.AddExitTransitions(
                lipSyncing,
                canceller.When.Complement(),
                0f);

            position.y += yStep * 2;
        }
    }

    private void SetCancellerClip(
        VirtualState state,
        LipSyncCancellerPlan plan)
    {
        state.SetNewClip(state.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            plan.BlendShapes.ToBlendShapeAnimations());
    }

    private static void SetLipSyncTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingMouth = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }
}

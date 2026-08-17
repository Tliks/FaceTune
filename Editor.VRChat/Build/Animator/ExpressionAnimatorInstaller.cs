using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Platforms.VRChat;

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

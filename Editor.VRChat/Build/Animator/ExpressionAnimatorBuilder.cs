using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTune表情の優先順位と書込方式を解決し、Expression layerを構築する。</summary>
internal sealed class ExpressionAnimatorBuilder
{
    private const float InitialRetryDurationSeconds = 0.1f;

    private readonly AvatarContext _avatarContext;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _managedZeroAnimations;
    private readonly AnimatorGraph _graph;
    private readonly DnfCondition? _lockFacialInactiveWhen;
    private readonly MmdSupport _mmdSupport;
    private readonly AapProtocol _aap;
    private readonly Dictionary<ExpressionClipKey, VirtualClip> _clips = new();

    public ExpressionAnimatorBuilder(
        BuildSettings settings,
        AnimatorGraph graph,
        AvatarControlSettings avatarControlSettings,
        MmdSupport mmdSupport,
        AapProtocol aap)
    {
        _avatarContext = settings.AvatarContext;
        _managedZeroAnimations = settings.GetManagedZeroBlendShapes()
            .ToBlendShapeAnimations()
            .ToArray();
        _graph = graph;
        _lockFacialInactiveWhen = avatarControlSettings.LockFacialWhen?.Complement();
        _mmdSupport = mmdSupport;
        _aap = aap;
    }

    public void Build(
        VirtualAnimatorController controller,
        int unitId,
        IReadOnlyList<ExpressionItem> expressions,
        int layerPriority)
    {
        _aap.EnsureExpressionParameters(controller);
        AnimatorGraph.EnsureConditionParameters(
            controller,
            expressions.Select(expression => (DnfCondition?)expression.RawWhen)
                .Append(_lockFacialInactiveWhen)
                .Append(_mmdSupport.LayerPlaybackWhen)
                .ToArray());

        var layerIndex = 0;
        for (var index = 0; index < expressions.Count;)
        {
            var writeMode = expressions[index].WriteMode;
            var transitionDurationSeconds = expressions[index].Transition.DurationSeconds;
            var run = new List<ExpressionItem>();
            while (index < expressions.Count
                   && expressions[index].WriteMode == writeMode
                   && expressions[index].Transition.DurationSeconds == transitionDurationSeconds)
            {
                run.Add(expressions[index]);
                index++;
            }

            switch (writeMode)
            {
                case ExpressionWriteMode.Replace:
                    BuildReplaceLayer(
                        controller,
                        unitId,
                        layerIndex++,
                        run,
                        layerPriority);
                    break;
                case ExpressionWriteMode.Blend:
                    foreach (var packedLayer in PackBlendRun(run))
                    {
                        BuildExpressionLayer(
                            controller,
                            $"{unitId}-{layerIndex++} Blend",
                            transitionDurationSeconds,
                            packedLayer,
                            packedLayer.Select(expression => expression.RawWhen).ToArray(),
                            layerPriority);
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported expression write mode: {writeMode}");
            }
        }
    }

    private void BuildReplaceLayer(
        VirtualAnimatorController controller,
        int unitId,
        int layerIndex,
        IReadOnlyList<ExpressionItem> expressions,
        int layerPriority)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Animator.Expression.ReplaceLayer");
        var enterConditions = new DnfCondition[expressions.Count];
        var higherPriority = DnfCondition.Never;
        for (var expressionIndex = expressions.Count - 1; expressionIndex >= 0; expressionIndex--)
        {
            var expression = expressions[expressionIndex];
            enterConditions[expressionIndex] = expression.RawWhen.Except(higherPriority);
            higherPriority = higherPriority.Or(expression.RawWhen);
        }

        BuildExpressionLayer(
            controller,
            $"{unitId}-{layerIndex} Replace",
            expressions[0].Transition.DurationSeconds,
            expressions,
            enterConditions,
            layerPriority);
    }

    private void BuildExpressionLayer(
        VirtualAnimatorController controller,
        string name,
        float transitionDurationSeconds,
        IReadOnlyList<ExpressionItem> expressions,
        IReadOnlyList<DnfCondition> enterConditions,
        int layerPriority)
    {
        if (enterConditions.All(condition => condition.IsNever)) return;

        var expressionWhen = DnfCondition.Any(enterConditions);
        var origin = AnimatorGraph.DefaultStatePosition;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, name, layerPriority);

        var initial = _graph.AddState(layer, "Initial", origin);
        _graph.AsPassThrough(initial);
        layer.StateMachine!.DefaultState = initial;
        _graph.SetExitTransitions(initial, DnfCondition.Always, InitialRetryDurationSeconds);

        _mmdSupport.AddPassThroughState(
            layer,
            origin - new Vector3(0, yStep * 2, 0));

        var passThroughWhen = expressionWhen.Complement();
        if (!passThroughWhen.IsNever)
        {
            var passThrough = _graph.AddState(
                layer,
                "PassThrough",
                origin + new Vector3(0, yStep * 2, 0));
            _graph.AsPassThrough(passThrough);
            _graph.AddEntryTransition(layer, passThrough, passThroughWhen);
            _graph.SetExitTransitions(
                passThrough,
                passThroughWhen.Complement(),
                transitionDurationSeconds);
        }

        var position = origin + new Vector3(0, yStep * 4, 0);
        for (var expressionIndex = 0; expressionIndex < expressions.Count; expressionIndex++)
        {
            AddExpressionStates(
                layer,
                expressions[expressionIndex],
                expressionIndex,
                enterConditions[expressionIndex],
                transitionDurationSeconds,
                ref position);
        }
    }

    private void AddExpressionStates(
        VirtualLayer layer,
        ExpressionItem expression,
        int expressionIndex,
        DnfCondition enterWhen,
        float transitionDurationSeconds,
        ref Vector3 position)
    {
        if (enterWhen.IsNever) return;

        // Splitting DNF cases keeps exit conditions small, but switching cases restarts time-dependent motions.
        var canSplitWithoutResettingMotion = GetOutputAnimations(expression)
            .All(animation => !animation.IsMultiFrame)
            && !expression.NonFacialAnimations.IsTimeDependent;
        var stateConditions = canSplitWithoutResettingMotion && enterWhen.Cases.Count > 1
            ? enterWhen.Cases.Select(DnfCondition.FromCase).ToArray()
            : new[] { enterWhen };

        for (var stateIndex = 0; stateIndex < stateConditions.Length; stateIndex++)
        {
            var stateCondition = stateConditions[stateIndex];
            var exitWhen = stateCondition.Complement();
            if (_lockFacialInactiveWhen != null)
                exitWhen = exitWhen.And(_lockFacialInactiveWhen);

            var name = $"{expressionIndex + 1} {expression.Name}";
            if (stateConditions.Length > 1) name += $" #{stateIndex + 1}";

            var state = _graph.AddState(layer, name, position);
            position.y += AnimatorGraph.PositionYStep;
            SetMotion(state, expression, _aap.BuildWrites(expression));
            _graph.AddEntryTransition(layer, state, stateCondition);
            _graph.SetExitTransitions(state, exitWhen, transitionDurationSeconds);
        }
    }

    private IReadOnlyList<List<ExpressionItem>> PackBlendRun(
        IReadOnlyList<ExpressionItem> expressions)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Animator.Expression.PackBlendRun");
        var layers = new List<List<ExpressionItem>>();
        var layerIndices = new int[expressions.Count];

        for (var currentIndex = 0; currentIndex < expressions.Count; currentIndex++)
        {
            // A later expression must be above every earlier expression that can be active with it.
            var layerIndex = 0;
            for (var previousIndex = 0; previousIndex < currentIndex; previousIndex++)
            {
                var canShareLayer = expressions[previousIndex].Transition.DurationSeconds
                                    == expressions[currentIndex].Transition.DurationSeconds
                                    && expressions[previousIndex].RawWhen
                                        .And(expressions[currentIndex].RawWhen)
                                        .IsNever;
                if (canShareLayer) continue;
                layerIndex = Math.Max(layerIndex, layerIndices[previousIndex] + 1);
            }

            while (layers.Count <= layerIndex) layers.Add(new List<ExpressionItem>());
            layers[layerIndex].Add(expressions[currentIndex]);
            layerIndices[currentIndex] = layerIndex;
        }

        return layers;
    }

    private void SetMotion(
        VirtualState state,
        ExpressionItem expression,
        IReadOnlyList<(string ParameterName, float Value)> aapWrites)
    {
        var outputAnimations = GetOutputAnimations(expression);
        var key = new ExpressionClipKey(
            outputAnimations,
            expression.NonFacialAnimations,
            expression.MultiFrame,
            aapWrites);
        state.Motion = _clips.GetOrAdd(
            key,
            _ => CreateClip(state.Name, expression, outputAnimations, aapWrites));
        if (expression.MultiFrame.MultiFrameMode == MultiFrameSettings.Kind.Parameter
            && !string.IsNullOrEmpty(expression.MultiFrame.ParameterName))
        {
            state.TimeParameter = expression.MultiFrame.ParameterName;
        }
    }

    private VirtualClip CreateClip(
        string name,
        ExpressionItem expression,
        BlendShapeWeightAnimationSet outputAnimations,
        IReadOnlyList<(string ParameterName, float Value)> aapWrites)
    {
        var clip = VirtualClip.Create(name);
        foreach (var (binding, curve) in expression.NonFacialAnimations.FloatCurves)
            clip.SetFloatCurve(binding, curve);
        foreach (var (binding, curve) in expression.NonFacialAnimations.ObjectCurves)
            clip.SetObjectCurve(binding, curve);
        clip.AddBlendShapeAnimations(_avatarContext.BodyPath, outputAnimations);
        foreach (var write in aapWrites)
        {
            var curve = new AnimationCurve(new Keyframe(0f, write.Value));
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), write.ParameterName, curve);
        }
        if (expression.MultiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        return clip;
    }

    private BlendShapeWeightAnimationSet GetOutputAnimations(ExpressionItem expression)
    {
        var animations = new BlendShapeWeightAnimationSet();
        if (expression.WriteMode == ExpressionWriteMode.Replace)
        {
            animations.AddRange(_managedZeroAnimations);
            animations.AddRange(expression.IncomingFacialAnimations);
        }
        animations.AddRange(expression.LocalFacialAnimations);
        return animations;
    }

    private sealed class ExpressionClipKey : IEquatable<ExpressionClipKey>
    {
        private readonly BlendShapeWeightAnimationSet _animations;
        private readonly ResolvedNonFacialAnimationSet _nonFacialAnimations;
        private readonly MultiFrameSettings _settings;
        private readonly IReadOnlyList<(string ParameterName, float Value)> _aapWrites;

        public ExpressionClipKey(
            BlendShapeWeightAnimationSet animations,
            ResolvedNonFacialAnimationSet nonFacialAnimations,
            MultiFrameSettings settings,
            IReadOnlyList<(string ParameterName, float Value)> aapWrites)
        {
            _animations = animations;
            _nonFacialAnimations = nonFacialAnimations;
            _settings = settings;
            _aapWrites = aapWrites;
        }

        public bool Equals(ExpressionClipKey? other)
            => other != null
               && _animations.Equals(other._animations)
               && _nonFacialAnimations.Equals(other._nonFacialAnimations)
               && _settings.Equals(other._settings)
               && _aapWrites.SequenceEqual(other._aapWrites);

        public override bool Equals(object? obj) => obj is ExpressionClipKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_animations);
            hash.Add(_nonFacialAnimations);
            hash.Add(_settings);
            foreach (var write in _aapWrites) hash.Add(write);
            return hash.ToHashCode();
        }
    }
}

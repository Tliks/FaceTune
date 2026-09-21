using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTune表情の優先順位と書込方式を解決し、Expression layerを構築する。</summary>
internal sealed class ExpressionAnimatorBuilder
{
    private static readonly Vector3 DefaultStatePosition = new(300, 0, 0);

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
        EnsureParameters(controller, expressions);

        var packedLayers = Pack(expressions);
        for (var layerIndex = 0; layerIndex < packedLayers.Count; layerIndex++)
        {
            var layer = packedLayers[layerIndex];
            BuildExpressionLayer(
                controller,
                $"Expression {unitId}-{layerIndex}",
                layer[0].Transition.DurationSeconds,
                layer,
                BuildEnterConditions(layer),
                layerPriority);
        }
    }

    private void EnsureParameters(
        VirtualAnimatorController controller,
        IReadOnlyList<ExpressionItem> expressions)
    {
        _aap.EnsureExpressionParameters(controller);
        AnimatorGraph.EnsureConditionParameters(
            controller,
            expressions.Select(expression => (DnfCondition?)expression.RawWhen)
                .Append(_lockFacialInactiveWhen)
                .Append(_mmdSupport.LayerPlaybackWhen)
                .ToArray());
        foreach (var expression in expressions)
        {
            var multiFrame = expression.MultiFrame;
            if (multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Parameter
                && !string.IsNullOrEmpty(multiFrame.ParameterName))
            {
                controller.EnsureFloatParameterExists(multiFrame.ParameterName);
            }
        }
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
        var origin = DefaultStatePosition;
        var yStep = AnimatorGraph.PositionYStep;
        var layer = _graph.AddLayer(controller, name, layerPriority);

        var defaultState = _graph.AddInitialDelayState(layer, origin);
        _graph.AddExitTimeExitTransition(defaultState);

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

    private static List<List<ExpressionItem>> Pack(
        IReadOnlyList<ExpressionItem> expressions)
    {
        using var _ = new Utils.ProfilingSampleScope("Animator.Expression.Pack");
        var layers = new List<List<ExpressionItem>>();
        var layerWhen = DnfCondition.Never;
        foreach (var expression in expressions)
        {
            var isReplace = expression.WriteMode == ExpressionWriteMode.Replace;
            var conflictWhen = isReplace ? DnfCondition.Never : layerWhen;
            var canShare = layers.Count > 0
                && layers[^1][0].Transition.DurationSeconds
                    == expression.Transition.DurationSeconds
                && conflictWhen.And(expression.RawWhen).IsNever;
            if (!canShare)
            {
                layers.Add(new List<ExpressionItem>());
                layerWhen = DnfCondition.Never;
            }

            layers[^1].Add(expression);
            layerWhen = layerWhen.Or(expression.RawWhen);
        }
        return layers;
    }

    private static IReadOnlyList<DnfCondition> BuildEnterConditions(
        IReadOnlyList<ExpressionItem> expressions)
    {
        var result = expressions.Select(expression => expression.RawWhen).ToArray();
        var higherPriority = DnfCondition.Never;
        for (var index = expressions.Count - 1; index >= 0; index--)
        {
            result[index] = expressions[index].RawWhen.Except(higherPriority);
            if (expressions[index].WriteMode == ExpressionWriteMode.Replace)
                higherPriority = higherPriority.Or(expressions[index].RawWhen);
        }
        return result;
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

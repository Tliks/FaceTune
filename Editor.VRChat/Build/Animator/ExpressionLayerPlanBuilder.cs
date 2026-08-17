using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class ExpressionLayerPlanBuilder
{
    private readonly DnfCondition? _lockFacialInactiveWhen;
    private readonly DnfCondition? _mmdPlaybackWhen;
    private readonly AapProtocol _aap;

    public ExpressionLayerPlanBuilder(
        AvatarControlSettings avatarControlSettings,
        DnfCondition? mmdPlaybackWhen,
        AapProtocol aap)
    {
        _lockFacialInactiveWhen = avatarControlSettings.LockFacialWhen?.Complement();
        _mmdPlaybackWhen = mmdPlaybackWhen;
        _aap = aap;
    }

    public (IReadOnlyList<ExpressionLayerPlan> Layers, IReadOnlyList<PlanParameter> Parameters) Build(
        int unitId,
        IReadOnlyList<ExpressionItem> expressions)
    {
        var layers = new List<ExpressionLayerPlan>();
        for (var index = 0; index < expressions.Count;)
        {
            var writeMode = expressions[index].WriteMode;
            var run = new List<ExpressionItem>();
            var transitionDurationSeconds = expressions[index].Transition.DurationSeconds;
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
                    layers.Add(BuildReplaceLayer(unitId, layers.Count, run));
                    break;
                case ExpressionWriteMode.Blend:
                    layers.AddRange(BuildBlendLayers(unitId, layers.Count, run));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported expression write mode: {writeMode}");
            }
        }

        var parameters = new Dictionary<string, PlanParameter>();
        CollectParameters(expressions, parameters);
        return (layers, parameters.Values.ToArray());
    }

    private ExpressionLayerPlan BuildReplaceLayer(
        int unitId,
        int layerIndex,
        IReadOnlyList<ExpressionItem> expressions)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.ReplaceLayer");
        var statePlans = new IReadOnlyList<ExpressionStatePlan>[expressions.Count];
        var higherPriority = DnfCondition.Never;
        for (var expressionIndex = expressions.Count - 1; expressionIndex >= 0; expressionIndex--)
        {
            var expression = expressions[expressionIndex];
            var enterWhen = expression.RawWhen.Except(higherPriority);
            statePlans[expressionIndex] = BuildExpressionStates(
                    unitId,
                    expression,
                    expressionIndex,
                    enterWhen)
                .ToArray();

            higherPriority = higherPriority.Or(expression.RawWhen);
        }

        return BuildExpressionLayer(
            $"{unitId}-{layerIndex} Replace",
            expressions[0].Transition.DurationSeconds,
            statePlans.SelectMany(plans => plans));
    }

    private IEnumerable<ExpressionLayerPlan> BuildBlendLayers(
        int unitId,
        int firstLayerIndex,
        IReadOnlyList<ExpressionItem> expressions)
    {
        var packedLayers = PackBlendRun(expressions);
        for (var layerIndex = 0; layerIndex < packedLayers.Count; layerIndex++)
        {
            var statePlans = packedLayers[layerIndex].SelectMany((expression, expressionIndex) =>
                BuildExpressionStates(unitId, expression, expressionIndex, expression.RawWhen));
            yield return BuildExpressionLayer(
                $"{unitId}-{firstLayerIndex + layerIndex} Blend",
                packedLayers[layerIndex][0].Transition.DurationSeconds,
                statePlans);
        }
    }

    private ExpressionLayerPlan BuildExpressionLayer(
        string name,
        float transitionDurationSeconds,
        IEnumerable<ExpressionStatePlan> states)
    {
        var statePlans = states.ToArray();
        var expressionWhen = DnfCondition.Any(statePlans.Select(state => state.EnterWhen));
        return new ExpressionLayerPlan(
            name,
            transitionDurationSeconds,
            DnfCondition.Always,
            expressionWhen.IsAlways ? null : expressionWhen.Complement(),
            _mmdPlaybackWhen,
            statePlans);
    }

    private void CollectParameters(
        IReadOnlyList<ExpressionItem> expressions,
        Dictionary<string, PlanParameter> parameters)
    {
        _aap.CollectExpressionParameters(parameters);
        foreach (var expression in expressions)
            AnimatorHelper.CollectConditionParameters(parameters, expression.RawWhen);
        if (_lockFacialInactiveWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _lockFacialInactiveWhen);
        if (_mmdPlaybackWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _mmdPlaybackWhen);
    }

    private List<List<ExpressionItem>> PackBlendRun(IReadOnlyList<ExpressionItem> expressions)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.PackBlendRun");
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

            while (layers.Count <= layerIndex)
            {
                layers.Add(new List<ExpressionItem>());
            }

            layers[layerIndex].Add(expressions[currentIndex]);
            layerIndices[currentIndex] = layerIndex;
        }

        return layers;
    }

    private IEnumerable<ExpressionStatePlan> BuildExpressionStates(
        int unitId,
        ExpressionItem expression,
        int expressionIndex,
        DnfCondition enterWhen)
    {
        if (enterWhen.IsNever) yield break;

        // Splitting DNF cases keeps exit conditions small, but switching cases restarts time-dependent motions.
        var canSplitWithoutResettingMotion = expression.AnimationSet.All(animation => !animation.IsMultiFrame);
        var stateConditions = canSplitWithoutResettingMotion && enterWhen.Cases.Count > 1
            ? enterWhen.Cases.Select(DnfCondition.FromCase).ToArray()
            : new[] { enterWhen };

        for (var stateIndex = 0; stateIndex < stateConditions.Length; stateIndex++)
        {
            var stateCondition = stateConditions[stateIndex];
            var exitWhen = stateCondition.Complement();
            if (_lockFacialInactiveWhen != null)
            {
                exitWhen = exitWhen.And(_lockFacialInactiveWhen);
            }

            var name = $"{expressionIndex + 1} {expression.Name}";
            if (stateConditions.Length > 1)
            {
                name += $" #{stateIndex + 1}";
            }

            yield return new ExpressionStatePlan(
                name,
                stateCondition,
                exitWhen,
                expression.AnimationSet,
                expression.MultiFrame,
                _aap.BuildWrites(expression));
        }
    }
}

using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class AnimatorBuildPlanBuilder
{
    private readonly ExpressionPlan _plan;
    private readonly BuildSettings _settings;
    private readonly AvatarControlSettings _avatarControlSettings;
    private readonly ISet<Transform> _unitBoundaryTransforms;
    private readonly AapProtocol _aap;
    private readonly MmdAnimatorPolicy _mmdPolicy;

    private AvatarContext AvatarContext => _settings.AvatarContext;
    private DnfCondition? LayerMmdPlaybackWhen
        => _mmdPolicy.DisableMode == MmdDisableMode.DisableLayers
            ? _mmdPolicy.PlaybackWhen
            : null;

    public static AnimatorBuildPlan Build(
        ExpressionPlan plan,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ISet<Transform> unitBoundaryTransforms,
        MmdAnimatorPolicy mmdPolicy)
    {
        return new AnimatorBuildPlanBuilder(
            plan,
            settings,
            avatarControlSettings,
            unitBoundaryTransforms,
            mmdPolicy).Build();
    }

    private AnimatorBuildPlanBuilder(
        ExpressionPlan plan,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ISet<Transform> unitBoundaryTransforms,
        MmdAnimatorPolicy mmdPolicy)
    {
        _plan = plan;
        _settings = settings;
        _avatarControlSettings = avatarControlSettings;
        _unitBoundaryTransforms = unitBoundaryTransforms;
        _aap = AapProtocol.From(
            plan.Items,
            settings.AvoidEyeBlinkConflicts,
            settings.AvoidLipSyncConflicts);
        _mmdPolicy = mmdPolicy;
    }

    private AnimatorBuildPlan Build()
    {
        IReadOnlyList<OutputUnitPlan> units;
        using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.Units"))
        {
            units = BuildUnits();
        }

        InitialLayerPlan initialLayer;
        using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.InitialLayer"))
        {
            var minimumPriority = units.Min(unit => unit.Priority);
            var initialPriority = minimumPriority == int.MinValue
                ? int.MinValue
                : minimumPriority - 1;
            initialLayer = BuildInitialLayer(
                units[0].Anchor,
                initialPriority);
        }

        var eyeBlinkLayer = new EyeBlinkLayerPlanBuilder(
            _aap,
            _avatarControlSettings.DisableEyeBlinkWhen,
            _settings.AvoidEyeBlinkConflicts)
            .Build(LayerMmdPlaybackWhen);
        var lipSyncLayer = new LipSyncLayerPlanBuilder(
            _aap,
            _avatarControlSettings.DisableLipSyncWhen,
            _settings.AvoidLipSyncConflicts)
            .Build(LayerMmdPlaybackWhen);

        var conditionLowerer = new AnimatorConditionPlanLowerer(_settings.ParameterDomains);
        initialLayer = conditionLowerer.Lower(initialLayer);
        units = units.Select(conditionLowerer.Lower).ToArray();
        eyeBlinkLayer = conditionLowerer.Lower(eyeBlinkLayer);
        lipSyncLayer = conditionLowerer.Lower(lipSyncLayer);

        return new AnimatorBuildPlan(
            initialLayer,
            units,
            units[^1].Anchor,
            units.Max(unit => unit.Priority),
            eyeBlinkLayer,
            lipSyncLayer);
    }

    private InitialLayerPlan BuildInitialLayer(
        Transform anchor,
        int priority)
    {
        var blendShapes = AvatarContext.FaceRenderer
            .GetBlendShapeWeights(AvatarContext.FaceMesh)
            .Where(shape => !_settings.IsBlendShapeExcluded(shape.Name))
            .ToArray();
        var playbackWhen = _mmdPolicy.PlaybackWhen;
        var mmdState = playbackWhen == null
            ? null
            : new InitialStatePlan(
                "MMD Playback",
                playbackWhen,
                blendShapes
                    .Where(shape => !_avatarControlSettings.MmdPlayback
                        .BlendShapeNames.Contains(shape.Name))
                    .ToArray());
        var parameters = new Dictionary<string, PlanParameter>();
        if (playbackWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, playbackWhen);
        }

        return new InitialLayerPlan(
            "Initial",
            priority,
            anchor,
            new InitialStatePlan("Default", playbackWhen ?? DnfCondition.Never, blendShapes),
            mmdState,
            _mmdPolicy.DisableMode,
            parameters.Values.ToArray());
    }

    private IReadOnlyList<OutputUnitPlan> BuildUnits()
    {
        int[] splitIndices;
        using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.FindUnitSplits"))
        {
            splitIndices = FindExternalOverlapSplitIndices()
                .Concat(FindSettingsSplitIndices())
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
        }

        var units = new List<OutputUnitPlan>();
        var start = 0;
        foreach (var splitIndex in splitIndices.Append(_plan.Items.Count))
        {
            var expressions = _plan.Items.Skip(start).Take(splitIndex - start).ToArray();
            if (expressions.Length == 0)
            {
                start = splitIndex;
                continue;
            }

            var unitId = units.Count;
            var expressionLayerBuilder = new ExpressionLayerPlanBuilder(
                _avatarControlSettings,
                LayerMmdPlaybackWhen,
                _aap);
            IReadOnlyList<ExpressionLayerPlan> expressionLayers;
            IReadOnlyList<PlanParameter> parameters;
            using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.ExpressionLayers"))
            {
                (expressionLayers, parameters) = expressionLayerBuilder.Build(unitId, expressions);
            }

            units.Add(new OutputUnitPlan(
                unitId,
                expressions[0].Priority.Priority,
                expressions[0].SourceTransform,
                expressionLayers,
                parameters));
            start = splitIndex;
        }

        return units;
    }

    private IEnumerable<int> FindSettingsSplitIndices()
    {
        for (var index = 1; index < _plan.Items.Count; index++)
        {
            var previous = _plan.Items[index - 1];
            var current = _plan.Items[index];
            if (previous.WriteMode != current.WriteMode
                || previous.Priority.Priority != current.Priority.Priority
                || previous.Transition.DurationSeconds != current.Transition.DurationSeconds)
            {
                yield return index;
            }
        }
    }

    private IEnumerable<int> FindExternalOverlapSplitIndices()
    {
        if (_plan.Items.Count < 2 || _unitBoundaryTransforms.Count == 0)
        {
            yield break;
        }

        var expressionIndexByTransform = _plan.Items
            .Select((item, index) => (item.SourceTransform, index))
            .ToDictionary(entry => entry.SourceTransform, entry => entry.index);

        var hasExpressionAbove = false;
        var hasBoundarySinceLastExpression = false;

        foreach (var transform in AvatarContext.Root.GetComponentsInChildren<Transform>(true))
        {
            if (expressionIndexByTransform.TryGetValue(transform, out var expressionIndex))
            {
                if (hasExpressionAbove && hasBoundarySinceLastExpression)
                {
                    yield return expressionIndex;
                }

                hasExpressionAbove = true;
                hasBoundarySinceLastExpression = false;
                continue;
            }

            if (!hasExpressionAbove || hasBoundarySinceLastExpression)
            {
                continue;
            }

            hasBoundarySinceLastExpression =
                _unitBoundaryTransforms.Contains(transform);
        }
    }
}

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorBuildPlanBuilder
{
    private readonly ExpressionPlan _plan;
    private readonly BuildSettings _settings;
    private readonly AvatarControlSettings _avatarControlSettings;
    private readonly ISet<Transform> _unitBoundaryTransforms;
    private readonly AapProtocol _aap;
    private readonly IAnimatorPlatformServices _platformServices;
    private readonly DnfCondition? _layerForceInactiveWhen;

    private AvatarContext AvatarContext => _settings.AvatarContext;

    public static AnimatorBuildPlan Build(
        ExpressionPlan plan,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ISet<Transform> unitBoundaryTransforms,
        IAnimatorPlatformServices platformServices)
    {
        return new AnimatorBuildPlanBuilder(
            plan,
            settings,
            avatarControlSettings,
            unitBoundaryTransforms,
            platformServices).Build();
    }

    private AnimatorBuildPlanBuilder(
        ExpressionPlan plan,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ISet<Transform> unitBoundaryTransforms,
        IAnimatorPlatformServices platformServices)
    {
        _plan = plan;
        _settings = settings;
        _avatarControlSettings = avatarControlSettings;
        _unitBoundaryTransforms = unitBoundaryTransforms;
        _aap = AapProtocol.From(plan.Items);
        _platformServices = platformServices;
        _layerForceInactiveWhen = platformServices.GetLayerForceInactiveWhen(avatarControlSettings);
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
            initialLayer = BuildInitialLayer(units[0].Anchor);
        }

        TrackingControlLayerPlan? trackingControlLayer;
        using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.TrackingControl"))
        {
            trackingControlLayer = BuildTrackingControlLayer(units[^1].Anchor);
        }

        var conditionLowerer = new AnimatorConditionPlanLowerer(_settings.ParameterDomains);
        initialLayer = conditionLowerer.Lower(initialLayer);
        units = units.Select(conditionLowerer.Lower).ToArray();
        trackingControlLayer = conditionLowerer.Lower(trackingControlLayer);

        return new AnimatorBuildPlan(
            initialLayer,
            units,
            trackingControlLayer);
    }

    private InitialLayerPlan BuildInitialLayer(Transform anchor)
    {
        var blendShapes = AvatarContext.FaceRenderer
            .GetBlendShapeWeights(AvatarContext.FaceMesh)
            .Where(shape => !_settings.IsBlendShapeExcluded(shape.Name))
            .ToArray();
        var initial = new InitialLayerPlan(
            "Initial",
            anchor,
            new InitialStatePlan("Default", DnfCondition.Never, blendShapes),
            Array.Empty<InitialStatePlan>(),
            Array.Empty<PlanParameter>());
        return _platformServices.TransformInitialLayer(initial, _avatarControlSettings);
    }

    private IReadOnlyList<OutputUnitPlan> BuildUnits()
    {
        int[] splitIndices;
        using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.FindUnitSplits"))
        {
            splitIndices = FindExternalOverlapSplitIndices().ToArray();
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
                _layerForceInactiveWhen,
                _aap);
            IReadOnlyList<ExpressionLayerPlan> expressionLayers;
            IReadOnlyList<PlanParameter> parameters;
            using (new Utils.ProfilingSampleScope("FaceTune.AnimatorPlan.ExpressionLayers"))
            {
                (expressionLayers, parameters) = expressionLayerBuilder.Build(unitId, expressions);
            }

            units.Add(new OutputUnitPlan(
                unitId,
                expressions[0].SourceTransform,
                expressionLayers,
                null,
                null,
                parameters));
            start = splitIndex;
        }

        return units;
    }

    private TrackingControlLayerPlan? BuildTrackingControlLayer(Transform anchor)
    {
        return new TrackingControlPlanBuilder(
            _settings,
            _avatarControlSettings,
            _layerForceInactiveWhen,
            _aap).Build(anchor);
    }

    private IEnumerable<int> FindExternalOverlapSplitIndices()
    {
        if (_plan.Items.Count < 2 || _unitBoundaryTransforms.Count == 0) yield break;

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

            if (!hasExpressionAbove || hasBoundarySinceLastExpression) continue;

            hasBoundarySinceLastExpression = _unitBoundaryTransforms.Contains(transform);
        }
    }
}

internal sealed class AnimatorConditionPlanLowerer
{
    private const string AlwaysParameterName = FaceTuneConstants.GeneratedParameterPrefix + "/Always";

    private readonly DnfCondition _alwaysCondition;

    public AnimatorConditionPlanLowerer(ParameterDomainRegistry parameterDomains)
    {
        _alwaysCondition = DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(AlwaysParameterName, true)),
            parameterDomains);
    }

    public InitialLayerPlan Lower(InitialLayerPlan layer)
    {
        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        return layer with
        {
            DefaultState = layer.DefaultState with { When = LowerCondition(layer.DefaultState.When) },
            States = layer.States.Select(state => state with { When = LowerCondition(state.When) }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(layer.Parameters, requiresAlwaysParameter)
        };
    }

    public OutputUnitPlan Lower(OutputUnitPlan unit)
    {
        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        DnfCondition? LowerOptionalCondition(DnfCondition? condition)
            => condition == null ? null : LowerCondition(condition);

        var layers = unit.ExpressionLayers.Select(layer => layer with
        {
            PassThroughExitWhen = LowerOptionalCondition(layer.PassThroughExitWhen),
            ForceInactiveWhen = LowerOptionalCondition(layer.ForceInactiveWhen),
            States = layer.States.Select(state => state with
            {
                EnterWhen = LowerCondition(state.EnterWhen),
                ExitWhen = LowerCondition(state.ExitWhen)
            }).ToArray()
        }).ToArray();

        var eyeBlink = unit.AdvancedEyeBlink == null
            ? null
            : unit.AdvancedEyeBlink with
            {
                ForceInactiveWhen = LowerOptionalCondition(unit.AdvancedEyeBlink.ForceInactiveWhen)
            };
        var lipSync = unit.AdvancedLipSync == null
            ? null
            : unit.AdvancedLipSync with
            {
                ForceInactiveWhen = LowerOptionalCondition(unit.AdvancedLipSync.ForceInactiveWhen)
            };

        return unit with
        {
            ExpressionLayers = layers,
            AdvancedEyeBlink = eyeBlink,
            AdvancedLipSync = lipSync,
            Parameters = AddAlwaysParameterIfRequired(unit.Parameters, requiresAlwaysParameter)
        };
    }

    public TrackingControlLayerPlan? Lower(TrackingControlLayerPlan? layer)
    {
        if (layer == null) return null;

        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        return layer with
        {
            DefaultExitWhen = LowerCondition(layer.DefaultExitWhen),
            ForceInactiveWhen = layer.ForceInactiveWhen == null
                ? null
                : LowerCondition(layer.ForceInactiveWhen),
            States = layer.States.Select(state => state with
            {
                When = LowerCondition(state.When)
            }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(layer.Parameters, requiresAlwaysParameter)
        };
    }

    private static IReadOnlyList<PlanParameter> AddAlwaysParameterIfRequired(
        IReadOnlyList<PlanParameter> parameters,
        bool required)
    {
        if (!required) return parameters;
        if (parameters.Any(parameter => parameter.Name == AlwaysParameterName))
        {
            throw new InvalidOperationException($"Animator parameter name is reserved by FaceTune: {AlwaysParameterName}");
        }

        return parameters.Append(new PlanParameter(
            AlwaysParameterName,
            AnimatorControllerParameterType.Bool,
            1f)).ToArray();
    }
}

internal sealed class ExpressionLayerPlanBuilder
{
    private readonly DnfCondition? _lockFacialInactiveWhen;
    private readonly DnfCondition? _forceInactiveWhen;
    private readonly AapProtocol _aap;

    public ExpressionLayerPlanBuilder(
        AvatarControlSettings avatarControlSettings,
        DnfCondition? forceInactiveWhen,
        AapProtocol aap)
    {
        _lockFacialInactiveWhen = avatarControlSettings.LockFacialWhen?.Complement();
        _forceInactiveWhen = forceInactiveWhen;
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
        var passThroughExitWhen = DnfCondition.Any(statePlans.Select(state => state.EnterWhen));
        return new ExpressionLayerPlan(
            name,
            transitionDurationSeconds,
            passThroughExitWhen.IsAlways ? null : passThroughExitWhen,
            _forceInactiveWhen,
            statePlans);
    }

    private void CollectParameters(
        IReadOnlyList<ExpressionItem> expressions,
        Dictionary<string, PlanParameter> parameters)
    {
        _aap.CollectParameters(parameters);
        foreach (var expression in expressions)
            AnimatorHelper.CollectConditionParameters(parameters, expression.RawWhen);
        if (_lockFacialInactiveWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _lockFacialInactiveWhen);
        if (_forceInactiveWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _forceInactiveWhen);
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

internal sealed class TrackingControlPlanBuilder
{
    private readonly BuildSettings _settings;
    private readonly AvatarControlSettings _avatarControlSettings;
    private readonly DnfCondition? _forceInactiveWhen;
    private readonly AapProtocol _aap;

    public TrackingControlPlanBuilder(
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        DnfCondition? forceInactiveWhen,
        AapProtocol aap)
    {
        _settings = settings;
        _avatarControlSettings = avatarControlSettings;
        _forceInactiveWhen = forceInactiveWhen;
        _aap = aap;
    }

    public TrackingControlLayerPlan? Build(Transform anchor)
    {
        var controlsEyeBlink = _settings.AvoidEyeBlinkConflicts
            && (_avatarControlSettings.DisableEyeBlinkWhen != null || _aap.WritesEyeBlinkAnimation);
        var controlsLipSync = _settings.AvoidLipSyncConflicts
            && (_avatarControlSettings.DisableLipSyncWhen != null || _aap.WritesLipSyncAnimation);

        if (!controlsEyeBlink && !controlsLipSync) return null;

        var defaultState = new TrackingControlStatePlan(
            "Default",
            DnfCondition.Always,
            controlsEyeBlink ? true : null,
            controlsLipSync ? true : null);
            
        var states = new List<TrackingControlStatePlan>();
        if (controlsEyeBlink)
        {
            var eyeBlink = ConditionsFor(
                AapProtocol.EyeBlinkAnimationName,
                _aap.WritesEyeBlinkAnimation,
                _avatarControlSettings.DisableEyeBlinkWhen);

            if (controlsLipSync)
            {
                var lipSync = ConditionsFor(
                    AapProtocol.LipSyncAnimationName,
                    _aap.WritesLipSyncAnimation,
                    _avatarControlSettings.DisableLipSyncWhen);

                states.Add(new TrackingControlStatePlan("EyeBlink Tracking / LipSync Tracking", eyeBlink.Tracking.And(lipSync.Tracking), true, true));
                states.Add(new TrackingControlStatePlan("EyeBlink Tracking / LipSync Animation", eyeBlink.Tracking.And(lipSync.Animation), true, false));
                states.Add(new TrackingControlStatePlan("EyeBlink Animation / LipSync Tracking", eyeBlink.Animation.And(lipSync.Tracking), false, true));
                states.Add(new TrackingControlStatePlan("EyeBlink Animation / LipSync Animation", eyeBlink.Animation.And(lipSync.Animation), false, false));
            }
            else
            {
                states.Add(new TrackingControlStatePlan("EyeBlink Tracking", eyeBlink.Tracking, true, null));
                states.Add(new TrackingControlStatePlan("EyeBlink Animation", eyeBlink.Animation, false, null));
            }
        }
        else
        {
            var lipSync = ConditionsFor(
                AapProtocol.LipSyncAnimationName,
                _aap.WritesLipSyncAnimation,
                _avatarControlSettings.DisableLipSyncWhen);

            states.Add(new TrackingControlStatePlan("LipSync Tracking", lipSync.Tracking, null, true));
            states.Add(new TrackingControlStatePlan("LipSync Animation", lipSync.Animation, null, false));
        }

        var parameters = new Dictionary<string, PlanParameter>();
        CollectParameters(parameters);

        return new TrackingControlLayerPlan(
            "Tracking Control",
            anchor,
            DnfCondition.Any(states.Select(state => state.When)),
            defaultState,
            _forceInactiveWhen,
            states,
            parameters.Values.ToArray());
    }

    private void CollectParameters(Dictionary<string, PlanParameter> parameters)
    {
        _aap.CollectParameters(parameters);

        if (_avatarControlSettings.DisableEyeBlinkWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _avatarControlSettings.DisableEyeBlinkWhen);
        if (_avatarControlSettings.DisableLipSyncWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _avatarControlSettings.DisableLipSyncWhen);
        if (_forceInactiveWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _forceInactiveWhen);
    }

    private (DnfCondition Tracking, DnfCondition Animation) ConditionsFor(
        string animationAapName,
        bool hasAnimationAap,
        DnfCondition? disableWhen)
    {
        var trackingConditions = new List<DnfCondition>();
        var animationConditions = new List<DnfCondition>();

        if (hasAnimationAap)
        {
            trackingConditions.Add(_aap.IndexIs(animationAapName, AapProtocol.AnimationDisabledIndex));
            animationConditions.Add(_aap.IndexIs(animationAapName, AapProtocol.AnimationEnabledIndex));
        }

        if (disableWhen != null)
        {
            trackingConditions.Add(disableWhen.Complement());
            animationConditions.Add(disableWhen);
        }

        return (DnfCondition.All(trackingConditions), DnfCondition.Any(animationConditions));
    }

}

internal sealed class AapProtocol
{
    public const int AnimationDisabledIndex = 0;
    public const int AnimationEnabledIndex = 1;

    private const string AapParameterPrefix = FaceTuneConstants.GeneratedParameterPrefix + "/AAP/";
    public const string EyeBlinkAnimationName = AapParameterPrefix + "Blink/Animation";
    public const string LipSyncAnimationName = AapParameterPrefix + "LipSync/Animation";

    public bool WritesEyeBlinkAnimation { get; }
    public bool WritesLipSyncAnimation { get; }

    private AapProtocol(bool writesEyeBlinkAnimation, bool writesLipSyncAnimation)
    {
        WritesEyeBlinkAnimation = writesEyeBlinkAnimation;
        WritesLipSyncAnimation = writesLipSyncAnimation;
    }

    public static AapProtocol From(IReadOnlyList<ExpressionItem> items)
        => new(
            items.Any(item => item.AllowEyeBlink == TrackingPermission.Disallow),
            items.Any(item => item.AllowLipSync == TrackingPermission.Disallow));

    public IReadOnlyList<AapWrite> BuildWrites(ExpressionItem expression)
    {
        var writes = new List<AapWrite>();
        if (WritesEyeBlinkAnimation)
            writes.Add(new AapWrite(EyeBlinkAnimationName, Value(
                expression.AllowEyeBlink == TrackingPermission.Disallow ? AnimationEnabledIndex : AnimationDisabledIndex)));
        if (WritesLipSyncAnimation)
            writes.Add(new AapWrite(LipSyncAnimationName, Value(
                expression.AllowLipSync == TrackingPermission.Disallow ? AnimationEnabledIndex : AnimationDisabledIndex)));
        return writes;
    }

    public void CollectParameters(Dictionary<string, PlanParameter> parameters)
    {
        if (WritesEyeBlinkAnimation)
            parameters.TryAdd(EyeBlinkAnimationName, new PlanParameter(EyeBlinkAnimationName, AnimatorControllerParameterType.Float, Value(AnimationDisabledIndex)));
        if (WritesLipSyncAnimation)
            parameters.TryAdd(LipSyncAnimationName, new PlanParameter(LipSyncAnimationName, AnimatorControllerParameterType.Float, Value(AnimationDisabledIndex)));
    }

    public DnfCondition IndexIs(string parameterName, int index) => AnimatorHelper.DiscreteFloatIndexCondition(parameterName, index);
    private static float Value(int index) => AnimatorHelper.DiscreteFloatIndexToValue(index);
}

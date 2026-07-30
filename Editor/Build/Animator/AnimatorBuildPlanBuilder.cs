namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorBuildPlanBuilder
{
    private readonly ExpressionProgram _program;
    private readonly BuildSettings _settings;
    private readonly ISet<Transform> _unitBoundaryTransforms;
    private readonly AapProtocol _aap;
    private readonly DnfCondition? _layerForceInactiveWhen;

    private AvatarContext AvatarContext => _settings.AvatarContext;

    public static AnimatorBuildPlan Build(
        ExpressionProgram program,
        BuildSettings settings,
        ISet<Transform> unitBoundaryTransforms,
        DnfCondition? layerForceInactiveWhen)
    {
        return new AnimatorBuildPlanBuilder(
            program,
            settings,
            unitBoundaryTransforms,
            layerForceInactiveWhen).Build();
    }

    private AnimatorBuildPlanBuilder(
        ExpressionProgram program,
        BuildSettings settings,
        ISet<Transform> unitBoundaryTransforms,
        DnfCondition? layerForceInactiveWhen)
    {
        _program = program;
        _settings = settings;
        _unitBoundaryTransforms = unitBoundaryTransforms;
        _aap = AapProtocol.From(program.Items);
        _layerForceInactiveWhen = layerForceInactiveWhen;
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
        units = units.Select(conditionLowerer.Lower).ToArray();
        trackingControlLayer = conditionLowerer.Lower(trackingControlLayer);

        return new AnimatorBuildPlan(
            initialLayer,
            units,
            trackingControlLayer,
            _settings.DurationSeconds);
    }

    private InitialLayerPlan BuildInitialLayer(Transform anchor)
    {
        var blendShapes = AvatarContext.FaceRenderer
            .GetBlendShapeWeights(AvatarContext.FaceMesh)
            .Where(shape => !_settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .ToArray();
        return new InitialLayerPlan("Initial", anchor, blendShapes);
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
        foreach (var splitIndex in splitIndices.Append(_program.Items.Count))
        {
            var expressions = _program.Items.Skip(start).Take(splitIndex - start).ToArray();
            if (expressions.Length == 0)
            {
                start = splitIndex;
                continue;
            }

            var unitId = units.Count;
            var expressionLayerBuilder = new ExpressionLayerPlanBuilder(_settings, _layerForceInactiveWhen, _aap);
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
                BuildAdvancedEyeBlinkLayer(expressions),
                BuildAdvancedLipSyncLayer(expressions),
                parameters));
            start = splitIndex;
        }

        return units;
    }

    private AdvancedEyeBlinkLayerPlan? BuildAdvancedEyeBlinkLayer(IReadOnlyList<ExpressionItem> expressions)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedEyBlinkSettings.IsAnimationEnabled())
            ? new AdvancedEyeBlinkLayerPlan("Advanced EyeBlink", _layerForceInactiveWhen)
            : null;
    }

    private AdvancedLipSyncLayerPlan? BuildAdvancedLipSyncLayer(IReadOnlyList<ExpressionItem> expressions)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedLipSyncSettings.IsAnimationEnabled())
            ? new AdvancedLipSyncLayerPlan("Advanced LipSync", _layerForceInactiveWhen)
            : null;
    }

    private TrackingControlLayerPlan? BuildTrackingControlLayer(Transform anchor)
    {
        return new TrackingControlPlanBuilder(_settings, _layerForceInactiveWhen, _aap).Build(anchor);
    }

    private IEnumerable<int> FindExternalOverlapSplitIndices()
    {
        if (_program.Items.Count < 2 || _unitBoundaryTransforms.Count == 0) yield break;

        var expressionIndexByTransform = _program.Items
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
        BuildSettings settings,
        DnfCondition? forceInactiveWhen,
        AapProtocol aap)
    {
        _lockFacialInactiveWhen = string.IsNullOrWhiteSpace(settings.LockFacialParameterName)
            ? null
            : DnfCondition.Single(
                AnimatorConditionRule.FromParameterCondition(
                    ParameterCondition.Bool(settings.LockFacialParameterName, false)),
                settings.ParameterDomains);
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
            while (index < expressions.Count && expressions[index].WriteMode == writeMode)
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
                statePlans);
        }
    }

    private ExpressionLayerPlan BuildExpressionLayer(
        string name,
        IEnumerable<ExpressionStatePlan> states)
    {
        var statePlans = states.ToArray();
        var passThroughExitWhen = DnfCondition.Any(statePlans.Select(state => state.EnterWhen));
        return new ExpressionLayerPlan(
            name,
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
                var conditionsOverlap = !expressions[previousIndex].RawWhen
                    .And(expressions[currentIndex].RawWhen)
                    .IsNever;
                if (!conditionsOverlap) continue;

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
                expression.ExpressionSettings,
                _aap.BuildWrites(unitId, expression.FacialSettings));
        }
    }
}

internal sealed class TrackingControlPlanBuilder
{
    private readonly BuildSettings _settings;
    private readonly DnfCondition? _forceInactiveWhen;
    private readonly AapProtocol _aap;

    public TrackingControlPlanBuilder(
        BuildSettings settings,
        DnfCondition? forceInactiveWhen,
        AapProtocol aap)
    {
        _settings = settings;
        _forceInactiveWhen = forceInactiveWhen;
        _aap = aap;
    }

    public TrackingControlLayerPlan? Build(Transform anchor)
    {
        if (_settings.SupressTrackingControl) return null;

        var controlsEyeBlink = !string.IsNullOrWhiteSpace(_settings.DisableEyeBlinkParameterName)
            || _aap.WritesEyeBlinkAnimation;
        var controlsLipSync = !string.IsNullOrWhiteSpace(_settings.DisableLipSyncParameterName)
            || _aap.WritesLipSyncAnimation;

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
                _settings.DisableEyeBlinkParameterName);

            if (controlsLipSync)
            {
                var lipSync = ConditionsFor(
                    AapProtocol.LipSyncAnimationName,
                    _aap.WritesLipSyncAnimation,
                    _settings.DisableLipSyncParameterName);

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
                _settings.DisableLipSyncParameterName);

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

        if (!string.IsNullOrWhiteSpace(_settings.DisableEyeBlinkParameterName))
        {
            var name = _settings.DisableEyeBlinkParameterName;
            parameters.TryAdd(name, new PlanParameter(name, AnimatorControllerParameterType.Bool, 0f));
        }
        if (!string.IsNullOrWhiteSpace(_settings.DisableLipSyncParameterName))
        {
            var name = _settings.DisableLipSyncParameterName;
            parameters.TryAdd(name, new PlanParameter(name, AnimatorControllerParameterType.Bool, 0f));
        }

        if (_forceInactiveWhen != null)
            AnimatorHelper.CollectConditionParameters(parameters, _forceInactiveWhen);
    }

    private (DnfCondition Tracking, DnfCondition Animation) ConditionsFor(
        string animationAapName,
        bool hasAnimationAap,
        string forceDisableParameterName)
    {
        var trackingConditions = new List<DnfCondition>();
        var animationConditions = new List<DnfCondition>();

        if (hasAnimationAap)
        {
            trackingConditions.Add(_aap.IndexIs(animationAapName, AapProtocol.AnimationDisabledIndex));
            animationConditions.Add(_aap.IndexIs(animationAapName, AapProtocol.AnimationEnabledIndex));
        }

        if (!string.IsNullOrWhiteSpace(forceDisableParameterName))
        {
            trackingConditions.Add(DnfCondition.Single(
                AnimatorConditionRule.FromParameterCondition(
                    ParameterCondition.Bool(forceDisableParameterName, false)),
                _settings.ParameterDomains));
            animationConditions.Add(DnfCondition.Single(
                AnimatorConditionRule.FromParameterCondition(
                    ParameterCondition.Bool(forceDisableParameterName, true)),
                _settings.ParameterDomains));
        }

        return (DnfCondition.All(trackingConditions), DnfCondition.Any(animationConditions));
    }

}

internal sealed class AapProtocol
{
    public const int AnimationDisabledIndex = 0;
    public const int AnimationEnabledIndex = 1;
    private const int AdvancedNoneIndex = 0;

    private readonly Dictionary<(int UnitId, AdvancedEyeBlinkSettings Settings), int> _eyeBlinkAdvancedIndices = new();
    private readonly Dictionary<(int UnitId, AdvancedLipSyncSettings Settings), int> _lipSyncAdvancedIndices = new();


    private const string AapParameterPrefix = FaceTuneConstants.GeneratedParameterPrefix + "/AAP/";
    public const string EyeBlinkAnimationName = AapParameterPrefix + "Blink/Animation";
    public const string LipSyncAnimationName = AapParameterPrefix + "LipSync/Animation";
    private const string EyeBlinkAdvancedSelectorName = AapParameterPrefix + "Blink/Advanced";
    private const string LipSyncAdvancedSelectorName = AapParameterPrefix + "LipSync/Advanced";

    public bool WritesEyeBlinkAnimation { get; }
    public bool WritesLipSyncAnimation { get; }
    private bool WritesEyeBlinkAdvancedSelector { get; }
    private bool WritesLipSyncAdvancedSelector { get; }

    private AapProtocol(
        bool writesEyeBlinkAnimation,
        bool writesLipSyncAnimation,
        bool writesEyeBlinkAdvancedSelector,
        bool writesLipSyncAdvancedSelector)
    {
        WritesEyeBlinkAnimation = writesEyeBlinkAnimation;
        WritesLipSyncAnimation = writesLipSyncAnimation;
        WritesEyeBlinkAdvancedSelector = writesEyeBlinkAdvancedSelector;
        WritesLipSyncAdvancedSelector = writesLipSyncAdvancedSelector;
    }

    public static AapProtocol From(IReadOnlyList<ExpressionItem> items)
    {
        var settings = items.Select(item => item.FacialSettings).ToArray();
        var writesEyeBlinkAdvancedSelector = settings.Any(setting => setting.AdvancedEyBlinkSettings.IsAnimationEnabled());
        var writesLipSyncAdvancedSelector = settings.Any(setting => setting.AdvancedLipSyncSettings.IsAnimationEnabled());
        return new AapProtocol(
            settings.Any(setting => setting.AllowEyeBlink == TrackingPermission.Disallow)
                || writesEyeBlinkAdvancedSelector,
            settings.Any(setting => setting.AllowLipSync == TrackingPermission.Disallow)
                || writesLipSyncAdvancedSelector,
            writesEyeBlinkAdvancedSelector,
            writesLipSyncAdvancedSelector);
    }

    public IReadOnlyList<AapWrite> BuildWrites(int unitId, FacialSettings settings)
    {
        var writes = new List<AapWrite>();

        if (WritesEyeBlinkAnimation)
        {
            writes.Add(new AapWrite(
                EyeBlinkAnimationName,
                Value(settings.AllowEyeBlink == TrackingPermission.Disallow || settings.AdvancedEyBlinkSettings.IsAnimationEnabled()
                    ? AnimationEnabledIndex
                    : AnimationDisabledIndex)));
        }

        if (WritesEyeBlinkAdvancedSelector)
        {
            writes.Add(new AapWrite(
                EyeBlinkAdvancedSelectorName,
                Value(settings.AdvancedEyBlinkSettings.IsAnimationEnabled()
                    ? AdvancedIndex(_eyeBlinkAdvancedIndices, unitId, settings.AdvancedEyBlinkSettings)
                    : AdvancedNoneIndex)));
        }

        if (WritesLipSyncAnimation)
        {
            writes.Add(new AapWrite(
                LipSyncAnimationName,
                Value(settings.AllowLipSync == TrackingPermission.Disallow
                    ? AnimationEnabledIndex
                    : AnimationDisabledIndex)));
        }

        if (WritesLipSyncAdvancedSelector)
        {
            writes.Add(new AapWrite(
                LipSyncAdvancedSelectorName,
                Value(settings.AdvancedLipSyncSettings.IsAnimationEnabled()
                    ? AdvancedIndex(_lipSyncAdvancedIndices, unitId, settings.AdvancedLipSyncSettings)
                    : AdvancedNoneIndex)));
        }

        return writes;
    }

    public void CollectParameters(Dictionary<string, PlanParameter> parameters)
    {
        if (WritesEyeBlinkAnimation)
            parameters.TryAdd(EyeBlinkAnimationName, new PlanParameter(
                EyeBlinkAnimationName, AnimatorControllerParameterType.Float, Value(AnimationDisabledIndex)));

        if (WritesEyeBlinkAdvancedSelector)
            parameters.TryAdd(EyeBlinkAdvancedSelectorName, new PlanParameter(
                EyeBlinkAdvancedSelectorName, AnimatorControllerParameterType.Float, Value(AdvancedNoneIndex)));

        if (WritesLipSyncAnimation)
            parameters.TryAdd(LipSyncAnimationName, new PlanParameter(
                LipSyncAnimationName, AnimatorControllerParameterType.Float, Value(AnimationDisabledIndex)));

        if (WritesLipSyncAdvancedSelector)
            parameters.TryAdd(LipSyncAdvancedSelectorName, new PlanParameter(
                LipSyncAdvancedSelectorName, AnimatorControllerParameterType.Float, Value(AdvancedNoneIndex)));
    }

    public DnfCondition IndexIs(string parameterName, int index)
    {
        return AnimatorHelper.DiscreteFloatIndexCondition(parameterName, index);
    }

    private float Value(int index)
    {
        return AnimatorHelper.DiscreteFloatIndexToValue(index);
    }

    private static int AdvancedIndex<TSettings>(
        Dictionary<(int UnitId, TSettings Settings), int> indices,
        int unitId,
        TSettings settings)
        where TSettings : notnull
    {
        var key = (unitId, settings);
        if (indices.TryGetValue(key, out var index)) return index;
        index = indices.Count + 1;
        indices.Add(key, index);
        return index;
    }
}

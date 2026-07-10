using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorBuildPlanBuilder
{
    private readonly ExpressionProgram _program;
    private readonly BuildSettings _settings;
    private readonly IAnimatorPlatformServices _platformServices;
    private readonly VirtualControllerContext _controllerContext;
    private readonly AapProtocol _aap;
    private readonly DnfCondition? _layerForceInactiveWhen;

    private AvatarContext AvatarContext => _settings.AvatarContext;

    public static AnimatorBuildPlan Build(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext,
        DnfCondition? layerForceInactiveWhen)
    {
        return new AnimatorBuildPlanBuilder(program, settings, platformServices, controllerContext, layerForceInactiveWhen).Build();
    }

    private AnimatorBuildPlanBuilder(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext,
        DnfCondition? layerForceInactiveWhen)
    {
        _program = program;
        _settings = settings;
        _platformServices = platformServices;
        _controllerContext = controllerContext;
        _aap = AapProtocol.From(program.Items, platformServices.AapFloatRange);
        _layerForceInactiveWhen = layerForceInactiveWhen;
    }

    private AnimatorBuildPlan Build()
    {
        var units = BuildUnits();
        return new AnimatorBuildPlan(
            BuildInitialLayer(units[0].Anchor),
            units,
            BuildTrackingControlLayer(units[^1].Anchor),
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
        var managedBlendShapeNames = AvatarContext.FaceMesh.GetBlendShapeNames()
            .Where(name => !_settings.ExcludedBlendShapeNames.Contains(name))
            .ToHashSet();

        var splitIndices = FindExternalOverlapSplitIndices(managedBlendShapeNames);

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
            var (expressionLayers, parameters) = expressionLayerBuilder.Build(unitId, expressions);
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

    private IEnumerable<int> FindExternalOverlapSplitIndices(ISet<string> managedBlendShapeNames)
    {
        if (_program.Items.Count < 2 || managedBlendShapeNames.Count == 0) yield break;

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

            hasBoundarySinceLastExpression = _platformServices.IsUnitBoundaryTransform(
                transform,
                _controllerContext,
                managedBlendShapeNames);
        }
    }
}

internal sealed class ExpressionLayerPlanBuilder
{
    private readonly DnfCondition? _lockFacialInactiveWhen;
    private readonly DnfCondition? _forceInactiveWhen;
    private readonly ParameterDomainRegistry _parameterDomains;
    private readonly AapProtocol _aap;

    public ExpressionLayerPlanBuilder(
        BuildSettings settings,
        DnfCondition? forceInactiveWhen,
        AapProtocol aap)
    {
        _lockFacialInactiveWhen = string.IsNullOrWhiteSpace(settings.LockFacialParameterName)
            ? null
            : DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(settings.LockFacialParameterName, false)));
        _forceInactiveWhen = forceInactiveWhen;
        _parameterDomains = settings.ParameterDomains;
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
        var statePlans = new List<ExpressionStatePlan>();
        for (var expressionIndex = 0; expressionIndex < expressions.Count; expressionIndex++)
        {
            var expression = expressions[expressionIndex];
            var higherPriority = DnfCondition.Any(expressions
                .Skip(expressionIndex + 1)
                .Select(item => item.RawWhen));
            var enterWhen = expression.RawWhen.And(
                higherPriority.Complement(_parameterDomains),
                _parameterDomains);

            statePlans.AddRange(BuildExpressionStates(unitId, expression, expressionIndex, enterWhen));
        }

        return BuildExpressionLayer(
            $"{unitId}-{layerIndex} Replace",
            statePlans);
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
        return new ExpressionLayerPlan(
            name,
            DnfCondition.Any(statePlans.Select(state => state.EnterWhen)),
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
        var layers = new List<List<ExpressionItem>>();
        var layerIndices = new int[expressions.Count];

        for (var currentIndex = 0; currentIndex < expressions.Count; currentIndex++)
        {
            // A later expression must be above every earlier expression that can be active with it.
            var layerIndex = 0;
            for (var previousIndex = 0; previousIndex < currentIndex; previousIndex++)
            {
                var conditionsOverlap = !expressions[previousIndex].RawWhen
                    .And(expressions[currentIndex].RawWhen, _parameterDomains)
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
            ? enterWhen.Cases.Select(DnfCondition.Single).ToArray()
            : new[] { enterWhen };

        for (var stateIndex = 0; stateIndex < stateConditions.Length; stateIndex++)
        {
            var stateCondition = stateConditions[stateIndex];
            var exitWhen = stateCondition.Complement(_parameterDomains);
            if (_lockFacialInactiveWhen != null)
            {
                exitWhen = exitWhen.And(_lockFacialInactiveWhen, _parameterDomains);
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
            || _aap.WritesEyeBlinkControl;
        var controlsLipSync = !string.IsNullOrWhiteSpace(_settings.DisableLipSyncParameterName)
            || _aap.WritesLipSyncControl;

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
                AapProtocol.EyeBlinkControlName,
                _aap.WritesEyeBlinkControl,
                _settings.DisableEyeBlinkParameterName);

            if (controlsLipSync)
            {
                var lipSync = ConditionsFor(
                    AapProtocol.LipSyncControlName,
                    _aap.WritesLipSyncControl,
                    _settings.DisableLipSyncParameterName);

                states.Add(new TrackingControlStatePlan("EyeBlink Tracking / LipSync Tracking", eyeBlink.Tracking.And(lipSync.Tracking, _settings.ParameterDomains), true, true));
                states.Add(new TrackingControlStatePlan("EyeBlink Tracking / LipSync Animation", eyeBlink.Tracking.And(lipSync.Animation, _settings.ParameterDomains), true, false));
                states.Add(new TrackingControlStatePlan("EyeBlink Animation / LipSync Tracking", eyeBlink.Animation.And(lipSync.Tracking, _settings.ParameterDomains), false, true));
                states.Add(new TrackingControlStatePlan("EyeBlink Animation / LipSync Animation", eyeBlink.Animation.And(lipSync.Animation, _settings.ParameterDomains), false, false));
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
                AapProtocol.LipSyncControlName,
                _aap.WritesLipSyncControl,
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
        string controlAapName,
        bool hasControlAap,
        string forceDisableParameterName)
    {
        var trackingConditions = new List<DnfCondition>();
        var animationConditions = new List<DnfCondition>();

        if (hasControlAap)
        {
            trackingConditions.Add(_aap.IndexIs(controlAapName, AapProtocol.ControlTrackingIndex));
            animationConditions.Add(_aap.IndexIs(controlAapName, AapProtocol.ControlAnimationIndex));
        }

        if (!string.IsNullOrWhiteSpace(forceDisableParameterName))
        {
            trackingConditions.Add(DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(forceDisableParameterName, false))));
            animationConditions.Add(DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(forceDisableParameterName, true))));
        }

        return (DnfCondition.All(trackingConditions, _settings.ParameterDomains), DnfCondition.Any(animationConditions));
    }

}

internal sealed class AapProtocol
{
    public const int ControlTrackingIndex = 0;
    public const int ControlAnimationIndex = 1;
    private const int AdvancedNoneIndex = 0;

    private readonly DiscreteFloatParameterRange _range;
    private readonly Dictionary<(int UnitId, AdvancedEyeBlinkSettings Settings), int> _eyeBlinkAdvancedIndices = new();
    private readonly Dictionary<(int UnitId, AdvancedLipSyncSettings Settings), int> _lipSyncAdvancedIndices = new();


    private const string AAPParameterPrefix = FaceTuneConstants.ParameterPrefix + "/InternalAAP";
    public const string EyeBlinkControlName = AAPParameterPrefix + "/Blink/Control";
    public const string LipSyncControlName = AAPParameterPrefix + "/LipSync/Control";
    private const string EyeBlinkAdvancedSelectorName = AAPParameterPrefix + "/Blink/Advanced";
    private const string LipSyncAdvancedSelectorName = AAPParameterPrefix + "/LipSync/Advanced";

    public bool WritesEyeBlinkControl { get; }
    public bool WritesLipSyncControl { get; }
    private bool WritesEyeBlinkAdvancedSelector { get; }
    private bool WritesLipSyncAdvancedSelector { get; }

    private AapProtocol(
        DiscreteFloatParameterRange range,
        bool writesEyeBlinkControl,
        bool writesLipSyncControl,
        bool writesEyeBlinkAdvancedSelector,
        bool writesLipSyncAdvancedSelector)
    {
        _range = range;
        WritesEyeBlinkControl = writesEyeBlinkControl;
        WritesLipSyncControl = writesLipSyncControl;
        WritesEyeBlinkAdvancedSelector = writesEyeBlinkAdvancedSelector;
        WritesLipSyncAdvancedSelector = writesLipSyncAdvancedSelector;
    }

    public static AapProtocol From(IReadOnlyList<ExpressionItem> items, DiscreteFloatParameterRange range)
    {
        var settings = items.Select(item => item.FacialSettings).ToArray();
        var writesEyeBlinkAdvancedSelector = settings.Any(setting => setting.AdvancedEyBlinkSettings.IsAnimationEnabled());
        var writesLipSyncAdvancedSelector = settings.Any(setting => setting.AdvancedLipSyncSettings.IsAnimationEnabled());
        return new AapProtocol(
            range,
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

        if (WritesEyeBlinkControl)
        {
            writes.Add(new AapWrite(
                EyeBlinkControlName,
                Value(settings.AllowEyeBlink == TrackingPermission.Disallow || settings.AdvancedEyBlinkSettings.IsAnimationEnabled()
                    ? ControlAnimationIndex
                    : ControlTrackingIndex)));
        }

        if (WritesEyeBlinkAdvancedSelector)
        {
            writes.Add(new AapWrite(
                EyeBlinkAdvancedSelectorName,
                Value(settings.AdvancedEyBlinkSettings.IsAnimationEnabled()
                    ? AdvancedIndex(_eyeBlinkAdvancedIndices, unitId, settings.AdvancedEyBlinkSettings)
                    : AdvancedNoneIndex)));
        }

        if (WritesLipSyncControl)
        {
            writes.Add(new AapWrite(
                LipSyncControlName,
                Value(settings.AllowLipSync == TrackingPermission.Disallow
                    ? ControlAnimationIndex
                    : ControlTrackingIndex)));
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
        if (WritesEyeBlinkControl)
            parameters.TryAdd(EyeBlinkControlName, new PlanParameter(
                EyeBlinkControlName, AnimatorControllerParameterType.Float, Value(ControlTrackingIndex)));

        if (WritesEyeBlinkAdvancedSelector)
            parameters.TryAdd(EyeBlinkAdvancedSelectorName, new PlanParameter(
                EyeBlinkAdvancedSelectorName, AnimatorControllerParameterType.Float, Value(AdvancedNoneIndex)));

        if (WritesLipSyncControl)
            parameters.TryAdd(LipSyncControlName, new PlanParameter(
                LipSyncControlName, AnimatorControllerParameterType.Float, Value(ControlTrackingIndex)));

        if (WritesLipSyncAdvancedSelector)
            parameters.TryAdd(LipSyncAdvancedSelectorName, new PlanParameter(
                LipSyncAdvancedSelectorName, AnimatorControllerParameterType.Float, Value(AdvancedNoneIndex)));
    }

    public DnfCondition IndexIs(string parameterName, int index)
    {
        return AnimatorHelper.DiscreteFloatIndexCondition(parameterName, true, index, _range);
    }

    private float Value(int index)
    {
        return AnimatorHelper.DiscreteFloatIndexToValue(index, _range);
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

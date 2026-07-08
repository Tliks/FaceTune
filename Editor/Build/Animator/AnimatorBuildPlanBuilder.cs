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
        var expressionLayerBuilder = new ExpressionLayerPlanBuilder(_settings, _layerForceInactiveWhen, _aap);

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
            units.Add(new OutputUnitPlan(
                unitId,
                expressions[0].SourceTransform,
                expressionLayerBuilder.Build(unitId, expressions),
                BuildAdvancedEyeBlinkLayer(expressions),
                BuildAdvancedLipSyncLayer(expressions)));
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
        _aap = aap;
    }

    public IReadOnlyList<ExpressionLayerPlan> Build(int unitId, IReadOnlyList<ExpressionItem> expressions)
    {
        var layers = new List<ExpressionLayerPlan>();
        for (var index = 0; index < expressions.Count;)
        {
            var expression = expressions[index];
            if (expression.WriteMode == ExpressionWriteMode.Replace)
            {
                var run = new List<ExpressionItem>();
                while (index < expressions.Count && expressions[index].WriteMode == ExpressionWriteMode.Replace)
                {
                    run.Add(expressions[index]);
                    index++;
                }

                layers.Add(BuildReplaceLayer(unitId, layers.Count, run));
                continue;
            }

            layers.Add(BuildBlendLayer(unitId, layers.Count, expression));
            index++;
        }

        return layers;
    }

    private ExpressionLayerPlan BuildReplaceLayer(int unitId, int layerIndex, IReadOnlyList<ExpressionItem> expressions)
    {
        return BuildExpressionLayer(
            ExpressionLayerName(unitId, layerIndex, "Replace"),
            expressions.Select((expression, index) => (Expression: expression, EnterWhen: ReplaceStateEnterWhen(expressions, index))),
            unitId);
    }

    private ExpressionLayerPlan BuildBlendLayer(int unitId, int layerIndex, ExpressionItem expression)
    {
        return BuildExpressionLayer(
            ExpressionLayerName(unitId, layerIndex, "Blend"),
            new[] { (Expression: expression, EnterWhen: expression.RawWhen) },
            unitId);
    }

    private ExpressionLayerPlan BuildExpressionLayer(
        string name,
        IEnumerable<(ExpressionItem Expression, DnfCondition EnterWhen)> states,
        int unitId)
    {
        var statePlans = states
            .Select(state => BuildExpressionState(unitId, state.Expression, state.EnterWhen))
            .ToArray();
        return new ExpressionLayerPlan(
            name,
            DnfCondition.Any(statePlans.Select(state => state.EnterWhen)),
            _forceInactiveWhen,
            statePlans);
    }

    private ExpressionStatePlan BuildExpressionState(int unitId, ExpressionItem expression, DnfCondition enterWhen)
    {
        return new ExpressionStatePlan(
            expression.Name,
            enterWhen,
            BuildExpressionExitWhen(enterWhen),
            expression.AnimationSet,
            expression.ExpressionSettings,
            _aap.BuildWrites(unitId, expression.FacialSettings));
    }

    private DnfCondition BuildExpressionExitWhen(DnfCondition enterWhen)
    {
        var exitWhen = enterWhen.Not();
        return _lockFacialInactiveWhen == null
            ? exitWhen
            : exitWhen.And(_lockFacialInactiveWhen);
    }

    private static string ExpressionLayerName(int unitId, int layerIndex, string kind)
    {
        return $"{unitId}-{layerIndex} {kind}";
    }

    private static DnfCondition ReplaceStateEnterWhen(IReadOnlyList<ExpressionItem> expressions, int index)
    {
        var expression = expressions[index];
        var higherReplaceWhen = DnfCondition.Any(expressions
            .Skip(index + 1)
            .Select(item => item.RawWhen));
        return expression.RawWhen.Except(higherReplaceWhen);
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
                AapProtocol.LipSyncControlName,
                _aap.WritesLipSyncControl,
                _settings.DisableLipSyncParameterName);

            states.Add(new TrackingControlStatePlan("LipSync Tracking", lipSync.Tracking, null, true));
            states.Add(new TrackingControlStatePlan("LipSync Animation", lipSync.Animation, null, false));
        }

        return new TrackingControlLayerPlan(
            "Tracking Control",
            anchor,
            DnfCondition.Any(states.Select(state => state.When)),
            defaultState,
            _forceInactiveWhen,
            states);
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
            var forceDisable = DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(forceDisableParameterName, true)));
            trackingConditions.Add(forceDisable.Not());
            animationConditions.Add(forceDisable);
        }

        return (DnfCondition.All(trackingConditions), DnfCondition.Any(animationConditions));
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

    public const string EyeBlinkControlName = FaceTuneConstants.ParameterPrefix + "/Blink/Control";
    public const string LipSyncControlName = FaceTuneConstants.ParameterPrefix + "/LipSync/Control";
    private const string EyeBlinkAdvancedSelectorName = FaceTuneConstants.ParameterPrefix + "/Blink/Advanced";
    private const string LipSyncAdvancedSelectorName = FaceTuneConstants.ParameterPrefix + "/LipSync/Advanced";

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

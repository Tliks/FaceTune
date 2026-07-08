using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorBuildPlanBuilder
{
    private const string EyeBlinkControlAap = $"{FaceTuneConstants.ParameterPrefix}/Blink/Control";
    private const string LipSyncControlAap = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Control";
    private const string EyeBlinkAdvancedSelectorAap = $"{FaceTuneConstants.ParameterPrefix}/Blink/Advanced";
    private const string LipSyncAdvancedSelectorAap = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Advanced";

    private const int ControlTrackingIndex = 0;
    private const int ControlAnimationIndex = 1;
    private const int AdvancedNoneIndex = 0;

    private readonly ExpressionProgram _program;
    private readonly BuildSettings _settings;
    private readonly IAnimatorPlatformServices _platformServices;
    private readonly VirtualControllerContext _controllerContext;
    private readonly DnfCondition? _layerForceInactiveWhen;
    private readonly Dictionary<AdvancedEyeBlinkSettings, int> _eyeBlinkAdvancedIndices = new();
    private readonly Dictionary<AdvancedLipSyncSettings, int> _lipSyncAdvancedIndices = new();

    private AvatarContext AvatarContext => _settings.AvatarContext;

    public static AnimatorBuildPlan Build(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext)
    {
        return new AnimatorBuildPlanBuilder(program, settings, platformServices, controllerContext).Build();
    }

    private AnimatorBuildPlanBuilder(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext)
    {
        _program = program;
        _settings = settings;
        _platformServices = platformServices;
        _controllerContext = controllerContext;
        _layerForceInactiveWhen = CreateLayerForceInactiveWhen();
    }

    private AnimatorBuildPlan Build()
    {
        var units = BuildUnits();
        return new AnimatorBuildPlan(
            BuildInitialLayer(),
            units,
            BuildTrackingControlLayer(units),
            BuildFxControl(),
            _settings.DurationSeconds);
    }

    private InitialLayerPlan BuildInitialLayer()
    {
        var blendShapes = AvatarContext.FaceRenderer
            .GetBlendShapeWeights(AvatarContext.FaceMesh)
            .Where(shape => !_settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .ToArray();
        return new InitialLayerPlan(blendShapes);
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

            units.Add(new OutputUnitPlan(
                units.Count,
                expressions[0].SourceTransform,
                BuildExpressionLayers(expressions),
                BuildAdvancedEyeBlinkLayer(expressions),
                BuildAdvancedLipSyncLayer(expressions)));
            start = splitIndex;
        }

        return units;
    }

    private IReadOnlyList<ExpressionLayerPlan> BuildExpressionLayers(IReadOnlyList<ExpressionItem> expressions)
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

                layers.Add(BuildReplaceLayer(run));
                continue;
            }

            layers.Add(BuildBlendLayer(expression));
            index++;
        }

        return layers;
    }

    private ExpressionLayerPlan BuildReplaceLayer(IReadOnlyList<ExpressionItem> expressions)
    {
        return BuildExpressionLayer(
            ReplaceLayerName(expressions),
            expressions.Select((expression, index) => (Expression: expression, EnterWhen: ReplaceStateEnterWhen(expressions, index))));
    }

    private ExpressionLayerPlan BuildBlendLayer(ExpressionItem expression)
    {
        return BuildExpressionLayer(
            expression.Name,
            new[] { (Expression: expression, EnterWhen: expression.RawWhen) });
    }

    private ExpressionLayerPlan BuildExpressionLayer(
        string name,
        IEnumerable<(ExpressionItem Expression, DnfCondition EnterWhen)> states)
    {
        return new ExpressionLayerPlan(
            name,
            _layerForceInactiveWhen,
            states.Select(state => BuildExpressionState(state.Expression, state.EnterWhen)).ToArray());
    }

    private ExpressionStatePlan BuildExpressionState(ExpressionItem expression, DnfCondition enterWhen)
    {
        return new ExpressionStatePlan(
            expression.Name,
            enterWhen,
            BuildExpressionExitWhen(enterWhen),
            expression.AnimationSet,
            expression.ExpressionSettings,
            BuildAapWrites(expression.FacialSettings));
    }

    private static string ReplaceLayerName(IReadOnlyList<ExpressionItem> expressions)
    {
        return expressions.Count > 1
            ? $"ReplaceRun {expressions[0].Name}..{expressions[^1].Name}"
            : expressions[0].Name;
    }

    private static DnfCondition ReplaceStateEnterWhen(IReadOnlyList<ExpressionItem> expressions, int index)
    {
        var expression = expressions[index];
        var higherReplaceWhen = DnfCondition.Any(expressions
            .Skip(index + 1)
            .Select(item => item.RawWhen));
        return expression.RawWhen.Except(higherReplaceWhen);
    }

    private DnfCondition BuildExpressionExitWhen(DnfCondition enterWhen)
    {
        var exitWhen = enterWhen.Not();
        if (string.IsNullOrWhiteSpace(_settings.LockFacialParameterName)) return exitWhen;

        return exitWhen.And(ParameterBool(_settings.LockFacialParameterName, false));
    }

    private IReadOnlyList<AapWrite> BuildAapWrites(FacialSettings settings)
    {
        var writes = new List<AapWrite>();

        if (settings.AllowEyeBlink != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(
                EyeBlinkControlAap,
                VRCAAPHelper.IndexToValue(settings.AllowEyeBlink == TrackingPermission.Allow
                    ? ControlTrackingIndex
                    : ControlAnimationIndex)));
        }

        if (settings.AdvancedEyBlinkSettings.IsAnimationEnabled())
        {
            writes.Add(new AapWrite(
                EyeBlinkAdvancedSelectorAap,
                VRCAAPHelper.IndexToValue(AdvancedIndex(_eyeBlinkAdvancedIndices, settings.AdvancedEyBlinkSettings))));
        }
        else if (settings.AllowEyeBlink != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(EyeBlinkAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedNoneIndex)));
        }

        if (settings.AllowLipSync != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(
                LipSyncControlAap,
                VRCAAPHelper.IndexToValue(settings.AllowLipSync == TrackingPermission.Allow
                    ? ControlTrackingIndex
                    : ControlAnimationIndex)));
        }

        if (settings.AdvancedLipSyncSettings.IsCancelerEnabled())
        {
            writes.Add(new AapWrite(
                LipSyncAdvancedSelectorAap,
                VRCAAPHelper.IndexToValue(AdvancedIndex(_lipSyncAdvancedIndices, settings.AdvancedLipSyncSettings))));
        }
        else if (settings.AllowLipSync != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(LipSyncAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedNoneIndex)));
        }

        return writes;
    }

    private static int AdvancedIndex<TSettings>(Dictionary<TSettings, int> indices, TSettings settings)
        where TSettings : notnull
    {
        if (indices.TryGetValue(settings, out var index)) return index;
        index = indices.Count + 1;
        indices.Add(settings, index);
        return index;
    }

    private AdvancedEyeBlinkLayerPlan? BuildAdvancedEyeBlinkLayer(IReadOnlyList<ExpressionItem> expressions)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedEyBlinkSettings.IsAnimationEnabled())
            ? new AdvancedEyeBlinkLayerPlan("Advanced EyeBlink", _layerForceInactiveWhen)
            : null;
    }

    private AdvancedLipSyncLayerPlan? BuildAdvancedLipSyncLayer(IReadOnlyList<ExpressionItem> expressions)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedLipSyncSettings.IsCancelerEnabled())
            ? new AdvancedLipSyncLayerPlan("Advanced LipSync", _layerForceInactiveWhen)
            : null;
    }

    private TrackingControlLayerPlan? BuildTrackingControlLayer(IReadOnlyList<OutputUnitPlan> units)
    {
        if (_settings.SupressTrackingControl) return null;

        var controlsEyeBlink = !string.IsNullOrWhiteSpace(_settings.DisableEyeBlinkParameterName)
            || HasAapWrite(units, EyeBlinkControlAap);
        var controlsLipSync = !string.IsNullOrWhiteSpace(_settings.DisableLipSyncParameterName)
            || HasAapWrite(units, LipSyncControlAap);

        if (!controlsEyeBlink && !controlsLipSync) return null;

        var hasEyeBlinkControlAap = HasAapWrite(units, EyeBlinkControlAap);
        var hasLipSyncControlAap = HasAapWrite(units, LipSyncControlAap);

        var eyeBlinkTrackingWhen = controlsEyeBlink ? TrackingControlWhen(EyeBlinkControlAap, hasEyeBlinkControlAap, _settings.DisableEyeBlinkParameterName, true) : null;
        var eyeBlinkAnimationWhen = controlsEyeBlink ? TrackingControlWhen(EyeBlinkControlAap, hasEyeBlinkControlAap, _settings.DisableEyeBlinkParameterName, false) : null;
        var lipSyncTrackingWhen = controlsLipSync ? TrackingControlWhen(LipSyncControlAap, hasLipSyncControlAap, _settings.DisableLipSyncParameterName, true) : null;
        var lipSyncAnimationWhen = controlsLipSync ? TrackingControlWhen(LipSyncControlAap, hasLipSyncControlAap, _settings.DisableLipSyncParameterName, false) : null;

        var states = new List<TrackingControlStatePlan>();
        foreach (var eyeBlinkState in TrackingDomainStates(controlsEyeBlink, eyeBlinkTrackingWhen, eyeBlinkAnimationWhen))
        {
            foreach (var lipSyncState in TrackingDomainStates(controlsLipSync, lipSyncTrackingWhen, lipSyncAnimationWhen))
            {
                states.Add(new TrackingControlStatePlan(
                    TrackingStateName(eyeBlinkState.Tracking, lipSyncState.Tracking),
                    DnfCondition.All(new[] { eyeBlinkState.When, lipSyncState.When }.OfType<DnfCondition>()),
                    eyeBlinkState.Tracking,
                    lipSyncState.Tracking));
            }
        }

        return new TrackingControlLayerPlan("Tracking Control", _layerForceInactiveWhen, states);
    }

    private static bool HasAapWrite(IReadOnlyList<OutputUnitPlan> units, string parameterName)
    {
        return units.SelectMany(unit => unit.ExpressionLayers)
            .SelectMany(layer => layer.States)
            .SelectMany(state => state.AapWrites)
            .Any(write => write.ParameterName == parameterName);
    }

    private static IEnumerable<(bool? Tracking, DnfCondition? When)> TrackingDomainStates(
        bool enabled,
        DnfCondition? trackingWhen,
        DnfCondition? animationWhen)
    {
        if (!enabled)
        {
            yield return (null, null);
            yield break;
        }

        yield return (true, trackingWhen!);
        yield return (false, animationWhen!);
    }

    private static string TrackingStateName(bool? eyeBlinkTracking, bool? lipSyncTracking)
    {
        var eye = eyeBlinkTracking switch
        {
            true => "EyeBlinkTracking",
            false => "EyeBlinkAnimation",
            null => "EyeBlinkKeep"
        };
        var lip = lipSyncTracking switch
        {
            true => "LipSyncTracking",
            false => "LipSyncAnimation",
            null => "LipSyncKeep"
        };
        return $"{eye} {lip}";
    }

    private static DnfCondition TrackingControlWhen(
        string controlAapName,
        bool hasControlAap,
        string forceDisableParameterName,
        bool tracking)
    {
        var forceDisable = string.IsNullOrWhiteSpace(forceDisableParameterName)
            ? null
            : ParameterBool(forceDisableParameterName, true);

        var aapCondition = hasControlAap
            ? AapIndexCondition(controlAapName, tracking ? ControlTrackingIndex : ControlAnimationIndex)
            : tracking ? DnfCondition.Always : DnfCondition.Never;

        if (forceDisable == null) return aapCondition;

        return tracking
            ? aapCondition.And(forceDisable.Not())
            : aapCondition.Or(forceDisable);
    }

    private FxControlPlan? BuildFxControl()
    {
        if (!_settings.MmdPlayback.Enabled || _settings.MmdPlayback.DisableMode != MmdDisableMode.DisableFx) return null;
        if (string.IsNullOrWhiteSpace(_settings.MmdPlayback.DisableParameterName)) return null;
        return new FxControlPlan("MMD FX Control", ParameterBool(_settings.MmdPlayback.DisableParameterName, true));
    }

    private DnfCondition? CreateLayerForceInactiveWhen()
    {
        if (!_settings.MmdPlayback.Enabled || _settings.MmdPlayback.DisableMode == MmdDisableMode.DisableFx) return null;
        if (string.IsNullOrWhiteSpace(_settings.MmdPlayback.DisableParameterName)) return null;
        return ParameterBool(_settings.MmdPlayback.DisableParameterName, true);
    }

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        return DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(ParameterCondition.Bool(parameterName, value)));
    }

    private static DnfCondition AapIndexCondition(string parameterName, int index)
    {
        return DnfCondition.All(VRCAAPHelper.IndexConditions(parameterName, true, index)
            .Select(condition => DnfCondition.Single(new AnimatorConditionRule(condition, AnimatorControllerParameterType.Float))));
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

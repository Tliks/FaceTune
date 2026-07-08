using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed class AnimatorBuildPlanBuilder
{
    private readonly ExpressionProgram _program;
    private readonly BuildSettings _settings;
    private readonly IAnimatorPlatformServices _platformServices;
    private readonly VirtualControllerContext _controllerContext;
    private readonly DnfCondition? _layerForceInactiveWhen;
    private readonly AapProtocol _aap;

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
        _aap = new AapProtocol(platformServices);
    }

    private AnimatorBuildPlan Build()
    {
        var units = BuildUnits();
        return new AnimatorBuildPlan(
            BuildInitialLayer(),
            units,
            BuildTrackingControlLayer(units),
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

            var unitId = units.Count;
            units.Add(new OutputUnitPlan(
                unitId,
                expressions[0].SourceTransform,
                BuildExpressionLayers(unitId, expressions),
                BuildAdvancedEyeBlinkLayer(expressions),
                BuildAdvancedLipSyncLayer(expressions)));
            start = splitIndex;
        }

        return units;
    }

    private IReadOnlyList<ExpressionLayerPlan> BuildExpressionLayers(int unitId, IReadOnlyList<ExpressionItem> expressions)
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

                layers.Add(BuildReplaceLayer(unitId, run));
                continue;
            }

            layers.Add(BuildBlendLayer(unitId, expression));
            index++;
        }

        return layers;
    }

    private ExpressionLayerPlan BuildReplaceLayer(int unitId, IReadOnlyList<ExpressionItem> expressions)
    {
        return BuildExpressionLayer(
            ReplaceLayerName(expressions),
            expressions.Select((expression, index) => (Expression: expression, EnterWhen: ReplaceStateEnterWhen(expressions, index))),
            unitId);
    }

    private ExpressionLayerPlan BuildBlendLayer(int unitId, ExpressionItem expression)
    {
        return BuildExpressionLayer(
            expression.Name,
            new[] { (Expression: expression, EnterWhen: expression.RawWhen) },
            unitId);
    }

    private ExpressionLayerPlan BuildExpressionLayer(
        string name,
        IEnumerable<(ExpressionItem Expression, DnfCondition EnterWhen)> states,
        int unitId)
    {
        return new ExpressionLayerPlan(
            name,
            _layerForceInactiveWhen,
            states.Select(state => BuildExpressionState(unitId, state.Expression, state.EnterWhen)).ToArray());
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
            || HasAapWrite(units, _aap.EyeBlinkControlName);
        var controlsLipSync = !string.IsNullOrWhiteSpace(_settings.DisableLipSyncParameterName)
            || HasAapWrite(units, _aap.LipSyncControlName);

        if (!controlsEyeBlink && !controlsLipSync) return null;

        var hasEyeBlinkControlAap = HasAapWrite(units, _aap.EyeBlinkControlName);
        var hasLipSyncControlAap = HasAapWrite(units, _aap.LipSyncControlName);

        var eyeBlinkTrackingWhen = controlsEyeBlink ? TrackingControlWhen(_aap.EyeBlinkControlName, hasEyeBlinkControlAap, _settings.DisableEyeBlinkParameterName, true) : null;
        var eyeBlinkAnimationWhen = controlsEyeBlink ? TrackingControlWhen(_aap.EyeBlinkControlName, hasEyeBlinkControlAap, _settings.DisableEyeBlinkParameterName, false) : null;
        var lipSyncTrackingWhen = controlsLipSync ? TrackingControlWhen(_aap.LipSyncControlName, hasLipSyncControlAap, _settings.DisableLipSyncParameterName, true) : null;
        var lipSyncAnimationWhen = controlsLipSync ? TrackingControlWhen(_aap.LipSyncControlName, hasLipSyncControlAap, _settings.DisableLipSyncParameterName, false) : null;

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

    private DnfCondition TrackingControlWhen(
        string controlAapName,
        bool hasControlAap,
        string forceDisableParameterName,
        bool tracking)
    {
        var forceDisable = string.IsNullOrWhiteSpace(forceDisableParameterName)
            ? null
            : ParameterBool(forceDisableParameterName, true);

        var aapCondition = hasControlAap
            ? _aap.IndexIs(controlAapName, tracking ? AapProtocol.ControlTrackingIndex : AapProtocol.ControlAnimationIndex)
            : tracking ? DnfCondition.Always : DnfCondition.Never;

        if (forceDisable == null) return aapCondition;

        return tracking
            ? aapCondition.And(forceDisable.Not())
            : aapCondition.Or(forceDisable);
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

    private sealed class AapProtocol
    {
        public const int ControlTrackingIndex = 0;
        public const int ControlAnimationIndex = 1;
        private const int AdvancedNoneIndex = 0;

        private readonly IAnimatorPlatformServices _platformServices;
        private readonly Dictionary<(int UnitId, AdvancedEyeBlinkSettings Settings), int> _eyeBlinkAdvancedIndices = new();
        private readonly Dictionary<(int UnitId, AdvancedLipSyncSettings Settings), int> _lipSyncAdvancedIndices = new();

        public string EyeBlinkControlName { get; } = $"{FaceTuneConstants.ParameterPrefix}/Blink/Control";
        public string LipSyncControlName { get; } = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Control";
        private string EyeBlinkAdvancedSelectorName { get; } = $"{FaceTuneConstants.ParameterPrefix}/Blink/Advanced";
        private string LipSyncAdvancedSelectorName { get; } = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Advanced";

        public AapProtocol(IAnimatorPlatformServices platformServices)
        {
            _platformServices = platformServices;
        }

        public IReadOnlyList<AapWrite> BuildWrites(int unitId, FacialSettings settings)
        {
            var writes = new List<AapWrite>();

            if (settings.AllowEyeBlink != TrackingPermission.Keep)
            {
                writes.Add(new AapWrite(
                    EyeBlinkControlName,
                    Encode(settings.AllowEyeBlink == TrackingPermission.Allow ? ControlTrackingIndex : ControlAnimationIndex)));
            }

            if (settings.AdvancedEyBlinkSettings.IsAnimationEnabled())
            {
                writes.Add(new AapWrite(
                    EyeBlinkAdvancedSelectorName,
                    Encode(AdvancedIndex(_eyeBlinkAdvancedIndices, unitId, settings.AdvancedEyBlinkSettings))));
            }
            else if (settings.AllowEyeBlink != TrackingPermission.Keep)
            {
                writes.Add(new AapWrite(EyeBlinkAdvancedSelectorName, Encode(AdvancedNoneIndex)));
            }

            if (settings.AllowLipSync != TrackingPermission.Keep)
            {
                writes.Add(new AapWrite(
                    LipSyncControlName,
                    Encode(settings.AllowLipSync == TrackingPermission.Allow ? ControlTrackingIndex : ControlAnimationIndex)));
            }

            if (settings.AdvancedLipSyncSettings.IsCancelerEnabled())
            {
                writes.Add(new AapWrite(
                    LipSyncAdvancedSelectorName,
                    Encode(AdvancedIndex(_lipSyncAdvancedIndices, unitId, settings.AdvancedLipSyncSettings))));
            }
            else if (settings.AllowLipSync != TrackingPermission.Keep)
            {
                writes.Add(new AapWrite(LipSyncAdvancedSelectorName, Encode(AdvancedNoneIndex)));
            }

            return writes;
        }

        public DnfCondition IndexIs(string parameterName, int index)
        {
            return DnfCondition.All(_platformServices.AapIndexConditions(parameterName, true, index)
                .Select(condition => DnfCondition.Single(new AnimatorConditionRule(condition, AnimatorControllerParameterType.Float))));
        }

        private float Encode(int index)
        {
            if (index < 0 || index > _platformServices.MaxAapIndex)
            {
                throw new InvalidOperationException($"Too many FaceTune AAP states. Index {index} exceeds platform max {_platformServices.MaxAapIndex}.");
            }

            return _platformServices.EncodeAapIndex(index);
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
}

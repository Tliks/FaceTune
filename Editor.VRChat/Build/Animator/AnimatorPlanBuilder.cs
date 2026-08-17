using Aoyon.FaceTune.Build;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>表情のTrackingPermissionとBlink/LipSync設定をAAPパラメータへ変換する。</summary>
internal sealed class AapProtocol
{
    private const int BuiltInMode = 1;
    private const int FirstCustomMode = 2;
    private const string AapParameterPrefix = FaceTuneConstants.GeneratedParameterPrefix + "/AAP/";
    private const string EyeBlinkEnabledName = AapParameterPrefix + "Blink/Enabled";
    private const string EyeBlinkModeName = AapParameterPrefix + "Blink/Mode";
    private const string LipSyncEnabledName = AapParameterPrefix + "LipSync/Enabled";
    private const string LipSyncModeName = AapParameterPrefix + "LipSync/Mode";

    private readonly IReadOnlyList<EyeBlinkSettings> _eyeBlinkAnimations;
    private readonly IReadOnlyList<LipSyncSettings> _lipSyncCancellers;

    public bool ControlsEyeBlink { get; }
    public bool ControlsLipSync { get; }
    public DnfCondition EyeBlinkEnabledWhen => ParameterBool(EyeBlinkEnabledName, true);
    public DnfCondition BuiltInEyeBlinkModeWhen
        => EyeBlinkModeIs(BuiltInMode);
    public DnfCondition LipSyncEnabledWhen
        => ParameterBool(LipSyncEnabledName, true);
    public IReadOnlyList<(EyeBlinkSettings Settings, int Mode)> EyeBlinkAnimationModes
        => _eyeBlinkAnimations
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();
    public IReadOnlyList<(LipSyncSettings Settings, int Mode)> LipSyncCancellerModes
        => _lipSyncCancellers
            .Select((settings, index) => (settings, FirstCustomMode + index))
            .ToArray();

    private AapProtocol(IReadOnlyList<ExpressionItem> items)
    {
        var animations = new List<EyeBlinkSettings>();
        var animationKinds = new[]
        {
            EyeBlinkSettings.Kind.SimpleAnimation,
            EyeBlinkSettings.Kind.CustomAnimation
        };
        foreach (var kind in animationKinds)
        {
            var settingsForKind = items
                .Select(item => item.EyeBlink)
                .Where(settings => settings.EyeBlinkMode == kind);
            foreach (var settings in settingsForKind)
            {
                if (!animations.Contains(settings))
                {
                    animations.Add(settings);
                }
            }
        }

        _eyeBlinkAnimations = animations;
        ControlsEyeBlink = animations.Count > 0
            || items.Any(item => item.AllowEyeBlink == TrackingPermission.Disallow);

        var cancellers = new List<LipSyncSettings>();
        foreach (var settings in items
                     .Select(item => item.LipSync)
                     .Where(settings => settings.CancellerBlendShapes.Count > 0))
        {
            if (!cancellers.Contains(settings))
            {
                cancellers.Add(settings);
            }
        }
        _lipSyncCancellers = cancellers;
        ControlsLipSync = cancellers.Count > 0
            || items.Any(item => item.AllowLipSync == TrackingPermission.Disallow);
    }

    public static AapProtocol From(IReadOnlyList<ExpressionItem> items)
        => new(items);

    /// <summary>
    /// 外部VRCAnimatorTrackingControlをAAP書込へ置き換えるための書き込み列を生成する。
    /// NoChangeは置換対象外なので何も書かない。
    /// </summary>
    public static IReadOnlyList<AapWrite> BuildTrackingReplacementWrites(
        VRCAnimatorTrackingControl.TrackingType eyeTracking,
        VRCAnimatorTrackingControl.TrackingType mouthTracking)
    {
        var writes = new List<AapWrite>();
        switch (eyeTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                writes.Add(new AapWrite(EyeBlinkEnabledName, 1f));
                writes.Add(new AapWrite(EyeBlinkModeName, BuiltInMode));
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                writes.Add(new AapWrite(EyeBlinkEnabledName, 0f));
                break;
        }
        switch (mouthTracking)
        {
            case VRCAnimatorTrackingControl.TrackingType.Tracking:
                writes.Add(new AapWrite(LipSyncEnabledName, 1f));
                writes.Add(new AapWrite(LipSyncModeName, BuiltInMode));
                break;
            case VRCAnimatorTrackingControl.TrackingType.Animation:
                writes.Add(new AapWrite(LipSyncEnabledName, 0f));
                break;
        }
        return writes;
    }

    public IReadOnlyList<AapWrite> BuildWrites(ExpressionItem expression)
    {
        var writes = new List<AapWrite>();
        if (ControlsEyeBlink)
        {
            AddEnabledWrite(
                writes,
                EyeBlinkEnabledName,
                expression.AllowEyeBlink);
            writes.Add(new AapWrite(
                EyeBlinkModeName,
                Value(EyeBlinkModeFor(expression.EyeBlink))));
        }
        if (ControlsLipSync)
        {
            AddEnabledWrite(
                writes,
                LipSyncEnabledName,
                expression.AllowLipSync);
            writes.Add(new AapWrite(
                LipSyncModeName,
                Value(LipSyncModeFor(expression.LipSync))));
        }
        return writes;
    }

    private static void AddEnabledWrite(
        ICollection<AapWrite> writes,
        string parameterName,
        TrackingPermission permission)
    {
        switch (permission)
        {
            case TrackingPermission.Allow:
                writes.Add(new AapWrite(parameterName, 1f));
                break;
            case TrackingPermission.Disallow:
                writes.Add(new AapWrite(parameterName, 0f));
                break;
            case TrackingPermission.Keep:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permission));
        }
    }

    public void CollectExpressionParameters(
        Dictionary<string, PlanParameter> parameters)
    {
        CollectEyeBlinkParameters(parameters);
        CollectLipSyncParameters(parameters);
    }

    public void CollectEyeBlinkParameters(
        IDictionary<string, PlanParameter> parameters)
    {
        if (ControlsEyeBlink)
        {
            AddParameters(parameters, EyeBlinkEnabledName, EyeBlinkModeName);
        }
    }

    public void CollectLipSyncParameters(
        IDictionary<string, PlanParameter> parameters)
    {
        if (ControlsLipSync)
        {
            AddParameters(parameters, LipSyncEnabledName, LipSyncModeName);
        }
    }

    private static void AddParameters(
        IDictionary<string, PlanParameter> parameters,
        string enabledName,
        string modeName)
    {
        parameters.TryAdd(
            enabledName,
            new PlanParameter(
                enabledName,
                AnimatorControllerParameterType.Bool,
                1f));
        parameters.TryAdd(
            modeName,
            new PlanParameter(
                modeName,
                AnimatorControllerParameterType.Float,
                Value(BuiltInMode)));
    }

    public DnfCondition EyeBlinkModeIs(int mode)
        => IndexIs(EyeBlinkModeName, mode);

    public DnfCondition LipSyncModeIs(int mode)
        => IndexIs(LipSyncModeName, mode);

    private int EyeBlinkModeFor(EyeBlinkSettings settings)
    {
        if (settings.EyeBlinkMode == EyeBlinkSettings.Kind.BuiltIn)
        {
            return BuiltInMode;
        }
        for (var index = 0; index < _eyeBlinkAnimations.Count; index++)
        {
            if (_eyeBlinkAnimations[index].Equals(settings))
            {
                return FirstCustomMode + index;
            }
        }
        throw new InvalidOperationException(
            "Eye blink animation mode was not registered.");
    }

    private int LipSyncModeFor(LipSyncSettings settings)
    {
        if (settings.CancellerBlendShapes.Count == 0)
        {
            return BuiltInMode;
        }
        for (var index = 0; index < _lipSyncCancellers.Count; index++)
        {
            if (_lipSyncCancellers[index].Equals(settings))
            {
                return FirstCustomMode + index;
            }
        }
        throw new InvalidOperationException(
            "Lip sync canceller mode was not registered.");
    }

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        var condition = ParameterCondition.Bool(parameterName, value);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }

    private static DnfCondition IndexIs(string parameterName, int index)
        => AnimatorHelper.DiscreteFloatIndexCondition(parameterName, index);

    private static float Value(int index)
        => AnimatorHelper.DiscreteFloatIndexToValue(index);
}

internal sealed class AnimatorBuildPlanBuilder
{
    private const int InitialLayerPriority = -1;

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
        _aap = AapProtocol.From(plan.Items);
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
            initialLayer = BuildInitialLayer(units[0].Anchor, InitialLayerPriority);
        }

        var eyeBlinkLayer = new EyeBlinkLayerPlanBuilder(
            _aap,
            _avatarControlSettings.DisableEyeBlinkWhen)
            .Build(LayerMmdPlaybackWhen);
        var lipSyncLayer = new LipSyncLayerPlanBuilder(
            _aap,
            _avatarControlSettings.DisableLipSyncWhen)
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
            lipSyncLayer,
            _aap.ControlsEyeBlink,
            _aap.ControlsLipSync);
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
            MmdPlaybackState = layer.MmdPlaybackState == null
                ? null
                : layer.MmdPlaybackState with { When = LowerCondition(layer.MmdPlaybackState.When) },
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
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            PassThroughWhen = LowerOptionalCondition(layer.PassThroughWhen),
            MmdPlaybackWhen = LowerOptionalCondition(layer.MmdPlaybackWhen),
            States = layer.States.Select(state => state with
            {
                EnterWhen = LowerCondition(state.EnterWhen),
                ExitWhen = LowerCondition(state.ExitWhen)
            }).ToArray()
        }).ToArray();

        return unit with
        {
            ExpressionLayers = layers,
            Parameters = AddAlwaysParameterIfRequired(unit.Parameters, requiresAlwaysParameter)
        };
    }

    public EyeBlinkLayerPlan? Lower(EyeBlinkLayerPlan? layer)
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
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            DisabledWhen = LowerCondition(layer.DisabledWhen),
            BuiltInWhen = LowerCondition(layer.BuiltInWhen),
            AnimationWhen = LowerCondition(layer.AnimationWhen),
            MmdPlaybackWhen = layer.MmdPlaybackWhen == null
                ? null
                : LowerCondition(layer.MmdPlaybackWhen),
            Animations = layer.Animations.Select(animation => animation with
            {
                When = LowerCondition(animation.When)
            }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(layer.Parameters, requiresAlwaysParameter)
        };
    }

    public LipSyncLayerPlan? Lower(LipSyncLayerPlan? layer)
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
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            DisabledWhen = LowerCondition(layer.DisabledWhen),
            BuiltInWhen = LowerCondition(layer.BuiltInWhen),
            VoiceActiveWhen = LowerCondition(layer.VoiceActiveWhen),
            VoiceInactiveWhen = LowerCondition(layer.VoiceInactiveWhen),
            MmdPlaybackWhen = layer.MmdPlaybackWhen == null
                ? null
                : LowerCondition(layer.MmdPlaybackWhen),
            Cancellers = layer.Cancellers.Select(canceller => canceller with
            {
                When = LowerCondition(canceller.When)
            }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(
                layer.Parameters,
                requiresAlwaysParameter)
        };
    }

    private static IReadOnlyList<PlanParameter> AddAlwaysParameterIfRequired(
        IReadOnlyList<PlanParameter> parameters,
        bool required)
    {
        if (!required) return parameters;
        if (parameters.Any(parameter => parameter.Name == AlwaysParameterName))
        {
            throw new InvalidOperationException(
                $"Animator parameter name is reserved by FaceTune: {AlwaysParameterName}");
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

internal sealed class EyeBlinkLayerPlanBuilder
{
    private const string SpeedParameterPrefix =
        FaceTuneConstants.GeneratedParameterPrefix + "/Blink/Speed/";

    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;

    public EyeBlinkLayerPlanBuilder(
        AapProtocol aap,
        DnfCondition? disableWhen)
    {
        _aap = aap;
        _disableWhen = disableWhen;
    }

    public EyeBlinkLayerPlan? Build(DnfCondition? mmdPlaybackWhen)
    {
        if (!_aap.ControlsEyeBlink && _disableWhen == null)
        {
            return null;
        }

        var enabled = _aap.ControlsEyeBlink
            ? _aap.EyeBlinkEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null)
        {
            enabled = enabled.And(_disableWhen.Complement());
        }

        var animations = BuildAnimations(enabled);
        var parameters = new Dictionary<string, PlanParameter>();
        _aap.CollectEyeBlinkParameters(parameters);
        CollectParameters(parameters, animations, mmdPlaybackWhen);

        var builtInWhen = _aap.ControlsEyeBlink
            ? enabled.And(_aap.BuiltInEyeBlinkModeWhen)
            : enabled;
        return new EyeBlinkLayerPlan(
            "Eye Blink",
            DnfCondition.Always,
            enabled.Complement(),
            builtInWhen,
            DnfCondition.Any(animations.Select(animation => animation.When)),
            mmdPlaybackWhen,
            animations,
            parameters.Values.ToArray());
    }

    private IReadOnlyList<EyeBlinkAnimationPlan> BuildAnimations(DnfCondition enabled)
    {
        var simpleNumber = 0;
        var customNumber = 0;
        return _aap.EyeBlinkAnimationModes.Select(entry =>
        {
            var settings = entry.Settings;
            var closeShapes = new BlendShapeWeightSet(settings.SimpleBlinkBlendShapes);
            closeShapes.AddRange(settings.SimpleConflictPreventionBlendShapes);
            return new EyeBlinkAnimationPlan(
                settings.EyeBlinkMode == EyeBlinkSettings.Kind.SimpleAnimation
                    ? $"Simple {++simpleNumber}"
                    : $"Custom {++customNumber}",
                settings.EyeBlinkMode,
                enabled.And(_aap.EyeBlinkModeIs(entry.Mode)),
                settings.IntervalSeconds,
                settings.SimpleDurationsSeconds,
                closeShapes.ToArray(),
                settings.Animations.ToArray(),
                SpeedParameterPrefix + entry.Mode);
        }).ToArray();
    }

    private void CollectParameters(
        Dictionary<string, PlanParameter> parameters,
        IEnumerable<EyeBlinkAnimationPlan> animations,
        DnfCondition? mmdPlaybackWhen)
    {
        foreach (var animation in animations)
        {
            var maximumInterval = Math.Max(
                animation.IntervalSeconds.x,
                animation.IntervalSeconds.y);
            parameters.TryAdd(
                animation.SpeedParameterName,
                new PlanParameter(
                    animation.SpeedParameterName,
                    AnimatorControllerParameterType.Float,
                    1f / Math.Max(maximumInterval, 0.001f)));
        }

        if (_disableWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, _disableWhen);
        }
        if (mmdPlaybackWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, mmdPlaybackWhen);
        }
    }
}

internal sealed class LipSyncLayerPlanBuilder
{
    private const string VoiceParameterName = "Voice";
    private const float VoiceThreshold = 0.01f;

    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;

    public LipSyncLayerPlanBuilder(
        AapProtocol aap,
        DnfCondition? disableWhen)
    {
        _aap = aap;
        _disableWhen = disableWhen;
    }

    public LipSyncLayerPlan? Build(DnfCondition? mmdPlaybackWhen)
    {
        if (!_aap.ControlsLipSync && _disableWhen == null)
        {
            return null;
        }

        var enabled = _aap.ControlsLipSync
            ? _aap.LipSyncEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null)
        {
            enabled = enabled.And(_disableWhen.Complement());
        }

        var cancellers = _aap.LipSyncCancellerModes
            .Select((entry, index) => new LipSyncCancellerPlan(
                $"Canceller {index + 1}",
                enabled.And(_aap.LipSyncModeIs(entry.Mode)),
                new BlendShapeWeightSet(entry.Settings.CancellerBlendShapes).ToArray()))
            .ToArray();
        var parameters = new Dictionary<string, PlanParameter>();
        _aap.CollectLipSyncParameters(parameters);
        if (cancellers.Length > 0)
        {
            parameters.TryAdd(
                VoiceParameterName,
                new PlanParameter(
                    VoiceParameterName,
                    AnimatorControllerParameterType.Float,
                    0f));
        }
        if (_disableWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, _disableWhen);
        }
        if (mmdPlaybackWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, mmdPlaybackWhen);
        }

        var voiceActiveWhen = VoiceActiveWhen();
        return new LipSyncLayerPlan(
            "Lip Sync",
            DnfCondition.Always,
            enabled.Complement(),
            enabled,
            voiceActiveWhen,
            voiceActiveWhen.Complement(),
            mmdPlaybackWhen,
            cancellers,
            parameters.Values.ToArray());
    }

    private static DnfCondition VoiceActiveWhen()
    {
        var condition = ParameterCondition.Float(
            VoiceParameterName,
            ComparisonType.GreaterThan,
            VoiceThreshold);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }
}

using UnityEditor.Animations;
using nadena.dev.ndmf.animator;
using nadena.dev.modular_avatar.core;
using com.aoyon.facetune.platform;

namespace com.aoyon.facetune.animator;

internal class AnimatorInstaller
{
    private readonly SessionContext _sessionContext;
    private readonly VirtualControllerContext _vcc;
    private readonly VirtualAnimatorController _virtualController;
    private readonly Dictionary<string, AnimatorControllerParameter> _parameterCache;

    private readonly DefaultExpressionContext _defaultExpressionContext;
    private readonly FacialExpression _globalDefaultExpression;

    private readonly IPlatformSupport _platformSupport;

    private readonly VirtualClip _emptyClip;
    private readonly string _relativePath;

    private const string SystemName = "FaceTune";
    private readonly bool _useWriteDefaults;
    private const float TransitionDurationSeconds = 0.1f; // 変更可能にすべき？
    private static readonly Vector3 DefaultStatePosition = new Vector3(300, 0, 0);
    private const float PositionYStep = 50;
    
    private const int WDLayerPriority = -1; // 上書きを意図したものではなく、WDの問題を回避するためのレイヤー。
    private const int LayerPriority = 1; // FaceEmo: 0
    private VirtualLayer? _disableExistingControlLayer;
    private VirtualState? _defaultState;

    private const string AllowEyeBlinkAAP = "FT/AllowEyeBlinkAAP";
    private const string AllowLipSyncAAP = "FT/AllowLipSyncAAP";

    public AnimatorInstaller(SessionContext sessionContext, VirtualControllerContext vcc, VirtualAnimatorController virtualController, bool useWriteDefaults)
    {
        _sessionContext = sessionContext;
        _vcc = vcc;
        _virtualController = virtualController;
        _parameterCache = virtualController.Parameters.Values.ToDictionary(p => p.name, p => p);
        _platformSupport = platform.PlatformSupport.GetSupports(_sessionContext.Root.transform).First();
        _useWriteDefaults = useWriteDefaults;
        _defaultExpressionContext = sessionContext.DEC;
        _globalDefaultExpression = _defaultExpressionContext.GetGlobalDefaultExpression();
        _emptyClip = VirtualAnimationUtility.CreateCustomEmpty();
        _relativePath = HierarchyUtility.GetRelativePath(_sessionContext.Root, _sessionContext.FaceRenderer.gameObject)!;
    }

    public VirtualLayer DisableExistingControl()
    {
        var (defaultLayer, defaultState) = AddDefaultLayer(LayerPriority);
        defaultLayer.Name = $"{SystemName}: Disable Existing Control";
        _disableExistingControlLayer = defaultLayer;
        _defaultState = defaultState;
        return _disableExistingControlLayer;
    }

    public (VirtualLayer defaultLayer, VirtualState defaultState) AddDefaultLayer(int priority)
    {
        var defaultLayer = AddFTLayer(_virtualController, "Default", priority);
        var defaultState = AddFTState(defaultLayer, "Default", DefaultStatePosition);
        AddExpressionToState(defaultState, _globalDefaultExpression);
        // SetTracks(defaultState, _globalDefaultExpression);
        return (defaultLayer, defaultState);
    }

    private static VirtualLayer AddFTLayer(VirtualAnimatorController controller, string layerName, int priority)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = controller.AddLayer(layerPriority, $"{SystemName}: {layerName}");
        layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        return layer; 
    }

    private VirtualState AddFTState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        // Transitionを用いて上のレイヤーとブレンドする際、WD OFFの場合は空のClipのままで問題ないが、WD ONの場合はNoneである必要がある
        if (_useWriteDefaults)
        {
            state.Motion = null; 
        }
        else
        {
            state.Motion = _emptyClip;
        }
        return state;
    }

    private void AddExpressionToState(VirtualState state, Expression expression)
    {
        if (expression is AnimationExpression animationExpression)
        {
            state.Motion = _vcc.Clone(animationExpression.Clip);
        }
        else
        {
            var name = expression.Name;
            var newAnimationClip = VirtualClip.Create(name);
            var shapes = expression.GetBlendShapeSet().BlendShapes;
            newAnimationClip.SetBlendShapes(_relativePath, shapes);
            state.Motion = newAnimationClip;
        }
    }

    public void InstallPatternData(PatternData patternData)
    {
        if (patternData.Count == 0) return;

        VirtualLayer? defaultLayer = null;
        VirtualState? defaultState = null;
        if (_disableExistingControlLayer != null && _defaultState != null)
        {
            defaultLayer = _disableExistingControlLayer;
            defaultState = _defaultState;
        }
        else
        {
            (defaultLayer, defaultState) = AddDefaultLayer(WDLayerPriority);
        }
        defaultLayer.Name = $"{SystemName}: Default";

        EnsureParameterExists(AnimatorControllerParameterType.Float, AllowEyeBlinkAAP);
        EnsureParameterExists(AnimatorControllerParameterType.Float, AllowLipSyncAAP);

        SetTracks(defaultState, _globalDefaultExpression);
        AddDefaultForPattern(patternData, defaultLayer, defaultState);

        foreach (var patternGroup in patternData.GetConsecutiveTypeGroups())
        {
            var type = patternGroup.Type;
            if (type == typeof(Preset))
            {
                var presets = patternGroup.Group.Select(item => (Preset)item).ToList();
                InstallPresetGroup(presets, LayerPriority);
            }
            else if (type == typeof(SingleExpressionPattern))
            {
                var singleExpressionPatterns = patternGroup.Group.Select(item => (SingleExpressionPattern)item).ToList();
                InstallSingleExpressionPatternGroup(singleExpressionPatterns, LayerPriority);
            }
        }

        AddEyeBlinkLayer();
        AddLipSyncLayer();
    }

    private void AddDefaultForPattern(PatternData patternData, VirtualLayer defaultLayer, VirtualState defaultState)
    {
        var presets = patternData.GetAllPresets().ToList();
        if (presets.Count > 0)
        {
            var presetConditions = presets.Select(p => new[] { p.PresetCondition }).ToArray();
            var basePosition = DefaultStatePosition + new Vector3(0, 2 * PositionYStep, 0);
            var presetStates = AddExclusiveStates(defaultLayer, defaultState, presetConditions, 0f, basePosition);
            for (int i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                var presetState = presetStates[i];
                presetState.Name = preset.Name;
                AddExpressionToState(presetState, preset.DefaultExpression);
                SetTracks(presetState, preset.DefaultExpression);
            }
        }
    }

    private void InstallPresetGroup(IReadOnlyList<Preset> presets, int priority)
    {
        var maxPatterns = presets.Max(p => p.Patterns.Count); 
        if (maxPatterns == 0) return;

        VirtualLayer[] layers = new VirtualLayer[maxPatterns];
        VirtualState[] defaultStates = new VirtualState[maxPatterns];

        for (int i = 0; i < maxPatterns; i++)
        {
            bool layerCreatedForThisIndex = false;
            foreach (var preset in presets)
            {
                if (i >= preset.Patterns.Count) continue;
                var pattern = preset.Patterns[i];
                if (pattern == null || pattern.ExpressionWithConditions == null || !pattern.ExpressionWithConditions.Any()) continue;

                if (!layerCreatedForThisIndex)
                {
                    layers[i] = AddFTLayer(_virtualController, $"Preset Pattern Group {i}", priority); 
                    defaultStates[i] = AddFTState(layers[i], "Default", DefaultStatePosition); // パススルー
                    layerCreatedForThisIndex = true;
                }
                
                var basePosition = layers[i].StateMachine!.States.Last().Position + new Vector3(0, 2 * PositionYStep, 0);
                AddExpressionWithConditions(layers[i], defaultStates[i], pattern.ExpressionWithConditions, basePosition);
            }
        }
    }

    private void InstallSingleExpressionPatternGroup(IReadOnlyList<SingleExpressionPattern> singleExpressionPatterns, int priority)
    {
        for (int i = 0; i < singleExpressionPatterns.Count; i++)
        {
            var singleExpressionPattern = singleExpressionPatterns[i];
            if (singleExpressionPattern == null || singleExpressionPattern.ExpressionPattern.ExpressionWithConditions == null || 
                !singleExpressionPattern.ExpressionPattern.ExpressionWithConditions.Any()) continue;

            var layer = AddFTLayer(_virtualController, singleExpressionPattern.Name, priority);
            var defaultState = AddFTState(layer, "Default", DefaultStatePosition); // パススルー
            var basePosition = DefaultStatePosition + new Vector3(0, 2 * PositionYStep, 0);
            AddExpressionWithConditions(layer, defaultState, singleExpressionPattern.ExpressionPattern.ExpressionWithConditions, basePosition); 
        }
    }

    private void AddExpressionWithConditions(VirtualLayer layer, VirtualState defaultState, IEnumerable<ExpressionWithCondition> expressionWithConditions, Vector3 basePosition)
    {
        var expressionWithConditionList = expressionWithConditions.ToList();
        var duration = TransitionDurationSeconds;
        var conditionsPerState = expressionWithConditionList.Select(e => (IEnumerable<Condition>)e.Conditions).ToArray();
        var states = AddExclusiveStates(layer, defaultState, conditionsPerState, duration, basePosition);
        for (int i = 0; i < states.Length; i++)
        {
            var expressionWithCondition = expressionWithConditionList[i];
            var state = states[i];
            state.Name = expressionWithCondition.Expressions.First().Name;
            var expression = expressionWithCondition.GetResolvedExpression();
            AddExpressionToState(state, expression);
            SetTracks(state, expression);
        }
    }

    private VirtualState[] AddExclusiveStates(VirtualLayer layer, VirtualState defaultState, IEnumerable<Condition>[] conditionsPerState, float duration, Vector3 basePosition)
    {
        var states = new VirtualState[conditionsPerState.Length];
        var newEntryTransitions = new List<VirtualTransition>();

        var position = basePosition;

        for (int i = 0; i < conditionsPerState.Length; i++)
        {
            var conditions = conditionsPerState[i];

            var state = AddFTState(layer, "unnamed", position);
            states[i] = state;
            position.y += PositionYStep;

            var entryTransition = VirtualTransition.Create();
            entryTransition.SetDestination(state);
            entryTransition.Conditions = ToAnimatorConditions(conditions).ToImmutableList();
            newEntryTransitions.Add(entryTransition);

            var newExpressionStateTransitions = new List<VirtualStateTransition>();
            foreach (var condition in conditions)
            {
                var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
                exitTransition.SetExitDestination();
                exitTransition.Conditions = ImmutableList.Create(ToAnimatorCondition(condition.GetNegate()));
                newExpressionStateTransitions.Add(exitTransition);
            }
            state.Transitions = ImmutableList.CreateRange(state.Transitions.Concat(newExpressionStateTransitions));
        }

        // entry to expressinの全TrasntionのORをdefault to Exitに入れる
        var exitTransitionsFromDefault = new List<VirtualStateTransition>();
        foreach (var entryTr in newEntryTransitions)
        {
            var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            exitTransition.SetExitDestination();
            exitTransition.Conditions = entryTr.Conditions; // newEntryTransition の条件をそのまま使用
            exitTransitionsFromDefault.Add(exitTransition);
        }
        defaultState.Transitions = ImmutableList.CreateRange(defaultState.Transitions.Concat(exitTransitionsFromDefault));

        layer.StateMachine!.EntryTransitions = ImmutableList.CreateRange(layer.StateMachine!.EntryTransitions.Concat(newEntryTransitions));

        return states;
    }
    
    private IEnumerable<AnimatorCondition> ToAnimatorConditions(IEnumerable<Condition> conditions)
    {
        if (!conditions.Any()) return new List<AnimatorCondition>();
        var transitionConditions = new List<AnimatorCondition>();
        foreach (var cond in conditions)
        {
            var animatorCondition = ToAnimatorCondition(cond);
            transitionConditions.Add(animatorCondition);
        }
        return transitionConditions;
    }

    private AnimatorCondition ToAnimatorCondition(Condition condition)
    {
        AnimatorControllerParameterType resolvedParamType = AnimatorControllerParameterType.Float;
        string parameter = "";
        AnimatorConditionMode mode = AnimatorConditionMode.Equals;
        float threshold = 0;

        switch (condition)
        {
            case HandGestureCondition hgc:
                resolvedParamType = AnimatorControllerParameterType.Int;
                parameter = hgc.Hand == Hand.Left ? "GestureLeft" : "GestureRight";
                mode = hgc.ComparisonType == BoolComparisonType.Equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual;
                threshold = (int)hgc.HandGesture; // 整数値をそのまま使う。
                break;
            case ParameterCondition pc:
                parameter = pc.ParameterName;
                switch (pc.ParameterType)
                {
                    case ParameterType.Int:
                        resolvedParamType = AnimatorControllerParameterType.Int;
                        mode = pc.IntComparisonType switch
                        {
                            IntComparisonType.Equal => AnimatorConditionMode.Equals,
                            IntComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
                            IntComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                            IntComparisonType.LessThan => AnimatorConditionMode.Less,
                            _ => mode
                        };
                        threshold = pc.IntValue;
                        break;
                    case ParameterType.Float:
                        resolvedParamType = AnimatorControllerParameterType.Float;
                        mode = pc.FloatComparisonType switch
                        {
                            FloatComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                            FloatComparisonType.LessThan => AnimatorConditionMode.Less,
                            _ => mode
                        };
                        threshold = pc.FloatValue;
                        break;
                    case ParameterType.Bool:
                        resolvedParamType = AnimatorControllerParameterType.Bool;
                        mode = pc.BoolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                        break;
                    default:
                        throw new NotImplementedException($"Parameter type {pc.ParameterType} is not implemented");
                }
                break;
            default:
                throw new NotImplementedException($"Condition type {condition.GetType()} is not implemented");
        }

        EnsureParameterExists(resolvedParamType, parameter);
        
        return new AnimatorCondition
        {
            parameter = parameter,
            mode = mode,
            threshold = threshold
        };
    }

    private AnimatorControllerParameter EnsureParameterExists(AnimatorControllerParameterType resolvedParamType, string parameter)
    {
        if (!_parameterCache.ContainsKey(parameter))
        {
            var param = new AnimatorControllerParameter
            {
                name = parameter,
                type = resolvedParamType
            };

            switch (resolvedParamType)
            {
                case AnimatorControllerParameterType.Bool:
                    param.defaultBool = false;
                    break;
                case AnimatorControllerParameterType.Int:
                    param.defaultInt = 0;
                    break;
                case AnimatorControllerParameterType.Float:
                    param.defaultFloat = 0f;
                    break;
            }
            _virtualController.Parameters = _virtualController.Parameters.Add(parameter, param);
            _parameterCache.Add(parameter, param);
        }

        return _parameterCache[parameter];
    }

    private void SetTracks(VirtualState state, Expression expression)
    {
        bool? allowEyeBlink = null;
        bool? allowLipSync = null;
        if (expression.AllowEyeBlink != TrackingPermission.Keep)
        {
            allowEyeBlink = expression.AllowEyeBlink == TrackingPermission.Allow;
        }
        if (expression.AllowLipSync != TrackingPermission.Keep)
        {
            allowLipSync = expression.AllowLipSync == TrackingPermission.Allow;
        }
        SetTracks(state, allowEyeBlink, allowLipSync);
    }

    private void SetTracks(VirtualState state, bool? allowEyeBlink, bool? allowLipSync)
    {
        var clip = state.Motion as VirtualClip;
        if (clip == null)
        {
            clip = VirtualClip.Create(state.Name);
            state.Motion = clip;
        }
        if (clip.IsMarkerClip) throw new Exception("clip is marker clip");

        // AAP
        if (allowEyeBlink != null)
        {
            var curve = new AnimationCurve();
            var value = allowEyeBlink.Value ? 1 : 0;
            curve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), AllowEyeBlinkAAP, curve);
        }
        if (allowLipSync != null)
        {
            var curve = new AnimationCurve();
            var value = allowLipSync.Value ? 1 : 0;
            curve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), AllowLipSyncAAP, curve);
        }
    }
    
    private void AddEyeBlinkLayer()
    {
        var eyeBlinkLayer = AddFTLayer(_virtualController, "EyeBlink", LayerPriority);

        var position = DefaultStatePosition;
        var enabled = AddFTState(eyeBlinkLayer, "Enabled", position);
        position.y += 2 * PositionYStep;
        var disabled = AddFTState(eyeBlinkLayer, "Disabled", position);

        _platformSupport.SetEyeBlinkTrack(enabled, true);
        _platformSupport.SetEyeBlinkTrack(disabled, false);

        var enabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledTransition.SetDestination(enabled);
        enabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowEyeBlinkAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = ImmutableList.Create(enabledTransition);

        var disabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledTransition.SetDestination(disabled);
        disabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowEyeBlinkAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = ImmutableList.Create(disabledTransition);
    }

    private void AddLipSyncLayer()
    {
        var lipSyncLayer = AddFTLayer(_virtualController, "LipSync", LayerPriority);

        var position = DefaultStatePosition;
        var enabled = AddFTState(lipSyncLayer, "Enabled", position);
        position.y += 2 * PositionYStep;
        var disabled = AddFTState(lipSyncLayer, "Disabled", position);

        _platformSupport.SetLipSyncTrack(enabled, true);
        _platformSupport.SetLipSyncTrack(disabled, false);

        var enabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledTransition.SetDestination(enabled);
        enabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowLipSyncAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = ImmutableList.Create(enabledTransition);

        var disabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledTransition.SetDestination(disabled);
        disabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowLipSyncAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = ImmutableList.Create(disabledTransition);
    }
}
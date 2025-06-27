using UnityEditor.Animations;
using nadena.dev.ndmf.animator;
using nadena.dev.modular_avatar.core;
using com.aoyon.facetune.platform;
using com.aoyon.facetune.ndmf;

namespace com.aoyon.facetune.animator;

internal class AnimatorInstaller
{
    private readonly SessionContext _sessionContext;
    private readonly VirtualAnimatorController _virtualController;
    private readonly Dictionary<string, AnimatorControllerParameter> _parameterCache;

    private readonly IPlatformSupport _platformSupport;

    private readonly List<VirtualState> _allStates = new();
    private readonly Dictionary<Expression, VirtualClip> _expressionClipCache = new();

    private readonly bool _useWriteDefaults;
    private const float TransitionDurationSeconds = 0.1f; // 変更可能にすべき？
    private static readonly Vector3 EntryStatePosition = new Vector3(50, 120, 0);
    private static readonly Vector3 DefaultStatePosition = new Vector3(300, 0, 0);
    private const float PositionYStep = 50;
    
    private const int WDLayerPriority = -1; // 上書きを意図したものではなく、WDの問題を回避するためのレイヤー。
    private const int LayerPriority = 1; // FaceEmo: 0
    private VirtualLayer? _disableExistingControlLayer;
    private VirtualState? _defaultState;

    private const string TrueParameterName = $"{FaceTuneConsts.ParameterPrefix}/True";
    private const string AllowEyeBlinkAAP = $"{FaceTuneConsts.ParameterPrefix}/AllowEyeBlinkAAP";
    private const string AllowLipSyncAAP = $"{FaceTuneConsts.ParameterPrefix}/AllowLipSyncAAP";

    public AnimatorInstaller(SessionContext sessionContext, VirtualAnimatorController virtualController, bool useWriteDefaults)
    {
        _sessionContext = sessionContext;
        _virtualController = virtualController;
        _parameterCache = virtualController.Parameters.Values.ToDictionary(p => p.name, p => p);
        _platformSupport = platform.PlatformSupport.GetSupport(_sessionContext.Root.transform);
        _useWriteDefaults = useWriteDefaults;
    }

    private static VirtualLayer AddFTLayer(VirtualAnimatorController controller, string layerName, int priority)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = controller.AddLayer(layerPriority, $"{FaceTuneConsts.ShortName}: {layerName}");
        layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        return layer; 
    }

    private VirtualState AddFTState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        _allStates.Add(state);
        return state;
    }

    private void AssignEmptyClipIfStateIsEmpty()
    {
        var emptyClip = AnimatorHelper.CreateCustomEmpty();
        foreach (var state in _allStates)
        {
            state.Motion ??= emptyClip;
        }
    }
    
    private void AddExpressionToState(VirtualState state, Expression expression)
    {
        if (state.TryGetClip(out var clip))
        {
            var duplicate = clip.Clone();
            Impl(duplicate);
            state.Motion = duplicate;
        }
        else
        {
            if (_expressionClipCache.TryGetValue(expression, out var cachedClip))
            {
                clip = cachedClip;
                state.Motion = clip;
            }
            else
            {
                clip = state.CreateClip(state.Name);
                Impl(clip);
                _expressionClipCache[expression] = clip;
            }
        }

        void Impl(VirtualClip clip)
        {
            AddSettingsToState(state, clip, expression.ExpressionSettings);
            AddAnimationToState(clip, expression.Animations);
            SetFacialSettings(clip, expression.FacialSettings);
        }
    }

    private void AddSettingsToState(VirtualState state, VirtualClip clip, ExpressionSettings expressionSettings)
    {
        if (expressionSettings.LoopTime)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        else if (!string.IsNullOrEmpty(expressionSettings.MotionTimeParameterName))
        {
            EnsureParameterExists(AnimatorControllerParameterType.Float, expressionSettings.MotionTimeParameterName);
            state.TimeParameter = expressionSettings.MotionTimeParameterName;
        }
    }

    private void AddAnimationToState(VirtualState state, IEnumerable<GenericAnimation> animations)
    {
        var clip = state.GetOrCreateClip(state.Name);
        AddAnimationToState(clip, animations);
    }

    private void AddAnimationToState(VirtualClip clip, IEnumerable<GenericAnimation> animations)
    {
        clip.SetAnimations(animations);
    }

    private void CreateDefaultLayer(bool overrideShapes, bool overrideProperties, PatternData patternData)
    {
        VirtualLayer defaultLayer;
        VirtualState defaultState;

        var shapesLayerPriority = overrideShapes ? LayerPriority : WDLayerPriority;
        var propertiesLayerPriority = overrideProperties ? LayerPriority : WDLayerPriority;

        // ブレンドシェイプの初期化レイヤー
        var shapesLayer = AddFTLayer(_virtualController, "Default", shapesLayerPriority);
        var shapesState = AddFTState(shapesLayer, "Default", DefaultStatePosition);
        AddAnimationToState(shapesState, _sessionContext.ZeroWeightBlendShapes.ToGenericAnimations(_sessionContext.BodyPath));

        var bindings = patternData.GetAllExpressions().SelectMany(e => e.Animations).Select(a => a.CurveBinding).Distinct().ToList();

        var facialBinding = SerializableCurveBinding.FloatCurve(_sessionContext.BodyPath, typeof(SkinnedMeshRenderer), FaceTuneConsts.AnimatedBlendShapePrefix);
        var nonFacialBindings = bindings.Where(b => b != facialBinding);
        if (!nonFacialBindings.Any()) return;

        var defaultPropertiesAnimations = AnimatorHelper.GetDefaultValueAnimations(_sessionContext.Root, nonFacialBindings);
        if (!defaultPropertiesAnimations.Any()) return;

        if (shapesLayerPriority == propertiesLayerPriority)
        {
            AddAnimationToState(shapesState, defaultPropertiesAnimations);
        }
        else
        {
            var propertiesLayer = AddFTLayer(_virtualController, "Default", propertiesLayerPriority);
            var propertiesState = AddFTState(propertiesLayer, "Default", DefaultStatePosition);
            AddAnimationToState(propertiesState, defaultPropertiesAnimations);
        }

        defaultLayer = shapesLayer;
        defaultState = shapesState;
    }

    public void DisableExistingControlAndInstallPatternData(InstallData installData)
    {
        var patternData = installData.PatternData;
        if (patternData.IsEmpty) return;

        EnsureParameterExists(AnimatorControllerParameterType.Bool, TrueParameterName).defaultBool = true;

        CreateDefaultLayer(installData.OverrideBlendShapes, installData.OverrideProperties, patternData);

        EnsureParameterExists(AnimatorControllerParameterType.Float, AllowEyeBlinkAAP);
        EnsureParameterExists(AnimatorControllerParameterType.Float, AllowLipSyncAAP);

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
        
        var patternExpressions = patternData.GetAllExpressions();
        if (patternExpressions.Any(e => e.FacialSettings.AllowEyeBlink == TrackingPermission.Disallow))
        {
            AddEyeBlinkLayer();
        }
        if (patternExpressions.Any(e => e.FacialSettings.AllowLipSync == TrackingPermission.Disallow))
        {
            AddLipSyncLayer();
        }

        // Transitionを用いて上のレイヤーとブレンドする際、WD OFFの場合は空のClipのままで問題ないが、WD ONの場合はNoneである必要がある
        // そのため、WD OFFの場合のみ空のClipを入れる
        if (!_useWriteDefaults)
        {
            AssignEmptyClipIfStateIsEmpty();
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

    private void AddExpressionWithConditions(VirtualLayer layer, VirtualState defaultState, IEnumerable<ExpressionWithConditions> expressionWithConditions, Vector3 basePosition)
    {
        var trueCondition = new[] { ParameterCondition.Bool(TrueParameterName, true) };
        var expressionWithConditionList = expressionWithConditions.Select(e => 
        {
            if (!e.Conditions.Any())
            {
                e.SetConditions(e.Conditions.Concat(trueCondition).ToList());
            }
            return e;
        }).ToList();
        var duration = TransitionDurationSeconds;
        var conditionsPerState = expressionWithConditionList.Select(e => (IEnumerable<Condition>)e.Conditions).ToArray();
        var states = AddExclusiveStates(layer, defaultState, conditionsPerState, duration, basePosition);
        for (int i = 0; i < states.Length; i++)
        {
            var expressionWithCondition = expressionWithConditionList[i];
            var state = states[i];
            state.Name = expressionWithCondition.Expression.Name;
            AddExpressionToState(state, expressionWithCondition.Expression);
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
                exitTransition.Conditions = ImmutableList.Create(ToAnimatorCondition(condition.ToNegation()));
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
                mode = hgc.IsEqual ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual;
                threshold = (int)hgc.HandGesture; // 整数値をそのまま使う。
                break;
            case ParameterCondition pc:
                parameter = pc.ParameterName;
                switch (pc.ParameterType)
                {
                    case ParameterType.Int:
                        resolvedParamType = AnimatorControllerParameterType.Int;
                        mode = pc.ComparisonType switch
                        {
                            ComparisonType.Equal => AnimatorConditionMode.Equals,
                            ComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
                            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                            ComparisonType.LessThan => AnimatorConditionMode.Less,
                            _ => mode
                        };
                        threshold = pc.IntValue;
                        break;
                    case ParameterType.Float:
                        resolvedParamType = AnimatorControllerParameterType.Float;
                        switch (pc.ComparisonType)
                        {
                            case ComparisonType.GreaterThan:
                                mode = AnimatorConditionMode.Greater;
                                break;
                            case ComparisonType.LessThan:
                                mode = AnimatorConditionMode.Less;
                                break;
                            case ComparisonType.Equal:
                                Debug.LogWarning("Equal is not supported for float parameters. Using Greater instead.");
                                mode = AnimatorConditionMode.Greater;
                                break;
                            case ComparisonType.NotEqual:
                                Debug.LogWarning("NotEqual is not supported for float parameters. Using Greater instead.");
                                mode = AnimatorConditionMode.Greater;
                                break;
                            default:
                                throw new NotImplementedException($"Comparison type {pc.ComparisonType} is not implemented");
                        }
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

    private void SetFacialSettings(VirtualClip clip, FacialSettings? facialSettings)
    {
        if (facialSettings == null) return;
        bool? allowEyeBlink = null;
        bool? allowLipSync = null;
        if (facialSettings.AllowEyeBlink != TrackingPermission.Keep)
        {
            allowEyeBlink = facialSettings.AllowEyeBlink == TrackingPermission.Allow;
        }
        if (facialSettings.AllowLipSync != TrackingPermission.Keep)
        {
            allowLipSync = facialSettings.AllowLipSync == TrackingPermission.Allow;
        }
        SetFacialSettings(clip, allowEyeBlink, allowLipSync);
    }

    private void SetFacialSettings(VirtualClip clip, bool? allowEyeBlink, bool? allowLipSync)
    {
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

        // 最初のフレームでTraking Controlを変更すると直後に巻き戻されるような挙動がある。
        // そのため、0.2s程度遅延させる
        var delayState = AddFTState(eyeBlinkLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.2f);
        delayState.Motion = delayClip;

        var position = DefaultStatePosition;
        var enabled = AddFTState(eyeBlinkLayer, "Enabled", position);
        position.y += 2 * PositionYStep;
        var disabled = AddFTState(eyeBlinkLayer, "Disabled", position);

        _platformSupport.SetEyeBlinkTrack(enabled, true);
        _platformSupport.SetEyeBlinkTrack(disabled, false);

        var delayTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayTransition);

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

        var delayState = AddFTState(lipSyncLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.2f);
        delayState.Motion = delayClip;

        var position = DefaultStatePosition;
        var enabled = AddFTState(lipSyncLayer, "Enabled", position);
        position.y += 2 * PositionYStep;
        var disabled = AddFTState(lipSyncLayer, "Disabled", position);

        _platformSupport.SetLipSyncTrack(enabled, true);
        _platformSupport.SetLipSyncTrack(disabled, false);

        var delayTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayTransition);

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
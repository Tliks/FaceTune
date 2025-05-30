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
    private const string TrueParameterName = "FT_True";
    private static readonly Vector3 DefaultStatePosition = new Vector3(300, 0, 0);
    private const float PositionYStep = 50;

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

    public VirtualLayer CreateDefaultLayer(PatternData patternData, int priority)
    {
        var defaultLayer = AddFTLayer(_virtualController, "Default", priority);
        var defaultState = AddFTState(defaultLayer, "Default", DefaultStatePosition);
        AddExpressionsToState(defaultState, new[] { _globalDefaultExpression });
        // SetTracks(defaultState, _globalDefaultExpression);
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
                AddExpressionsToState(presetState, new[] { preset.DefaultExpression });
                // SetTracks(presetState, preset.DefaultExpression);
            }
        }
        return defaultLayer;
    }

    private static VirtualLayer AddFTLayer(VirtualAnimatorController controller, string layerName, int priority)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = controller.AddLayer(layerPriority, $"{SystemName}_{layerName}");
        layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        return layer; 
    }

    private VirtualState AddFTState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    private void AddExpressionsToState(VirtualState state, IEnumerable<Expression> expressions)
    {
        var facialExpressions = expressions.UnityOfType<FacialExpression>();
        var animationExpressions = expressions.UnityOfType<AnimationExpression>();
        
        if (animationExpressions.Any())
        {
            state.Motion = _vcc.Clone(animationExpressions.First().Clip);
        }
        else
        {
            var name = facialExpressions.First().Name;
            var newAnimationClip = VirtualClip.Create(name);
            var shapes = facialExpressions.SelectMany(f => f.BlendShapeSet.BlendShapes).ToList();
            newAnimationClip.SetBlendShapes(_relativePath, shapes);
            state.Motion = newAnimationClip;
        }
    }

    public void InstallPatternData(PatternData patternData, int priority)
    {
        EnsureParameterExists(new ParameterCondition(TrueParameterName, true), param => param.defaultBool = true);

        foreach (var patternGroup in patternData.GetConsecutiveTypeGroups())
        {
            var type = patternGroup.Type;

            if (type == typeof(Preset))
            {
                var presets = patternGroup.Group.Select(item => (Preset)item).ToList();
                InstallPresetGroup(presets, priority);
            }
            else if (type == typeof(SingleExpressionPattern))
            {
                var singleExpressionPatterns = patternGroup.Group.Select(item => (SingleExpressionPattern)item).ToList();
                InstallSingleExpressionPatternGroup(singleExpressionPatterns, priority);
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
                    defaultStates[i] = CreateDefaultState(layers[i], DefaultStatePosition);
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
            var defaultState = CreateDefaultState(layer, DefaultStatePosition);
            var basePosition = DefaultStatePosition + new Vector3(0, 2 * PositionYStep, 0);
            AddExpressionWithConditions(layer, defaultState, singleExpressionPattern.ExpressionPattern.ExpressionWithConditions, basePosition); 
        }
    }

    private VirtualState CreateDefaultState(VirtualLayer layer, Vector3 position)
    {
        var defaultState = AddFTState(layer, "Default", position);
        // Transitionを用いて上のレイヤーとブレンドする際、WD OFFの場合は空のClipのままで問題ないが、WD ONの場合はNoneである必要がある
        if (_useWriteDefaults)
        {
            defaultState.Motion = null; 
        }
        else
        {
            defaultState.Motion = _emptyClip;
        }
        SetTracks(defaultState, _globalDefaultExpression);
        return defaultState;
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
            var expressions = expressionWithCondition.Expressions;
            AddExpressionsToState(state, expressions);
            SetTracks(state, expressions);
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
            entryTransition.Conditions = ToAnimatorConditions(conditions, negate: false).ToImmutableList();
            newEntryTransitions.Add(entryTransition);

            var newExpressionStateTransitions = new List<VirtualStateTransition>();
            foreach (var condition in conditions)
            {
                var exitTransition = VirtualStateTransition.Create();
                exitTransition.SetExitDestination();
                exitTransition.HasFixedDuration = true;
                exitTransition.Duration = duration;
                exitTransition.ExitTime = null; 
                exitTransition.Conditions = ImmutableList.Create(ToAnimatorCondition(condition, negate: true));
                newExpressionStateTransitions.Add(exitTransition);
            }
            state.Transitions = ImmutableList.CreateRange(state.Transitions.Concat(newExpressionStateTransitions));
        }

        // entry to expressinの全TrasntionのORをdefault to Exitに入れる
        var exitTransitionsFromDefault = new List<VirtualStateTransition>();
        foreach (var entryTr in newEntryTransitions)
        {
            var exitTransition = VirtualStateTransition.Create();
            exitTransition.SetExitDestination();
            exitTransition.HasFixedDuration = true;
            exitTransition.Duration = duration;
            exitTransition.ExitTime = null;
            exitTransition.Conditions = entryTr.Conditions; // newEntryTransition の条件をそのまま使用
            exitTransitionsFromDefault.Add(exitTransition);
        }
        defaultState.Transitions = ImmutableList.CreateRange(defaultState.Transitions.Concat(exitTransitionsFromDefault));

        layer.StateMachine!.EntryTransitions = ImmutableList.CreateRange(layer.StateMachine!.EntryTransitions.Concat(newEntryTransitions));

        return states;
    }

    private AnimatorCondition ToAnimatorCondition(Condition condition, bool? negate = null)
    {
        var currentCondition = negate == true ? condition.GetNegate() : condition;
        var animatorCondition = CreateAnimatorCondition(currentCondition);
        EnsureParameterExists(currentCondition);
        return animatorCondition;
    }
    
    private List<AnimatorCondition> ToAnimatorConditions(IEnumerable<Condition> conditions, bool negate)
    {
        if (!conditions.Any()) return new List<AnimatorCondition>();

        var transitionConditions = new List<AnimatorCondition>();

        foreach (var cond in conditions)
        {
            var animatorCondition = ToAnimatorCondition(cond, negate);
            transitionConditions.Add(animatorCondition);
        }
        return transitionConditions;
    }

    private AnimatorCondition CreateAnimatorCondition(Condition condition)
    {
        switch (condition)
        {
            case HandGestureCondition hgc:
                return new AnimatorCondition
                {
                    mode = hgc.ComparisonType == BoolComparisonType.Equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual,
                    parameter = hgc.Hand == Hand.Left ? "GestureLeft" : "GestureRight",
                    threshold = (int)hgc.HandGesture
                };
            case ParameterCondition pc:
                return CreateParameterAnimatorCondition(pc);
            default:
                throw new NotImplementedException($"Condition type {condition.GetType()} is not implemented");
        }
    }

    private AnimatorCondition CreateParameterAnimatorCondition(ParameterCondition pc)
    {
        AnimatorConditionMode mode = AnimatorConditionMode.Equals;
        float threshold = 0;

        switch (pc.ParameterType)
        {
            case ParameterType.Int:
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
                mode = pc.FloatComparisonType switch
                {
                    FloatComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                    FloatComparisonType.LessThan => AnimatorConditionMode.Less,
                    _ => mode
                };
                threshold = pc.FloatValue;
                break;
            case ParameterType.Bool:
                mode = pc.BoolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                break;
        }

        if (pc.ParameterType == ParameterType.Bool)
        {
            return new AnimatorCondition { mode = mode, parameter = pc.ParameterName };
        }
        else
        {
            return new AnimatorCondition { mode = mode, parameter = pc.ParameterName, threshold = threshold };
        }
    }

    private void EnsureParameterExists(Condition condition, Action<AnimatorControllerParameter>? onParameterCreated = null)
    {
        string paramName = "";
        AnimatorControllerParameterType resolvedParamType = AnimatorControllerParameterType.Float;

        switch (condition)
        {
            case HandGestureCondition hgc:
                paramName = hgc.Hand == Hand.Left ? "GestureLeft" : "GestureRight";
                resolvedParamType = AnimatorControllerParameterType.Int;
                break;
            case ParameterCondition pc:
                paramName = pc.ParameterName;
                resolvedParamType = pc.ParameterType switch
                {
                    ParameterType.Int => AnimatorControllerParameterType.Int,
                    ParameterType.Float => AnimatorControllerParameterType.Float,
                    ParameterType.Bool => AnimatorControllerParameterType.Bool,
                    _ => resolvedParamType
                };
                break;
            default:
                return;
        }

        if (!_parameterCache.ContainsKey(paramName))
        {
            var param = new AnimatorControllerParameter
            {
                name = paramName,
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
            onParameterCreated?.Invoke(param);
            _virtualController.Parameters = _virtualController.Parameters.Add(paramName, param);
            _parameterCache.Add(paramName, param);
        }
    }

    private void SetTracks(VirtualState state, IEnumerable<Expression> expressions)
    {
        foreach (var expression in expressions) SetTracks(state, expression);
    }

    private void SetTracks(VirtualState state, Expression expression)
    {
        _platformSupport.SetTracks(state, expression);
    }
}
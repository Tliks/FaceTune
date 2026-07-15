using UnityEditor.Animations;
using Aoyon.FaceTune.Build.Animator;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Importer;

internal class AnimatorControllerImporter
{
    private readonly AvatarContext _context;
    private readonly AnimatorController _animatorController;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly ParameterDomainRegistry _parameterDomains;
    private readonly Dictionary<string, AnimatorControllerParameterType> _parameterTypes;


    public AnimatorControllerImporter(
        AvatarContext context,
        AnimatorController animatorController,
        IMetabasePlatformSupport platformSupport)
    {
        _context = context;
        _animatorController = animatorController;
        _platformSupport = platformSupport;
        _parameterDomains = _platformSupport.CreateBuiltInParameterDomains();
        _parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);
    }

    public void Import(GameObject parent)
    {
        LocalizedLog.Info("animatorControllerImporter.log.info.animatorControllerImporter.importing", _animatorController.name);
        AssetDatabase.StartAssetEditing();
        try
        {
            var expressionCount = 0;
            GameObject? firstLayerObj = null;
            foreach (var layer in _animatorController.layers)
            {
                var stateMachine = layer.stateMachine;
                if (stateMachine == null) continue;

                var stateConditions = CollectStateConditions(stateMachine);
                var expressions = CreateExpressions(stateConditions);
                var layerObj = PlaceExpressions(parent, layer.name, expressions);

                expressionCount += expressions.Count;
                firstLayerObj ??= layerObj;

                LocalizedLog.Info("animatorControllerImporter.log.info.animatorControllerImporter.layerCollected", layer.name, expressions.Count, stateConditions.Count);
            }

            Undo.RegisterCreatedObjectUndo(parent, "Import FX");
            if (firstLayerObj != null)
            {
                Selection.activeObject = firstLayerObj;
                EditorGUIUtility.PingObject(firstLayerObj);
            }

            LocalizedLog.Info("animatorControllerImporter.log.info.animatorControllerImporter.finishedImporting", _animatorController.name, expressionCount);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }
    }

    private List<(AnimatorState State, DnfCondition Condition)> CollectStateConditions(AnimatorStateMachine rootStateMachine)
    {
        var stateConditions = new Dictionary<AnimatorState, DnfCondition>();
        var anyStateConditions = new Dictionary<AnimatorState, DnfCondition>();

        Collect(rootStateMachine);

        // AnyState由来のコンポーネントをレイヤーの一番下に配置する。
        var orderedConditions = stateConditions
            .Where(pair => !anyStateConditions.ContainsKey(pair.Key))
            .Select(pair => (pair.Key, pair.Value))
            .ToList();

        foreach (var (state, anyStateCondition) in anyStateConditions)
        {
            var condition = stateConditions.GetOrAdd(state, DnfCondition.Never).Or(anyStateCondition);
            orderedConditions.Add((state, condition));
        }

        return orderedConditions;

        void Collect(AnimatorStateMachine stateMachine)
        {
            var transitionCount = 0;

            foreach (var transition in stateMachine.entryTransitions)
            {
                if (TryAddTransition(transition, stateConditions)) transitionCount++;
            }

            foreach (var stateInfo in stateMachine.states)
            {
                foreach (var transition in stateInfo.state.transitions)
                {
                    if (TryAddTransition(transition, stateConditions)) transitionCount++;
                }
            }

            foreach (var transition in stateMachine.anyStateTransitions)
            {
                if (TryAddTransition(transition, anyStateConditions)) transitionCount++;
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                Collect(subStateMachine.stateMachine);
            }

            // 条件を設定できないため、デフォルトステートの追加は他が空の場合のみ
            if (transitionCount == 0 && stateMachine.defaultState is { motion: AnimationClip } defaultState)
            {
                stateConditions.TryAdd(defaultState, DnfCondition.Always);
            }
        }

        bool TryAddTransition(AnimatorTransitionBase transition, Dictionary<AnimatorState, DnfCondition> conditionsByState)
        {
            if (transition.destinationState is not { motion: AnimationClip } state) return false;

            var condition = ToDnfCondition(transition.conditions);
            if (condition == null) return false;

            conditionsByState[state] = conditionsByState
                .GetOrAdd(state, DnfCondition.Never)
                .Or(condition);
            return true;
        }
    }

    private List<GameObject> CreateExpressions(IReadOnlyList<(AnimatorState State, DnfCondition Condition)> stateConditions)
    {
        var expressions = new List<GameObject>();
        foreach (var (state, condition) in stateConditions)
        {
            if (state.motion is not AnimationClip clip) continue;

            var facialBlendShapes = new List<BlendShapeWeightAnimation>();
            clip.GetBlendShapeAnimations(ClipImportOption.All, facialBlendShapes, _context.BodyPath);
            if (facialBlendShapes.Count == 0) continue;

            expressions.Add(CreateExpression(state, clip, condition, IsBlending(facialBlendShapes)));
        }

        return expressions;
    }

    private static GameObject? PlaceExpressions(GameObject parent, string layerName, IReadOnlyList<GameObject> expressions)
    {
        if (expressions.Count == 1)
        {
            var expression = expressions[0];
            expression.transform.parent = parent.transform;
            expression.name = layerName + "_" + expression.name;
            return null;
        }

        if (expressions.Count == 0) return null;

        var layerObj = new GameObject(layerName);
        layerObj.transform.parent = parent.transform;
        foreach (var expression in expressions)
        {
            expression.transform.parent = layerObj.transform;
        }

        return layerObj;
    }

    private DnfCondition? ToDnfCondition(IReadOnlyList<AnimatorCondition> animatorConditions)
    {
        if (animatorConditions.Count == 0) return DnfCondition.Always;

        var resolved = animatorConditions
            .Select(ConvertCondition)
            .OfType<DnfCondition>()
            .ToList();

        return resolved.Count == 0
            ? null
            : DnfCondition.All(resolved);

        DnfCondition? ConvertCondition(AnimatorCondition condition)
        {
            if (!_parameterTypes.TryGetValue(condition.parameter, out var parameterType))
            {
                LocalizedLog.Warning("AnimatorControllerImporter:Log:warning:AnimatorControllerImporter:FailedToFindParameter", condition.parameter);
                return null;
            }

            var rule = new AnimatorConditionRule(condition, parameterType);
            return _platformSupport.ResolveParameterCondition(
                rule.ToParameterCondition(),
                _parameterDomains);
        }
    }


    private bool IsBlending(List<BlendShapeWeightAnimation> facialAnimations)
    {
        var count = facialAnimations.Count;
        var zeroCount = facialAnimations.Count(a => a.IsZero);
        var nonZeroCount = count - zeroCount;

        return !(nonZeroCount > 0 && zeroCount > 5);
    }

    private GameObject CreateExpression(AnimatorState state, AnimationClip clip, DnfCondition dnf, bool isBlending)
    {
        var obj = new GameObject(state.name);

        var expression = obj.AddComponent<FaceTuneComponent>();

        expression.Data.Clip = clip;
        expression.Data.ClipOption = isBlending ? ClipImportOption.All : ClipImportOption.NonZero;

        if (!dnf.IsAlways && !dnf.IsNever)
        {
            var conditionCases = dnf.Cases
                .Select(ToConditionCase)
                .Where(c => !c.IsEmpty)
                .ToArray();
            if (conditionCases.Length > 0)
            {
                expression.ConditionEnabled = true;
                expression.Condition = new Condition(conditionCases);
            }
        }

        var timeParameter = state.timeParameterActive ? state.timeParameter : string.Empty;
        var leftGestureWeightParameter = _platformSupport.ResolveGestureWeightParameter(Hand.Left);
        var rightGestureWeightParameter = _platformSupport.ResolveGestureWeightParameter(Hand.Right);
        expression.ExpressionSettings = timeParameter switch
        {
            _ when leftGestureWeightParameter != null && timeParameter == leftGestureWeightParameter =>
                new ExpressionSettings { MultiFrameMode = MultiFrameMode.Trigger, TriggerHand = Hand.Left },
            _ when rightGestureWeightParameter != null && timeParameter == rightGestureWeightParameter =>
                new ExpressionSettings { MultiFrameMode = MultiFrameMode.Trigger, TriggerHand = Hand.Right },
            _ when clip.isLooping => new ExpressionSettings { MultiFrameMode = MultiFrameMode.Loop },
            _ when !string.IsNullOrEmpty(timeParameter) => new ExpressionSettings
            {
                MultiFrameMode = MultiFrameMode.Parameter,
                ParameterName = timeParameter
            },
            _ => new ExpressionSettings()
        };

        expression.FacialSettings = new FacialSettings()
        {
            AllowEyeBlink = TrackingPermission.Disallow,
            AllowLipSync = TrackingPermission.Allow,
            WriteMode = isBlending ? ExpressionWriteMode.Blend : ExpressionWriteMode.Replace,
            AdvancedEyBlinkSettings = AdvancedEyeBlinkSettings.Disabled()
        };

        return obj;
    }

    private ConditionCase ToConditionCase(DnfCase dnfCase)
    {
        var leftGestureParameter = _platformSupport.ResolveGestureParameter(Hand.Left);
        var rightGestureParameter = _platformSupport.ResolveGestureParameter(Hand.Right);
        var handGestureConditions = new List<HandGestureCondition>();
        var parameterConditions = new List<ParameterCondition>();

        foreach (var rule in dnfCase.Rules)
        {
            if (rule is AnimatorConditionRule acr)
            {
                if (TryConvertToHandGestureCondition(acr, out var handGesture))
                {
                    handGestureConditions.Add(handGesture);
                }
                else
                {
                    parameterConditions.Add(acr.ToParameterCondition());
                }
            }
        }

        return new ConditionCase
        {
            Conditions = handGestureConditions.Cast<ConditionBase>()
                .Concat(parameterConditions)
                .ToList()
        };

        bool TryConvertToHandGestureCondition(
            AnimatorConditionRule rule,
            [NotNullWhen(true)] out HandGestureCondition? condition)
        {
            var match = rule.ParameterName switch
            {
                var parameter when leftGestureParameter != null && parameter == leftGestureParameter => HandGestureMatch.LeftHand,
                var parameter when rightGestureParameter != null && parameter == rightGestureParameter => HandGestureMatch.RightHand,
                _ => (HandGestureMatch?)null
            };
            if (match == null)
            {
                condition = null;
                return false;
            }

            condition = new HandGestureCondition
            {
                Match = match.Value,
                HandGesture = (HandGesture)(int)rule.Condition.threshold
            };
            return true;
        }
    }

}

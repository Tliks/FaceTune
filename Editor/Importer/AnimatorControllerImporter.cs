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
            var layers = _animatorController.layers;
            GameObject? firstLayerObj = null;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var stateMachine = layer.stateMachine;
                if (stateMachine == null) continue;

                var stateConditions = new Dictionary<AnimatorState, DnfCondition>();
                CollectConditionsAndStates(stateMachine, stateConditions);

                var validExpressionsPerLayer = new List<GameObject>();
                foreach (var (state, dnf) in stateConditions)
                {
                    var clip = state.motion as AnimationClip;
                    if (clip == null) continue;

                    var facialBlendShapes = new List<BlendShapeWeightAnimation>();
                    clip.GetBlendShapeAnimations(ClipImportOption.All, facialBlendShapes, _context.BodyPath);

                    if (facialBlendShapes.Count > 0)
                    {
                        var isBlending = IsBlending(facialBlendShapes);
                        var obj = CreateExpression(state, clip, dnf, isBlending);


                        validExpressionsPerLayer.Add(obj);
                    }
                }

                var count = validExpressionsPerLayer.Count;
                expressionCount += count;

                if (count == 1)
                {
                    var obj = validExpressionsPerLayer[0];
                    obj.transform.parent = parent.transform;
                    obj.name = layer.name + "_" + obj.name;
                }
                else if (count > 1)
                {
                    var layerObj = new GameObject(layer.name);
                    firstLayerObj ??= layerObj;
                    layerObj.transform.parent = parent.transform;
                    foreach (var obj in validExpressionsPerLayer)
                    {
                        obj.transform.parent = layerObj.transform;
                    }
                }

                LocalizedLog.Info("animatorControllerImporter.log.info.animatorControllerImporter.layerCollected", layer.name, validExpressionsPerLayer.Count, stateConditions.Count);
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

    private void CollectConditionsAndStates(AnimatorStateMachine stateMachine, Dictionary<AnimatorState, DnfCondition> stateConditions)
    {
        var count = 0;

        foreach (var transition in stateMachine.entryTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                AddTransitionCondition(state, transition.conditions);
            }
        }

        foreach (var stateInfo in stateMachine.states)
        {
            foreach (var transition in stateInfo.state.transitions)
            {
                if (IsValidTransition(transition, out var state))
                {
                    AddTransitionCondition(state, transition.conditions);
                }
            }
        }

        var anyStateTransitions = new List<(AnimatorState, IReadOnlyList<AnimatorCondition>)>();
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                anyStateTransitions.Add((state, transition.conditions));
            }
        }
        if (anyStateTransitions.Count > 0)
        {
            ProcessAnyState(anyStateTransitions);
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            CollectConditionsAndStates(subStateMachine.stateMachine, stateConditions);
        }

        // 条件を設定できないため、デフォルトステートの追加は他が空の場合のみ
        if (count == 0 && stateMachine.defaultState is { } defaultState)
        {
            if (defaultState.motion is AnimationClip)
            {
                stateConditions.TryAdd(defaultState, DnfCondition.Always);
                count++;
            }
        }

        return;

        static bool IsValidTransition(AnimatorTransitionBase transition, [NotNullWhen(true)] out AnimatorState? state)
        {
            state = null;

            if (transition.destinationState is not { } destinationState)
            {
                return false;
            }

            if (destinationState.motion is not AnimationClip)
            {
                return false;
            }

            state = destinationState;
            return true;
        }

        void AddTransitionCondition(AnimatorState state, IReadOnlyList<AnimatorCondition> animatorConditions)
        {
            var dnf = ToDnfCondition(animatorConditions);
            if (dnf == null) return;

            if (stateConditions.TryGetValue(state, out var existing))
            {
                stateConditions[state] = existing.Or(dnf);
            }
            else
            {
                stateConditions[state] = dnf;
            }
            count++;
        }

        void ProcessAnyState(List<(AnimatorState, IReadOnlyList<AnimatorCondition>)> anyStateTransitions)
        {
            var convertedAnyState = anyStateTransitions
                .Select(p => (State: p.Item1, Condition: ToDnfCondition(p.Item2)))
                .Where(p => p.Condition != null)
                .Select(p => (p.State, Condition: p.Condition!))
                .ToList();

            var anyStateSuppressor = DnfCondition.Any(convertedAnyState.Select(p => p.Item2));

            foreach (var (state, dnf) in stateConditions.ToArray())
            {
                stateConditions[state] = dnf.And(anyStateSuppressor.Complement(_parameterDomains), _parameterDomains);
            }

            foreach (var (state, dnf) in convertedAnyState)
            {
                if (stateConditions.TryGetValue(state, out var existing))
                {
                    stateConditions[state] = existing.Or(dnf);
                }
                else
                {
                    stateConditions[state] = dnf;
                }
            }
        }
    }

    private DnfCondition? ToDnfCondition(IReadOnlyList<AnimatorCondition> animatorConditions)
    {
        if (animatorConditions.Count == 0) return DnfCondition.Always;

        var resolved = animatorConditions
            .Select(ToDnfCondition)
            .OfType<DnfCondition>()
            .ToList();

        return resolved.Count == 0
            ? null
            : DnfCondition.All(resolved, _parameterDomains);
    }

    private DnfCondition? ToDnfCondition(AnimatorCondition condition)
    {
        if (!_parameterTypes.TryGetValue(condition.parameter, out var parameterType))
        {
            LocalizedLog.Warning("AnimatorControllerImporter:Log:warning:AnimatorControllerImporter:FailedToFindParameter", condition.parameter);
            return null;
        }

        var rule = new AnimatorConditionRule(condition, parameterType);
        return _platformSupport.ResolveParameterCondition(rule.ToParameterCondition());
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
        expression.ExpressionSettings = timeParameter switch
        {
            "GestureLeftWeight" => new ExpressionSettings { MultiFrameMode = MultiFrameMode.Trigger, TriggerHand = Hand.Left },
            "GestureRightWeight" => new ExpressionSettings { MultiFrameMode = MultiFrameMode.Trigger, TriggerHand = Hand.Right },
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

    private static ConditionCase ToConditionCase(DnfCase dnfCase)
    {
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
    }

    private static bool TryConvertToHandGestureCondition(AnimatorConditionRule rule, out HandGestureCondition condition)
    {
        if (rule.ParameterName != "GestureLeft" && rule.ParameterName != "GestureRight")
        {
            condition = null!;
            return false;
        }

        condition = new HandGestureCondition
        {
            Match = rule.ParameterName == "GestureLeft" ? HandGestureMatch.LeftHand : HandGestureMatch.RightHand,
            HandGesture = (HandGesture)(int)rule.Condition.threshold
        };
        return true;
    }

}

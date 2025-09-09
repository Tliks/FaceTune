using UnityEditor.Animations;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Importer;

internal class AnimatorControllerImporter
{
    private readonly AvatarContext _context;
    private readonly int _allFacialBlendshapeCount;
    private readonly AnimatorController _animatorController;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly Dictionary<string, AnimatorControllerParameterType> _parameterTypes;

    public AnimatorControllerImporter(AvatarContext context, AnimatorController animatorController)
    {
        _context = context;
        _animatorController = animatorController;
        _platformSupport = MetabasePlatformSupport.GetSupportInParents(context.Root.transform);
        _parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);
        _allFacialBlendshapeCount = context.FaceRenderer.GetBlendShapes(context.FaceMesh).Length;
    }

    public void Import(GameObject root)
    {
        LocalizedLog.Info("AnimatorControllerImporter:Log:info:AnimatorControllerImporter:Importing", _animatorController.name);
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

                var stateConditions = new Dictionary<AnimatorState, List<List<Condition>>>();
                CollectConditionsAndStates(stateMachine, stateConditions);
                
                var validExpressionsPerLayer = new List<GameObject>();
                if (stateConditions.Count > 0)
                {
                    foreach (var (state, conditions) in stateConditions)
                    {
                        var clip = state.motion as AnimationClip;
                        if (clip == null) continue;

                        var facialBlendShapes = new List<BlendShapeWeightAnimation>();
                        clip.GetAllBlendShapeAnimations(facialBlendShapes, _context.BodyPath);

                        if (facialBlendShapes.Count > 0)
                        {
                            var isBlending = IsBlending(facialBlendShapes);
                            var obj = CreateConditionAndExpression(state, clip, conditions, isBlending);

                            var expressionData = obj.AddComponent<ExpressionDataComponent>();
                            expressionData.Clip = clip;
                            expressionData.ClipOption = isBlending ? ClipImportOption.All : ClipImportOption.NonZero;
                            
                            validExpressionsPerLayer.Add(obj);
                        }
                    }

                    var count = validExpressionsPerLayer.Count;
                    expressionCount += count;

                    if (count == 1)
                    {
                        var obj = validExpressionsPerLayer[0];
                        obj.transform.parent = root.transform;
                        obj.name = layer.name + "_" + obj.name;
                        // obj.AddComponent<PatternComponent>();
                    }
                    else if (count > 1)
                    {
                        var layerObj = new GameObject(layer.name);
                        firstLayerObj ??= layerObj;
                        layerObj.transform.parent = root.transform;
                        layerObj.AddComponent<PatternComponent>();
                        foreach (var obj in validExpressionsPerLayer)
                        {
                            obj.transform.parent = layerObj.transform;
                        }
                    }
                }

                LocalizedLog.Info("AnimatorControllerImporter:Log:info:AnimatorControllerImporter:LayerCollected", layer.name, validExpressionsPerLayer.Count, stateConditions.Count);
            }

            Undo.RegisterCreatedObjectUndo(root, "Import FX");
            if (firstLayerObj != null)
            {
                Selection.activeObject = firstLayerObj;
                EditorGUIUtility.PingObject(firstLayerObj);
            }

            LocalizedLog.Info("AnimatorControllerImporter:Log:info:AnimatorControllerImporter:FinishedImporting", _animatorController.name, expressionCount);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }
    }

    private void CollectConditionsAndStates(AnimatorStateMachine stateMachine, Dictionary<AnimatorState, List<List<Condition>>> conditionStateList)
    {
        var count = 0;

        foreach (var transition in stateMachine.entryTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                AddAnimatorCondition(state, transition.conditions.ToList());
            }
        }
        
        foreach (var stateInfo in stateMachine.states)
        {
            foreach (var transition in stateInfo.state.transitions)
            {
                if (IsValidTransition(transition, out var state))
                {
                    AddAnimatorCondition(state, transition.conditions.ToList());
                }
            }
        }

        var anyStateConditions = new List<(AnimatorState, List<AnimatorCondition>)>();
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                anyStateConditions.Add((state, transition.conditions.ToList()));
            }
        }
        if (anyStateConditions.Count > 0)
        {
            ProcessAnyState(anyStateConditions);
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            CollectConditionsAndStates(subStateMachine.stateMachine, conditionStateList);
        }

        // 条件を設定できないため、デフォルトステートの追加は他が空の場合のみ
        if (count == 0 && stateMachine.defaultState is { } defaultState)
        {
            if (defaultState.motion is AnimationClip)
            {
                AddCondition(defaultState, new());
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

            // Debug.Log($"Valid transition found: state '{destinationState.name}' with {transition.conditions.Length} conditions");
            state = destinationState;
            return true;
        }
        
        Condition ToCondition(AnimatorCondition condition)
        {
            var (handGestureCondition, parameterCondition) = condition.ToCondition(_parameterTypes, (parameter) => LocalizedLog.Warning("AnimatorControllerImporter:Log:warning:AnimatorControllerImporter:FailedToFindParameter", parameter));
            if (handGestureCondition != null)
            {
                return handGestureCondition;
            }
            if (parameterCondition != null)
            {
                return parameterCondition;
            }
            throw new InvalidOperationException("failed to convert condition");
        }

        void AddCondition(AnimatorState state, List<Condition> conditions)
        {
            conditionStateList.GetOrAddNew(state).Add(conditions);
            count++;
        }

        void AddAnimatorCondition(AnimatorState state, List<AnimatorCondition> conditions)
        {
            AddCondition(state, conditions.Select(ToCondition).ToList());
        }

        // AnyStateの全条件の否定をAnyState以外の各条件が満たす必要がある
        void ProcessAnyState(List<(AnimatorState, List<AnimatorCondition>)> anyStateConditionsPerState)
        {
            var convertedAnyStateConditionsPerState = anyStateConditionsPerState.Select(p => (p.Item1, p.Item2.Select(ToCondition).ToList())).ToList();
            var convertedAnyStateConditions = convertedAnyStateConditionsPerState.Select(p => p.Item2).ToList();

            foreach (var (state, orConditions) in conditionStateList)
            {
                foreach (var orCondition in orConditions)
                {
                    conditionStateList[state] = ConditionUtility.AddNegation(orCondition, convertedAnyStateConditions);
                }
            }
            foreach (var (state, orConditions) in convertedAnyStateConditionsPerState)
            {
                AddCondition(state, orConditions);
            }
        }
    }

    private bool IsBlending(List<BlendShapeWeightAnimation> facialAnimations)
    {
        var count = facialAnimations.Count;
        var zeroCount = facialAnimations.Count(a => a.IsZero);
        var nonZeroCount = count - zeroCount;

        // zeroCount < _allFacialBlendshapeCount * 0.9 && ((_allFacialBlendshapeCount - zeroCount) >= 100)

        return !(nonZeroCount > 0 && zeroCount > 5);
    }

    private GameObject CreateConditionAndExpression(AnimatorState state, AnimationClip clip, List<List<Condition>> conditions, bool isBlending)
    {
        var obj = new GameObject(state.name);

        if (conditions.Count > 0)
        {
            var orConditions = conditions;
            foreach (var animConditions in orConditions)
            {
                // ORは複数のConditionComponentで表現
                var condition = obj.AddComponent<ConditionComponent>();
                foreach (var animCondition in animConditions)
                {
                    // ANDは単一のConditionComponent内で表現
                    if (animCondition is HandGestureCondition handGestureCondition)
                    {
                        condition.HandGestureConditions.Add(handGestureCondition);
                    }
                    else if (animCondition is ParameterCondition parameterCondition)
                    {
                        condition.ParameterConditions.Add(parameterCondition);
                    }
                }
            }
        }

        var expression = obj.AddComponent<ExpressionComponent>();

        expression.ExpressionSettings = new ExpressionSettings()
        {
            LoopTime = clip.isLooping,
            MotionTimeParameterName = state.timeParameterActive && !string.IsNullOrEmpty(state.timeParameter) ? state.timeParameter : string.Empty
        };

        var (eye, mouth) = _platformSupport.GetTrackingPermission(state) ?? (TrackingPermission.Disallow, TrackingPermission.Allow);
        expression.FacialSettings = new FacialSettings()
        {
            AllowEyeBlink = eye,
            AllowLipSync = mouth,
            EnableBlending = isBlending,
            AdvancedEyBlinkSettings = AdvancedEyeBlinkSettings.Disabled()
        };

        return obj;
    }
}
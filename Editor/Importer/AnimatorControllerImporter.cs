using UnityEditor.Animations;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Importer;

internal class AnimatorControllerImporter
{
    private readonly SessionContext _context;
    private readonly int _allFacialBlendshapeCount;
    private readonly AnimatorController _animatorController;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly Dictionary<string, AnimatorControllerParameterType> _parameterTypes;

    public AnimatorControllerImporter(SessionContext context, AnimatorController animatorController)
    {
        _context = context;
        _animatorController = animatorController;
        _platformSupport = MetabasePlatformSupport.GetSupportInParents(context.Root.transform);
        _parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);
        _allFacialBlendshapeCount = context.FaceRenderer.GetBlendShapes(context.FaceMesh).Count();
    }

    public void Import(GameObject root)
    {
        Debug.Log($"Importing {_animatorController.name}");
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

                var conditionStateList = new List<(AnimatorCondition[] conditions, AnimatorState state)>();
                CollectConditionsAndStates(stateMachine, conditionStateList);
                Debug.Log($"Layer {i} collected: {conditionStateList.Count} condition-state pairs");
                
                if (conditionStateList.Count > 0)
                {
                    var layerObj = new GameObject(layer.name);
                    firstLayerObj ??= layerObj;
                    layerObj.transform.parent = root.transform;
                    layerObj.AddComponent<PatternComponent>();

                    var validExpressionPerLayer = 0;

                    foreach (var (conditions, state) in conditionStateList)
                    {
                        var clip = state.motion as AnimationClip;
                        if (clip == null) continue;

                        var facialBlendShapes = new List<BlendShapeWeightAnimation>();
                        clip.GetAllBlendShapeAnimations(facialBlendShapes, _context.BodyPath);

                        if (facialBlendShapes.Any())
                        {
                            var isBlending = IsBlending(facialBlendShapes);
                            var obj = CreateConditionAndExpression(state, conditions, isBlending);
                            obj.transform.parent = layerObj.transform;

                            var expressionData = obj.AddComponent<ExpressionDataComponent>();
                            expressionData.Clip = clip;
                            expressionData.ClipOption = isBlending ? ClipImportOption.All : ClipImportOption.NonZero;
                            
                            validExpressionPerLayer++;
                            expressionCount++;
                        }
                    }

                    if (validExpressionPerLayer == 0)
                    {
                        Object.DestroyImmediate(layerObj);
                    }
                }
            }

            Undo.RegisterCreatedObjectUndo(root, "Import FX");
            if (firstLayerObj != null)
            {
                Selection.activeObject = firstLayerObj;
                EditorGUIUtility.PingObject(firstLayerObj);
            }

            Debug.Log($"Finished to import {_animatorController.name}: {expressionCount} expressions");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }
    }

    private static void CollectConditionsAndStates(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] conditions, AnimatorState state)> conditionStateList)
    {
        if (stateMachine.defaultState is { } defaultState)
        {
            if (defaultState.motion is AnimationClip)
            {
                conditionStateList.Add((new AnimatorCondition[0], defaultState));
            }
        }
        
        foreach (var transition in stateMachine.entryTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                conditionStateList.Add((transition.conditions, state));
            }
        }
        
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                conditionStateList.Add((transition.conditions, state));
            }
        }

        foreach (var stateInfo in stateMachine.states)
        {
            foreach (var transition in stateInfo.state.transitions)
            {
                if (IsValidTransition(transition, out var state))
                {
                    conditionStateList.Add((transition.conditions, state));
                }
            }
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            CollectConditionsAndStates(subStateMachine.stateMachine, conditionStateList);
        }

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
    }

    private bool IsBlending(List<BlendShapeWeightAnimation> facialAnimations)
    {
        var count = facialAnimations.Count;
        var zeroCount = facialAnimations.Count(a => a.IsZero);
        var nonZeroCount = count - zeroCount;

        // zeroCount < _allFacialBlendshapeCount * 0.9 && ((_allFacialBlendshapeCount - zeroCount) >= 100)

        return !(nonZeroCount > 0 && zeroCount > 5);
    }

    private GameObject CreateConditionAndExpression(AnimatorState state, AnimatorCondition[] conditions, bool isBlending)
    {
        var obj = new GameObject(state.name);

        if (conditions.Length > 0)
        {
            var condition = obj.AddComponent<ConditionComponent>();
            foreach (var animCondition in conditions)
            {
                var (handGestureCondition, parameterCondition) = animCondition.ToCondition(_parameterTypes, (parameter) => Debug.LogWarning($"failed to find parameter: {parameter}"));
                if (handGestureCondition != null)
                {
                    condition.HandGestureConditions.Add(handGestureCondition);
                }
                if (parameterCondition != null)
                {
                    condition.ParameterConditions.Add(parameterCondition);
                }
            }
        }

        var expression = obj.AddComponent<ExpressionComponent>();
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
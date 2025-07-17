using UnityEditor.Animations;
using VRC.SDKBase;

namespace aoyon.facetune.importer;

internal static class FXImporter
{
    public static GameObject? ImportFromVRChatFX(AnimatorController animatorController)
    {
        var layers = animatorController.layers;
        var parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);
        
        var rootObj = new GameObject($"FT_{animatorController.name}");
        var totalExpressionCount = 0;
        var validLayerCount = 0;

        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            var stateMachine = layer.stateMachine;
            if (stateMachine == null)
            {
                Debug.Log($"Layer {i} has no state machine");
                continue;
            }

            Debug.Log($"Layer {i} - Entry transitions: {stateMachine.entryTransitions.Length}");
            Debug.Log($"Layer {i} - Any state transitions: {stateMachine.anyStateTransitions.Length}");
            Debug.Log($"Layer {i} - States: {stateMachine.states.Length}");
            Debug.Log($"Layer {i} - Sub state machines: {stateMachine.stateMachines.Length}");

            // 条件とステートのペアを収集
            var conditionStateList = new List<(AnimatorCondition[] conditions, AnimatorState state)>();
            CollectConditionsAndStates(stateMachine, conditionStateList);

            Debug.Log($"Layer {i} collected: {conditionStateList.Count} condition-state pairs");
            
            if (conditionStateList.Count > 0)
            {
                // レイヤーオブジェクトを作成
                var layerObj = new GameObject(layer.name);
                layerObj.transform.parent = rootObj.transform;
                layerObj.AddComponent<PatternComponent>();

                // GameObjectを生成
                foreach (var (conditions, state) in conditionStateList)
                {
                    CreateExpressionGameObject(layerObj, conditions, state, parameterTypes);
                    totalExpressionCount++;
                }

                validLayerCount++;
            }
            else
            {
                Debug.LogWarning($"Layer {i} ({layer.name}) has no valid expressions");
            }
        }

        Debug.Log($"Final result: {validLayerCount} layers with {totalExpressionCount} expressions");
        if (totalExpressionCount == 0)
        {
            Debug.LogWarning("failed to find any expression");
            UnityEngine.Object.DestroyImmediate(rootObj);
            return null;
        }

        Undo.RegisterCreatedObjectUndo(rootObj, "Import FX");

        Debug.Log($"finished to import {totalExpressionCount} expressions");
        return rootObj;
    }

    private static void CollectConditionsAndStates(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] conditions, AnimatorState state)> conditionStateList)
    {
        if (stateMachine.defaultState is { } defaultState)
        {
            if (defaultState.motion is AnimationClip)
            {
                Debug.Log($"Valid default state found: state '{defaultState.name}'");
                conditionStateList.Add((new AnimatorCondition[0], defaultState));
            }
            else
            {
                Debug.Log($"Default state: motion is not AnimationClip. State: {defaultState.name}, Motion={defaultState.motion?.name}, Type={defaultState.motion?.GetType()}");
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
                Debug.Log($"Transition: destinationState is null (exit transition) - skipping. Transition: {transition.name}");
                return false;
            }
                
            if (destinationState.motion is not AnimationClip)
            {
                Debug.Log($"Transition: motion is not AnimationClip. State: {destinationState.name}, Motion={destinationState.motion?.name}, Type={destinationState.motion?.GetType()}");
                return false;
            }

            Debug.Log($"Valid transition found: state '{destinationState.name}' with {transition.conditions.Length} conditions");
            state = destinationState;
            return true;
        }
    }

    private static void CreateExpressionGameObject(GameObject layerObj, AnimatorCondition[] conditions, AnimatorState state, Dictionary<string, AnimatorControllerParameterType> parameterTypes)
    {
        var expObj = new GameObject(state.name);
        expObj.transform.parent = layerObj.transform;
        
        // 条件を設定
        if (conditions.Length > 0)
        {
            var condition = expObj.AddComponent<ConditionComponent>();
            ProcessConditions(conditions, condition, parameterTypes);
        }

        // ExpressionComponentを追加
        var exp = expObj.AddComponent<ExpressionComponent>();
        SetFacialSettings(exp, state);

        // 表情データコンポーネントを追加
        var facialData = expObj.AddComponent<FacialDataComponent>();
        facialData.SourceMode = AnimationSourceMode.AnimationClip;
        facialData.Clip = state.motion as AnimationClip;
    }

    private static void ProcessConditions(AnimatorCondition[] conditions, ConditionComponent condition, Dictionary<string, AnimatorControllerParameterType> parameterTypes)
    {
        foreach (var animCondition in conditions)
        {
            var (handGestureCondition, parameterCondition) = animCondition.ToCondition(parameterTypes, (parameter) => Debug.LogWarning($"パラメータ '{parameter}' が見つかりません"));
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

    private static void SetFacialSettings(ExpressionComponent expressionComponent, AnimatorState state)
    {
        TrackingPermission eye;
        TrackingPermission mouth;

        var trackingControl = GetVRCAnimatorTrackingControl(state);
        if (trackingControl != null)
        {
            (eye, mouth) = GetTrackingPermission(trackingControl);
        }
        else
        {
            eye = TrackingPermission.Disallow;
            mouth = TrackingPermission.Allow;
        }

        var facialSettings = new FacialSettings()
        {
            AllowEyeBlink = eye,
            AllowLipSync = mouth,
            EnableBlending = false,// Todo
            AdvancedEyBlinkSettings = AdvancedEyeBlinkSettings.Disabled()
        };

        expressionComponent.FacialSettings = facialSettings;

        return;

        static VRC_AnimatorTrackingControl? GetVRCAnimatorTrackingControl(AnimatorState state)
        {
            if (state.behaviours == null) return null;
            foreach (var behaviour in state.behaviours)
            {
                if (behaviour is VRC_AnimatorTrackingControl trackingControl)
                {
                    return trackingControl;
                }
            }
            return null;
        }

        static (TrackingPermission eye, TrackingPermission mouth) GetTrackingPermission(VRC_AnimatorTrackingControl trackingControl)
        {
            var eye = trackingControl.trackingEyes switch
            {
                VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };
            
            var mouth = trackingControl.trackingMouth switch
            {
                VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };

            return (eye, mouth);
        }
    }
}
using UnityEditor.Animations;
using VRC.SDKBase;

namespace aoyon.facetune.importer;

internal static class FXImporter
{
    public static GameObject? ImportFromVRChatFX(AnimatorController animatorController)
    {
        var layers = animatorController.layers;
        var expressionsByLayer = new Dictionary<int, List<(AnimatorCondition[] Conditions, AnimatorState State)>>();
        
        var parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);

        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            Debug.Log($"Processing layer {i}: {layer.name}");
            var stateMachine = layer.stateMachine;
            if (stateMachine == null)
            {
                Debug.Log($"Layer {i} has no state machine");
                continue;
            }

            var layerExpressions = new List<(AnimatorCondition[] Conditions, AnimatorState State)>();

            // StateMachine全体を処理
            ProcessStateMachineTransitions(stateMachine, layerExpressions);

            Debug.Log($"Layer {i} processed: {layerExpressions.Count} expressions found");
            if (layerExpressions.Count > 0)
            {
                expressionsByLayer[i] = layerExpressions;
            }
        }

        Debug.Log($"Final result: {expressionsByLayer.Count} layers with expressions");
        if (expressionsByLayer.Count == 0)
        {
            Debug.LogWarning("failed to find any expression");
            return null;
        }

        var rootObj = new GameObject($"FT_{animatorController.name}");

        var totalExpressionCount = 0;

        foreach (var (layerIndex, expressions) in expressionsByLayer.OrderBy(x => x.Key))
        {
            var layerName = layers[layerIndex].name;
            
            var layerObj = new GameObject(layerName);
            layerObj.transform.parent = rootObj.transform;
            layerObj.AddComponent<PatternComponent>();

            // 各表情を処理
            foreach (var (conditions, state) in expressions)
            {
                // ExpressionComponentを作成（condition/expression/dataすべて同一GameObject）
                var expObj = new GameObject(state.name);
                expObj.transform.parent = layerObj.transform;
                
                // 単一のConditionComponentに全条件を設定（AND条件
                if (conditions.Length > 0)
                {
                    var condition = expObj.AddComponent<ConditionComponent>();
                    ProcessConditions(conditions, condition, parameterTypes);
                }

                // ExpressionComponentを追加
                var exp = expObj.AddComponent<ExpressionComponent>();
                SetFacialSettings(exp, state);

                // 表情データコンポーネントを追加してアニメーションクリップを設定
                var facialData = expObj.AddComponent<FacialDataComponent>();
                facialData.SourceMode = AnimationSourceMode.FromAnimationClip;
                facialData.Clip = state.motion as AnimationClip;

                totalExpressionCount++;
            }
        }

        Debug.Log($"finished to import {totalExpressionCount} expressions");
        return rootObj;
    }

    private static void ProcessStateMachineTransitions(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
    {
        foreach (var transition in stateMachine.entryTransitions)
        {
            ProcessTransition(transition, layerExpressions);
        }
        
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            ProcessTransition(transition, layerExpressions);
        }

        foreach (var state in stateMachine.states)
        {
            foreach (var transition in state.state.transitions)
            {
                ProcessTransition(transition, layerExpressions);
            }
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            ProcessStateMachineTransitions(subStateMachine.stateMachine, layerExpressions);
        }

        return;

        static void ProcessTransition(AnimatorTransitionBase transition, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
        {
            if (transition.destinationState is not { } state)
            {
                Debug.Log("ProcessTransition: destinationState is null (exit transition) - skipping");
                return;
            }
                
            if (state.motion is not AnimationClip clip)
            {
                Debug.Log($"ProcessTransition: motion is not AnimationClip. Motion={state.motion?.name}, Type={state.motion?.GetType()}");
                return;
            }

            layerExpressions.Add((transition.conditions, state));

            return;
        }
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
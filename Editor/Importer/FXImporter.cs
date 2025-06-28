using UnityEditor.Animations;
using VRC.SDKBase;

namespace com.aoyon.facetune;

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
            var stateMachine = layer.stateMachine;
            if (stateMachine == null) continue;

            var layerExpressions = new List<(AnimatorCondition[] Conditions, AnimatorState State)>();

            // StateMachine全体を処理（anyState、通常のstate、sub state machinesを含む）
            ProcessStateMachineTransitions(stateMachine, layerExpressions);

            if (layerExpressions.Count > 0)
            {
                expressionsByLayer[i] = layerExpressions;
            }
        }

        if (expressionsByLayer.Count == 0)
        {
            Debug.LogWarning("インポート可能なジェスチャーアニメーションが見つかりませんでした");
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
                
                // 単一のConditionComponentに全条件を設定（AND条件）
                var condition = expObj.AddComponent<ConditionComponent>();
                ProcessConditions(conditions, condition, parameterTypes);

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

        Debug.Log($"FaceTuneパターンのインポートが完了しました: {expressionsByLayer.Count}レイヤー、{totalExpressionCount}個の表情を作成");
        return rootObj;
    }

    private static void ProcessStateMachineTransitions(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
    {
        // anyStateTransitions を処理
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            ProcessTransition(transition, layerExpressions);
        }

        // 通常のstate transitionsを処理
        foreach (var state in stateMachine.states)
        {
            foreach (var transition in state.state.transitions)
            {
                ProcessTransition(transition, layerExpressions);
            }
        }

        // sub state machinesも再帰的に処理
        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            ProcessStateMachineTransitions(subStateMachine.stateMachine, layerExpressions);
        }

        return;

        static void ProcessTransition(AnimatorStateTransition transition, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
        {
            if (transition.destinationState is not { } state || state.motion is not AnimationClip clip)
                return;

            if (transition.conditions.Length == 0) return;

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
            AdvancedEyBlinkSettings = AdvancedEyBlinkSettings.Disabled()
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
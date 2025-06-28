using UnityEditor.Animations;

namespace com.aoyon.facetune;

internal static class FXImporter
{

    public static GameObject? ImportFromVRChatFX(AnimatorController animatorController)
    {
        var layers = animatorController.layers;
        var expressionsByLayer = new Dictionary<int, List<(AnimatorCondition[] Conditions, AnimatorState State)>>();
        
        // パラメータの型情報を取得
        var parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);

        // レイヤーごとに表情を収集
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
            return default;
        }

        // ルートオブジェクトを作成
        var rootObj = new GameObject($"FT_{animatorController.name}");

        var totalExpressionCount = 0;

        // レイヤーごとにPatternComponentを作成（排他制御の単位）
        foreach (var (layerIndex, expressions) in expressionsByLayer.OrderBy(x => x.Key))
        {
            var layerName = layers[layerIndex].name;
            
            // レイヤーごとのPatternComponentを作成
            var layerObj = new GameObject(layerName);
            layerObj.transform.parent = rootObj.transform;
            layerObj.AddComponent<PatternComponent>();

            // 各表情をConditionComponentとして作成（同じレイヤー内で排他制御）
            foreach (var (conditions, state) in expressions)
            {
                // ExpressionComponentを作成（condition/expression/dataすべて同一GameObject）
                var expObj = new GameObject(state.name);
                expObj.transform.parent = layerObj.transform;
                
                // 単一のConditionComponentに全条件を設定（AND条件）
                var condition = expObj.AddComponent<ConditionComponent>();
                var handGestureConditions = new List<HandGestureCondition>();
                var parameterConditions = new List<ParameterCondition>();
                
                foreach (var animCondition in conditions)
                {
                    if (animCondition.parameter is "GestureLeft" or "GestureRight" && animCondition.threshold is >= 0 and < 8)
                    {
                        // HandGestureConditionを作成
                        var hand = animCondition.parameter == "GestureLeft" ? Hand.Left : Hand.Right;
                        var gesture = (HandGesture)(int)animCondition.threshold;
                        handGestureConditions.Add(new HandGestureCondition(hand, true, gesture));
                    }
                    else
                    {
                        // パラメータの型をcontrollerから取得
                        if (!parameterTypes.TryGetValue(animCondition.parameter, out var parameterType))
                        {
                            Debug.LogWarning($"パラメータ '{animCondition.parameter}' が見つかりません");
                            continue;
                        }

                        // ParameterConditionを作成
                        ParameterCondition paramCondition;
                        
                        switch (parameterType)
                        {
                            case AnimatorControllerParameterType.Bool:
                                bool boolValue = animCondition.mode == AnimatorConditionMode.If;
                                paramCondition = ParameterCondition.Bool(animCondition.parameter, boolValue);
                                break;
                                
                            case AnimatorControllerParameterType.Float:
                                ComparisonType floatComparison = animCondition.mode switch
                                {
                                    AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
                                    AnimatorConditionMode.Less => ComparisonType.LessThan,
                                    _ => ComparisonType.GreaterThan // デフォルト
                                };
                                paramCondition = ParameterCondition.Float(animCondition.parameter, floatComparison, animCondition.threshold);
                                break;
                                
                            case AnimatorControllerParameterType.Int:
                                ComparisonType intComparison = animCondition.mode switch
                                {
                                    AnimatorConditionMode.Equals => ComparisonType.Equal,
                                    AnimatorConditionMode.NotEqual => ComparisonType.NotEqual,
                                    AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
                                    AnimatorConditionMode.Less => ComparisonType.LessThan,
                                    _ => ComparisonType.Equal // デフォルト
                                };
                                paramCondition = ParameterCondition.Int(animCondition.parameter, intComparison, (int)animCondition.threshold);
                                break;
                                
                            case AnimatorControllerParameterType.Trigger:
                                // Triggerの場合はBoolとして扱う
                                paramCondition = ParameterCondition.Bool(animCondition.parameter, true);
                                break;
                                
                            default:
                                Debug.LogWarning($"サポートされていないパラメータ型: {parameterType}");
                                continue;
                        }
                        
                        parameterConditions.Add(paramCondition);
                    }
                }
                
                // ConditionComponentに条件を設定
                condition.HandGestureConditions = handGestureConditions;
                condition.ParameterConditions = parameterConditions;
                
                // ExpressionComponentを追加
                var exp = expObj.AddComponent<ExpressionComponent>();

                // VRC_AnimatorTrackingControlからまばたきとリップシンクの設定を取得
                var trackingControl = GetVRCAnimatorTrackingControl(state);
                if (trackingControl != null)
                {
                    // FacialSettingsを設定（リフレクションを使用）
                    SetFacialSettings(exp, trackingControl);
                }

                // 表情データコンポーネントを追加してアニメーションクリップを設定
                var facialData = expObj.AddComponent<FacialDataComponent>();
                facialData.SourceMode = AnimationSourceMode.FromAnimationClip;
                facialData.Clip = state.motion as AnimationClip;

                totalExpressionCount++;
            }
        }

        Debug.Log($"FaceTuneパターンのインポートが完了しました: {expressionsByLayer.Count}レイヤー、{totalExpressionCount}個の表情を作成");
        return rootObj;

        static void ProcessTransition(AnimatorStateTransition transition, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
        {
            if (transition.destinationState is not { } state || state.motion is not AnimationClip clip)
                return;

            if (transition.conditions.Length == 0) return;

            layerExpressions.Add((transition.conditions, state));
        }

        static void ProcessStateMachineTransitions(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] Conditions, AnimatorState State)> layerExpressions)
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
        }

        static VRC.SDKBase.VRC_AnimatorTrackingControl? GetVRCAnimatorTrackingControl(AnimatorState state)
        {
            if (state.behaviours == null) return null;
            
            foreach (var behaviour in state.behaviours)
            {
                if (behaviour is VRC.SDKBase.VRC_AnimatorTrackingControl trackingControl)
                {
                    return trackingControl;
                }
            }
            return null;
        }

        static void SetFacialSettings(ExpressionComponent expressionComponent, VRC.SDKBase.VRC_AnimatorTrackingControl trackingControl)
        {
            var allowEyeBlink = trackingControl.trackingEyes switch
            {
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };
            
            var allowLipSync = trackingControl.trackingMouth switch
            {
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };

            var newFacialSettings = new FacialSettings()
            {
                AllowEyeBlink = allowEyeBlink,
                AllowLipSync = allowLipSync,
                EnableBlending = false,// Todo
                AdvancedEyBlinkSettings = AdvancedEyBlinkSettings.Disabled()
            };

            expressionComponent.FacialSettings = newFacialSettings;
        }
    }
}
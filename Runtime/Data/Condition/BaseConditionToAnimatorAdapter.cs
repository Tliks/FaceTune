#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace Aoyon.FaceTune;

internal static class BaseConditionAdapter
{
    // Todo
    private const string VRCLeftHandGesture = "GestureLeft";
    private const string VRCRightHandGesture = "GestureRight";
    public static (HandGestureCondition? handGestureCondition, ParameterCondition? parameterCondition) ToSerializableCondition(this IBaseCondition condition)
    {
        switch (condition)
        {
            case FloatCondition fc:
                return (null, ParameterCondition.Float(fc.ParameterName, fc.ComparisonType, fc.Value));
            case IntCondition ic:
                var comparisonType = ic.ComparisonType switch
                {
                    ComparisonType.Equal => EqualityComparison.Equal,
                    ComparisonType.NotEqual => EqualityComparison.NotEqual,
                    _ => throw new InvalidOperationException($"Comparison type {ic.ComparisonType} is not supported for hand gesture parameters")
                };
                if (ic.ParameterName == VRCLeftHandGesture)
                {
                    return (new HandGestureCondition(Hand.Left, comparisonType, (HandGesture)ic.Value), null);
                }
                else if (ic.ParameterName == VRCRightHandGesture)
                {
                    return (new HandGestureCondition(Hand.Right, comparisonType, (HandGesture)ic.Value), null);
                }
                return (null, ParameterCondition.Int(ic.ParameterName, ic.ComparisonType, ic.Value));
            case BoolCondition bc:
                return (null, ParameterCondition.Bool(bc.ParameterName, bc.Value));                
            case TrueCondition:
            case FalseCondition:
                Debug.LogWarning("FalseCondition is not supported");
                return (null, null);
            default:
                throw new NotImplementedException($"Condition type {condition.GetType()} is not implemented");
        }
    }

#if UNITY_EDITOR
    public static IBaseCondition? ToBaseCondition(this AnimatorCondition animCondition, Dictionary<string, AnimatorControllerParameterType> parameterTypes, Action<string> onParameterTypeNotFound)
    {
        // パラメータの型をcontrollerから取得
        if (!parameterTypes.TryGetValue(animCondition.parameter, out var parameterType))
        {
            onParameterTypeNotFound(animCondition.parameter);
            return null;
        }

        // IBaseConditionを作成
        switch (parameterType)
        {
            case AnimatorControllerParameterType.Bool:
                bool boolValue = animCondition.mode == AnimatorConditionMode.If;
                return new BoolCondition(animCondition.parameter, boolValue);
                
            case AnimatorControllerParameterType.Float:
                ComparisonType floatComparison = animCondition.mode switch
                {
                    AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
                    AnimatorConditionMode.Less => ComparisonType.LessThan,
                    _ => ComparisonType.GreaterThan // デフォルト
                };
                return new FloatCondition(animCondition.parameter, animCondition.threshold, floatComparison);
                
            case AnimatorControllerParameterType.Int:
                ComparisonType intComparison = animCondition.mode switch
                {
                    AnimatorConditionMode.Equals => ComparisonType.Equal,
                    AnimatorConditionMode.NotEqual => ComparisonType.NotEqual,
                    AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
                    AnimatorConditionMode.Less => ComparisonType.LessThan,
                    _ => ComparisonType.Equal // デフォルト
                };
                return new IntCondition(animCondition.parameter, (int)animCondition.threshold, intComparison);
                
            case AnimatorControllerParameterType.Trigger:
                // Triggerの場合はBoolとして扱う
                return new BoolCondition(animCondition.parameter, true);
                
            default:
                throw new InvalidCastException("invalid parameter type");
        }
    }

    public static (AnimatorCondition animatorCondition, string parameter, AnimatorControllerParameterType parameterType) ToAnimatorCondition(this IBaseCondition condition)
    {
        string parameter;
        AnimatorControllerParameterType parameterType;
        AnimatorConditionMode mode;
        float threshold;

        switch (condition)
        {
            case FloatCondition fc:
                parameter = fc.ParameterName;
                parameterType = AnimatorControllerParameterType.Float;
                mode = fc.ComparisonType switch
                {
                    ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                    ComparisonType.LessThan => AnimatorConditionMode.Less,
                    _ => throw new InvalidOperationException($"Comparison type {fc.ComparisonType} is not supported for float parameters")
                };
                threshold = fc.Value;
                break;
                
            case IntCondition ic:
                parameter = ic.ParameterName;
                parameterType = AnimatorControllerParameterType.Int;
                mode = ic.ComparisonType switch
                {
                    ComparisonType.Equal => AnimatorConditionMode.Equals,
                    ComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
                    ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                    ComparisonType.LessThan => AnimatorConditionMode.Less,
                    _ => throw new InvalidOperationException($"Comparison type {ic.ComparisonType} is invalid")
                };
                threshold = ic.Value;
                break;
                
            case BoolCondition bc:
                parameter = bc.ParameterName;
                parameterType = AnimatorControllerParameterType.Bool;
                mode = bc.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                threshold = 0;
                break;
                
            case TrueCondition:
                parameter = FaceTuneConstants.TrueParameterName;
                parameterType = AnimatorControllerParameterType.Bool;
                mode = AnimatorConditionMode.If;
                threshold = 0;
                break;
                
            case FalseCondition:
                parameter = FaceTuneConstants.FalseParameterName;
                parameterType = AnimatorControllerParameterType.Bool;
                mode = AnimatorConditionMode.IfNot;
                threshold = 0;
                break;
                
            default:
                throw new NotImplementedException($"Condition type {condition.GetType()} is not implemented");
        }

        var animatorCondition = new AnimatorCondition
        {
            parameter = parameter,
            mode = mode,
            threshold = threshold
        };

        return (animatorCondition, parameter, parameterType);
    }
#endif
}


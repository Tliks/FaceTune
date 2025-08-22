#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace Aoyon.FaceTune;

internal static class ConditionUtility
{
    public static EqualityComparison Negate(this EqualityComparison type)
    {
        return type switch
        {
            EqualityComparison.Equal => EqualityComparison.NotEqual,
            EqualityComparison.NotEqual => EqualityComparison.Equal,
            _ => throw new InvalidOperationException($"Invalid equality comparison type: {type}")
        };
    }

    public static (ComparisonType newType, int newValue) Negate(this ComparisonType type, int currentValue)
    {
        return type switch
        {
            ComparisonType.Equal => (ComparisonType.NotEqual, currentValue),
            ComparisonType.NotEqual => (ComparisonType.Equal, currentValue),
            ComparisonType.GreaterThan => (ComparisonType.LessThan, currentValue + 1),
            ComparisonType.LessThan => (ComparisonType.GreaterThan, currentValue - 1),
            _ => (type, currentValue)
        };
    }

    private const float FloatTolerance = 0.00001f;
    public static (ComparisonType newType, float newValue) Negate(this ComparisonType type, float currentValue)
    {
        return type switch
        {
            ComparisonType.GreaterThan => (ComparisonType.LessThan, currentValue + FloatTolerance), // Math.BitIncrement使えなくない？？
            ComparisonType.LessThan => (ComparisonType.GreaterThan, currentValue - FloatTolerance),
            _ => (type, currentValue)
        };
    }

    // Todo: Refactorと常にtrue/falseとなる際のハンドリング
    // 条件の簡略化の処理も欲しい
    public static List<List<Condition>> AddNegation(ICollection<Condition> conditions, ICollection<Condition> conditionsToNegate)
    {
        // A: conditions, B: ConditionsToNegate
        // A∧¬B
        // 結果は (Aの全条件) AND NOT (Bの全条件)。
        // ド・モルガンの法則よりNOT (B1 AND B2 AND ...) = (NOT B1) OR (NOT B2) OR ...
        // したがって、Aの全条件 + 否定したBの各条件を1つずつ加えたリストをORでまとめて返す。

        var result = new List<List<Condition>>();

        foreach (var negateCondition in conditionsToNegate)
        {
            var andList = new List<Condition>(conditions)
            {
                negateCondition.ToNegation()
            };
            result.Add(andList);
        }

        return result;
    }   

    // Todo: 上に同じく
    public static List<List<Condition>> AddNegation(ICollection<Condition> conditions, List<List<Condition>> conditionsToNegate)
    {
        // A ∧ ¬(B1 ∨ B2 ∨ ...)
        // = A ∧ (¬B1 ∧ ¬B2 ∧ ...)
        // ただし各 Bi は AND のまとまり（List<Condition>）。
        // ¬(B1 AND B2 AND ...) = (¬B1) OR (¬B2) OR ... を利用して、
        // DNF としては A と各 Bi の否定からの直積展開になる。

        // 初期は「A の AND 条件」の単一節
        var clauses = new List<List<Condition>> { new List<Condition>(conditions) };

        foreach (var group in conditionsToNegate)
        {
            var expanded = new List<List<Condition>>();
            foreach (var clause in clauses)
            {
                foreach (var cond in group)
                {
                    var newClause = new List<Condition>(clause) { cond.ToNegation() };
                    expanded.Add(newClause);
                }
            }
            clauses = expanded;
        }

        return clauses;
    }

#if UNITY_EDITOR
    public static (HandGestureCondition?, ParameterCondition?) ToCondition(this AnimatorCondition animCondition, Dictionary<string, AnimatorControllerParameterType> parameterTypes, Action<string> onParameterTypeNotFound)
    {
        if (animCondition.parameter is "GestureLeft" or "GestureRight" && animCondition.threshold is >= 0 and < 8)
        {
            var hand = animCondition.parameter == "GestureLeft" ? Hand.Left : Hand.Right;
            var gesture = (HandGesture)(int)animCondition.threshold;
            return (new HandGestureCondition(hand, EqualityComparison.Equal, gesture), null);
        }
        else
        {
            // パラメータの型をcontrollerから取得
            if (!parameterTypes.TryGetValue(animCondition.parameter, out var parameterType))
            {
                onParameterTypeNotFound(animCondition.parameter);
                return (null, null);
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
                    throw new InvalidCastException("invalid parameter type");
            }

            return (null, paramCondition);
        }
    }

    public static (AnimatorCondition animatorCondition, string parameter, AnimatorControllerParameterType parameterType) ToAnimatorCondition(this Condition condition)
    {
        AnimatorCondition animatorCondition;

        string parameter;
        AnimatorControllerParameterType parameterType;
        AnimatorConditionMode mode;
        float threshold;

        switch (condition)
        {
            case HandGestureCondition hgc:
                parameter = hgc.Hand == Hand.Left ? "GestureLeft" : "GestureRight";
                parameterType = AnimatorControllerParameterType.Int;
                mode = hgc.EqualityComparison == EqualityComparison.Equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual;
                threshold = (int)hgc.HandGesture; // 整数値をそのまま使う。
                break;
            case ParameterCondition pc:
                parameter = pc.ParameterName;
                switch (pc.ParameterType)
                {
                    case ParameterType.Int:
                        parameterType = AnimatorControllerParameterType.Int;
                        mode = pc.ComparisonType switch
                        {
                            ComparisonType.Equal => AnimatorConditionMode.Equals,
                            ComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
                            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
                            ComparisonType.LessThan => AnimatorConditionMode.Less,
                            _ => throw new InvalidOperationException($"Comparison type {pc.ComparisonType} is invalid")
                        };
                        threshold = pc.IntValue;
                        break;
                    case ParameterType.Float:
                        parameterType = AnimatorControllerParameterType.Float;
                        switch (pc.ComparisonType)
                        {
                            case ComparisonType.GreaterThan:
                                mode = AnimatorConditionMode.Greater;
                                break;
                            case ComparisonType.LessThan:
                                mode = AnimatorConditionMode.Less;
                                break;
                            case ComparisonType.Equal:
                                throw new InvalidOperationException("Equal is not supported for float parameters. Using Greater instead.");
                            case ComparisonType.NotEqual:
                                throw new InvalidOperationException("NotEqual is not supported for float parameters. Using Greater instead.");
                            default:
                                throw new NotImplementedException($"Comparison type {pc.ComparisonType} is not implemented");
                        }
                        threshold = pc.FloatValue;
                        break;
                    case ParameterType.Bool:
                        parameterType = AnimatorControllerParameterType.Bool;
                        mode = pc.BoolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                        threshold = 0;
                        break;
                    default:
                        throw new NotImplementedException($"Parameter type {pc.ParameterType} is not implemented");
                }
                break;
            default:
                throw new NotImplementedException($"Condition type {condition.GetType()} is not implemented");
        }

        animatorCondition = new AnimatorCondition()
        {
            parameter = parameter,
            mode = mode,
            threshold = threshold
        };

        return (animatorCondition, parameter, parameterType);
    }
#endif
}
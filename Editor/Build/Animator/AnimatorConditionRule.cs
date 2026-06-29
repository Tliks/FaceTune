using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

/// <summary>
/// Animator condition carried as a DNF rule. ParameterType is kept only because AnimatorCondition itself does not contain it.
/// </summary>
internal sealed record class AnimatorConditionRule(
    AnimatorCondition Condition,
    AnimatorControllerParameterType ParameterType) : DnfRule
{
    public string ParameterName => Condition.parameter;

    public static AnimatorConditionRule FromParameterCondition(ParameterCondition condition)
    {
        return condition.ParameterType switch
        {
            FaceTune.ParameterType.Int => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorConditionMode(condition.ComparisonType),
                    threshold = condition.IntValue
                },
                AnimatorControllerParameterType.Int),
            FaceTune.ParameterType.Float => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorFloatConditionMode(condition.ComparisonType),
                    threshold = condition.FloatValue
                },
                AnimatorControllerParameterType.Float),
            FaceTune.ParameterType.Bool => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = condition.BoolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    threshold = 0
                },
                AnimatorControllerParameterType.Bool),
            _ => throw new InvalidOperationException($"Invalid parameter type: {condition.ParameterType}")
        };
    }

    public override DnfCondition Not()
    {
        return DnfCondition.Single(this with { Condition = Condition.Negate(ParameterType) });
    }

    private static AnimatorConditionMode ToAnimatorConditionMode(ComparisonType comparisonType)
    {
        return comparisonType switch
        {
            ComparisonType.Equal => AnimatorConditionMode.Equals,
            ComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
            ComparisonType.LessThan => AnimatorConditionMode.Less,
            _ => throw new InvalidOperationException($"Invalid comparison type: {comparisonType}")
        };
    }

    private static AnimatorConditionMode ToAnimatorFloatConditionMode(ComparisonType comparisonType)
    {
        return comparisonType switch
        {
            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
            ComparisonType.LessThan => AnimatorConditionMode.Less,
            ComparisonType.Equal => throw new InvalidOperationException("Equal is not supported for float parameters."),
            ComparisonType.NotEqual => throw new InvalidOperationException("NotEqual is not supported for float parameters."),
            _ => throw new InvalidOperationException($"Invalid comparison type: {comparisonType}")
        };
    }
}

internal static class AnimatorConditionExtensions
{
    private const float FloatTolerance = 0.00001f;

    public static AnimatorCondition Negate(this AnimatorCondition condition, AnimatorControllerParameterType parameterType)
    {
        return parameterType switch
        {
            AnimatorControllerParameterType.Bool => condition.NegateBool(),
            AnimatorControllerParameterType.Int => condition.NegateInt(),
            AnimatorControllerParameterType.Float => condition.NegateFloat(),
            _ => throw new InvalidOperationException($"Invalid parameter type: {parameterType}")
        };
    }

    public static AnimatorCondition NegateBool(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.If => condition with { mode = AnimatorConditionMode.IfNot },
            AnimatorConditionMode.IfNot => condition with { mode = AnimatorConditionMode.If },
            _ => throw new InvalidOperationException($"Invalid bool condition mode: {condition.mode}")
        };
    }

    public static AnimatorCondition NegateInt(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.Equals => condition with { mode = AnimatorConditionMode.NotEqual },
            AnimatorConditionMode.NotEqual => condition with { mode = AnimatorConditionMode.Equals },
            AnimatorConditionMode.Greater => condition with { mode = AnimatorConditionMode.Less, threshold = condition.threshold + 1 },
            AnimatorConditionMode.Less => condition with { mode = AnimatorConditionMode.Greater, threshold = condition.threshold - 1 },
            _ => throw new InvalidOperationException($"Invalid int condition mode: {condition.mode}")
        };
    }

    public static AnimatorCondition NegateFloat(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.Greater => condition with { mode = AnimatorConditionMode.Less, threshold = condition.threshold + FloatTolerance },
            AnimatorConditionMode.Less => condition with { mode = AnimatorConditionMode.Greater, threshold = condition.threshold - FloatTolerance },
            _ => throw new InvalidOperationException($"Invalid float condition mode: {condition.mode}")
        };
    }
}

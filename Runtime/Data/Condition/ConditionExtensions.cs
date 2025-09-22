namespace Aoyon.FaceTune;

internal static class ConditionExtensions
{
    public static ICondition And(this ICondition left, ICondition right)
    {
        var leftConditions = left is AndCondition acLeft ? acLeft.Conditions : new[] { left };
        var rightConditions = right is AndCondition acRight ? acRight.Conditions : new[] { right };
        return new AndCondition(leftConditions.Concat(rightConditions).ToList());
    }

    public static ICondition Or(this ICondition left, ICondition right)
    {
        var leftConditions = left is OrCondition ocLeft ? ocLeft.Conditions : new[] { left };
        var rightConditions = right is OrCondition ocRight ? ocRight.Conditions : new[] { right };
        return new OrCondition(leftConditions.Concat(rightConditions).ToList());
    }

    public static ICondition Not(this ICondition condition)
    {
        return condition.ToNegation();
    }

    public static NormalizedCondition Normalize(this ICondition condition)
    {
        var text = ConditionTreeDebugVisitor.Dump(condition);
        Debug.Log(text);
        return NormalizationVisitor.Normalize(condition);
    }

    public static NormalizedCondition Optimize(this NormalizedCondition condition)
    {
        return ConditionOptimizer.Optimize(condition);
    }
}
namespace Aoyon.FaceTune;

internal record class ExpressionWithCondition
{
    public ICondition Condition { get; init; }
    public AvatarExpression Expression { get; init; }

    public ExpressionWithCondition(ICondition condition, AvatarExpression expression)
    {
        Condition = condition;
        Expression = expression;
    }

    public ExpressionWithNormalizedCondition NormalizeAndOptimize()
    {
        return new ExpressionWithNormalizedCondition(Condition.Normalize().Optimize(), Expression);
    }
}

internal record class ExpressionWithNormalizedCondition
{
    public NormalizedCondition Condition { get; private set; }
    public AvatarExpression Expression { get; private set; }

    public ExpressionWithNormalizedCondition(NormalizedCondition condition, AvatarExpression expression)
    {
        Condition = condition;
        Expression = expression;
    }
}

internal record class ExpressionWithConditionGroup
{
    public bool IsBlending { get; private set; }
    public List<ExpressionWithNormalizedCondition> ExpressionWithConditions { get; private set; }

    public ExpressionWithConditionGroup(bool isBlending, List<ExpressionWithNormalizedCondition> expressionWithConditions)
    {
        IsBlending = isBlending;
        ExpressionWithConditions = expressionWithConditions;
    }
}

internal record PatternData
{
    public IReadOnlyList<ExpressionWithConditionGroup> Groups { get; private set; }
    public bool IsEmpty => Groups.Count == 0;

    public PatternData(IReadOnlyList<ExpressionWithConditionGroup> groups)
    {
        Groups = groups;
    }

    public List<AvatarExpression> GetAllExpressions()
    {
        var expressions = new List<AvatarExpression>();
        foreach (var group in Groups)
        {
            foreach (var expressionWithDnfCondition in group.ExpressionWithConditions)
            {
                expressions.Add(expressionWithDnfCondition.Expression);
            }
        }
        return expressions;
    }
}
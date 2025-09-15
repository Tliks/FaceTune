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

    public ExpressionWithDnfCondition ToDnfCondition(DnfVisitor dnfVisitor)
    {
        return new ExpressionWithDnfCondition(new DnfCondition(Condition.Accept(dnfVisitor)), Expression);
    }
}

internal record class ExpressionWithDnfCondition
{
    public DnfCondition Condition { get; private set; }
    public AvatarExpression Expression { get; private set; }

    public ExpressionWithDnfCondition(DnfCondition condition, AvatarExpression expression)
    {
        Condition = condition;
        Expression = expression;
    }
}

internal record class ExpressionWithConditionGroup
{
    public bool IsBlending { get; private set; }
    public List<ExpressionWithDnfCondition> ExpressionWithConditions { get; private set; }

    public ExpressionWithConditionGroup(bool isBlending, List<ExpressionWithDnfCondition> expressionWithConditions)
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
namespace Aoyon.FaceTune;

// Immutable

/// <summary>
/// 複数の条件をANDで結合
/// </summary>
internal record AndCondition(IReadOnlyList<ICondition> Conditions) : ICompositeCondition
{
    public AndCondition(params ICondition[] conditions) : this(conditions.ToList().AsReadOnly()) { }

    public ICondition ToNegation()
    {
        // Not(A and B) = (Not A) or (Not B)
        return new OrCondition(Conditions.Select(c => c.ToNegation()).ToList());
    }

    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}

/// <summary>
/// 複数の条件をORで結合
/// </summary>
internal record OrCondition(IReadOnlyList<ICondition> Conditions) : ICompositeCondition
{
    public OrCondition(params ICondition[] conditions) : this(conditions.ToList().AsReadOnly()) { }

    public ICondition ToNegation()
    {
        // Not(A or B) = (Not A) and (Not B)
        return new AndCondition(Conditions.Select(c => c.ToNegation()).ToList());
    }

    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}
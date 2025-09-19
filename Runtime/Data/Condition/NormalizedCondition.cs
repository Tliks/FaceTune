namespace Aoyon.FaceTune;

/// <summary>
/// DNF用のAND条件の節
/// </summary>
internal record AndClause(IReadOnlyList<IBaseCondition> Conditions)
{
    public AndClause(params IBaseCondition[] conditions) : this(conditions.ToList().AsReadOnly()) { }

    public ICondition ToNegation()
    {
        // Not(A and B) = (Not A) or (Not B)
        return new OrCondition(Conditions.Select(c => c.ToNegation()).ToList());
    }
}
/// <summary>
/// 正規化された条件(DNF)
/// </summary>
internal record NormalizedCondition(IReadOnlyList<AndClause> Clauses)
{
    public NormalizedCondition(params AndClause[] clauses) : this(clauses.ToList().AsReadOnly()) { }

    public NormalizedCondition ToNegation()
    {
        // Not(A or B) = (Not A) and (Not B)
        return new AndCondition(Clauses.Select(c => c.ToNegation()).ToList()).Normalize();
    }
}
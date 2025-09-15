namespace Aoyon.FaceTune;

/// <summary>
/// DNF用のAND条件の節
/// </summary>
internal record AndClause(IReadOnlyList<IBaseCondition> Conditions)
{
    public AndClause(params IBaseCondition[] conditions) : this(conditions.ToList().AsReadOnly()) { }
}
/// <summary>
/// DNFを保証する条件
/// </summary>
internal record DnfCondition(IReadOnlyList<AndClause> Clauses)
{
    public DnfCondition(params AndClause[] clauses) : this(clauses.ToList().AsReadOnly()) { }
}
namespace Aoyon.FaceTune;

/// <summary>
/// Component hierarchy interpreted as FaceTune expressions for build backends.
/// Conditions are platform-resolved, data is merged, and hierarchy order is preserved.
/// Backend-specific layout decisions such as animator layer packing are intentionally not represented here.
/// </summary>
internal sealed class ExpressionProgram
{
    public IReadOnlyList<ExpressionItem> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    public ExpressionProgram(IReadOnlyList<ExpressionItem> items)
    {
        Items = items;
    }

    public IEnumerable<AvatarExpression> GetAllExpressions()
    {
        return Items.Select(item => item.Expression);
    }
}

internal sealed class ExpressionItem
{
    public GameObject SourceObject { get; }
    public AvatarExpression Expression { get; }

    /// <summary>
    /// The expression's own activation condition after parent/scope conditions are applied.
    /// This does not include priority suppression by later replace expressions.
    /// </summary>
    public DnfCondition RawWhen { get; }

    /// <summary>
    /// Positive-form condition that suppresses this expression according to FaceTune priority semantics.
    /// Backends that need a flat condition can use ActiveWhen.
    /// </summary>
    public DnfCondition SuppressedBy { get; private set; } = DnfCondition.Never;

    public DnfCondition ActiveWhen => RawWhen.Except(SuppressedBy);

    public ExpressionItem(GameObject sourceObject, AvatarExpression expression, DnfCondition rawWhen)
    {
        SourceObject = sourceObject;
        Expression = expression;
        RawWhen = rawWhen;
    }

    public void SetSuppressedBy(DnfCondition suppressedBy)
    {
        SuppressedBy = suppressedBy;
    }
}

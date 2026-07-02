namespace Aoyon.FaceTune.Build;

/// <summary>
/// Component hierarchy interpreted as FaceTune expressions for build backends.
/// Conditions are platform-resolved, data is merged, and hierarchy order is preserved.
/// Backend-specific layout decisions such as animator layer packing are intentionally not represented here.
/// </summary>
internal sealed class ExpressionProgram
{
    public IReadOnlyList<ExpressionItem> Items { get; }

    public bool IsEmpty => Items.Count == 0;

    public ExpressionProgram(IEnumerable<ExpressionItem> items)
    {
        Items = items.ToArray();
    }
}

internal sealed record class ExpressionItem
{
    public Transform SourceTransform { get; init; }
    public string Name { get; init; }
    
    public BlendShapeWeightAnimationSet AnimationSet { get; init; }
    public ExpressionSettings ExpressionSettings { get; init; }
    public FacialSettings FacialSettings { get; init; }

    /// <summary>
    /// The expression's own activation condition after parent/scope conditions are applied.
    /// This does not include priority suppression by later replace expressions.
    /// </summary>
    public DnfCondition RawWhen { get; init; }

    /// <summary>
    /// Positive-form condition that suppresses this expression according to FaceTune priority semantics.
    /// Backends that need a flat condition can use ActiveWhen.
    /// </summary>
    public DnfCondition SuppressedBy { get; init; } = DnfCondition.Never;

    public ExpressionWriteMode WriteMode => FacialSettings.WriteMode;
    public DnfCondition ActiveWhen => RawWhen.Except(SuppressedBy);

    public ExpressionItem(
        Transform sourceTransform,
        string name,
        BlendShapeWeightAnimationSet animationSet,
        ExpressionSettings expressionSettings,
        FacialSettings facialSettings,
        DnfCondition rawWhen)
    {
        SourceTransform = sourceTransform;
        Name = name;
        AnimationSet = new(animationSet);
        ExpressionSettings = expressionSettings;
        FacialSettings = facialSettings;
        RawWhen = rawWhen;
    }

    public ExpressionItem WithSuppressedBy(DnfCondition suppressedBy)
    {
        return this with { SuppressedBy = suppressedBy };
    }
}

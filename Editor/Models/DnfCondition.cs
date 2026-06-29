namespace Aoyon.FaceTune;

/// <summary>
/// OR of AND。条件の中身はDnfRuleが持つ。
/// </summary>
internal sealed class DnfCondition
{
    public IReadOnlyList<DnfCase> Cases { get; }

    public bool IsAlways => Cases.Count == 1 && Cases[0].IsAlways;
    public bool IsNever => Cases.Count == 0;

    public static DnfCondition Always { get; } = new(new[] { DnfCase.Always });
    public static DnfCondition Never { get; } = new(Array.Empty<DnfCase>());

    public DnfCondition(IReadOnlyList<DnfCase> cases)
    {
        Cases = cases;
    }

    public static DnfCondition Single(DnfRule rule)
    {
        return new DnfCondition(new[] { new DnfCase(new[] { rule }) });
    }

    public static DnfCondition All(IEnumerable<DnfCondition> conditions)
    {
        return conditions.Aggregate(Always, (current, condition) => current.And(condition));
    }

    public static DnfCondition Any(IEnumerable<DnfCondition> conditions)
    {
        return conditions.Aggregate(Never, (current, condition) => current.Or(condition));
    }

    public DnfCondition Except(DnfCondition suppressor)
    {
        return And(suppressor.Not());
    }

    public DnfCondition And(DnfCondition other)
    {
        if (Cases.Count == 0 || other.Cases.Count == 0) return Never;

        var cases = new List<DnfCase>();
        foreach (var left in Cases)
        {
            foreach (var right in other.Cases)
            {
                cases.Add(left.And(right));
            }
        }
        return new DnfCondition(cases);
    }

    public DnfCondition Or(DnfCondition other)
    {
        if (Cases.Count == 0) return other;
        if (other.Cases.Count == 0) return this;
        return new DnfCondition(Cases.Concat(other.Cases).ToList());
    }

    public DnfCondition Not()
    {
        var result = Always;
        foreach (var activationCase in Cases)
        {
            result = result.And(activationCase.Not());
        }
        return result;
    }
}

internal sealed class DnfCase
{
    public IReadOnlyList<DnfRule> Rules { get; }

    public bool IsAlways => Rules.Count == 0;

    public static DnfCase Always { get; } = new(Array.Empty<DnfRule>());

    public DnfCase(IReadOnlyList<DnfRule> rules)
    {
        Rules = rules;
    }

    public DnfCase And(DnfCase other)
    {
        return new DnfCase(Rules.Concat(other.Rules).ToList());
    }

    public DnfCondition Not()
    {
        if (Rules.Count == 0) return DnfCondition.Never;

        var result = DnfCondition.Never;
        foreach (var rule in Rules)
        {
            result = result.Or(rule.Not());
        }
        return result;
    }
}

/// <summary>
/// DNFの1条件。
/// </summary>
internal abstract record class DnfRule
{
    public abstract DnfCondition Not();
}
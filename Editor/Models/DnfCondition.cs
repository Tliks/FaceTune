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
        return Single(new DnfCase(new[] { rule }));
    }

    public static DnfCondition Single(DnfCase conditionCase)
    {
        return new DnfCondition(new[] { conditionCase });
    }

    public static DnfCondition All(
        IEnumerable<DnfCondition> conditions,
        ParameterDomainRegistry? parameterDomains = null)
    {
        var combined = conditions.Aggregate(
            Always,
            (current, condition) => current.And(condition, parameterDomains));
        return parameterDomains == null ? combined : combined.Simplify(parameterDomains);
    }

    public static DnfCondition Any(
        IEnumerable<DnfCondition> conditions,
        ParameterDomainRegistry? parameterDomains = null)
    {
        var cases = conditions
            .SelectMany(condition => condition.Cases)
            .ToHashSet();
        if (cases.Count == 0) return Never;

        var combined = new DnfCondition(cases.ToArray());
        return parameterDomains == null ? combined : combined.Simplify(parameterDomains);
    }

    public DnfCondition And(DnfCondition other, ParameterDomainRegistry? parameterDomains = null)
    {
        if (Cases.Count == 0 || other.Cases.Count == 0) return Never;

        var cases = new HashSet<DnfCase>();
        foreach (var left in Cases)
        {
            foreach (var right in other.Cases)
            {
                if (DnfCase.TryAnd(left, right, out var combined, parameterDomains))
                {
                    cases.UnionWith(combined.Cases);
                }
            }
        }

        return cases.Count == 0 ? Never : new DnfCondition(cases.ToArray());
    }

    public DnfCondition Or(
        DnfCondition other,
        ParameterDomainRegistry? parameterDomains = null)
    {
        if (Cases.Count == 0)
            return parameterDomains == null ? other : other.Simplify(parameterDomains);
        if (other.Cases.Count == 0)
            return parameterDomains == null ? this : Simplify(parameterDomains);

        var cases = new HashSet<DnfCase>(Cases);
        cases.UnionWith(other.Cases);
        var combined = new DnfCondition(cases.ToArray());
        return parameterDomains == null ? combined : combined.Simplify(parameterDomains);
    }

    public DnfCondition Complement(ParameterDomainRegistry? parameterDomains = null)
    {
        var result = Always;
        foreach (var conditionCase in Cases)
        {
            result = result.And(conditionCase.Complement(), parameterDomains);
        }
        return result;
    }

    public DnfCondition Except(DnfCondition other, ParameterDomainRegistry parameterDomains)
    {
        var result = this;
        foreach (var otherCase in other.Cases)
        {
            var otherCondition = Single(otherCase);
            if (result.And(otherCondition, parameterDomains).IsNever) continue;

            result = result
                .And(otherCase.Complement(), parameterDomains)
                .Simplify(parameterDomains);
            if (result.IsNever) break;
        }
        return result;
    }

    private DnfCondition Simplify(ParameterDomainRegistry parameterDomains)
    {
        var cases = Cases.ToList();
        while (true)
        {
            RemoveSubsumedCases(cases);
            if (cases.Any(conditionCase => conditionCase.IsAlways)) return Always;

            var merged = false;
            var keys = cases
                .SelectMany(conditionCase => conditionCase.Rules)
                .Select(rule =>
                {
                    rule.GetSimplifier(out var key, out var simplifier);
                    return (Key: key, Simplifier: simplifier);
                })
                .GroupBy(pair => pair.Key)
                .Select(group => group.First())
                .ToArray();

            foreach (var (key, simplifier) in keys)
            {
                var groups = new List<DisjunctionGroup>();
                for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
                {
                    var groupedRules = cases[caseIndex].Rules
                        .GroupBy(rule =>
                        {
                            rule.GetSimplifier(out var ruleKey, out _);
                            return ruleKey;
                        })
                        .ToDictionary(group => group.Key, group => (IReadOnlyList<DnfRule>)group.ToArray());
                    if (!groupedRules.TryGetValue(key, out var rulesForKey)) continue;

                    var commonRules = groupedRules
                        .Where(pair => !Equals(pair.Key, key))
                        .SelectMany(pair => pair.Value)
                        .ToHashSet();
                    var group = groups.FirstOrDefault(candidate => candidate.CommonRules.SetEquals(commonRules));
                    if (group == null)
                    {
                        group = new DisjunctionGroup(commonRules);
                        groups.Add(group);
                    }

                    group.CaseIndices.Add(caseIndex);
                    group.Alternatives.Add(rulesForKey);
                }

                foreach (var group in groups)
                {
                    if (group.Alternatives.Count <= 1 ||
                        !simplifier.TrySimplifyDisjunction(group.Alternatives, parameterDomains, out var simplified))
                    {
                        continue;
                    }

                    for (var index = group.CaseIndices.Count - 1; index >= 0; index--)
                    {
                        cases.RemoveAt(group.CaseIndices[index]);
                    }

                    var commonCondition = Single(new DnfCase(group.CommonRules.ToArray()));
                    cases.AddRange(commonCondition.And(simplified, parameterDomains).Cases);
                    merged = true;
                    break;
                }

                if (merged) break;
            }

            if (!merged) return cases.Count == 0 ? Never : new DnfCondition(cases.ToArray());
        }
    }

    private static void RemoveSubsumedCases(List<DnfCase> cases)
    {
        for (var leftIndex = 0; leftIndex < cases.Count; leftIndex++)
        {
            var leftRules = cases[leftIndex].Rules.ToHashSet();
            for (var rightIndex = cases.Count - 1; rightIndex > leftIndex; rightIndex--)
            {
                var rightRules = cases[rightIndex].Rules.ToHashSet();
                if (leftRules.IsSubsetOf(rightRules))
                {
                    cases.RemoveAt(rightIndex);
                }
                else if (rightRules.IsSubsetOf(leftRules))
                {
                    cases.RemoveAt(leftIndex);
                    leftIndex--;
                    break;
                }
            }
        }
    }

    private sealed class DisjunctionGroup
    {
        public HashSet<DnfRule> CommonRules { get; }
        public List<int> CaseIndices { get; } = new();
        public List<IReadOnlyList<DnfRule>> Alternatives { get; } = new();

        public DisjunctionGroup(HashSet<DnfRule> commonRules)
        {
            CommonRules = commonRules;
        }
    }
}

internal sealed class DnfCase : IEquatable<DnfCase>
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
        if (!TryAnd(this, other, out var combined) || combined.Cases.Count != 1)
        {
            throw new InvalidOperationException("DNF cases cannot be represented as a single DnfCase.");
        }

        return combined.Cases[0];
    }

    public static bool TryAnd(
        DnfCase left,
        DnfCase right,
        [NotNullWhen(true)] out DnfCondition? combined,
        ParameterDomainRegistry? parameterDomains = null)
    {
        if (left.IsAlways)
        {
            combined = DnfCondition.Single(right);
            return true;
        }
        if (right.IsAlways)
        {
            combined = DnfCondition.Single(left);
            return true;
        }

        var groups = new Dictionary<object, RuleGroup>();

        AddRules(left.Rules);
        AddRules(right.Rules);

        var cases = new[] { Always };
        foreach (var group in groups.Values)
        {
            cases = AndCasesWithoutSimplification(cases, group.Simplifier.Simplify(group.Rules, parameterDomains).Cases);
            if (cases.Length == 0)
            {
                combined = DnfCondition.Never;
                return false;
            }
        }

        combined = cases.Length == 0 ? DnfCondition.Never : new DnfCondition(cases.ToHashSet().ToArray());
        return !combined.IsNever;

        void AddRules(IReadOnlyList<DnfRule> source)
        {
            foreach (var rule in source)
            {
                rule.GetSimplifier(out var key, out var simplifier);

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new RuleGroup(simplifier);
                    groups.Add(key, group);
                }

                group.Rules.Add(rule);
            }
        }
    }

    private static DnfCase[] AndCasesWithoutSimplification(IReadOnlyList<DnfCase> left, IReadOnlyList<DnfCase> right)
    {
        if (left.Count == 0 || right.Count == 0) return Array.Empty<DnfCase>();

        var result = new DnfCase[left.Count * right.Count];
        var index = 0;
        foreach (var leftCase in left)
        {
            foreach (var rightCase in right)
            {
                result[index++] = new DnfCase(leftCase.Rules.Concat(rightCase.Rules).ToArray());
            }
        }

        return result;
    }

    public DnfCondition Complement()
    {
        return Rules.Count == 0
            ? DnfCondition.Never
            : new DnfCondition(Rules.Select(rule => new DnfCase(new[] { rule.Negate() })).ToArray());
    }

    public bool Equals(DnfCase? other)
    {
        return other != null && Rules.SequenceEqual(other.Rules);
    }

    public override bool Equals(object? obj)
    {
        return obj is DnfCase other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var rule in Rules)
        {
            hash.Add(rule);
        }
        return hash.ToHashCode();
    }
}

internal sealed class RuleGroup
{
    public DnfRuleGroupSimplifier Simplifier { get; }
    public List<DnfRule> Rules { get; } = new();

    public RuleGroup(DnfRuleGroupSimplifier simplifier)
    {
        Simplifier = simplifier;
    }
}

internal abstract class DnfRuleGroupSimplifier
{
    public abstract DnfCondition Simplify(
        IReadOnlyList<DnfRule> rules,
        ParameterDomainRegistry? parameterDomains);

    public virtual bool TrySimplifyDisjunction(
        IReadOnlyList<IReadOnlyList<DnfRule>> alternatives,
        ParameterDomainRegistry? parameterDomains,
        [NotNullWhen(true)] out DnfCondition? simplified)
    {
        simplified = null;
        return false;
    }
}

internal sealed class PassthroughDnfRuleSimplifier : DnfRuleGroupSimplifier
{
    public static PassthroughDnfRuleSimplifier Instance { get; } = new();

    private PassthroughDnfRuleSimplifier()
    {
    }

    public override DnfCondition Simplify(
        IReadOnlyList<DnfRule> rules,
        ParameterDomainRegistry? parameterDomains)
    {
        return DnfCondition.Single(new DnfCase(rules.Distinct().ToArray()));
    }
}

/// <summary>
/// DNFの1条件。
/// </summary>
internal abstract record class DnfRule
{
    public virtual void GetSimplifier(out object key, out DnfRuleGroupSimplifier simplifier)
    {
        key = this;
        simplifier = PassthroughDnfRuleSimplifier.Instance;
    }

    public abstract DnfRule Negate();
}

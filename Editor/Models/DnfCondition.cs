namespace Aoyon.FaceTune;

/// <summary>
/// OR of AND。条件の中身はDnfConstraintが持つ。
/// </summary>
internal sealed class DnfCondition
{
    public IReadOnlyList<DnfCase> Cases { get; }

    public bool IsAlways => Cases.Count == 1 && Cases[0].IsAlways;
    public bool IsNever => Cases.Count == 0;

    public static DnfCondition Always { get; } = new(new[] { DnfCase.Always });
    public static DnfCondition Never { get; } = new(Array.Empty<DnfCase>());

    internal DnfCondition(IReadOnlyList<DnfCase> cases)
    {
        Cases = cases;
    }

    public static DnfCondition Single(DnfRule rule, ParameterDomainRegistry parameterDomains)
    {
        return FromConstraint(rule.CreateConstraint(parameterDomains));
    }

    internal static DnfCondition FromConstraint(DnfConstraint constraint)
    {
        return constraint.IsEmpty
            ? Never
            : new DnfCondition(new[] { DnfCase.FromConstraint(constraint) });
    }

    internal static DnfCondition FromCase(DnfCase conditionCase)
    {
        return new DnfCondition(new[] { conditionCase });
    }

    public static DnfCondition All(IEnumerable<DnfCondition> conditions)
    {
        return conditions.Aggregate(Always, (current, condition) => current.And(condition));
    }

    public static DnfCondition Any(IEnumerable<DnfCondition> conditions)
    {
        var cases = conditions.SelectMany(condition => condition.Cases).ToList();
        return cases.Count == 0 ? Never : new DnfCondition(cases).Simplify();
    }

    public DnfCondition And(DnfCondition other)
    {
        if (IsNever || other.IsNever) return Never;

        var cases = new List<DnfCase>();
        foreach (var left in Cases)
        {
            foreach (var right in other.Cases)
            {
                var combined = left.Intersect(right);
                if (combined != null) cases.Add(combined);
            }
        }

        return cases.Count == 0 ? Never : new DnfCondition(cases).Simplify();
    }

    public DnfCondition Or(DnfCondition other)
    {
        if (IsNever) return other;
        if (other.IsNever) return this;
        return new DnfCondition(Cases.Concat(other.Cases).ToArray()).Simplify();
    }

    public DnfCondition Complement()
    {
        return Cases.Aggregate(Always, (result, conditionCase) => result.And(conditionCase.Complement()));
    }

    public DnfCondition Except(DnfCondition other)
    {
        var result = this;
        foreach (var otherCase in other.Cases)
        {
            if (!result.Cases.Any(conditionCase => conditionCase.Overlaps(otherCase))) continue;
            result = result.And(otherCase.Complement());
            if (result.IsNever) break;
        }
        return result;
    }

    private DnfCondition Simplify()
    {
        var cases = Cases.Distinct(DnfCase.EqualityComparer).ToList();
        while (true)
        {
            RemoveSubsumedCases(cases);
            if (cases.Any(conditionCase => conditionCase.IsAlways)) return Always;

            var merged = false;
            var keys = cases
                .SelectMany(conditionCase => conditionCase.Constraints)
                .Select(constraint => constraint.Key)
                .Distinct()
                .ToArray();

            foreach (var key in keys)
            {
                var groups = new List<DisjunctionGroup>();
                for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
                {
                    if (!cases[caseIndex].TryGetConstraint(key, out var constraint)) continue;
                    var commonConstraints = cases[caseIndex].Constraints
                        .Where(candidate => !Equals(candidate.Key, key))
                        .ToArray();
                    var group = groups.FirstOrDefault(candidate =>
                        DnfCase.ConstraintSequencesEqual(candidate.CommonConstraints, commonConstraints));
                    if (group == null)
                    {
                        group = new DisjunctionGroup(commonConstraints);
                        groups.Add(group);
                    }
                    group.CaseIndices.Add(caseIndex);
                    group.Alternatives.Add(constraint);
                }

                foreach (var group in groups)
                {
                    if (group.Alternatives.Count <= 1) continue;
                    var union = group.Alternatives[0];
                    var supported = true;
                    foreach (var alternative in group.Alternatives.Skip(1))
                    {
                        if (!union.TryUnion(alternative, out var mergedUnion))
                        {
                            supported = false;
                            break;
                        }
                        union = mergedUnion;
                    }
                    if (!supported || !union.IsUniversal) continue;

                    for (var index = group.CaseIndices.Count - 1; index >= 0; index--)
                    {
                        cases.RemoveAt(group.CaseIndices[index]);
                    }
                    cases.Add(new DnfCase(group.CommonConstraints));
                    merged = true;
                    break;
                }

                if (merged) break;
            }

            if (!merged) return cases.Count == 0 ? Never : new DnfCondition(SortCases(cases));
        }
    }

    private static void RemoveSubsumedCases(List<DnfCase> cases)
    {
        for (var leftIndex = 0; leftIndex < cases.Count; leftIndex++)
        {
            for (var rightIndex = cases.Count - 1; rightIndex > leftIndex; rightIndex--)
            {
                if (cases[leftIndex].Contains(cases[rightIndex]))
                {
                    cases.RemoveAt(rightIndex);
                }
                else if (cases[rightIndex].Contains(cases[leftIndex]))
                {
                    cases.RemoveAt(leftIndex);
                    leftIndex--;
                    break;
                }
            }
        }
    }

    private static DnfCase[] SortCases(IEnumerable<DnfCase> cases)
    {
        return cases.OrderBy(conditionCase => conditionCase, DnfCase.OrderComparer).ToArray();
    }

    private sealed class DisjunctionGroup
    {
        public DnfConstraint[] CommonConstraints { get; }
        public List<int> CaseIndices { get; } = new();
        public List<DnfConstraint> Alternatives { get; } = new();

        public DisjunctionGroup(DnfConstraint[] commonConstraints)
        {
            CommonConstraints = commonConstraints;
        }
    }
}

internal sealed class DnfCase
{
    private static readonly IComparer<object> KeyComparer = Comparer<object>.Create(CompareKeys);

    public IReadOnlyList<DnfConstraint> Constraints { get; }
    public IEnumerable<DnfRule> Rules => Constraints.SelectMany(constraint => constraint.ToRules());
    public bool IsAlways => Constraints.Count == 0;

    public static DnfCase Always { get; } = new(Array.Empty<DnfConstraint>());
    public static IEqualityComparer<DnfCase> EqualityComparer { get; } = new CaseEqualityComparer();
    public static IComparer<DnfCase> OrderComparer { get; } = new CaseOrderComparer();

    internal DnfCase(IEnumerable<DnfConstraint> constraints)
    {
        Constraints = constraints.OrderBy(constraint => constraint.Key, KeyComparer).ToArray();
    }

    internal static DnfCase FromConstraint(DnfConstraint constraint)
    {
        return constraint.IsUniversal ? Always : new DnfCase(new[] { constraint });
    }

    public DnfCase? Intersect(DnfCase other)
    {
        var constraints = new List<DnfConstraint>(Constraints.Count + other.Constraints.Count);
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < Constraints.Count || rightIndex < other.Constraints.Count)
        {
            if (leftIndex >= Constraints.Count)
            {
                constraints.Add(other.Constraints[rightIndex++]);
                continue;
            }
            if (rightIndex >= other.Constraints.Count)
            {
                constraints.Add(Constraints[leftIndex++]);
                continue;
            }

            var left = Constraints[leftIndex];
            var right = other.Constraints[rightIndex];
            var comparison = CompareKeys(left.Key, right.Key);
            if (comparison < 0)
            {
                constraints.Add(left);
                leftIndex++;
            }
            else if (comparison > 0)
            {
                constraints.Add(right);
                rightIndex++;
            }
            else
            {
                var intersection = left.Intersect(right);
                if (intersection.IsEmpty) return null;
                if (!intersection.IsUniversal) constraints.Add(intersection);
                leftIndex++;
                rightIndex++;
            }
        }
        return new DnfCase(constraints);
    }

    public bool Overlaps(DnfCase other) => Intersect(other) != null;

    public bool Contains(DnfCase other)
    {
        foreach (var constraint in Constraints)
        {
            if (!other.TryGetConstraint(constraint.Key, out var otherConstraint) ||
                !constraint.Contains(otherConstraint))
            {
                return false;
            }
        }
        return true;
    }

    public DnfCondition Complement()
    {
        return IsAlways
            ? DnfCondition.Never
            : DnfCondition.Any(Constraints.Select(constraint => constraint.Complement()));
    }

    public bool TryGetConstraint(object key, [NotNullWhen(true)] out DnfConstraint? constraint)
    {
        constraint = Constraints.FirstOrDefault(candidate => Equals(candidate.Key, key));
        return constraint != null;
    }

    internal static bool ConstraintSequencesEqual(
        IReadOnlyList<DnfConstraint> left,
        IReadOnlyList<DnfConstraint> right)
    {
        return left.Count == right.Count && left.Zip(right, (a, b) => a.SetEquals(b)).All(equal => equal);
    }

    private static int CompareKeys(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is IComparable comparable) return comparable.CompareTo(right);
        return StringComparer.Ordinal.Compare(left?.ToString(), right?.ToString());
    }

    private sealed class CaseEqualityComparer : IEqualityComparer<DnfCase>
    {
        public bool Equals(DnfCase? left, DnfCase? right)
        {
            return left != null && right != null && ConstraintSequencesEqual(left.Constraints, right.Constraints);
        }

        public int GetHashCode(DnfCase conditionCase)
        {
            var hash = new HashCode();
            foreach (var constraint in conditionCase.Constraints) hash.Add(constraint.GetSemanticHashCode());
            return hash.ToHashCode();
        }
    }

    private sealed class CaseOrderComparer : IComparer<DnfCase>
    {
        public int Compare(DnfCase? left, DnfCase? right)
        {
            if (left == null || right == null) return left == right ? 0 : left == null ? -1 : 1;
            for (var index = 0; index < Math.Min(left.Constraints.Count, right.Constraints.Count); index++)
            {
                var keyComparison = CompareKeys(left.Constraints[index].Key, right.Constraints[index].Key);
                if (keyComparison != 0) return keyComparison;
                var constraintComparison = left.Constraints[index].CompareTo(right.Constraints[index]);
                if (constraintComparison != 0) return constraintComparison;
            }
            return left.Constraints.Count.CompareTo(right.Constraints.Count);
        }
    }
}

internal abstract class DnfConstraint
{
    public abstract object Key { get; }
    public abstract bool IsEmpty { get; }
    public abstract bool IsUniversal { get; }

    public abstract DnfConstraint Intersect(DnfConstraint other);
    public abstract bool TryUnion(DnfConstraint other, [NotNullWhen(true)] out DnfConstraint? union);
    public abstract DnfCondition Complement();
    public abstract bool Contains(DnfConstraint other);
    public abstract bool SetEquals(DnfConstraint other);
    public abstract int CompareTo(DnfConstraint other);
    public abstract int GetSemanticHashCode();
    public abstract IEnumerable<DnfRule> ToRules();
}

internal abstract record class DnfRule
{
    public abstract object ConstraintKey { get; }
    public abstract DnfConstraint CreateConstraint(ParameterDomainRegistry parameterDomains);
    public abstract DnfRule Negate();
}

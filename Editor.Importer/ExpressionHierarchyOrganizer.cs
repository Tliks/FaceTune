namespace Aoyon.FaceTune.Importing;

/// <summary>
/// Groups consecutive expressions by shared condition structure while preserving their order.
/// Exact conditions required by every expression in a group are lifted to a parent Settings component.
/// </summary>
internal static class ExpressionHierarchyOrganizer
{
    private const int MinimumGroupSize = 3;
    private const int MaximumNestingDepth = 2;

    /// <param name="parentAlreadyGroupsExpressions">
    ///     Set to true when the caller already created a group for this complete expression set.
    ///     Sub-groups for partial runs are still created.
    /// </param>
    public static void Organize(
        GameObject parent,
        IReadOnlyList<GameObject> expressions,
        bool parentAlreadyGroupsExpressions = false)
    {
        for (var index = 0; index < expressions.Count; index++)
        {
            var expression = expressions[index];
            expression.transform.SetParent(parent.transform, false);
            expression.transform.SetSiblingIndex(index);
        }

        Organize(
            parent,
            expressions,
            0,
            new HashSet<GroupKey>(),
            parentAlreadyGroupsExpressions);
    }

    /// <summary>
    /// Reorders only contiguous runs whose conditions are pairwise exclusive.
    /// </summary>
    public static IReadOnlyList<T> NormalizeExclusiveRuns<T>(
        IReadOnlyList<T> items,
        Func<T, DnfCondition> getCondition,
        Func<T, bool> canNormalize,
        IComparer<T> comparer)
    {
        var result = items.ToList();
        var start = 0;
        while (start + 1 < result.Count)
        {
            var end = start + 1;
            while (end < result.Count && IsExclusiveWithRun(result, start, end, getCondition))
                end++;

            var count = end - start;
            if (count > 1 && result.GetRange(start, count).All(canNormalize))
            {
                var ordered = result
                    .Skip(start)
                    .Take(count)
                    .OrderBy(item => item, comparer)
                    .ToArray();
                for (var index = 0; index < ordered.Length; index++)
                    result[start + index] = ordered[index];
            }

            start = end;
        }

        return result;
    }

    private static bool IsExclusiveWithRun<T>(
        IReadOnlyList<T> items,
        int start,
        int candidate,
        Func<T, DnfCondition> getCondition)
    {
        var condition = getCondition(items[candidate]);
        for (var index = start; index < candidate; index++)
        {
            if (!getCondition(items[index]).And(condition).IsNever)
                return false;
        }
        return true;
    }

    private static void Organize(
        GameObject parent,
        IReadOnlyList<GameObject> expressions,
        int depth,
        ISet<GroupKey> inheritedKeys,
        bool parentAlreadyGroupsExpressions)
    {
        if (depth >= MaximumNestingDepth || expressions.Count < MinimumGroupSize) return;

        var index = 0;
        while (index <= expressions.Count - MinimumGroupSize)
        {
            var candidate = FindCandidate(expressions, index, inheritedKeys);
            if (candidate == null)
            {
                index++;
                continue;
            }

            var (key, count) = candidate.Value;
            if (parentAlreadyGroupsExpressions && index == 0 && count == expressions.Count)
            {
                inheritedKeys.Add(key);
                continue;
            }

            var members = expressions.Skip(index).Take(count).ToArray();
            var group = new GameObject(key.Name);
            group.transform.SetParent(parent.transform, false);
            group.transform.SetSiblingIndex(members[0].transform.GetSiblingIndex());
            foreach (var member in members)
                member.transform.SetParent(group.transform, false);

            LiftCommonConditions(group, members);

            var nestedInheritedKeys = inheritedKeys.Concat(key.CoveredKeys()).ToHashSet();
            Organize(group, members, depth + 1, nestedInheritedKeys, false);
            index += count;
        }
    }

    private static (GroupKey Key, int Count)? FindCandidate(
        IReadOnlyList<GameObject> expressions,
        int start,
        ISet<GroupKey> inheritedKeys)
    {
        var firstKeys = GetGroupingKeys(expressions[start]);
        var candidates = firstKeys
            .Intersect(GetGroupingKeys(expressions[start + 1]))
            .Where(key => !inheritedKeys.Contains(key))
            .Select(key => (Key: key, Count: CountRun(expressions, start, key)))
            .Where(candidate => candidate.Count >= MinimumGroupSize)
            .OrderByDescending(candidate => candidate.Count)
            .ThenBy(candidate => candidate.Key.Priority)
            .ThenBy(candidate => candidate.Key.Name, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[0];
    }

    private static int CountRun(IReadOnlyList<GameObject> expressions, int start, GroupKey key)
    {
        var count = 0;
        for (var index = start; index < expressions.Count; index++)
        {
            if (!GetGroupingKeys(expressions[index]).Contains(key)) break;
            count++;
        }
        return count;
    }

    private static HashSet<GroupKey> GetGroupingKeys(GameObject obj)
    {
        var expression = obj.GetComponent<ExpressionComponent>();
        if (expression == null || !expression.HasCondition
                               || expression.Condition.Mode != ConditionSelection.Kind.Conditional)
            return new HashSet<GroupKey>();

        var condition = expression.Condition.Condition;
        if (condition.Cases.Count == 0) return new HashSet<GroupKey>();

        var keys = GetRequiredAtoms(condition)
            .Select(atom => GroupKey.Exact(atom))
            .ToHashSet();

        foreach (var hand in IntersectCases(condition, conditionCase =>
                     conditionCase.HandGestureConditions.Select(item => item.Hand)))
            keys.Add(GroupKey.Hand(hand));

        foreach (var parameterName in IntersectCases(condition, conditionCase =>
                     conditionCase.ParameterConditions
                         .Select(item => item.ParameterName)
                         .Where(name => !string.IsNullOrWhiteSpace(name))))
            keys.Add(GroupKey.Parameter(parameterName));

        foreach (var menu in IntersectCases(condition, conditionCase =>
                     conditionCase.MenuConditions
                         .Select(item => item.MenuSource)
                         .Where(source => source != null)
                         .Cast<MenuComponent>()))
            keys.Add(GroupKey.Menu(menu));

        return keys;
    }

    private static HashSet<T> IntersectCases<T>(
        Condition condition,
        Func<ConditionCase, IEnumerable<T>> select)
        where T : notnull
    {
        HashSet<T>? result = null;
        foreach (var conditionCase in condition.Cases)
        {
            var values = select(conditionCase).ToHashSet();
            if (result == null)
                result = values;
            else
                result.IntersectWith(values);
        }
        return result ?? new HashSet<T>();
    }

    private static void LiftCommonConditions(GameObject group, IReadOnlyList<GameObject> members)
    {
        HashSet<ConditionAtom>? common = null;
        foreach (var member in members)
        {
            var expression = member.GetComponent<ExpressionComponent>();
            if (expression == null) return;
            var required = GetRequiredAtoms(expression.Condition.Condition);
            if (common == null)
                common = required;
            else
                common.IntersectWith(required);
        }

        if (common == null || common.Count == 0) return;

        var ordered = common.OrderBy(atom => atom.SortKey, StringComparer.Ordinal).ToArray();
        var settings = group.AddComponent<SettingsComponent>();
        settings.HasCondition = true;
        settings.Condition = CreateCondition(ordered);

        foreach (var member in members)
            RemoveConditions(member.GetComponent<ExpressionComponent>(), common);
    }

    private static HashSet<ConditionAtom> GetRequiredAtoms(Condition condition)
    {
        HashSet<ConditionAtom>? required = null;
        foreach (var conditionCase in condition.Cases)
        {
            var atoms = conditionCase.EnumerateConditions()
                .Select(ConditionAtom.From)
                .ToHashSet();
            if (required == null)
                required = atoms;
            else
                required.IntersectWith(atoms);
        }
        return required ?? new HashSet<ConditionAtom>();
    }

    private static Condition CreateCondition(IEnumerable<ConditionAtom> atoms)
    {
        var conditionCase = new ConditionCase();
        foreach (var atom in atoms)
            atom.AddTo(conditionCase);
        return new Condition(conditionCase);
    }

    private static void RemoveConditions(ExpressionComponent expression, ISet<ConditionAtom> removed)
    {
        foreach (var conditionCase in expression.Condition.Condition.Cases)
        {
            conditionCase.HandGestureConditions.RemoveAll(item => removed.Contains(ConditionAtom.From(item)));
            conditionCase.MenuConditions.RemoveAll(item => removed.Contains(ConditionAtom.From(item)));
            conditionCase.ParameterConditions.RemoveAll(item => removed.Contains(ConditionAtom.From(item)));
        }

        if (expression.Condition.Condition.Cases.Any(conditionCase => conditionCase.IsEmpty))
            expression.Condition.Mode = ConditionSelection.Kind.Always;
    }

    private enum GroupKind
    {
        Exact,
        Hand,
        Parameter,
        Menu
    }

    private readonly record struct GroupKey(GroupKind Kind, object Identity, string Name, int Priority)
    {
        public static GroupKey Exact(ConditionAtom atom)
            => new(GroupKind.Exact, atom, atom.DisplayName, 0);

        public static GroupKey Hand(HandGestureHand hand)
            => new(GroupKind.Hand, hand, $"{Humanize(hand.ToString())} Hand", 1);

        public static GroupKey Parameter(string name)
            => new(GroupKind.Parameter, name, name, 2);

        public static GroupKey Menu(MenuComponent menu)
            => new(GroupKind.Menu, menu, menu.gameObject.name, 3);

        public IEnumerable<GroupKey> CoveredKeys()
        {
            yield return this;
            if (Kind != GroupKind.Exact || Identity is not ConditionAtom atom) yield break;
            switch (atom.Kind)
            {
                case AtomKind.Hand:
                    yield return Hand(atom.Hand);
                    break;
                case AtomKind.Parameter when !string.IsNullOrWhiteSpace(atom.ParameterName):
                    yield return Parameter(atom.ParameterName);
                    break;
                case AtomKind.Menu when atom.Menu != null:
                    yield return Menu(atom.Menu);
                    break;
            }
        }
    }

    private enum AtomKind
    {
        Hand,
        Menu,
        Parameter
    }

    private readonly record struct ConditionAtom(
        AtomKind Kind,
        HandGestureHand Hand,
        HandGesture Gesture,
        bool Matches,
        MenuComponent? Menu,
        MenuConditionMode MenuMode,
        float Threshold,
        string ParameterName,
        ParameterType ParameterType,
        ComparisonType ComparisonType,
        float FloatValue,
        int IntValue,
        bool BoolValue)
    {
        public string SortKey => $"{(int)Kind}:{DisplayName}";

        public string DisplayName => Kind switch
        {
            AtomKind.Hand => $"{(Matches ? string.Empty : "Not ")}{Humanize(Hand.ToString())} {Humanize(Gesture.ToString())}",
            AtomKind.Menu => Menu == null ? "Menu" : Menu.gameObject.name,
            AtomKind.Parameter => ParameterName,
            _ => throw new ArgumentOutOfRangeException()
        };

        public static ConditionAtom From(object value) => value switch
        {
            HandGestureCondition hand => new ConditionAtom(
                AtomKind.Hand,
                hand.Hand,
                hand.Gesture,
                hand.Matches,
                null,
                default,
                default,
                string.Empty,
                default,
                default,
                default,
                default,
                default),
            MenuCondition menu => new ConditionAtom(
                AtomKind.Menu,
                default,
                default,
                default,
                menu.MenuSource,
                menu.Mode,
                menu.Mode is MenuConditionMode.GreaterThan or MenuConditionMode.LessThan
                    ? menu.Threshold
                    : 0f,
                string.Empty,
                default,
                default,
                default,
                default,
                default),
            ParameterCondition parameter => new ConditionAtom(
                AtomKind.Parameter,
                default,
                default,
                default,
                null,
                default,
                default,
                parameter.ParameterName,
                parameter.ParameterType,
                parameter.ParameterType == ParameterType.Bool
                    ? ComparisonType.Equal
                    : parameter.ComparisonType,
                parameter.ParameterType == ParameterType.Float ? parameter.FloatValue : 0f,
                parameter.ParameterType == ParameterType.Int ? parameter.IntValue : 0,
                parameter.ParameterType == ParameterType.Bool && parameter.BoolValue),
            _ => throw new InvalidOperationException($"Unsupported condition: {value.GetType().FullName}")
        };

        public void AddTo(ConditionCase conditionCase)
        {
            switch (Kind)
            {
                case AtomKind.Hand:
                    conditionCase.HandGestureConditions.Add(new HandGestureCondition
                    {
                        Hand = Hand,
                        Gesture = Gesture,
                        Matches = Matches
                    });
                    break;
                case AtomKind.Menu:
                    conditionCase.MenuConditions.Add(new MenuCondition
                    {
                        MenuSource = Menu,
                        Mode = MenuMode,
                        Threshold = Threshold
                    });
                    break;
                case AtomKind.Parameter:
                    conditionCase.ParameterConditions.Add(new ParameterCondition
                    {
                        ParameterName = ParameterName,
                        ParameterType = ParameterType,
                        ComparisonType = ComparisonType,
                        FloatValue = FloatValue,
                        IntValue = IntValue,
                        BoolValue = BoolValue
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static string Humanize(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            if (index > 0 && char.IsUpper(value[index]) && !char.IsUpper(value[index - 1]))
                result.Append(' ');
            result.Append(value[index]);
        }
        return result.ToString();
    }
}

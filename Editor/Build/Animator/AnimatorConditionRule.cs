using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

/// <summary>
/// Animator condition carried as a DNF rule. ParameterType is kept only because AnimatorCondition itself does not contain it.
/// </summary>
internal sealed record class AnimatorConditionRule(
    AnimatorCondition Condition,
    AnimatorControllerParameterType ParameterType) : DnfRule
{
    public string ParameterName => Condition.parameter;

    public static AnimatorConditionRule FromParameterCondition(ParameterCondition condition)
    {
        return AnimatorParameterConditionConverter.ToAnimatorConditionRule(condition);
    }

    public ParameterCondition ToParameterCondition()
    {
        return AnimatorParameterConditionConverter.ToParameterCondition(this);
    }

    public override void GetSimplifier(out object key, out DnfRuleGroupSimplifier simplifier)
    {
        key = new ParameterConstraintKey(ParameterName, ParameterType);
        simplifier = ParameterRuleGroupSimplifier.Instance;
    }

    public override DnfRule Negate()
    {
        return this with { Condition = Condition.Negate(ParameterType) };
    }

}

internal readonly record struct ParameterConstraintKey(
    string ParameterName,
    AnimatorControllerParameterType ParameterType);

internal sealed class ParameterRuleGroupSimplifier : DnfRuleGroupSimplifier
{
    public static ParameterRuleGroupSimplifier Instance { get; } = new();

    public override DnfCondition Simplify(
        IReadOnlyList<DnfRule> rules,
        ParameterDomainRegistry? parameterDomains)
    {
        AnimatorConditionRule? equal = null;
        var notEquals = new Dictionary<float, AnimatorConditionRule>();
        AnimatorConditionRule? greater = null;
        AnimatorConditionRule? less = null;

        var typedRules = rules.Cast<AnimatorConditionRule>().ToArray();
        var firstRule = typedRules[0];
        var intDomain = firstRule.ParameterType == AnimatorControllerParameterType.Int &&
            parameterDomains != null &&
            parameterDomains.TryGetIntDomain(firstRule.ParameterName, out var domain)
                ? domain
                : (IntParameterDomain?)null;

        foreach (var rule in typedRules)
        {
            var succeeded = rule.ParameterType switch
            {
                AnimatorControllerParameterType.Bool => SimplifyBool(rule, ref equal),
                AnimatorControllerParameterType.Int => SimplifyComparable(rule, true, intDomain, ref equal, notEquals, ref greater, ref less),
                AnimatorControllerParameterType.Float => SimplifyComparable(rule, false, intDomain, ref equal, notEquals, ref greater, ref less),
                _ => true
            };

            if (!succeeded) return DnfCondition.Never;
        }

        if (equal != null) return DnfCondition.Single(equal);
        if (TryResolveFiniteIntDomain(intDomain, notEquals, greater, less, out var domainCondition)) return domainCondition;

        var simplifiedRules = new List<DnfRule>();
        if (greater != null) simplifiedRules.Add(greater);
        if (less != null) simplifiedRules.Add(less);
        simplifiedRules.AddRange(notEquals.Values.OrderBy(rule => rule.Condition.threshold));
        return DnfCondition.Single(new DnfCase(simplifiedRules));
    }

    private static bool SimplifyBool(AnimatorConditionRule rule, ref AnimatorConditionRule? equal)
    {
        var mode = rule.Condition.mode;
        if (mode != AnimatorConditionMode.If && mode != AnimatorConditionMode.IfNot) return true;

        var normalized = rule with
        {
            Condition = rule.Condition with
            {
                mode = mode,
                threshold = 0
            }
        };

        if (equal != null) return equal.Condition.mode == mode;
        equal = normalized;
        return true;
    }

    private static bool SimplifyComparable(
        AnimatorConditionRule rule,
        bool isInt,
        IntParameterDomain? intDomain,
        ref AnimatorConditionRule? equal,
        Dictionary<float, AnimatorConditionRule> notEquals,
        ref AnimatorConditionRule? greater,
        ref AnimatorConditionRule? less)
    {
        return rule.Condition.mode switch
        {
            AnimatorConditionMode.Equals => SimplifyEqual(rule, isInt, intDomain, ref equal, notEquals, ref greater, ref less),
            AnimatorConditionMode.NotEqual => SimplifyNotEqual(rule, isInt, intDomain, equal, notEquals),
            AnimatorConditionMode.Greater => SimplifyGreater(rule, isInt, equal, ref greater, less),
            AnimatorConditionMode.Less => SimplifyLess(rule, isInt, equal, greater, ref less),
            _ => true
        };
    }

    private static bool SimplifyEqual(
        AnimatorConditionRule rule,
        bool isInt,
        IntParameterDomain? intDomain,
        ref AnimatorConditionRule? equal,
        Dictionary<float, AnimatorConditionRule> notEquals,
        ref AnimatorConditionRule? greater,
        ref AnimatorConditionRule? less)
    {
        var value = NormalizeValue(rule.Condition.threshold, isInt);
        var normalized = WithThreshold(rule, value);

        if (equal != null) return equal.Condition.threshold == value;
        if (intDomain.HasValue && isInt && !intDomain.Value.Contains((int)value)) return false;
        if (notEquals.ContainsKey(value)) return false;
        if (!SatisfiesBounds(value, greater, less)) return false;

        equal = normalized;
        notEquals.Clear();
        greater = null;
        less = null;
        return true;
    }

    private static bool SimplifyNotEqual(
        AnimatorConditionRule rule,
        bool isInt,
        IntParameterDomain? intDomain,
        AnimatorConditionRule? equal,
        Dictionary<float, AnimatorConditionRule> notEquals)
    {
        var value = NormalizeValue(rule.Condition.threshold, isInt);
        if (equal != null) return equal.Condition.threshold != value;
        if (intDomain.HasValue && isInt && !intDomain.Value.Contains((int)value)) return true;

        notEquals.TryAdd(value, WithThreshold(rule, value));
        return true;
    }

    private static bool SimplifyGreater(
        AnimatorConditionRule rule,
        bool isInt,
        AnimatorConditionRule? equal,
        ref AnimatorConditionRule? greater,
        AnimatorConditionRule? less)
    {
        var value = NormalizeValue(rule.Condition.threshold, isInt);
        if (equal != null) return equal.Condition.threshold > value;

        if (greater == null || value > greater.Condition.threshold)
        {
            greater = WithThreshold(rule, value);
        }

        return BoundsOverlap(isInt, greater, less);
    }

    private static bool SimplifyLess(
        AnimatorConditionRule rule,
        bool isInt,
        AnimatorConditionRule? equal,
        AnimatorConditionRule? greater,
        ref AnimatorConditionRule? less)
    {
        var value = NormalizeValue(rule.Condition.threshold, isInt);
        if (equal != null) return equal.Condition.threshold < value;

        if (less == null || value < less.Condition.threshold)
        {
            less = WithThreshold(rule, value);
        }

        return BoundsOverlap(isInt, greater, less);
    }

    private static bool TryResolveFiniteIntDomain(
        IntParameterDomain? intDomain,
        Dictionary<float, AnimatorConditionRule> notEquals,
        AnimatorConditionRule? greater,
        AnimatorConditionRule? less,
        [NotNullWhen(true)] out DnfCondition? condition)
    {
        condition = null;
        if (!intDomain.HasValue) return false;

        var domain = intDomain.Value;
        var values = new List<int>();
        for (var value = domain.MinValue; value <= domain.MaxValue; value++)
        {
            if (!SatisfiesBounds(value, greater, less) || notEquals.ContainsKey(value)) continue;
            values.Add(value);
        }

        if (values.Count == 0)
        {
            condition = DnfCondition.Never;
            return true;
        }

        if (values.Count == 1)
        {
            var template = greater ?? less ?? notEquals.Values.FirstOrDefault();
            if (template == null) return false;
            condition = DnfCondition.Single(template with
            {
                Condition = template.Condition with
                {
                    mode = AnimatorConditionMode.Equals,
                    threshold = values[0]
                }
            });
            return true;
        }

        return false;
    }

    private static bool SatisfiesBounds(
        float value,
        AnimatorConditionRule? greater,
        AnimatorConditionRule? less)
    {
        return (greater == null || value > greater.Condition.threshold)
            && (less == null || value < less.Condition.threshold);
    }

    private static bool BoundsOverlap(
        bool isInt,
        AnimatorConditionRule? greater,
        AnimatorConditionRule? less)
    {
        if (greater == null || less == null) return true;
        return isInt
            ? greater.Condition.threshold + 1 < less.Condition.threshold
            : greater.Condition.threshold < less.Condition.threshold;
    }

    private static float NormalizeValue(float value, bool isInt)
    {
        return isInt ? (int)value : value;
    }

    private static AnimatorConditionRule WithThreshold(AnimatorConditionRule rule, float threshold)
    {
        return rule with { Condition = rule.Condition with { threshold = threshold } };
    }
}

internal static class AnimatorParameterConditionConverter
{
    public static AnimatorConditionRule ToAnimatorConditionRule(ParameterCondition condition)
    {
        return condition.ParameterType switch
        {
            ParameterType.Int => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorConditionMode(condition.ComparisonType),
                    threshold = condition.IntValue
                },
                AnimatorControllerParameterType.Int),
            ParameterType.Float => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorFloatConditionMode(condition.ComparisonType),
                    threshold = condition.FloatValue
                },
                AnimatorControllerParameterType.Float),
            ParameterType.Bool => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = condition.BoolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    threshold = 0
                },
                AnimatorControllerParameterType.Bool),
            _ => throw new InvalidOperationException($"Invalid parameter type: {condition.ParameterType}")
        };
    }

    public static ParameterCondition ToParameterCondition(AnimatorConditionRule rule)
    {
        return rule.ParameterType switch
        {
            AnimatorControllerParameterType.Int => ParameterCondition.Int(
                rule.ParameterName,
                ToComparisonType(rule.Condition.mode),
                (int)rule.Condition.threshold),
            AnimatorControllerParameterType.Float => ParameterCondition.Float(
                rule.ParameterName,
                ToFloatComparisonType(rule.Condition.mode),
                rule.Condition.threshold),
            AnimatorControllerParameterType.Bool => ParameterCondition.Bool(
                rule.ParameterName,
                rule.Condition.mode == AnimatorConditionMode.If),
            _ => throw new InvalidOperationException($"Unsupported parameter type: {rule.ParameterType}")
        };
    }

    private static AnimatorConditionMode ToAnimatorConditionMode(ComparisonType comparisonType)
    {
        return comparisonType switch
        {
            ComparisonType.Equal => AnimatorConditionMode.Equals,
            ComparisonType.NotEqual => AnimatorConditionMode.NotEqual,
            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
            ComparisonType.LessThan => AnimatorConditionMode.Less,
            _ => throw new InvalidOperationException($"Invalid comparison type: {comparisonType}")
        };
    }

    private static AnimatorConditionMode ToAnimatorFloatConditionMode(ComparisonType comparisonType)
    {
        return comparisonType switch
        {
            ComparisonType.GreaterThan => AnimatorConditionMode.Greater,
            ComparisonType.LessThan => AnimatorConditionMode.Less,
            ComparisonType.Equal => throw new InvalidOperationException("Equal is not supported for float parameters."),
            ComparisonType.NotEqual => throw new InvalidOperationException("NotEqual is not supported for float parameters."),
            _ => throw new InvalidOperationException($"Invalid comparison type: {comparisonType}")
        };
    }

    private static ComparisonType ToComparisonType(AnimatorConditionMode mode)
    {
        return mode switch
        {
            AnimatorConditionMode.Equals => ComparisonType.Equal,
            AnimatorConditionMode.NotEqual => ComparisonType.NotEqual,
            AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
            AnimatorConditionMode.Less => ComparisonType.LessThan,
            _ => throw new InvalidOperationException($"Unsupported condition mode: {mode}")
        };
    }

    private static ComparisonType ToFloatComparisonType(AnimatorConditionMode mode)
    {
        return mode switch
        {
            AnimatorConditionMode.Greater => ComparisonType.GreaterThan,
            AnimatorConditionMode.Less => ComparisonType.LessThan,
            _ => throw new InvalidOperationException($"Unsupported float condition mode: {mode}")
        };
    }
}

internal static class AnimatorConditionExtensions
{
    private const float FloatTolerance = 0.00001f;

    public static AnimatorCondition Negate(this AnimatorCondition condition, AnimatorControllerParameterType parameterType)
    {
        return parameterType switch
        {
            AnimatorControllerParameterType.Bool => condition.NegateBool(),
            AnimatorControllerParameterType.Int => condition.NegateInt(),
            AnimatorControllerParameterType.Float => condition.NegateFloat(),
            _ => throw new InvalidOperationException($"Invalid parameter type: {parameterType}")
        };
    }

    public static AnimatorCondition NegateBool(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.If => condition with { mode = AnimatorConditionMode.IfNot },
            AnimatorConditionMode.IfNot => condition with { mode = AnimatorConditionMode.If },
            _ => throw new InvalidOperationException($"Invalid bool condition mode: {condition.mode}")
        };
    }

    public static AnimatorCondition NegateInt(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.Equals => condition with { mode = AnimatorConditionMode.NotEqual },
            AnimatorConditionMode.NotEqual => condition with { mode = AnimatorConditionMode.Equals },
            AnimatorConditionMode.Greater => condition with { mode = AnimatorConditionMode.Less, threshold = condition.threshold + 1 },
            AnimatorConditionMode.Less => condition with { mode = AnimatorConditionMode.Greater, threshold = condition.threshold - 1 },
            _ => throw new InvalidOperationException($"Invalid int condition mode: {condition.mode}")
        };
    }

    public static AnimatorCondition NegateFloat(this AnimatorCondition condition)
    {
        return condition.mode switch
        {
            AnimatorConditionMode.Greater => condition with { mode = AnimatorConditionMode.Less, threshold = condition.threshold + FloatTolerance },
            AnimatorConditionMode.Less => condition with { mode = AnimatorConditionMode.Greater, threshold = condition.threshold - FloatTolerance },
            _ => throw new InvalidOperationException($"Invalid float condition mode: {condition.mode}")
        };
    }
}

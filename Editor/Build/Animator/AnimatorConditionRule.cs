using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

/// <summary>
/// Animator condition carried as a DNF rule. ParameterType is kept only because AnimatorCondition itself does not contain it.
/// </summary>
internal sealed record class AnimatorConditionRule(
    AnimatorCondition Condition,
    AnimatorControllerParameterType ParameterType,
    IntParameterDomain? IntDomain = null) : DnfRule
{
    public string ParameterName => Condition.parameter;

    public static AnimatorConditionRule FromParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry? parameterDomains = null)
    {
        var intDomain = parameterDomains != null && condition.ParameterType == FaceTune.ParameterType.Int &&
            parameterDomains.TryGetIntDomain(condition.ParameterName, out var domain)
                ? domain
                : (IntParameterDomain?)null;

        return condition.ParameterType switch
        {
            FaceTune.ParameterType.Int => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorConditionMode(condition.ComparisonType),
                    threshold = condition.IntValue
                },
                AnimatorControllerParameterType.Int,
                intDomain),
            FaceTune.ParameterType.Float => new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = condition.ParameterName,
                    mode = ToAnimatorFloatConditionMode(condition.ComparisonType),
                    threshold = condition.FloatValue
                },
                AnimatorControllerParameterType.Float),
            FaceTune.ParameterType.Bool => new AnimatorConditionRule(
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

    public ParameterCondition ToParameterCondition()
    {
        return ParameterType switch
        {
            AnimatorControllerParameterType.Int => ParameterCondition.Int(
                ParameterName,
                ToComparisonType(Condition.mode),
                (int)Condition.threshold),
            AnimatorControllerParameterType.Float => ParameterCondition.Float(
                ParameterName,
                ToFloatComparisonType(Condition.mode),
                Condition.threshold),
            AnimatorControllerParameterType.Bool => ParameterCondition.Bool(
                ParameterName,
                Condition.mode == AnimatorConditionMode.If),
            _ => throw new InvalidOperationException($"Unsupported parameter type: {ParameterType}")
        };
    }

    public override object SimplificationKey => new ParameterConstraintKey(ParameterName, ParameterType);

    public override DnfRuleConstraint CreateConstraint()
    {
        return new ParameterConstraint();
    }

    public override DnfRule Negate()
    {
        return this with { Condition = Condition.Negate(ParameterType) };
    }

    private readonly record struct ParameterConstraintKey(
        string ParameterName,
        AnimatorControllerParameterType ParameterType);

    private sealed class ParameterConstraint : DnfRuleConstraint
    {
        private AnimatorConditionRule? equal;
        private readonly Dictionary<float, AnimatorConditionRule> notEquals = new();
        private AnimatorConditionRule? greater;
        private AnimatorConditionRule? less;
        private IntParameterDomain? intDomain;

        public override bool Add(DnfRule rule)
        {
            var animatorRule = (AnimatorConditionRule)rule;
            if (animatorRule.ParameterType == AnimatorControllerParameterType.Int && animatorRule.IntDomain is { } domain)
            {
                intDomain = intDomain.HasValue
                    ? new IntParameterDomain(
                        Math.Max(intDomain.Value.MinValue, domain.MinValue),
                        Math.Min(intDomain.Value.MaxValue, domain.MaxValue))
                    : domain;
                if (!intDomain.Value.IsValid) return false;
            }

            return animatorRule.ParameterType switch
            {
                AnimatorControllerParameterType.Bool => AddBool(animatorRule),
                AnimatorControllerParameterType.Int => AddComparable(animatorRule, true),
                AnimatorControllerParameterType.Float => AddComparable(animatorRule, false),
                _ => true
            };
        }

        public override DnfCondition ToCondition()
        {
            if (equal != null) return DnfCondition.Single(equal);

            if (TryResolveFiniteIntDomain(out var domainCondition)) return domainCondition;

            var baseRules = new List<DnfRule>();
            if (greater != null) baseRules.Add(greater);
            if (less != null) baseRules.Add(less);
            baseRules.AddRange(notEquals.Values.OrderBy(rule => rule.Condition.threshold));
            return DnfCondition.Single(new DnfCase(baseRules));
        }

        private bool AddBool(AnimatorConditionRule rule)
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

        private bool AddComparable(AnimatorConditionRule rule, bool isInt)
        {
            return rule.Condition.mode switch
            {
                AnimatorConditionMode.Equals => AddEqual(rule, isInt),
                AnimatorConditionMode.NotEqual => AddNotEqual(rule, isInt),
                AnimatorConditionMode.Greater => AddGreater(rule, isInt),
                AnimatorConditionMode.Less => AddLess(rule, isInt),
                _ => true
            };
        }

        private bool AddEqual(AnimatorConditionRule rule, bool isInt)
        {
            var value = NormalizeValue(rule.Condition.threshold, isInt);
            var normalized = WithThreshold(rule, value);

            if (equal != null) return equal.Condition.threshold == value;
            if (intDomain.HasValue && isInt && !intDomain.Value.Contains((int)value)) return false;
            if (notEquals.ContainsKey(value)) return false;
            if (!SatisfiesBounds(value)) return false;

            equal = normalized;
            notEquals.Clear();
            greater = null;
            less = null;
            return true;
        }

        private bool AddNotEqual(AnimatorConditionRule rule, bool isInt)
        {
            var value = NormalizeValue(rule.Condition.threshold, isInt);
            if (equal != null) return equal.Condition.threshold != value;
            if (intDomain.HasValue && isInt && !intDomain.Value.Contains((int)value)) return true;

            notEquals.TryAdd(value, WithThreshold(rule, value));
            return true;
        }

        private bool AddGreater(AnimatorConditionRule rule, bool isInt)
        {
            var value = NormalizeValue(rule.Condition.threshold, isInt);
            if (equal != null) return equal.Condition.threshold > value;

            if (greater == null || value > greater.Condition.threshold)
            {
                greater = WithThreshold(rule, value);
            }

            return BoundsOverlap(isInt);
        }

        private bool AddLess(AnimatorConditionRule rule, bool isInt)
        {
            var value = NormalizeValue(rule.Condition.threshold, isInt);
            if (equal != null) return equal.Condition.threshold < value;

            if (less == null || value < less.Condition.threshold)
            {
                less = WithThreshold(rule, value);
            }

            return BoundsOverlap(isInt);
        }

        private bool TryResolveFiniteIntDomain([NotNullWhen(true)] out DnfCondition? condition)
        {
            condition = null;
            if (!intDomain.HasValue) return false;

            var domain = intDomain.Value;
            var values = new List<int>();
            for (var value = domain.MinValue; value <= domain.MaxValue; value++)
            {
                if (!SatisfiesBounds(value) || notEquals.ContainsKey(value)) continue;
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

        private bool SatisfiesBounds(float value)
        {
            return (greater == null || value > greater.Condition.threshold)
                && (less == null || value < less.Condition.threshold);
        }

        private bool BoundsOverlap(bool isInt)
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

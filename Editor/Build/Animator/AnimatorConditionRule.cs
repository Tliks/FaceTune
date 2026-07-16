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

    public override object ConstraintKey => new ParameterConstraintKey(ParameterName, ParameterType);

    public override DnfConstraint CreateConstraint(ParameterDomainRegistry parameterDomains)
    {
        if (ParameterType == AnimatorControllerParameterType.Bool)
        {
            return FiniteParameterConstraint.FromBoolRule(this);
        }
        if (ParameterType == AnimatorControllerParameterType.Int &&
            parameterDomains.TryGetIntDomain(ParameterName, out var domain) &&
            (long)domain.MaxValue - domain.MinValue < 4096)
        {
            return FiniteParameterConstraint.FromIntRule(this, domain);
        }
        return RangeParameterConstraint.FromRule(this);
    }

    public override DnfRule Negate()
    {
        return this with { Condition = Condition.Negate(ParameterType) };
    }

}

internal readonly record struct ParameterConstraintKey(
    string ParameterName,
    AnimatorControllerParameterType ParameterType) : IComparable<ParameterConstraintKey>, IComparable
{
    public int CompareTo(ParameterConstraintKey other)
    {
        var nameComparison = StringComparer.Ordinal.Compare(ParameterName, other.ParameterName);
        return nameComparison != 0 ? nameComparison : ParameterType.CompareTo(other.ParameterType);
    }

    public int CompareTo(object? obj)
    {
        return obj is ParameterConstraintKey other
            ? CompareTo(other)
            : throw new ArgumentException("Object is not a parameter constraint key.", nameof(obj));
    }
}

internal sealed class RangeParameterConstraint : DnfConstraint
{
    private readonly AnimatorConditionRule _template;
    private readonly bool _isInt;
    private readonly float? _equal;
    private readonly float? _greaterThan;
    private readonly float? _lessThan;
    private readonly HashSet<float> _excluded;

    public override object Key => _template.ConstraintKey;
    public override bool IsEmpty { get; }
    public override bool IsUniversal => !IsEmpty && _equal == null && _greaterThan == null &&
                                        _lessThan == null && _excluded.Count == 0;

    private RangeParameterConstraint(
        AnimatorConditionRule template,
        float? equal,
        float? greaterThan,
        float? lessThan,
        IEnumerable<float>? excluded = null,
        bool isEmpty = false)
    {
        _template = template;
        _isInt = template.ParameterType == AnimatorControllerParameterType.Int;
        _equal = Normalize(equal);
        _greaterThan = Normalize(greaterThan);
        _lessThan = Normalize(lessThan);
        _excluded = excluded?.Select(value => Normalize(value)!.Value).ToHashSet() ?? new HashSet<float>();
        IsEmpty = isEmpty || IsContradictory();
    }

    public static RangeParameterConstraint FromRule(AnimatorConditionRule rule)
    {
        var value = rule.Condition.threshold;
        return rule.Condition.mode switch
        {
            AnimatorConditionMode.Equals => new RangeParameterConstraint(rule, value, null, null),
            AnimatorConditionMode.NotEqual => new RangeParameterConstraint(rule, null, null, null, new[] { value }),
            AnimatorConditionMode.Greater => new RangeParameterConstraint(rule, null, value, null),
            AnimatorConditionMode.Less => new RangeParameterConstraint(rule, null, null, value),
            _ => throw new InvalidOperationException($"Invalid comparable condition mode: {rule.Condition.mode}")
        };
    }

    public override DnfConstraint Intersect(DnfConstraint other)
    {
        var right = Validate(other);
        var equal = _equal ?? right._equal;
        var conflictingEquals = _equal != null && right._equal != null && _equal != right._equal;
        return new RangeParameterConstraint(
            _template,
            equal,
            Max(_greaterThan, right._greaterThan),
            Min(_lessThan, right._lessThan),
            _excluded.Concat(right._excluded),
            IsEmpty || right.IsEmpty || conflictingEquals);
    }

    public override bool TryUnion(
        DnfConstraint other,
        [NotNullWhen(true)] out DnfConstraint? union)
    {
        var right = Validate(other);
        if (Contains(right))
        {
            union = this;
            return true;
        }
        if (right.Contains(this))
        {
            union = right;
            return true;
        }
        union = null;
        return false;
    }

    public override DnfCondition Complement()
    {
        return IsEmpty
            ? DnfCondition.Always
            : DnfCondition.Any(ToRules().Select(rule =>
                DnfCondition.FromConstraint(FromRule((AnimatorConditionRule)rule.Negate()))));
    }

    public override bool Contains(DnfConstraint other)
    {
        var right = Validate(other);
        return Intersect(right).SetEquals(right);
    }

    public override bool SetEquals(DnfConstraint other)
    {
        return other is RangeParameterConstraint constraint &&
               Equals(Key, constraint.Key) &&
               IsEmpty == constraint.IsEmpty &&
               _equal == constraint._equal &&
               _greaterThan == constraint._greaterThan &&
               _lessThan == constraint._lessThan &&
               _excluded.SetEquals(constraint._excluded);
    }

    public override int CompareTo(DnfConstraint other)
    {
        var right = Validate(other);
        var comparison = Nullable.Compare(_equal, right._equal);
        if (comparison != 0) return comparison;
        comparison = Nullable.Compare(_greaterThan, right._greaterThan);
        if (comparison != 0) return comparison;
        comparison = Nullable.Compare(_lessThan, right._lessThan);
        if (comparison != 0) return comparison;
        return CompareValues(_excluded, right._excluded);
    }

    public override int GetSemanticHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(_equal);
        hash.Add(_greaterThan);
        hash.Add(_lessThan);
        foreach (var excluded in _excluded.OrderBy(value => value)) hash.Add(excluded);
        return hash.ToHashCode();
    }

    public override IEnumerable<DnfRule> ToRules()
    {
        if (IsEmpty) yield break;
        if (_equal is { } equal)
        {
            yield return WithCondition(AnimatorConditionMode.Equals, equal);
            yield break;
        }
        if (_greaterThan is { } greater) yield return WithCondition(AnimatorConditionMode.Greater, greater);
        if (_lessThan is { } less) yield return WithCondition(AnimatorConditionMode.Less, less);
        foreach (var excluded in _excluded.OrderBy(value => value))
        {
            if (SatisfiesBounds(excluded))
                yield return WithCondition(AnimatorConditionMode.NotEqual, excluded);
        }
    }

    private bool IsContradictory()
    {
        if (_equal is { } equal)
            return _excluded.Contains(equal) || !SatisfiesBounds(equal);
        if (_greaterThan is not { } greater || _lessThan is not { } less) return false;
        return _isInt ? greater + 1 >= less : greater >= less;
    }

    private bool SatisfiesBounds(float value)
    {
        return (_greaterThan == null || value > _greaterThan)
            && (_lessThan == null || value < _lessThan);
    }

    private RangeParameterConstraint Validate(DnfConstraint other)
    {
        if (other is not RangeParameterConstraint constraint || !Equals(Key, constraint.Key))
            throw new InvalidOperationException("Cannot combine constraints from different parameters.");
        return constraint;
    }

    private float? Normalize(float? value) => value == null ? null : _isInt ? (int)value.Value : value;

    private AnimatorConditionRule WithCondition(AnimatorConditionMode mode, float threshold)
    {
        return _template with
        {
            Condition = _template.Condition with { mode = mode, threshold = threshold }
        };
    }

    private static float? Max(float? left, float? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return Math.Max(left.Value, right.Value);
    }

    private static float? Min(float? left, float? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return Math.Min(left.Value, right.Value);
    }

    private static int CompareValues(IEnumerable<float> left, IEnumerable<float> right)
    {
        return string.CompareOrdinal(
            string.Join(",", left.OrderBy(value => value)),
            string.Join(",", right.OrderBy(value => value)));
    }
}

internal sealed class FiniteParameterConstraint : DnfConstraint
{
    private readonly AnimatorConditionRule _template;
    private readonly int _minValue;
    private readonly int _valueCount;
    private readonly ulong[] _allowedValues;

    public override object Key => _template.ConstraintKey;
    public override bool IsEmpty => _allowedValues.All(word => word == 0);
    public override bool IsUniversal
    {
        get
        {
            var fullWords = _valueCount / 64;
            for (var index = 0; index < fullWords; index++)
            {
                if (_allowedValues[index] != ulong.MaxValue) return false;
            }

            var remainder = _valueCount % 64;
            return remainder == 0 || _allowedValues[fullWords] == (1UL << remainder) - 1;
        }
    }

    private FiniteParameterConstraint(
        AnimatorConditionRule template,
        int minValue,
        int valueCount,
        ulong[] allowedValues)
    {
        _template = template;
        _minValue = minValue;
        _valueCount = valueCount;
        _allowedValues = allowedValues;
    }

    public static FiniteParameterConstraint FromBoolRule(AnimatorConditionRule rule)
    {
        var bits = rule.Condition.mode switch
        {
            AnimatorConditionMode.If => 0b10UL,
            AnimatorConditionMode.IfNot => 0b01UL,
            _ => throw new InvalidOperationException($"Invalid bool condition mode: {rule.Condition.mode}")
        };
        return new FiniteParameterConstraint(rule, 0, 2, new[] { bits });
    }

    public static FiniteParameterConstraint FromIntRule(AnimatorConditionRule rule, IntParameterDomain domain)
    {
        var valueCount = domain.MaxValue - domain.MinValue + 1;
        var allowedValues = new ulong[(valueCount + 63) / 64];
        var threshold = (int)rule.Condition.threshold;
        for (var value = domain.MinValue; value <= domain.MaxValue; value++)
        {
            var allowed = rule.Condition.mode switch
            {
                AnimatorConditionMode.Equals => value == threshold,
                AnimatorConditionMode.NotEqual => value != threshold,
                AnimatorConditionMode.Greater => value > threshold,
                AnimatorConditionMode.Less => value < threshold,
                _ => throw new InvalidOperationException($"Invalid int condition mode: {rule.Condition.mode}")
            };
            if (allowed) Set(allowedValues, value - domain.MinValue);
        }

        return new FiniteParameterConstraint(rule, domain.MinValue, valueCount, allowedValues);
    }

    public override DnfConstraint Intersect(DnfConstraint other)
    {
        var right = Validate(other);
        var values = new ulong[_allowedValues.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = _allowedValues[index] & right._allowedValues[index];
        }
        return new FiniteParameterConstraint(_template, _minValue, _valueCount, values);
    }

    public override bool TryUnion(
        DnfConstraint other,
        [NotNullWhen(true)] out DnfConstraint? union)
    {
        var right = Validate(other);
        var values = new ulong[_allowedValues.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = _allowedValues[index] | right._allowedValues[index];
        }
        union = new FiniteParameterConstraint(_template, _minValue, _valueCount, values);
        return true;
    }

    public override DnfCondition Complement()
    {
        var values = new ulong[_allowedValues.Length];
        for (var index = 0; index < values.Length; index++) values[index] = ~_allowedValues[index];
        MaskUnusedBits(values);
        return DnfCondition.FromConstraint(new FiniteParameterConstraint(
            _template,
            _minValue,
            _valueCount,
            values));
    }

    public override bool Contains(DnfConstraint other)
    {
        var right = Validate(other);
        for (var index = 0; index < _allowedValues.Length; index++)
        {
            if ((_allowedValues[index] | right._allowedValues[index]) != _allowedValues[index]) return false;
        }
        return true;
    }

    public override bool SetEquals(DnfConstraint other)
    {
        return other is FiniteParameterConstraint constraint &&
               Equals(Key, constraint.Key) &&
               _minValue == constraint._minValue &&
               _valueCount == constraint._valueCount &&
               _allowedValues.SequenceEqual(constraint._allowedValues);
    }

    public override int CompareTo(DnfConstraint other)
    {
        if (other is not FiniteParameterConstraint constraint) return StringComparer.Ordinal.Compare(GetType().Name, other.GetType().Name);
        for (var index = 0; index < Math.Min(_allowedValues.Length, constraint._allowedValues.Length); index++)
        {
            var comparison = _allowedValues[index].CompareTo(constraint._allowedValues[index]);
            if (comparison != 0) return comparison;
        }
        return _allowedValues.Length.CompareTo(constraint._allowedValues.Length);
    }

    public override int GetSemanticHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var value in _allowedValues) hash.Add(value);
        return hash.ToHashCode();
    }

    public override IEnumerable<DnfRule> ToRules()
    {
        if (IsEmpty || IsUniversal) yield break;

        var allowed = Enumerable.Range(_minValue, _valueCount)
            .Where(value => IsSet(value - _minValue))
            .ToArray();
        if (_template.ParameterType == AnimatorControllerParameterType.Bool)
        {
            yield return WithCondition(
                allowed[0] == 0 ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If,
                0);
            yield break;
        }

        if (allowed.Length == 1)
        {
            yield return WithCondition(AnimatorConditionMode.Equals, allowed[0]);
            yield break;
        }

        var boundedRules = new List<DnfRule>();
        if (allowed[0] > _minValue)
            boundedRules.Add(WithCondition(AnimatorConditionMode.Greater, allowed[0] - 1));
        if (allowed[^1] < _minValue + _valueCount - 1)
            boundedRules.Add(WithCondition(AnimatorConditionMode.Less, allowed[^1] + 1));
        for (var value = allowed[0] + 1; value < allowed[^1]; value++)
        {
            if (!IsSet(value - _minValue))
                boundedRules.Add(WithCondition(AnimatorConditionMode.NotEqual, value));
        }

        var excluded = Enumerable.Range(_minValue, _valueCount)
            .Where(value => !IsSet(value - _minValue))
            .ToArray();
        if (excluded.Length <= boundedRules.Count)
        {
            foreach (var value in excluded)
                yield return WithCondition(AnimatorConditionMode.NotEqual, value);
        }
        else
        {
            foreach (var rule in boundedRules) yield return rule;
        }
    }

    private FiniteParameterConstraint Validate(DnfConstraint other)
    {
        if (other is not FiniteParameterConstraint constraint ||
            constraint._template.ParameterName != _template.ParameterName ||
            constraint._template.ParameterType != _template.ParameterType ||
            constraint._minValue != _minValue ||
            constraint._valueCount != _valueCount)
        {
            throw new InvalidOperationException("Cannot combine constraints from different parameter domains.");
        }
        return constraint;
    }

    private AnimatorConditionRule WithCondition(AnimatorConditionMode mode, int threshold)
    {
        return _template with
        {
            Condition = _template.Condition with
            {
                mode = mode,
                threshold = threshold
            }
        };
    }

    private bool IsSet(int index)
    {
        return (_allowedValues[index / 64] & (1UL << (index % 64))) != 0;
    }

    private void MaskUnusedBits(ulong[] values)
    {
        var remainder = _valueCount % 64;
        if (remainder != 0) values[^1] &= (1UL << remainder) - 1;
    }

    private static void Set(ulong[] values, int index)
    {
        values[index / 64] |= 1UL << (index % 64);
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

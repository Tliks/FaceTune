#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Serializable]
[Obsolete("Legacy serialized data retained only for migration.")]
internal abstract record class LegacyCondition // Immutable
{
    internal abstract LegacyCondition ToNegation();
}

[Serializable]
[Obsolete("Legacy serialized data retained only for migration.")]
internal record class LegacyHandGestureCondition : LegacyCondition // Immutable
{
    [SerializeField] private LegacyHand hand;
    public LegacyHand Hand { get => hand; init => hand = value; }
    public const string HandPropName = nameof(hand);

    [SerializeField] private LegacyEqualityComparison equalityComparison;
    public LegacyEqualityComparison EqualityComparison { get => equalityComparison; init => equalityComparison = value; }
    public const string EqualityComparisonPropName = nameof(equalityComparison);
    
    [SerializeField] private LegacyHandGesture handGesture;
    public LegacyHandGesture HandGesture { get => handGesture; init => handGesture = value; }
    public const string HandGesturePropName = nameof(handGesture);

    public LegacyHandGestureCondition()
    {
        hand = LegacyHand.Left;
        equalityComparison = LegacyEqualityComparison.Equal;
        handGesture = LegacyHandGesture.Fist;
    }

    public LegacyHandGestureCondition(LegacyHand hand, LegacyEqualityComparison equalityComparison, LegacyHandGesture handGesture)
    {
        this.hand = hand;
        this.equalityComparison = equalityComparison;
        this.handGesture = handGesture;
    }

    internal override LegacyCondition ToNegation()
    {
        return this with { equalityComparison = equalityComparison.Negate() };
    }

    public virtual bool Equals(LegacyHandGestureCondition other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return hand == other.hand
            && equalityComparison == other.equalityComparison
            && handGesture == other.handGesture;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(hand, equalityComparison, handGesture);
    }
}

[Serializable]
[Obsolete("Legacy serialized data retained only for migration.")]
internal record class LegacyParameterCondition : LegacyCondition // Immutable
{
    [SerializeField] private string parameterName;
    public string ParameterName { get => parameterName; init => parameterName = value; }
    public const string ParameterNamePropName = nameof(parameterName);

    [SerializeField] private LegacyParameterType parameterType;
    public LegacyParameterType ParameterType { get => parameterType; init => parameterType = value; }
    public const string ParameterTypePropName = nameof(parameterType);

    [SerializeField] private LegacyComparisonType comparisonType;
    public LegacyComparisonType ComparisonType { get => comparisonType; init => comparisonType = value; }
    public const string ComparisonTypePropName = nameof(comparisonType);

    [SerializeField] private float floatValue;
    public float FloatValue { get => floatValue; init => floatValue = value; }
    public const string FloatValuePropName = nameof(floatValue);

    [SerializeField] private int intValue;
    public int IntValue { get => intValue; init => intValue = value; }
    public const string IntValuePropName = nameof(intValue);

    [SerializeField] private bool boolValue;
    public bool BoolValue { get => boolValue; init => boolValue = value; }
    public const string BoolValuePropName = nameof(boolValue);

    public LegacyParameterCondition()
    {
        parameterName = string.Empty;
        parameterType = LegacyParameterType.Int;
        comparisonType = LegacyComparisonType.Equal;
        floatValue = 0;
        intValue = 0;
        boolValue = false;
    }

    public LegacyParameterCondition(string parameterName, LegacyParameterType parameterType, LegacyComparisonType comparisonType, float floatValue, int intValue, bool boolValue)
    {
        this.parameterName = parameterName;
        this.parameterType = parameterType;
        this.comparisonType = comparisonType;
        this.floatValue = floatValue;
        this.intValue = intValue;
        this.boolValue = boolValue;
    }

    public static LegacyParameterCondition Float(string parameterName, LegacyComparisonType comparisonType, float floatValue)
    {
        if (comparisonType != LegacyComparisonType.GreaterThan && comparisonType != LegacyComparisonType.LessThan)
        {
            throw new ArgumentException("Comparison type must be GreaterThan or LessThan for float parameters");
        }
        return new LegacyParameterCondition()
        {
            parameterName = parameterName,
            parameterType = LegacyParameterType.Float,
            comparisonType = comparisonType,
            floatValue = floatValue
        };
    }

    public static LegacyParameterCondition Int(string parameterName, LegacyComparisonType comparisonType, int intValue)
    {
        return new LegacyParameterCondition()
        {
            parameterName = parameterName,
            parameterType = LegacyParameterType.Int,
            comparisonType = comparisonType,
            intValue = intValue
        };
    }

    public static LegacyParameterCondition Bool(string parameterName, bool boolValue)
    {
        return new LegacyParameterCondition()
        {
            parameterName = parameterName,
            parameterType = LegacyParameterType.Bool,
            boolValue = boolValue
        };
    }

    internal override LegacyCondition ToNegation()
    {
        switch (parameterType)
        {
            case LegacyParameterType.Float:
                var (newType_float, newValue_float) = comparisonType.Negate(floatValue);
                return this with { 
                    comparisonType = newType_float, 
                    floatValue = newValue_float 
                };
            case LegacyParameterType.Int:
                var (newType_int, newValue_int) = comparisonType.Negate(intValue);
                return this with { 
                    comparisonType = newType_int, 
                    intValue = newValue_int 
                };
            case LegacyParameterType.Bool:
                return this with { boolValue = !boolValue };
            default:
                throw new InvalidOperationException($"Invalid parameter type: {parameterType}");
        }
    }

    public virtual bool Equals(LegacyParameterCondition other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return parameterName == other.parameterName
            && parameterType == other.parameterType
            && comparisonType == other.comparisonType
            && floatValue == other.floatValue
            && intValue == other.intValue
            && boolValue == other.boolValue;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(parameterName, parameterType, comparisonType, floatValue, intValue, boolValue);
    }
}

#pragma warning restore CS0618

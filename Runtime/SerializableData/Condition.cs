namespace Aoyon.FaceTune;

[Serializable]
public abstract record class Condition // Immutable
{
    internal abstract Condition ToNegation();
}

[Serializable]
public record class HandGestureCondition : Condition // Immutable
{
    [SerializeField] private Hand hand;
    public Hand Hand { get => hand; init => hand = value; }
    public const string HandPropName = nameof(hand);

    [SerializeField] private EqualityComparison equalityComparison;
    public EqualityComparison EqualityComparison { get => equalityComparison; init => equalityComparison = value; }
    public const string EqualityComparisonPropName = nameof(equalityComparison);
    
    [SerializeField] private HandGesture handGesture;
    public HandGesture HandGesture { get => handGesture; init => handGesture = value; }
    public const string HandGesturePropName = nameof(handGesture);

    public HandGestureCondition()
    {
        hand = Hand.Left;
        equalityComparison = EqualityComparison.Equal;
        handGesture = HandGesture.Fist;
    }

    public HandGestureCondition(Hand hand, EqualityComparison equalityComparison, HandGesture handGesture)
    {
        this.hand = hand;
        this.equalityComparison = equalityComparison;
        this.handGesture = handGesture;
    }

    internal override Condition ToNegation()
    {
        return this with { equalityComparison = equalityComparison.Negate() };
    }

    public virtual bool Equals(HandGestureCondition other)
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
public record class ParameterCondition : Condition // Immutable
{
    [SerializeField] private string parameterName;
    public string ParameterName { get => parameterName; init => parameterName = value; }
    public const string ParameterNamePropName = nameof(parameterName);

    [SerializeField] private ParameterType parameterType;
    public ParameterType ParameterType { get => parameterType; init => parameterType = value; }
    public const string ParameterTypePropName = nameof(parameterType);

    [SerializeField] private ComparisonType comparisonType;
    public ComparisonType ComparisonType { get => comparisonType; init => comparisonType = value; }
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

    public ParameterCondition()
    {
        parameterName = string.Empty;
        parameterType = ParameterType.Int;
        comparisonType = ComparisonType.Equal;
        floatValue = 0;
        intValue = 0;
        boolValue = false;
    }

    public ParameterCondition(string parameterName, ParameterType parameterType, ComparisonType comparisonType, float floatValue, int intValue, bool boolValue)
    {
        this.parameterName = parameterName;
        this.parameterType = parameterType;
        this.comparisonType = comparisonType;
        this.floatValue = floatValue;
        this.intValue = intValue;
        this.boolValue = boolValue;
    }

    public static ParameterCondition Float(string parameterName, ComparisonType comparisonType, float floatValue)
    {
        if (comparisonType != ComparisonType.GreaterThan && comparisonType != ComparisonType.LessThan)
        {
            throw new ArgumentException("Comparison type must be GreaterThan or LessThan for float parameters");
        }
        return new ParameterCondition()
        {
            parameterName = parameterName,
            parameterType = ParameterType.Float,
            comparisonType = comparisonType,
            floatValue = floatValue
        };
    }

    public static ParameterCondition Int(string parameterName, ComparisonType comparisonType, int intValue)
    {
        return new ParameterCondition()
        {
            parameterName = parameterName,
            parameterType = ParameterType.Int,
            comparisonType = comparisonType,
            intValue = intValue
        };
    }

    public static ParameterCondition Bool(string parameterName, bool boolValue)
    {
        return new ParameterCondition()
        {
            parameterName = parameterName,
            parameterType = ParameterType.Bool,
            boolValue = boolValue
        };
    }

    internal override Condition ToNegation()
    {
        switch (parameterType)
        {
            case ParameterType.Float:
                var (newType_float, newValue_float) = comparisonType.Negate(floatValue);
                return this with { 
                    comparisonType = newType_float, 
                    floatValue = newValue_float 
                };
            case ParameterType.Int:
                var (newType_int, newValue_int) = comparisonType.Negate(intValue);
                return this with { 
                    comparisonType = newType_int, 
                    intValue = newValue_int 
                };
            case ParameterType.Bool:
                return this with { boolValue = !boolValue };
            default:
                throw new InvalidOperationException($"Invalid parameter type: {parameterType}");
        }
    }

    public virtual bool Equals(ParameterCondition other)
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
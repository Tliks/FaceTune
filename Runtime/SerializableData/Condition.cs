namespace com.aoyon.facetune;

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

    [SerializeField] private BoolComparisonType comparisonType;
    public BoolComparisonType ComparisonType { get => comparisonType; init => comparisonType = value; }
    public const string ComparisonTypePropName = nameof(comparisonType);

    [SerializeField] private HandGesture handGesture;
    public HandGesture HandGesture { get => handGesture; init => handGesture = value; }
    public const string HandGesturePropName = nameof(handGesture);

    public HandGestureCondition()
    {
        hand = Hand.Left;
        comparisonType = BoolComparisonType.Equal;
        handGesture = HandGesture.Fist;
    }

    public HandGestureCondition(Hand hand, BoolComparisonType comparisonType, HandGesture handGesture)
    {
        this.hand = hand;
        this.comparisonType = comparisonType;
        this.handGesture = handGesture;
    }

    internal override Condition ToNegation()
    {
        return this with { comparisonType = comparisonType.Negate() };
    }

    public virtual bool Equals(HandGestureCondition other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return hand == other.hand
            && comparisonType == other.comparisonType
            && handGesture == other.handGesture;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(hand, comparisonType, handGesture);
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

    [SerializeField] private FloatComparisonType floatComparisonType;
    public FloatComparisonType FloatComparisonType { get => floatComparisonType; init => floatComparisonType = value; }
    public const string FloatComparisonTypePropName = nameof(floatComparisonType);

    [SerializeField] private IntComparisonType intComparisonType;
    public IntComparisonType IntComparisonType { get => intComparisonType; init => intComparisonType = value; }
    public const string IntComparisonTypePropName = nameof(intComparisonType);

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
        floatComparisonType = FloatComparisonType.GreaterThan;
        intComparisonType = IntComparisonType.Equal;
        floatValue = 0;
        intValue = 0;
        boolValue = false;
    }

    public ParameterCondition(string parameterName, ParameterType parameterType, FloatComparisonType floatComparisonType, IntComparisonType intComparisonType, float floatValue, int intValue, bool boolValue)
    {
        this.parameterName = parameterName;
        this.parameterType = parameterType;
        this.floatComparisonType = floatComparisonType;
        this.intComparisonType = intComparisonType;
        this.floatValue = floatValue;
        this.intValue = intValue;
        this.boolValue = boolValue;
    }

    public static ParameterCondition Float(string parameterName, FloatComparisonType comparisonType, float floatValue)
    {
        return new ParameterCondition()
        {
            parameterName = parameterName,
            parameterType = ParameterType.Float,
            floatComparisonType = comparisonType,
            floatValue = floatValue
        };
    }

    public static ParameterCondition Int(string parameterName, IntComparisonType comparisonType, int intValue)
    {
        return new ParameterCondition()
        {
            parameterName = parameterName,
            parameterType = ParameterType.Int,
            intComparisonType = comparisonType,
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
                var (newType_float, newValue_float) = floatComparisonType.Negate(floatValue);
                return this with { 
                    floatComparisonType = newType_float, 
                    floatValue = newValue_float 
                };
            case ParameterType.Int:
                var (newType_int, newValue_int) = intComparisonType.Negate(intValue);
                return this with { 
                    intComparisonType = newType_int, 
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
            && floatComparisonType == other.floatComparisonType
            && intComparisonType == other.intComparisonType
            && floatValue == other.floatValue
            && intValue == other.intValue
            && boolValue == other.boolValue;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(parameterName, parameterType, floatComparisonType, intComparisonType, floatValue, intValue, boolValue);
    }
}
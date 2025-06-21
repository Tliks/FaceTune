namespace com.aoyon.facetune;

[Serializable]
public abstract record class Condition // Immutable
{
    internal abstract Condition ToNegation();
}

[Serializable]
public record class HandGestureCondition : Condition // Immutable
{
    [SerializeField] private Hand _hand;
    public const string HandPropName = "_hand";
    public Hand Hand { get => _hand; init => _hand = value; }

    [SerializeField] private BoolComparisonType _comparisonType;
    public const string ComparisonTypePropName = "_comparisonType";
    public BoolComparisonType ComparisonType { get => _comparisonType; init => _comparisonType = value; }

    [SerializeField] private HandGesture _handGesture;
    public const string HandGesturePropName = "_handGesture";
    public HandGesture HandGesture { get => _handGesture; init => _handGesture = value; }

    public HandGestureCondition()
    {
        _hand = Hand.Left;
        _comparisonType = BoolComparisonType.Equal;
        _handGesture = HandGesture.Fist;
    }

    public HandGestureCondition(Hand hand, BoolComparisonType comparisonType, HandGesture handGesture)
    {
        _hand = hand;
        _comparisonType = comparisonType;
        _handGesture = handGesture;
    }

    internal override Condition ToNegation()
    {
        return this with { _comparisonType = _comparisonType.Negate() };
    }

    public virtual bool Equals(HandGestureCondition other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _hand == other._hand
            && _comparisonType == other._comparisonType
            && _handGesture == other._handGesture;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_hand, _comparisonType, _handGesture);
    }
}

[Serializable]
public record class ParameterCondition : Condition // Immutable
{
    [SerializeField] private string _parameterName;
    public const string ParameterNamePropName = "_parameterName";
    public string ParameterName { get => _parameterName; init => _parameterName = value; }

    [SerializeField] private ParameterType _parameterType;
    public const string ParameterTypePropName = "_parameterType";
    public ParameterType ParameterType { get => _parameterType; init => _parameterType = value; }

    [SerializeField] private FloatComparisonType _floatComparisonType;
    public const string FloatComparisonTypePropName = "_floatComparisonType";
    public FloatComparisonType FloatComparisonType { get => _floatComparisonType; init => _floatComparisonType = value; }

    [SerializeField] private IntComparisonType _intComparisonType;
    public const string IntComparisonTypePropName = "_intComparisonType";
    public IntComparisonType IntComparisonType { get => _intComparisonType; init => _intComparisonType = value; }

    [SerializeField] private float _floatValue;
    public const string FloatValuePropName = "_floatValue";
    public float FloatValue { get => _floatValue; init => _floatValue = value; }

    [SerializeField] private int _intValue;
    public const string IntValuePropName = "_intValue";
    public int IntValue { get => _intValue; init => _intValue = value; }

    [SerializeField] private bool _boolValue;
    public const string BoolValuePropName = "_boolValue";
    public bool BoolValue { get => _boolValue; init => _boolValue = value; }

    public ParameterCondition()
    {
        _parameterName = string.Empty;
        _parameterType = ParameterType.Int;
        _floatComparisonType = FloatComparisonType.GreaterThan;
        _intComparisonType = IntComparisonType.Equal;
        _floatValue = 0;
        _intValue = 0;
        _boolValue = false;
    }

    public ParameterCondition(string parameterName, ParameterType parameterType, FloatComparisonType floatComparisonType, IntComparisonType intComparisonType, float floatValue, int intValue, bool boolValue)
    {
        _parameterName = parameterName;
        _parameterType = parameterType;
        _floatComparisonType = floatComparisonType;
        _intComparisonType = intComparisonType;
        _floatValue = floatValue;
        _intValue = intValue;
        _boolValue = boolValue;
    }

    public static ParameterCondition Float(string parameterName, FloatComparisonType comparisonType, float floatValue)
    {
        return new ParameterCondition()
        {
            _parameterName = parameterName,
            _parameterType = ParameterType.Float,
            _floatComparisonType = comparisonType,
            _floatValue = floatValue
        };
    }

    public static ParameterCondition Int(string parameterName, IntComparisonType comparisonType, int intValue)
    {
        return new ParameterCondition()
        {
            _parameterName = parameterName,
            _parameterType = ParameterType.Int,
            _intComparisonType = comparisonType,
            _intValue = intValue
        };
    }

    public static ParameterCondition Bool(string parameterName, bool boolValue)
    {
        return new ParameterCondition()
        {
            _parameterName = parameterName,
            _parameterType = ParameterType.Bool,
            _boolValue = boolValue
        };
    }

    internal override Condition ToNegation()
    {
        switch (_parameterType)
        {
            case ParameterType.Float:
                var (newType_float, newValue_float) = _floatComparisonType.Negate(_floatValue);
                return this with { 
                    _floatComparisonType = newType_float, 
                    _floatValue = newValue_float 
                };
            case ParameterType.Int:
                var (newType_int, newValue_int) = _intComparisonType.Negate(_intValue);
                return this with { 
                    _intComparisonType = newType_int, 
                    _intValue = newValue_int 
                };
            case ParameterType.Bool:
                return this with { _boolValue = !_boolValue };
            default:
                throw new InvalidOperationException($"Invalid parameter type: {_parameterType}");
        }
    }

    public virtual bool Equals(ParameterCondition other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _parameterName == other._parameterName
            && _parameterType == other._parameterType
            && _floatComparisonType == other._floatComparisonType
            && _intComparisonType == other._intComparisonType
            && _floatValue == other._floatValue
            && _intValue == other._intValue
            && _boolValue == other._boolValue;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_parameterName, _parameterType, _floatComparisonType, _intComparisonType, _floatValue, _intValue, _boolValue);
    }
}
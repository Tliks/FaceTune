namespace com.aoyon.facetune;

[Serializable]
public abstract record class Condition
{
    internal abstract Condition Negate();
}

[Serializable]
public record class HandGestureCondition : Condition
{
    public Hand Hand = Hand.Left;
    public BoolComparisonType ComparisonType = BoolComparisonType.Equal;
    public HandGesture HandGesture = HandGesture.Fist;

    public HandGestureCondition()
    {
    }

    internal override Condition Negate()
    {
        ComparisonType = ComparisonType.Negate();
        return this;
    }
}

[Serializable]
public record class ParameterCondition : Condition
{
    public string ParameterName = string.Empty;
    public ParameterType ParameterType = ParameterType.Int;

    public FloatComparisonType FloatComparisonType = FloatComparisonType.GreaterThan;
    public IntComparisonType IntComparisonType = IntComparisonType.Equal;
    public float FloatValue = 0;
    public int IntValue = 0;
    public bool BoolValue = false;

    public ParameterCondition()
    {
    }

    public ParameterCondition(string parameterName, FloatComparisonType comparisonType, float floatValue)
    {
        ParameterName = parameterName;
        ParameterType = ParameterType.Float;
        FloatComparisonType = comparisonType;
        FloatValue = floatValue;
    }

    public ParameterCondition(string parameterName, IntComparisonType comparisonType, int intValue)
    {
        ParameterName = parameterName;
        ParameterType = ParameterType.Int;
        IntComparisonType = comparisonType;
        IntValue = intValue;
    }

    public ParameterCondition(string parameterName, bool boolValue)
    {
        ParameterName = parameterName;
        ParameterType = ParameterType.Bool;
        BoolValue = boolValue;
    }

    internal override Condition Negate()
    {
        switch (ParameterType)
        {
            case ParameterType.Float:
                FloatComparisonType = FloatComparisonType.Negate();
                break;
            case ParameterType.Int:
                (IntComparisonType, IntValue) = ConditionUtility.Negate(IntComparisonType, IntValue);
                break;
            case ParameterType.Bool:
                BoolValue = !BoolValue;
                break;
        }
        return this;
    }
}

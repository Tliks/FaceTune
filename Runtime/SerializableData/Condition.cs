namespace com.aoyon.facetune;

[Serializable]
public record class Condition
{
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
}

namespace com.aoyon.facetune;

internal record class Condition
{
}

internal record class HandGestureCondition : Condition
{
    public Hand Hand { get; private set; }
    public BoolComparisonType ComparisonType { get; private set; }
    public HandGesture HandGesture { get; private set; }

    public HandGestureCondition(Hand hand, BoolComparisonType comparisonType, HandGesture handGesture)
    {
        Hand = hand;
        ComparisonType = comparisonType;
        HandGesture = handGesture;
    }
}

internal record class ParameterConditionBase : Condition
{
    public string ParameterName { get; private set; }

    public ParameterConditionBase(string parameterName)
    {
        ParameterName = parameterName;
    }
}

internal record class IntParameterCondition : ParameterConditionBase
{
    public ComparisonType ComparisonType { get; private set; }
    public int Value { get; private set; }

    public IntParameterCondition(string parameterName, ComparisonType comparisonType, int value)
        : base(parameterName)
    {
        ComparisonType = comparisonType;
        Value = value;
    }
}

internal record class FloatParameterCondition : ParameterConditionBase
{
    public ComparisonType ComparisonType { get; private set; }
    public float Value { get; private set; }

    public FloatParameterCondition(string parameterName, ComparisonType comparisonType, float value)
        : base(parameterName)
    {
        ComparisonType = comparisonType;
        Value = value;
    }
}

internal record class BoolParameterCondition : ParameterConditionBase
{
    public BoolComparisonType ComparisonType { get; private set; }
    public bool Value { get; private set; }

    public BoolParameterCondition(string parameterName, BoolComparisonType comparisonType, bool value)
        : base(parameterName)
    {
        ComparisonType = comparisonType;
        Value = value;
    }
}
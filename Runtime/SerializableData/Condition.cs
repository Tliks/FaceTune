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

    public ComparisonType ComparisonType = ComparisonType.GreaterThan;
    public float FloatValue = 0;
    public int IntValue = 0;
    public bool BoolValue = false;

    public ParameterCondition()
    {
    }
}

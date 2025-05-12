namespace com.aoyon.facetune;

internal interface IConditionProvider
{
    Condition ToCondition();
}

internal struct Condition
{
    public ConditionType Type = default;

    public HandGestureCondition HandGestureCondition = default;
    public ParameterCondition ParameterCondition = default;

    public Condition(Hand hand, HandGesture handGesture)
    {
        Type = ConditionType.HandGesture;
        HandGestureCondition = new HandGestureCondition(hand, handGesture);
    }

    public Condition(Parameter parameter)
    {
        Type = ConditionType.Parameter;
        ParameterCondition = new ParameterCondition(parameter);
    }
}

internal enum ConditionType
{
    HandGesture,
    Parameter
}

internal struct HandGestureCondition
{
    public Hand Hand;
    public HandGesture HandGesture;

    public HandGestureCondition(Hand hand, HandGesture handGesture)
    {
        Hand = hand;
        HandGesture = handGesture;
    }
}

internal struct ParameterCondition
{
    public Parameter Parameter;

    public ParameterCondition(Parameter parameter)
    {
        Parameter = parameter;
    }
}

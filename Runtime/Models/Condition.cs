using UnityEngine.Serialization;

namespace Aoyon.FaceTune;

[Serializable]
internal class Condition
{
    public bool Always = true;
    public List<ConditionCase> Cases = new();

    public Condition()
    {
    }

    public Condition(bool always, IEnumerable<ConditionCase> cases)
    {
        Always = always;
        Cases = cases.ToList();
    }
}

[Serializable]
internal class ConditionCase
{
    public List<HandGestureCondition> HandGestureConditions = new();
    public List<MenuConditon> MenuConditons = new();
    public List<ParameterCondition> ParameterConditions = new();

    public ConditionCase()
    {
    }

    public ConditionCase(IEnumerable<HandGestureCondition> handGestureConditions, IEnumerable<ParameterCondition> parameterConditions)
    {
        HandGestureConditions = handGestureConditions.ToList();
        ParameterConditions = parameterConditions.ToList();
    }
}

[Serializable]
internal class HandGestureCondition
{
    public HandGestureMatch Match = HandGestureMatch.LeftHand;

    [FormerlySerializedAs("handGesture")]
    public HandGesture HandGesture = HandGesture.Fist;

    [Obsolete("Use Match")]
    public Hand hand = Hand.Left;

    [Obsolete("Use Match")]
    public EqualityComparison equalityComparison = EqualityComparison.Equal;

    public HandGestureCondition()
    {
    }

    public HandGestureCondition(HandGesture handGesture, HandGestureMatch match)
    {
        HandGesture = handGesture;
        Match = match;
    }
}

[Serializable]
internal class MenuConditon
{
    public MenuComponent? MenuSource = null;

    public MenuConditon()
    {
    }
}

[Serializable]
internal class ParameterCondition
{
    [FormerlySerializedAs("parameterName")]
    public string ParameterName = string.Empty;

    [FormerlySerializedAs("parameterType")]
    public ParameterType ParameterType = ParameterType.Int;

    [FormerlySerializedAs("comparisonType")]
    public ComparisonType ComparisonType = ComparisonType.Equal;

    [FormerlySerializedAs("floatValue")]
    public float FloatValue;

    [FormerlySerializedAs("intValue")]
    public int IntValue;

    [FormerlySerializedAs("boolValue")]
    public bool BoolValue;

    public ParameterCondition()
    {
    }

    public ParameterCondition(string parameterName, ParameterType parameterType, ComparisonType comparisonType, float floatValue, int intValue, bool boolValue)
    {
        ParameterName = parameterName;
        ParameterType = parameterType;
        ComparisonType = comparisonType;
        FloatValue = floatValue;
        IntValue = intValue;
        BoolValue = boolValue;
    }

    public static ParameterCondition Float(string parameterName, ComparisonType comparisonType, float floatValue)
    {
        if (comparisonType != ComparisonType.GreaterThan && comparisonType != ComparisonType.LessThan)
        {
            throw new ArgumentException("Comparison type must be GreaterThan or LessThan for float parameters");
        }
        return new ParameterCondition
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Float,
            ComparisonType = comparisonType,
            FloatValue = floatValue
        };
    }

    public static ParameterCondition Int(string parameterName, ComparisonType comparisonType, int intValue)
    {
        // intは全ComparisonTypeを取れる
        return new ParameterCondition
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Int,
            ComparisonType = comparisonType,
            IntValue = intValue
        };
    }

    public static ParameterCondition Bool(string parameterName, bool boolValue)
    {
        // boolはComparisonType不要
        return new ParameterCondition
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Bool,
            ComparisonType = ComparisonType.Equal,
            BoolValue = boolValue
        };
    }
}


[Obsolete]
internal enum Hand
{
    Left,
    Right
}

internal enum HandGestureMatch
{
    LeftHand,
    RightHand,
    BothHands,
    AtLeastOneHand,
    ExactlyOneHand,
    NeitherHand
}

internal enum HandGesture
{
    Neutral,
    Fist,
    HandOpen,
    FingerPoint,
    Victory,
    RockNRoll,
    HandGun,
    ThumbsUp
}

internal enum ParameterType
{
    Int,
    Float,
    Bool
}

internal enum ComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan
}

internal enum EqualityComparison
{
    Equal,
    NotEqual
}

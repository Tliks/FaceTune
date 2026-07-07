using UnityEngine.Serialization;

namespace Aoyon.FaceTune;

[Serializable]
internal class Condition
{
    public bool Always = false;
    public List<ConditionCase> Cases = new();

    public bool IsEmpty => !Always && Cases.Count == 0;

    public Condition()
    {
    }

    public Condition(params ConditionCase[] conditionCases)
    {
        Always = false;
        Cases.AddRange(conditionCases);
    }
}

[Serializable]
internal class ConditionCase
{
    public List<HandGestureCondition> HandGestureConditions = new();
    [FormerlySerializedAs("MenuConditons")]
    public List<MenuCondition> MenuConditions = new();
    public List<ParameterCondition> ParameterConditions = new();

    public bool IsEmpty => HandGestureConditions.Count == 0 && MenuConditions.Count == 0 && ParameterConditions.Count == 0;

    public ConditionCase()
    {
    }

    public static ConditionCase From(params MenuCondition[] menuConditions)
    {
        return new ConditionCase
        {
            MenuConditions = menuConditions.ToList()
        };
    }

    public static ConditionCase From(params ParameterCondition[] parameterConditions)
    {
        return new ConditionCase
        {
            ParameterConditions = parameterConditions.ToList()
        };
    }

    public static ConditionCase From(params HandGestureCondition[] handGestureConditions)
    {
        return new ConditionCase
        {
            HandGestureConditions = handGestureConditions.ToList()
        };
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
}

internal enum MenuConditionMode
{
    // Toggle
    Enabled,
    Disabled,
    // Radial
    GreaterThan,
    LessThan
}

[Serializable]
internal class MenuCondition
{
    public MenuComponent? MenuSource = null;
    public MenuConditionMode Mode = MenuConditionMode.Enabled;
    public float Threshold = 0.5f;

    public MenuCondition()
    {
    }

    public static MenuCondition Enabled(MenuComponent menu)
    {
        return new MenuCondition
        {
            MenuSource = menu,
            Mode = MenuConditionMode.Enabled
        };
    }

    public static MenuCondition Disabled(MenuComponent menu)
    {
        return new MenuCondition
        {
            MenuSource = menu,
            Mode = MenuConditionMode.Disabled
        };
    }

    public static MenuCondition GreaterThan(MenuComponent menu, float threshold)
    {
        return new MenuCondition
        {
            MenuSource = menu,
            Mode = MenuConditionMode.GreaterThan,
            Threshold = threshold
        };
    }

    public static MenuCondition LessThan(MenuComponent menu, float threshold)
    {
        return new MenuCondition
        {
            MenuSource = menu,
            Mode = MenuConditionMode.LessThan,
            Threshold = threshold
        };
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

[Obsolete]
internal enum EqualityComparison
{
    Equal,
    NotEqual
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
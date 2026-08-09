using UnityEngine.Serialization;

namespace Aoyon.FaceTune;

/// <summary>親の条件だけを使うAlways、またはこのComponent自身のCondition。</summary>
[Serializable]
internal class ConditionSelection
{
    public enum Kind
    {
        Always,
        Conditional
    }

    public Kind Mode = Kind.Conditional;
    public Condition Condition = new();
}

/// <summary>Casesのいずれかが成立すれば成立する条件。</summary>
[Serializable]
internal class Condition
{
    public List<ConditionCase> Cases = new();

    public bool IsEmpty => Cases.Count == 0;

    public Condition()
    {
    }

    public Condition(params ConditionCase[] conditionCases)
    {
        Cases.AddRange(conditionCases);
    }
}

/// <summary>ConditionsをANDする一つのcase。</summary>
[Serializable]
internal class ConditionCase
{
    [SerializeReference]
    public List<ConditionBase> Conditions = new();

    public bool IsEmpty => Conditions.Count == 0;

    public ConditionCase()
    {
    }

    public static ConditionCase From(params MenuCondition[] menuConditions)
    {
        return new ConditionCase
        {
            Conditions = menuConditions.Cast<ConditionBase>().ToList()
        };
    }

    public static ConditionCase From(params ParameterCondition[] parameterConditions)
    {
        return new ConditionCase
        {
            Conditions = parameterConditions.Cast<ConditionBase>().ToList()
        };
    }

    public static ConditionCase From(params HandGestureCondition[] handGestureConditions)
    {
        return new ConditionCase
        {
            Conditions = handGestureConditions.Cast<ConditionBase>().ToList()
        };
    }
}

[Serializable]
internal abstract class ConditionBase
{
}


[Serializable]
internal sealed class HandGestureCondition : ConditionBase
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

/// <summary>Menu parameterの割当後にParameterConditionへ解決するalias。</summary>
[Serializable]
internal sealed class MenuCondition : ConditionBase
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
internal sealed class ParameterCondition : ConditionBase
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
    ExactlyOneHand,
    BothHands,
    AtLeastOneHand,
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
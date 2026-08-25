namespace Aoyon.FaceTune;

[Serializable]
internal class ConditionSelection
{
    public enum Kind
    {
        Always = 0,
        Conditional = 10
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
    public List<HandGestureCondition> HandGestureConditions = new();
    public List<MenuCondition> MenuConditions = new();
    public List<ParameterCondition> ParameterConditions = new();

    public bool IsEmpty => HandGestureConditions.Count == 0
                        && MenuConditions.Count == 0
                        && ParameterConditions.Count == 0;

    public IEnumerable<object> EnumerateConditions()
    {
        foreach (var condition in HandGestureConditions) yield return condition;
        foreach (var condition in MenuConditions) yield return condition;
        foreach (var condition in ParameterConditions) yield return condition;
    }

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

    public static ConditionCase From(params HandGestureCondition[] handGestureConditions)
    {
        return new ConditionCase
        {
            HandGestureConditions = handGestureConditions.ToList()
        };
    }
}

internal enum HandGestureHand
{
    Left = 0,
    Right = 10,
    Any = 20,
    Both = 30
}

[Serializable]
internal sealed class HandGestureCondition
{
    public HandGestureHand Hand = HandGestureHand.Left;
    public HandGesture Gesture = HandGesture.Fist;

    public bool Matches = true;

    public HandGestureCondition()
    {
    }
}

internal enum MenuConditionMode
{
    // Toggle
    Enabled = 0,
    Disabled = 10,
    // Radial
    GreaterThan = 20,
    LessThan = 30
}

/// <summary>Menu parameterの割当後にParameterConditionへ解決するalias。</summary>
[Serializable]
internal sealed class MenuCondition
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

}


[Serializable]
internal sealed class ParameterCondition
{
    public string ParameterName = string.Empty;
    public ParameterType ParameterType = ParameterType.Bool;
    public ComparisonType ComparisonType = ComparisonType.Equal;
    public float FloatValue = 0f;
    public int IntValue = 0;
    public bool BoolValue = true;

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
    Left = 0,

    Right = 10
}

internal enum HandGesture
{
    Neutral = 0,

    Fist = 10,
    HandOpen = 20,
    FingerPoint = 30,
    Victory = 40,
    RockNRoll = 50,
    HandGun = 60,
    ThumbsUp = 70
}

/// <summary>Serialized parameter type shared by current and legacy data.</summary>
internal enum ParameterType
{
    Int,
    Float,
    Bool
}

/// <summary>Serialized comparison shared by current and legacy data.</summary>
internal enum ComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan
}
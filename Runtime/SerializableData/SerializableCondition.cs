using UnityEngine.Serialization;

namespace Aoyon.FaceTune;

internal interface ISerializableCondition
{
    ICondition ToCondition(AvatarContext avatarContext);
}

[Serializable]
public record class HandGestureCondition : ISerializableCondition
{
    [FormerlySerializedAs("hand")]
    public Hand Hand;

    [FormerlySerializedAs("equalityComparison")]
    public EqualityComparison EqualityComparison;
    
    [FormerlySerializedAs("handGesture")]
    public HandGesture HandGesture;

    public HandGestureCondition()
    {
        Hand = Hand.Left;
        EqualityComparison = EqualityComparison.Equal;
        HandGesture = HandGesture.Fist;
    }

    public HandGestureCondition(Hand hand, EqualityComparison equalityComparison, HandGesture handGesture)
    {
        Hand = hand;
        EqualityComparison = equalityComparison;
        HandGesture = handGesture;
    }

    ICondition ISerializableCondition.ToCondition(AvatarContext avatarContext)
    {
        // TOdo: see platform
        return ProcessVRChatAvatar30();
    }
    
    // Todo: use PlatformSupport
    private const string VRCLeftHandGesture = "GestureLeft";
    private const string VRCRightHandGesture = "GestureRight";
    private ICondition ProcessVRChatAvatar30()
    {
        var comparisonType = EqualityComparison switch {
            EqualityComparison.Equal => ComparisonType.Equal,
            EqualityComparison.NotEqual => ComparisonType.NotEqual,
            _ => throw new InvalidOperationException($"Invalid equality comparison: {EqualityComparison}"),
        };
        return Hand switch
        {
            // enum indexをそのまま使える
            Hand.Left => new IntCondition(VRCLeftHandGesture, (int)HandGesture, comparisonType),
            Hand.Right => new IntCondition(VRCRightHandGesture, (int)HandGesture, comparisonType),
            _ => throw new InvalidOperationException($"Invalid hand: {Hand}"),
        };
    }
}

[Serializable]
public record class ParameterCondition : ISerializableCondition
{
    [FormerlySerializedAs("parameterName")]
    public string ParameterName;

    [FormerlySerializedAs("parameterType")]
    public ParameterType ParameterType;

    [FormerlySerializedAs("comparisonType")]
    public ComparisonType ComparisonType;

    [FormerlySerializedAs("floatValue")]
    public float FloatValue;

    [FormerlySerializedAs("intValue")]
    public int IntValue;

    [FormerlySerializedAs("boolValue")]
    public bool BoolValue;

    public ParameterCondition()
    {
        ParameterName = string.Empty;
        ParameterType = ParameterType.Int;
        ComparisonType = ComparisonType.Equal;
        FloatValue = 0;
        IntValue = 0;
        BoolValue = false;
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
        return new ParameterCondition()
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Float,
            ComparisonType = comparisonType,
            FloatValue = floatValue
        };
    }

    public static ParameterCondition Int(string parameterName, ComparisonType comparisonType, int intValue)
    {
        return new ParameterCondition()
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Int,
            ComparisonType = comparisonType,
            IntValue = intValue
        };
    }

    public static ParameterCondition Bool(string parameterName, bool boolValue)
    {
        return new ParameterCondition()
        {
            ParameterName = parameterName,
            ParameterType = ParameterType.Bool,
            BoolValue = boolValue
        };
    }

    ICondition ISerializableCondition.ToCondition(AvatarContext avatarContext)
    {
        return ParameterType switch
        {
            ParameterType.Float => new FloatCondition(ParameterName, FloatValue, ComparisonType),
            ParameterType.Int => new IntCondition(ParameterName, IntValue, ComparisonType),
            ParameterType.Bool => new BoolCondition(ParameterName, BoolValue),
            _ => throw new InvalidOperationException($"Invalid parameter type: {ParameterType}"),
        };
    }
}
namespace com.aoyon.facetune;

[Flags]
public enum HandGesture
{
    Neutral = 1 << 0,
    Fist = 1 << 1,
    HandOpen = 1 << 2,
    FingerPoint = 1 << 3,
    Victory = 1 << 4,
    RockNRoll = 1 << 5,
    HandGun = 1 << 6,
    ThumbsUp = 1 << 7
}

[Flags]
public enum Hand
{
    Left = 1 << 0,
    Right = 1 << 1
}

public enum PathType
{
    Absolute,
    Relative
}

public enum ParameterType
{
    Int,
    Float,
    Bool,
    Trigger
}
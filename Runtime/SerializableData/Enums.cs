namespace com.aoyon.facetune;

public enum HandGesture
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

public enum Hand
{
    Left,
    Right
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
    Bool
}

public enum ComparisonType
{
    GreaterThan,
    LessThan,
}

public enum BoolComparisonType
{
    Equal,
    NotEqual
}

public enum TrackingPermission
{
    Allow,
    Disallow,
    Keep
}

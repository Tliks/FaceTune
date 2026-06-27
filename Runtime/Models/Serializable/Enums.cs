namespace Aoyon.FaceTune;

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

public enum ParameterType
{
    Int,
    Float,
    Bool
}

public enum ComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan
}

public enum EqualityComparison
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

public enum ClipImportOption
{
    All,
    NonZero,
    FacialStyleOverridesOrNonZero
}

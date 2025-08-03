namespace aoyon.facetune;

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

public enum AnimationSourceMode
{
    Manual,
    AnimationClip
}
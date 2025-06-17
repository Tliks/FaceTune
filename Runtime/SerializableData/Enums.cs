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

public enum IntComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan
}

public enum FloatComparisonType
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

public enum BlendingPermission
{
    Allow,
    Disallow,
    Keep
}

public enum ClipExcludeOption
{
    None,
    ExcludeZeroWeight,
    ExcludeDefault
}

public enum AnimationSourceMode
{
    Manual,
    FromAnimationClip
}
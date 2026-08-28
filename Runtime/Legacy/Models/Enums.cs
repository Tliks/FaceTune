#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyHandGesture
{
    Neutral = 0,
    Fist = 1,
    HandOpen = 2,
    FingerPoint = 3,
    Victory = 4,
    RockNRoll = 5,
    HandGun = 6,
    ThumbsUp = 7
}

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyHand
{
    Left = 0,
    Right = 1
}



[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyEqualityComparison
{
    Equal = 0,
    NotEqual = 1
}


[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyClipImportOption
{
    All = 0,
    NonZero = 1,
    FacialStyleOverridesOrNonZero = 2
}

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyParameterType
{
    Int = 0,
    Float = 1,
    Bool = 2
}

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyComparisonType
{
    Equal = 0,
    NotEqual = 1,
    GreaterThan = 2,
    LessThan = 3
}

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyTrackingPermission
{
    Allow = 0,
    Disallow = 1,
    Keep = 2
}

#pragma warning restore CS0618

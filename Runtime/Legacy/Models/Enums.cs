#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyHandGesture
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

[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyHand
{
    Left,
    Right
}



[Obsolete("Legacy serialized data retained only for migration.")]
internal enum EqualityComparison
{
    Equal,
    NotEqual
}


[Obsolete("Legacy serialized data retained only for migration.")]
internal enum LegacyClipImportOption
{
    All,
    NonZero,
    FacialStyleOverridesOrNonZero
}

#pragma warning restore CS0618

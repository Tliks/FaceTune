#pragma warning disable CS0618

namespace Aoyon.FaceTune;

internal static class LegacyFaceTuneConstants
{
    internal const string AnimatedBlendShapePrefix = "blendShape.";
}

internal static class LegacyModelSupport
{
    internal static AnimationCurve Clone(this AnimationCurve curve)
        => new(curve.keys)
        {
            preWrapMode = curve.preWrapMode,
            postWrapMode = curve.postWrapMode
        };

    internal static int GetSequenceHashCode<T>(this IEnumerable<T> values)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in values) hash = hash * 31 + (value?.GetHashCode() ?? 0);
            return hash;
        }
    }

    internal static LegacyEqualityComparison Negate(this LegacyEqualityComparison value)
        => value == LegacyEqualityComparison.Equal
            ? LegacyEqualityComparison.NotEqual
            : LegacyEqualityComparison.Equal;

    internal static (LegacyComparisonType, int) Negate(this LegacyComparisonType value, int current)
        => value switch
        {
            LegacyComparisonType.Equal => (LegacyComparisonType.NotEqual, current),
            LegacyComparisonType.NotEqual => (LegacyComparisonType.Equal, current),
            LegacyComparisonType.GreaterThan => (LegacyComparisonType.LessThan, current + 1),
            LegacyComparisonType.LessThan => (LegacyComparisonType.GreaterThan, current - 1),
            _ => (value, current)
        };

    internal static (LegacyComparisonType, float) Negate(this LegacyComparisonType value, float current)
        => value switch
        {
            LegacyComparisonType.GreaterThan => (LegacyComparisonType.LessThan, current + 0.00001f),
            LegacyComparisonType.LessThan => (LegacyComparisonType.GreaterThan, current - 0.00001f),
            _ => (value, current)
        };
}

#pragma warning restore CS0618
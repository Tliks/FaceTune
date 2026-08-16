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

    internal static EqualityComparison Negate(this EqualityComparison value)
        => value == EqualityComparison.Equal ? EqualityComparison.NotEqual : EqualityComparison.Equal;

    internal static (ComparisonType, int) Negate(this ComparisonType value, int current)
        => value switch
        {
            ComparisonType.Equal => (ComparisonType.NotEqual, current),
            ComparisonType.NotEqual => (ComparisonType.Equal, current),
            ComparisonType.GreaterThan => (ComparisonType.LessThan, current + 1),
            ComparisonType.LessThan => (ComparisonType.GreaterThan, current - 1),
            _ => (value, current)
        };

    internal static (ComparisonType, float) Negate(this ComparisonType value, float current)
        => value switch
        {
            ComparisonType.GreaterThan => (ComparisonType.LessThan, current + 0.00001f),
            ComparisonType.LessThan => (ComparisonType.GreaterThan, current - 0.00001f),
            _ => (value, current)
        };
}

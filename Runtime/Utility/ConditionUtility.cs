namespace com.aoyon.facetune;

internal static class ConditionUtility
{
    public static (ComparisonType newType, int newValue) Negate(this ComparisonType type, int currentValue)
    {
        return type switch
        {
            ComparisonType.Equal => (ComparisonType.NotEqual, currentValue),
            ComparisonType.NotEqual => (ComparisonType.Equal, currentValue),
            ComparisonType.GreaterThan => (ComparisonType.LessThan, currentValue + 1),
            ComparisonType.LessThan => (ComparisonType.GreaterThan, currentValue - 1),
            _ => (type, currentValue)
        };
    }

    private const float FloatTolerance = 0.00001f;
    public static (ComparisonType newType, float newValue) Negate(this ComparisonType type, float currentValue)
    {
        return type switch
        {
            ComparisonType.GreaterThan => (ComparisonType.LessThan, currentValue + FloatTolerance), // Math.BitIncrement使えなくない？？
            ComparisonType.LessThan => (ComparisonType.GreaterThan, currentValue - FloatTolerance),
            _ => (type, currentValue)
        };
    }
}
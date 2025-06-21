namespace com.aoyon.facetune;

internal static class ConditionUtility
{
    public static (IntComparisonType newType, int newValue) Negate(this IntComparisonType type, int currentValue)
    {
        return type switch
        {
            IntComparisonType.Equal => (IntComparisonType.NotEqual, currentValue),
            IntComparisonType.NotEqual => (IntComparisonType.Equal, currentValue),
            IntComparisonType.GreaterThan => (IntComparisonType.LessThan, currentValue + 1),
            IntComparisonType.LessThan => (IntComparisonType.GreaterThan, currentValue - 1),
            _ => (type, currentValue)
        };
    }

    private const float FloatTolerance = 0.00001f;
    public static (FloatComparisonType newType, float newValue) Negate(this FloatComparisonType type, float currentValue)
    {
        return type switch
        {
            FloatComparisonType.GreaterThan => (FloatComparisonType.LessThan, currentValue + FloatTolerance), // Math.BitIncrement使えなくない？？
            FloatComparisonType.LessThan => (FloatComparisonType.GreaterThan, currentValue - FloatTolerance),
            _ => (type, currentValue)
        };
    }

    public static BoolComparisonType Negate(this BoolComparisonType type)
    {
        return type switch
        {
            BoolComparisonType.Equal => BoolComparisonType.NotEqual,
            BoolComparisonType.NotEqual => BoolComparisonType.Equal,
            _ => type
        };
    }
}
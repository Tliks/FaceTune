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

    public static FloatComparisonType Negate(this FloatComparisonType type)
    {
        return type switch
        {
            FloatComparisonType.GreaterThan => FloatComparisonType.LessThan,
            FloatComparisonType.LessThan => FloatComparisonType.GreaterThan,
            _ => type
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

    public static bool Negate(this bool value)
    {
        return !value;
    }
}
namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public sealed class ParameterConditionComponent : FaceTuneTagComponent, IConditionProvider
    {
        internal const string ComponentName = "FT Parameter Condition";
        internal const string MenuPath = FaceTune + "/" + Condition + "/" + ComponentName;

        public string ParameterName = string.Empty;
        public ParameterType ParameterType = ParameterType.Int;
        public ComparisonType ComparisonType = ComparisonType.GreaterThan;
        public BoolComparisonType BoolComparisonType = BoolComparisonType.Equal;
        public float FloatValue = 0;
        public int IntValue = 0;
        public bool BoolValue = false;

        Condition IConditionProvider.ToCondition()
        {
            switch (ParameterType)
            {
                case ParameterType.Float:
                    return new FloatParameterCondition(ParameterName, ComparisonType, FloatValue);
                case ParameterType.Int:
                    return new IntParameterCondition(ParameterName, ComparisonType, IntValue);
                case ParameterType.Bool:
                    return new BoolParameterCondition(ParameterName, BoolComparisonType, BoolValue);
                default:
                    throw new ArgumentException($"Invalid parameter type: {ParameterType}");
            }
        }
    }
}
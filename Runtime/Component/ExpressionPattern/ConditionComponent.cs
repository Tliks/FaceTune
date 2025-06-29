namespace aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Condition";
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();
    }
}
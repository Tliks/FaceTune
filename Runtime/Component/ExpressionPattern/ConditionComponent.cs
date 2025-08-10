namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Condition";
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();
    }
}
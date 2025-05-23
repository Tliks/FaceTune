namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class CommonConditionComponent : FaceTuneTagComponent, IModifyEarlyData
    {
        internal const string ComponentName = "FT Common Condition";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();

        void IModifyEarlyData.Excute()
        {
            var conditionComponents = gameObject.GetDirectChildComponents<ConditionComponent>();
            foreach (var conditionComponent in conditionComponents)
            {
                conditionComponent.HandGestureConditions.AddRange(HandGestureConditions);
                conditionComponent.ParameterConditions.AddRange(ParameterConditions);
            }
        }
    }
}
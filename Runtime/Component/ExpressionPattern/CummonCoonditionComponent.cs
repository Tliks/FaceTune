namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class CommonConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Common Condition";
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;

        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();
        
        [SerializeField, HideInInspector]
        internal bool AllChildren = false;

        internal void Modify()
        {
            var conditionComponents = AllChildren ?
                gameObject.GetComponentsInChildren<ConditionComponent>(false) :
                gameObject.GetDirectChildComponents<ConditionComponent>();
            foreach (var conditionComponent in conditionComponents)
            {
                conditionComponent.HandGestureConditions.AddRange(HandGestureConditions);
                conditionComponent.ParameterConditions.AddRange(ParameterConditions);
            }
        }
    }
}
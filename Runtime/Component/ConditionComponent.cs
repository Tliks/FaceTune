namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Condition";

        public Condition Condition = new();

        [Obsolete] public List<HandGestureCondition> HandGestureConditions = new();
        [Obsolete] public List<ParameterCondition> ParameterConditions = new();
    }
}
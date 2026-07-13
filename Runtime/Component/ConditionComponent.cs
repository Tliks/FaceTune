namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class ConditionComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Condition";

        public Condition Condition = new(new ConditionCase());

        [Obsolete] public List<HandGestureCondition> HandGestureConditions = new();
        [Obsolete] public List<ParameterCondition> ParameterConditions = new();

        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
    }
}
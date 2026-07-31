namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ConditionComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Condition";

        public Condition Condition = new(new ConditionCase());

        [Obsolete] public List<HandGestureCondition> HandGestureConditions = new();
        [Obsolete] public List<ParameterCondition> ParameterConditions = new();

        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
    }
}
namespace Aoyon.FaceTune
{
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class ConditionComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Condition";

        public Condition Condition = new();

        [Obsolete] public List<HandGestureCondition> HandGestureConditions = new();
        [Obsolete] public List<ParameterCondition> ParameterConditions = new();


        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
    }
}
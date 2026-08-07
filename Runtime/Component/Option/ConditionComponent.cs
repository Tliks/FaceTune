namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ConditionComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Condition";

        public Condition Condition = CreateDefaultCondition();

        [Obsolete] public List<HandGestureCondition> HandGestureConditions = new();
        [Obsolete] public List<ParameterCondition> ParameterConditions = new();

        internal static Condition CreateDefaultCondition() => new(new ConditionCase());

        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
    }
}
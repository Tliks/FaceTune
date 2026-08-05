namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "MMD Support";

        public MmdSupportSettings Settings = new();
        public Condition DisableWhen = new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

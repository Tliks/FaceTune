namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableEyeBlinkComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable EyeBlink";

        public Condition DisableWhen = new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

namespace Aoyon.FaceTune
{
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class DisableEyeBlinkComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable EyeBlink";

        public Condition DisableWhen = CreateDefaultDisableWhen();

        internal static Condition CreateDefaultDisableWhen() => new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

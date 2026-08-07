namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableLipSyncComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable LipSync";

        public Condition DisableWhen = CreateDefaultDisableWhen();

        internal static Condition CreateDefaultDisableWhen() => new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

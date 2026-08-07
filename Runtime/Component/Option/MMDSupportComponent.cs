namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "MMD Support";

        public MmdSupportSettings Settings = CreateDefaultSettings();
        public Condition DisableWhen = CreateDefaultDisableWhen();

        internal static MmdSupportSettings CreateDefaultSettings() => new();
        internal static Condition CreateDefaultDisableWhen() => new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

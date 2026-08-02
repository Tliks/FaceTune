namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent, IHasSingleConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "MMD Support";

        public MmdSupportSettings Settings = new();

        IEnumerable<SingleConditionBase> IHasSingleConditions.SingleConditions => new[] { Settings.DisableWhen };
    }
}

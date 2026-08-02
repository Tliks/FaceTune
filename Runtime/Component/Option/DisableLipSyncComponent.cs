namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableLipSyncComponent : FaceTuneTagComponent, IHasSingleConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable LipSync";

        public SingleConditionBase DisableWhen = SingleConditionBase.Menu();

        IEnumerable<SingleConditionBase> IHasSingleConditions.SingleConditions => new[] { DisableWhen };
    }
}

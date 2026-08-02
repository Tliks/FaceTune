namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableEyeBlinkComponent : FaceTuneTagComponent, IHasSingleConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable EyeBlink";

        public SingleConditionBase DisableWhen = SingleConditionBase.Menu();

        IEnumerable<SingleConditionBase> IHasSingleConditions.SingleConditions => new[] { DisableWhen };
    }
}

namespace Aoyon.FaceTune
{
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "MMD Support";

        public MmdSupportSettings Settings = new();
        public Condition DisableWhen = new();

        IEnumerable<Condition> IHasConditions.Conditions => new[] { DisableWhen };
    }
}

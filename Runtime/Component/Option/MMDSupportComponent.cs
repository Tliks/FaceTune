namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "MMD Support";

        public MmdSupportSettings Settings = new();
    }
}

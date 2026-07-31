namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableLipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable LipSync";

        public string DisableParameterName = string.Empty;
    }
}

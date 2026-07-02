namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DisableLipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Disable LipSync";

        public string DisableParameterName = string.Empty;
    }
}

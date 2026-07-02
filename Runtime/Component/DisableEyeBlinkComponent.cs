namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DisableEyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Disable EyeBlink";

        public string DisableParameterName = string.Empty;
    }
}

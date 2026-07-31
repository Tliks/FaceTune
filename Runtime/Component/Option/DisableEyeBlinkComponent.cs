namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DisableEyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Disable EyeBlink";

        public string DisableParameterName = string.Empty;
    }
}

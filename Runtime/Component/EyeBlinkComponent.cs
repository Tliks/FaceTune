namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} EyeBlink";

        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();
    }
}
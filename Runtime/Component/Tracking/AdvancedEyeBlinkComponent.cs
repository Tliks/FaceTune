namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedEyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Advanced EyeBlink";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();
    }
}
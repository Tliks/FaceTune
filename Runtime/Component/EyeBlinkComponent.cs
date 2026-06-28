namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class EyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} EyeBlink";
        internal const string MenuPath = BasePath + "/" + ComponentName;

        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();
    }
}
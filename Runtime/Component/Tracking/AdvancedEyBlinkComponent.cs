namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedEyBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Advanced EyBlink";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public AdvancedEyBlinkSettings AdvancedEyBlinkSettings = new();
    }
}
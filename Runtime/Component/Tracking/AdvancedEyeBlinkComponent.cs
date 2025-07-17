namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedEyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Advanced EyeBlink";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();
    }
}
namespace com.aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedEyBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Advanced EyBlink";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public bool AutoDetectEyeBlendShapes = true;
        public AdvancedEyBlinkSettings AdvancedEyBlinkSettings = new();

        internal AdvancedEyBlinkSettings GetAdvancedEyBlinkSettings(List<string> eyeBlendShapeNames)
        {
            if (AutoDetectEyeBlendShapes)
            {
                return AdvancedEyBlinkSettings with { EyeBlendShapeNames = eyeBlendShapeNames };
            }
            else
            {
                return AdvancedEyBlinkSettings;
            }
        }
    }
}
namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedEyBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Advanced EyBlink";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public bool AutoDetectBlinkAnimations = true;
        public bool AutoDetectCancelerBlendShapeNames = true;
        public AdvancedEyBlinkSettings AdvancedEyBlinkSettings = new();

        internal AdvancedEyBlinkSettings GetAdvancedEyBlinkSettings(List<BlendShapeAnimation> blinkAnimations, List<string> cancelerBlendShapeNames)
        {
            if (AutoDetectBlinkAnimations)
            {
                if (AutoDetectCancelerBlendShapeNames)
                {
                    return AdvancedEyBlinkSettings with { BlinkAnimations = blinkAnimations, CancelerBlendShapeNames = cancelerBlendShapeNames };
                }
                else
                {
                    return AdvancedEyBlinkSettings with { BlinkAnimations = blinkAnimations };
                }
            }
            else
            {
                return AdvancedEyBlinkSettings;
            }
        }
    }
}
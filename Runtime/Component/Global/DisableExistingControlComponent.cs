namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class DisableExistingControlComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Disable Existing Control";
        internal const string MenuPath = FaceTune + "/" + Global + "/" + ComponentName;

        public bool OverrideBlendShapes = true;
        public bool OverrideProperties = true;
    }
}
namespace aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class DisableExistingControlComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Disable Existing Control";
        internal const string MenuPath = BasePath + "/" + Global + "/" + ComponentName;

        public bool OverrideBlendShapes = true;
        public bool OverrideProperties = true;
    }
}
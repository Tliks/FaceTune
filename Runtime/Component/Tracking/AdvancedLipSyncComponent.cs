namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedLipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Advanced LipSync";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();
    }  
}
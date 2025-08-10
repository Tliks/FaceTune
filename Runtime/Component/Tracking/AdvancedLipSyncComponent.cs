namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AdvancedLipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Advanced LipSync";
        internal const string MenuPath = BasePath + "/" + Tracking + "/" + ComponentName;

        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();
    }  
}
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class LipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} LipSync";
        internal const string MenuPath = BasePath + "/" + ComponentName;

        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();
    }  
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "LipSync";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public Transform? Reference = null;
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();

    }  
}
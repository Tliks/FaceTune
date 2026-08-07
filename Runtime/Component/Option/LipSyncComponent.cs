
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "LipSync";

        public ComponentReferenceMode ReferenceMode = DefaultReferenceMode;
        public LipSyncComponent? Reference = null;
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = CreateDefaultSettings();

        internal const ComponentReferenceMode DefaultReferenceMode = ComponentReferenceMode.Direct;
        internal static AdvancedLipSyncSettings CreateDefaultSettings() => new();

    }  
}
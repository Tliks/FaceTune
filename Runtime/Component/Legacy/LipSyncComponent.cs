
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "LipSync";

        public SettingsSourceMode ReferenceMode = DefaultReferenceMode;
        public LipSyncComponent? Reference = null;
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = CreateDefaultSettings();

        internal const SettingsSourceMode DefaultReferenceMode = SettingsSourceMode.Direct;
        internal static AdvancedLipSyncSettings CreateDefaultSettings() => new();

    }  
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "LipSync";

        public SettingSourceMode ReferenceMode = DefaultReferenceMode;
        public LipSyncComponent? Reference = null;
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = CreateDefaultSettings();

        internal const SettingSourceMode DefaultReferenceMode = SettingSourceMode.Direct;
        internal static AdvancedLipSyncSettings CreateDefaultSettings() => new();

    }  
}
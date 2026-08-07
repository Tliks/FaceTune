
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "EyeBlink";

        public SettingSourceMode ReferenceMode = DefaultReferenceMode;
        public EyeBlinkComponent? Reference = null;
        public EyeBlinkSettings Settings = CreateDefaultSettings();

        [Obsolete("Use Settings instead.")]
        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();

        internal const SettingSourceMode DefaultReferenceMode = SettingSourceMode.Direct;
        internal static EyeBlinkSettings CreateDefaultSettings() => new();

    }
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "EyeBlink";

        public ComponentReferenceMode ReferenceMode = DefaultReferenceMode;
        public EyeBlinkComponent? Reference = null;
        public EyeBlinkSettings Settings = CreateDefaultSettings();

        [Obsolete("Use Settings instead.")]
        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();

        internal const ComponentReferenceMode DefaultReferenceMode = ComponentReferenceMode.Direct;
        internal static EyeBlinkSettings CreateDefaultSettings() => new();

    }
}
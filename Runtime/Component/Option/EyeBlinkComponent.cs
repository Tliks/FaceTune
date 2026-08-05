
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "EyeBlink";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public Transform? Reference = null;
        public EyeBlinkSettings Settings = new();

        [Obsolete("Use Settings instead.")]
        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();

    }
}
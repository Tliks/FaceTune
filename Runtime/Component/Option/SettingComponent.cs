namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class SettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";

        public AvatarSettings Settings = CreateDefaultSettings();

        internal static AvatarSettings CreateDefaultSettings() => AvatarSettings.Default;

        void IHasObjectReferences.ResolveReferences() => Settings.ResolveReferences(this);
    }
}
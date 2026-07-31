namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class SettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";

        public AvatarSettings Settings = AvatarSettings.Default;

        public void ResolveReferences() => Settings.ResolveReferences(this);
    }
}
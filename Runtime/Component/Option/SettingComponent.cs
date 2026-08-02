namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class SettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";

        public AvatarSettings Settings = AvatarSettings.Default;

        void IHasObjectReferences.ResolveReferences() => Settings.ResolveReferences(this);
    }
}
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class AvatarSettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Avatar Settings";

        public AvatarSettings Settings = new();

        void IHasObjectReferences.ResolveReferences() => Settings.ResolveReferences(this);
    }
}
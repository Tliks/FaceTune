namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class SetComponent : FaceTuneTagComponent, IHasObjectReferences, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Set";

        public MenuSettings Menu = new();
        public bool DefaultSelected = false;

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
        void IHasObjectReferences.ResolveReferences() => Menu.ResolveReferences(this);
    }
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasObjectReferences, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu Folder";

        public MenuSettings Menu = new();

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
        void IHasObjectReferences.ResolveReferences() => Menu.ResolveReferences(this);
    }
}

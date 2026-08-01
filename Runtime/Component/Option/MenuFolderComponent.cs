namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasObjectReferences, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu Folder";

        public string MenuName = string.Empty;
        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => InstallSettings;

        public void ResolveReferences()
        {
            Icon.ResolveReferences(this);
            InstallSettings.ResolveReferences(this);
        }
    }
}

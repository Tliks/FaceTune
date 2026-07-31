namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu Folder";

        public string MenuName = string.Empty;
        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();

        public void ResolveReferences()
        {
            Icon.ResolveReferences(this);
            InstallSettings.ResolveReferences(this);
        }
    }
}

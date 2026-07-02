namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = FaceTuneConstants.ComponentPrefix + " Menu Folder";

        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();

        public void ResolveReferences()
        {
            InstallSettings.ResolveReferences(this);
        }
    }
}

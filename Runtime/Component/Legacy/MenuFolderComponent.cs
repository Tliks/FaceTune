namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasMenuInstallContainer
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu Folder";

        public MenuSettings Menu = CreateDefaultMenu();

        internal static MenuSettings CreateDefaultMenu() => new();

        MenuInstallSettings? IHasMenuInstallContainer.InstallSettings => Menu.InstallSettings;
    }
}

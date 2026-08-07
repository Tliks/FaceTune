namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuFolderComponent : FaceTuneTagComponent, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu Folder";

        public MenuSettings Menu = CreateDefaultMenu();

        internal static MenuSettings CreateDefaultMenu() => new();

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
    }
}

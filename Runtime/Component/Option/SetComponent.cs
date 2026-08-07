namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class SetComponent : FaceTuneTagComponent, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Set";

        public MenuSettings Menu = CreateDefaultMenu();
        public bool DefaultSelected = DefaultSelectedValue;

        public const bool DefaultSelectedValue = false;
        internal static MenuSettings CreateDefaultMenu() => new();

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
    }
}

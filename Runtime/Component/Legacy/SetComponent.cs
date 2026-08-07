namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class SetComponent : FaceTuneTagComponent, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Set";

        public MenuSettings Menu = new();
        public bool DefaultSelected;

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
    }
}

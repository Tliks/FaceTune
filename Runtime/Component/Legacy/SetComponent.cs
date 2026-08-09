namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class SetComponent : FaceTuneTagComponent, IHasMenuInstallContainer
    {
        internal const string ComponentName = ComponentNamePrefix + "Set";

        public MenuSettings Menu = new();
        public bool DefaultSelected;

        MenuInstallSettings? IHasMenuInstallContainer.InstallSettings => Menu.InstallSettings;
    }
}

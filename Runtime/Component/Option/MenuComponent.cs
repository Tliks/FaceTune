namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public MenuSettings Menu = new();
        public MenuItemKind Kind;
        public ExclusiveToggleGroup ExclusiveToggleGroup = new();
        public string ParameterName = string.Empty;
        [Range(0f, 1f)] public float FloatDefaultValue;
        public bool DefaultSelected;

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
    }
}

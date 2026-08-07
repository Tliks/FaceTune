namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public MenuSettings Menu = CreateDefaultMenu();
        public MenuItemKind Kind = DefaultKind;
        public ExclusiveToggleGroup ExclusiveToggleGroup = CreateDefaultExclusiveToggleGroup();
        public string ParameterName = DefaultParameterName;
        [Range(0f, 1f)] public float FloatDefaultValue = DefaultFloatValue;
        public bool DefaultSelected = DefaultSelectedValue;

        public const MenuItemKind DefaultKind = MenuItemKind.Toggle;
        public const string DefaultParameterName = "";
        public const float DefaultFloatValue = 0f;
        public const bool DefaultSelectedValue = false;

        internal static MenuSettings CreateDefaultMenu() => new();
        internal static ExclusiveToggleGroup CreateDefaultExclusiveToggleGroup() => new();

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
    }
}

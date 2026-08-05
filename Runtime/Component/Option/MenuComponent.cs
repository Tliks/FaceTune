namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasObjectReferences, IHasMenuInstallSettings
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public MenuSettings Menu = new();
        public MenuItemKind Kind = MenuItemKind.Toggle;
        public ExclusiveToggleGroup ExclusiveToggleGroup = new();
        public string ParameterName = string.Empty;
        [Range(0f, 1f)] public float FloatDefaultValue = 0f;
        public bool DefaultSelected = false;

        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Menu.InstallSettings;
        void IHasObjectReferences.ResolveReferences() => Menu.ResolveReferences(this);
    }
}

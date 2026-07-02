namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = FaceTuneConstants.ComponentPrefix + " Menu";

        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();
        public MenuItemKind Kind = MenuItemKind.Toggle;
        public string ParameterName = string.Empty; // opt-inでの明示用。Exclusiveでは無視。
        public bool DefaultSelected = false; // Toggleの初期値。Radialでは無視。
        public ExclusiveToggleGroup ExclusiveToggleGroup = new();

        public void ResolveReferences()
        {
            InstallSettings.ResolveReferences(this);
        }
    }
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = FaceTuneConstants.ComponentPrefix + " Menu";

        public MenuItemKind Kind = MenuItemKind.Toggle;
        public string ParameterName = string.Empty; // opt-inでの明示用。Exclusiveでは無視。
        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();
        public ExclusiveToggleGroup ExclusiveToggleGroup = new();

        public void ResolveReferences()
        {
            InstallSettings.ResolveReferences(this);
        }
    }
}

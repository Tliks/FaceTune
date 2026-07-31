namespace Aoyon.FaceTune
{
    // 厳密なhierarchy構造を要求せず、近くFolderなど入るだけなので、禁止する理由がないかも
    // [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public string MenuName = string.Empty;
        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();
        public MenuItemKind Kind = MenuItemKind.Toggle;
        public ExclusiveToggleGroup ExclusiveToggleGroup = new();
        public string ParameterName = string.Empty; // opt-inでの明示用。Toggleのとき、排他でないなら有効、Radialなら有効。
        [Range(0f, 1f)] public float FloatDefaultValue = 0f; // Radialの初期値。Toggleでは無視。
        public bool DefaultSelected = false; // Toggleの初期値。Radialでは無視。

        public void ResolveReferences()
        {
            Icon.ResolveReferences(this);
            InstallSettings.ResolveReferences(this);
        }
    }
}

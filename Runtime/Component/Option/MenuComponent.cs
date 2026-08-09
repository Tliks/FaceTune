namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public enum Kind
        {
            Toggle,
            Radial,
            Folder
        }

        public Kind MenuKind = Kind.Toggle;

        // Toggle, Radial, Folderの表示とinstall先。
        public MenuSettings Menu = new();

        // Toggle
        [ExclusiveToggleMenuGroup]
        public string ExclusiveToggleGroup = string.Empty;
        public bool DefaultSelected = false;

        // Radial
        [Range(0f, 1f)]
        public float FloatDefaultValue = 0f;

        public string ParameterName = string.Empty;
    }
}

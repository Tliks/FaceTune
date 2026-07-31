namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class PresetComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Preset";

        public string MenuName = string.Empty;
        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();
        public bool DefaultSelected = false;

    }
}
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class PresetComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Preset";

        public MenuIconSettings Icon = new();
        public MenuInstallSettings InstallSettings = new();
        public bool DefaultSelected = false;

    }
}
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class PresetComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Preset";

        internal GameObject GetMenuTarget()
        {
            // デフォルトは同階層にPresetのトグルを作る。
            // Todo: option to override
            var menuTarget = new GameObject(name);
            menuTarget.transform.SetParent(transform.parent);
            return menuTarget;
        }
    }
}
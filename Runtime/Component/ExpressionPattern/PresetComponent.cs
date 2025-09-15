namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Preset";

        internal GameObject GetORCreateMenuTarget()
        {
            // デフォルトは同階層にPresetのトグルを作る。
            // Todo: option to override
            var menuTarget = new GameObject(name);
            menuTarget.transform.SetParent(transform.parent);
            return menuTarget;
        }
    }
}
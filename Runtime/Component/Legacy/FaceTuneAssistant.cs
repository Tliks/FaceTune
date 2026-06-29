namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class FaceTuneAssistantComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Assistant (EditorOnly)";
        internal const string MenuPath = BaseMenuPath + "/" + LegacyMenuName + "/" + ComponentName;
    }
}
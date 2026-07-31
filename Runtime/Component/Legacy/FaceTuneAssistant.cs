namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class FaceTuneAssistantComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Assistant (EditorOnly)";
        internal const string MenuPath = LegacyMenuPathPrefix + ComponentName;
    }
}
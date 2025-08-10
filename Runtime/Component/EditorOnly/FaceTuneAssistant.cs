namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    internal class FaceTuneAssistantComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Assistant (EditorOnly)";
        internal const string MenuPath = BasePath + "/" + EditorOnly + "/" + ComponentName;
    }
}
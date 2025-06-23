namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    internal class FaceTuneAssistantComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConsts.Name} Assistant (EditorOnly)";
        internal const string MenuPath = BasePath + "/" + EditorOnly + "/" + ComponentName;
    }
}
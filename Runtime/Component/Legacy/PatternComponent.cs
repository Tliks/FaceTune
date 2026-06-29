namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Pattern";
        internal const string MenuPath = BaseMenuPath + "/" + LegacyMenuName + "/" + ComponentName;
        
    }
}

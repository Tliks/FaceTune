namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Pattern";
        internal const string MenuPath = LegacyMenuPathPrefix + ComponentName;
        
    }
}

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class AllowTrackedBlendShapesComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Allow Tracked BlendShapes";
        internal const string MenuPath = BaseMenuPath + "/" + LegacyMenuName + "/" + ComponentName;
    }
}
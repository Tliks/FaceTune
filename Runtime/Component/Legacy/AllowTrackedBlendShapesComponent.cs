namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    internal class AllowTrackedBlendShapesComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Allow Tracked BlendShapes";
        internal const string MenuPath = LegacyMenuPathPrefix + ComponentName;
    }
}
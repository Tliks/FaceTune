namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    [Obsolete]
    public class AllowTrackedBlendShapesComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Allow Tracked BlendShapes";
        internal const string MenuPath = BasePath + "/" + Legacy + "/" + ComponentName;
    }
}
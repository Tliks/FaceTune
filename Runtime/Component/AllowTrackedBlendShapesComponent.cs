namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AllowTrackedBlendShapesComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Allow Tracked BlendShapes";
        internal const string MenuPath = BasePath + "/" + Global + "/" + ComponentName;
    }
}
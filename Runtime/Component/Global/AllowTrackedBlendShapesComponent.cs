namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class AllowTrackedBlendShapesComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = "FT Allow Tracked BlendShapes";
        internal const string MenuPath = BasePath + "/" + Global + "/" + ComponentName;
    }
}
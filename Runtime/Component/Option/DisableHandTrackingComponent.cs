namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class DisableHandTrackingComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = BasePath + "/" + Option + "/" + ComponentName;
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Disable Hand Tracking";
    }
}   

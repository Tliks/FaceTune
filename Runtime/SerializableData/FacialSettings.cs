namespace com.aoyon.facetune;

[Serializable]
public record class FacialSettings
{
    public TrackingPermission AllowEyeBlink;
    public TrackingPermission AllowLipSync;

    public BlendingPermission BlendingPermission;

    public FacialSettings()
    {
        AllowEyeBlink = TrackingPermission.Disallow;
        AllowLipSync = TrackingPermission.Allow;
        BlendingPermission = BlendingPermission.Disallow;
    }

    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, BlendingPermission blendingPermission)
    {
        AllowEyeBlink = allowEyeBlink;
        AllowLipSync = allowLipSync;
        BlendingPermission = blendingPermission;
    }

    internal static FacialSettings Keep = new FacialSettings(TrackingPermission.Keep, TrackingPermission.Keep, BlendingPermission.Keep);

}
